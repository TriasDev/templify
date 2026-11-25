// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using TriasDev.Templify.Builder.Json;

namespace TriasDev.Templify.Builder;

/// <summary>
/// Components that a JSON definition refers to by name: an exported definition writes a registered component as
/// <c>{ "component": name, "params": { … } }</c>, and an import creates it from the current component code, so a
/// stored definition picks up changes to the component.
/// </summary>
/// <remarks>
/// A registrable component has one public constructor, and every parameter has a public property of the same name
/// (records satisfy this). Parameters are paths (<see cref="TemplatePath"/>, <see cref="TemplatePath{T}"/>),
/// <see cref="string"/>, <see cref="bool"/>, <see cref="int"/>, <see cref="long"/>, <see cref="double"/>,
/// <see cref="decimal"/>, enums, style keys, <see cref="Length"/>, <see cref="Color"/>, their nullable forms, or
/// <see cref="IReadOnlyList{T}"/> of those.
/// </remarks>
public sealed partial class ComponentCatalog
{
    private readonly Dictionary<string, Registration> _byName = new Dictionary<string, Registration>(StringComparer.Ordinal);
    private readonly Dictionary<Type, Registration> _byType = new Dictionary<Type, Registration>();

    /// <summary>
    /// Gets how C# property names become data names for typed parameters (<see cref="TemplatePath{T}"/>), as the
    /// definitions pass it to <see cref="TemplateComposer.DataAs{T}"/>; null for camelCase.
    /// </summary>
    public System.Text.Json.JsonNamingPolicy? DataNaming { get; init; }

    /// <summary>Gets the registered names.</summary>
    public IReadOnlyCollection<string> Names => _byName.Keys;

    /// <summary>Registers a component type under a name.</summary>
    /// <typeparam name="TComponent">The component type.</typeparam>
    /// <param name="name">The name in JSON: a letter, then letters and digits (<c>address</c>, <c>verdictBox</c>).</param>
    /// <returns>This catalog.</returns>
    /// <exception cref="ArgumentException">The name is malformed or taken, or the type cannot be written as parameters.</exception>
    public ComponentCatalog Register<TComponent>(string name)
        where TComponent : ITemplateComponent
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!NamePattern().IsMatch(name))
        {
            throw new ArgumentException($"'{name}' is not a component name: a letter, then letters and digits.", nameof(name));
        }

        if (_byName.ContainsKey(name))
        {
            throw new ArgumentException($"The name '{name}' is already registered.", nameof(name));
        }

        Type type = typeof(TComponent);
        ConstructorInfo[] constructors = type.GetConstructors();
        if (constructors.Length != 1)
        {
            throw new ArgumentException($"{type.Name} needs exactly one public constructor to be registered.", nameof(TComponent));
        }

        List<(ParameterInfo Parameter, PropertyInfo Property)> parameters = new List<(ParameterInfo, PropertyInfo)>();
        foreach (ParameterInfo parameter in constructors[0].GetParameters())
        {
            PropertyInfo? property = type.GetProperty(parameter.Name!, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is null || !property.CanRead)
            {
                throw new ArgumentException($"{type.Name}: the parameter '{parameter.Name}' has no public property of the same name, so it cannot be written to JSON.", nameof(TComponent));
            }

            if (!IsAllowed(parameter.ParameterType))
            {
                throw new ArgumentException(
                    $"{type.Name}: the parameter '{parameter.Name}' is a {parameter.ParameterType.Name}, which cannot be written to JSON (paths, strings, numbers, booleans, enums, style keys, lengths, colours and lists of those can).",
                    nameof(TComponent));
            }

            parameters.Add((parameter, property));
        }

        Registration registration = new Registration(name, type, constructors[0], parameters);
        _byName.Add(name, registration);
        _byType[type] = registration;
        return this;
    }

    internal Registration? Find(string name) => _byName.GetValueOrDefault(name);

    internal Registration? Find(Type type) => _byType.GetValueOrDefault(type);

    private static bool IsAllowed(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            return IsAllowed(type.GetGenericArguments()[0]);
        }

        return type == typeof(TemplatePath) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(TemplatePath<>))
            || type == typeof(string) || type == typeof(bool) || type == typeof(int) || type == typeof(long) || type == typeof(double) || type == typeof(decimal)
            || type.IsEnum
            || type == typeof(ParagraphStyleKey) || type == typeof(CharacterStyleKey) || type == typeof(BoxStyleKey) || type == typeof(TableStyleKey) || type == typeof(ListStyleKey)
            || type == typeof(Length) || type == typeof(Color);
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    /// <summary>A registered component type: its name and how its parameters map to JSON.</summary>
    internal sealed class Registration(string name, Type type, ConstructorInfo constructor, List<(ParameterInfo Parameter, PropertyInfo Property)> parameters)
    {
        public string Name { get; } = name;

        public Type Type { get; } = type;

        /// <summary>Writes the component's parameters as a JSON object.</summary>
        /// <exception cref="ArgumentException">A typed path parameter uses another naming than <paramref name="dataNaming"/>, so it could not be read back.</exception>
        public void WriteParameters(Utf8JsonWriter json, ITemplateComponent component, JsonNamingPolicy? dataNaming)
        {
            json.WriteStartObject();
            foreach ((ParameterInfo parameter, PropertyInfo property) in parameters)
            {
                object? value = property.GetValue(component);
                if (value is not null && TypedPathNaming(value, out JsonNamingPolicy? naming) && (naming ?? JsonNamingPolicy.CamelCase) != (dataNaming ?? JsonNamingPolicy.CamelCase))
                {
                    throw new ArgumentException(
                        $"The component '{Name}' gets the typed path '{parameter.Name}' with a different naming policy than the catalog's DataNaming; set ComponentCatalog.DataNaming to the policy the definition passes to DataAs<T>().");
                }

                json.WritePropertyName(JsonNamingPolicy.CamelCase.ConvertName(parameter.Name!));
                WriteValue(json, value);
            }

            json.WriteEndObject();
        }

        private static bool TypedPathNaming(object value, out JsonNamingPolicy? naming)
        {
            naming = null;
            Type type = value.GetType();
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(TemplatePath<>))
            {
                return false;
            }

            naming = (JsonNamingPolicy?)type.GetProperty("Naming", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value);
            return true;
        }

        /// <summary>Creates the component from its JSON parameters.</summary>
        public ITemplateComponent Create(JsonElement values, JsonNamingPolicy? dataNaming, Func<string, DataKind?, TemplatePath> resolvePath, Func<string, ParagraphStyleKey> paragraphStyle, Func<string, CharacterStyleKey> characterStyle, string at)
        {
            if (values.ValueKind != JsonValueKind.Object)
            {
                throw new JsonImportException(at, "'params' must be an object.");
            }

            HashSet<string> unknown = values.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            object?[] arguments = new object?[parameters.Count];
            for (int i = 0; i < parameters.Count; i++)
            {
                ParameterInfo parameter = parameters[i].Parameter;
                string key = JsonNamingPolicy.CamelCase.ConvertName(parameter.Name!);
                unknown.Remove(key);
                arguments[i] = values.TryGetProperty(key, out JsonElement value)
                    ? ReadValue(value, parameter.ParameterType, dataNaming, resolvePath, paragraphStyle, characterStyle, at + "/" + key)
                    : throw new JsonImportException(at, $"The parameter '{key}' of '{Name}' is missing.");
            }

            if (unknown.Count > 0)
            {
                string first = unknown.Order(StringComparer.Ordinal).First();
                throw new JsonImportException(at + "/" + first, $"'{Name}' has no parameter '{first}'.");
            }

            try
            {
                return (ITemplateComponent)constructor.Invoke(arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is { } inner)
            {
                // The component rejected its parameters: reported as an invalid value (TB9201) at the reference.
                throw new ArgumentException($"The component '{Name}' rejected its parameters: {inner.Message}", inner);
            }
        }

        private static void WriteValue(Utf8JsonWriter json, object? value)
        {
            switch (value)
            {
                case null:
                    json.WriteNullValue();
                    break;
                case TemplatePath path:
                    json.WriteStringValue(path.ToString());
                    break;
                case string text:
                    json.WriteStringValue(text);
                    break;
                case bool flag:
                    json.WriteBooleanValue(flag);
                    break;
                case int number:
                    json.WriteNumberValue(number);
                    break;
                case long number:
                    json.WriteNumberValue(number);
                    break;
                case double number:
                    json.WriteNumberValue(number);
                    break;
                case decimal number:
                    json.WriteNumberValue(number);
                    break;
                case Enum e:
                    string name = e.ToString();
                    json.WriteStringValue(char.ToLowerInvariant(name[0]) + name[1..]);
                    break;
                case ParagraphStyleKey key:
                    json.WriteStringValue(key.Id);
                    break;
                case CharacterStyleKey key:
                    json.WriteStringValue(key.Id);
                    break;
                case BoxStyleKey key:
                    json.WriteStringValue(key.Id);
                    break;
                case TableStyleKey key:
                    json.WriteStringValue(key.Id);
                    break;
                case ListStyleKey key:
                    json.WriteStringValue(key.Id);
                    break;
                case Length length:
                    json.WriteStringValue(JsonValues.Length(length));
                    break;
                case Color color:
                    json.WriteStringValue(JsonValues.Color(color));
                    break;
                case System.Collections.IEnumerable list:
                    json.WriteStartArray();
                    foreach (object? item in list)
                    {
                        WriteValue(json, item);
                    }

                    json.WriteEndArray();
                    break;
                default:
                    // A TemplatePath<T>: written as its plain path.
                    if (value.GetType() is { IsGenericType: true } type && type.GetGenericTypeDefinition() == typeof(TemplatePath<>))
                    {
                        json.WriteStringValue(value.ToString());
                        break;
                    }

                    throw new NotSupportedException($"A component parameter of type {value.GetType().Name} cannot be written.");
            }
        }

        private static object? ReadValue(JsonElement value, Type type, JsonNamingPolicy? dataNaming, Func<string, DataKind?, TemplatePath> resolvePath, Func<string, ParagraphStyleKey> paragraphStyle, Func<string, CharacterStyleKey> characterStyle, string at)
        {
            Type? underlying = Nullable.GetUnderlyingType(type);
            if (value.ValueKind == JsonValueKind.Null)
            {
                return underlying != null || !type.IsValueType ? null : throw new JsonImportException(at, "A value is required.");
            }

            type = underlying ?? type;
            try
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
                {
                    Type itemType = type.GetGenericArguments()[0];
                    System.Collections.IList list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
                    int i = 0;
                    foreach (JsonElement item in value.EnumerateArray())
                    {
                        list.Add(ReadValue(item, itemType, dataNaming, resolvePath, paragraphStyle, characterStyle, at + "/" + i++));
                    }

                    return list;
                }

                if (type == typeof(TemplatePath))
                {
                    return resolvePath(value.GetString()!, null);
                }

                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(TemplatePath<>))
                {
                    TemplatePath path = resolvePath(value.GetString()!, DataCatalog.KindOfType(type.GetGenericArguments()[0]));
                    return Activator.CreateInstance(type, BindingFlags.NonPublic | BindingFlags.Instance, null, [path, dataNaming], null);
                }

                if (type.IsEnum)
                {
                    string text = value.GetString()!;
                    return Enum.TryParse(type, text, ignoreCase: true, out object? parsed) && !text.Any(char.IsDigit)
                        ? parsed
                        : throw new JsonImportException(at, $"'{text}' is not one of {string.Join(", ", Enum.GetNames(type))}.");
                }

                return type switch
                {
                    _ when type == typeof(string) => value.GetString(),
                    _ when type == typeof(bool) => value.GetBoolean(),
                    _ when type == typeof(int) => value.GetInt32(),
                    _ when type == typeof(long) => value.GetInt64(),
                    _ when type == typeof(double) => value.GetDouble(),
                    _ when type == typeof(decimal) => value.GetDecimal(),
                    _ when type == typeof(ParagraphStyleKey) => paragraphStyle(value.GetString()!),
                    _ when type == typeof(CharacterStyleKey) => characterStyle(value.GetString()!),
                    _ when type == typeof(BoxStyleKey) => new BoxStyleKey(value.GetString()!),
                    _ when type == typeof(TableStyleKey) => new TableStyleKey(value.GetString()!),
                    _ when type == typeof(ListStyleKey) => new ListStyleKey(value.GetString()!),
                    _ when type == typeof(Length) => JsonValues.TryLength(value.GetString(), out Length length) ? length : throw new JsonImportException(at, $"{value} is not a length."),
                    _ when type == typeof(Color) => JsonValues.TryColor(value.GetString(), out Color color) ? color : throw new JsonImportException(at, $"{value} is not a colour."),
                    _ => throw new JsonImportException(at, $"A parameter of type {type.Name} cannot be read."),
                };
            }
            catch (Exception exception) when (exception is InvalidOperationException or FormatException)
            {
                throw new JsonImportException(at, $"{value} is not a {type.Name}: {exception.Message}");
            }
        }
    }
}
