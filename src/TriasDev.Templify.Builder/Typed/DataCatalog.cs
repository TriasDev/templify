// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder;

/// <summary>What a path of the data is.</summary>
public enum DataKind
{
    /// <summary>A value printed or compared (text, number, date, boolean, enum).</summary>
    Value,

    /// <summary>An object with properties.</summary>
    Object,

    /// <summary>A collection to loop over; its items are listed under <c>path[]</c>.</summary>
    Collection,

    /// <summary>A shape the type does not fix (a dictionary, <see cref="object"/>, <see cref="JsonElement"/>): any path below is accepted.</summary>
    Open,
}

/// <summary>One path of a data catalogue.</summary>
/// <param name="Path">The path in the data contract's form, for example <c>order.lines[].description</c>.</param>
/// <param name="Kind">What the path is.</param>
/// <param name="ClrType">The C# type of the value.</param>
public sealed record DataCatalogEntry(string Path, DataKind Kind, Type ClrType);

/// <summary>
/// Every data path a C# type offers, named as its JSON (camelCase by default, <see cref="JsonPropertyNameAttribute"/>
/// wins, <see cref="JsonIgnoreAttribute"/> hides). An import checks a definition's paths against it; a template's data
/// contract should be inside it.
/// </summary>
public sealed partial class DataCatalog
{
    private const int MaximumDepth = 16;
    private const int JsonVersion = 1;

    private readonly HashSet<string> _paths;
    private readonly List<string> _open;

    private DataCatalog(List<DataCatalogEntry> entries)
    {
        Entries = entries;
        _paths = entries.Select(e => e.Path).ToHashSet(StringComparer.Ordinal);
        _open = entries.Where(e => e.Kind == DataKind.Open).Select(e => e.Path).ToList();
    }

    /// <summary>Gets the paths, in property order, parents before children.</summary>
    public IReadOnlyList<DataCatalogEntry> Entries { get; }

    /// <summary>The catalogue of a type.</summary>
    /// <typeparam name="T">The data's type.</typeparam>
    /// <param name="naming">How property names become data names; null for camelCase.</param>
    /// <returns>The catalogue.</returns>
    public static DataCatalog From<T>(JsonNamingPolicy? naming = null) => ForType(typeof(T), naming);

    /// <summary>The catalogue of a type.</summary>
    /// <param name="type">The data's type.</param>
    /// <param name="naming">How property names become data names; null for camelCase.</param>
    /// <returns>The catalogue.</returns>
    public static DataCatalog ForType(Type type, JsonNamingPolicy? naming = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        List<DataCatalogEntry> entries = new List<DataCatalogEntry>();
        Properties(type, string.Empty, naming ?? JsonNamingPolicy.CamelCase, new List<Type> { type }, entries, 0);
        return new DataCatalog(entries);
    }

    /// <summary>Whether the data has this path (canonical form); any path below an open shape counts.</summary>
    /// <param name="path">The path, for example <c>order.lines[].description</c>.</param>
    /// <returns>Whether it exists.</returns>
    public bool Contains(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return _paths.Contains(path)
            || _open.Any(open => path.StartsWith(open, StringComparison.Ordinal) && path.Length > open.Length && path[open.Length] is '.' or '[');
    }

    /// <summary>
    /// Writes the catalogue as JSON, for agents and for hosts that keep a snapshot: a version and the paths in
    /// <see cref="Entries"/> order, each with its kind (<c>value</c>, <c>object</c>, <c>collection</c>, <c>open</c>) and
    /// its C# type name.
    /// </summary>
    /// <returns>The JSON, indented.</returns>
    public string ToJson()
    {
        JsonObject root = new JsonObject
        {
            ["version"] = JsonVersion,
            ["paths"] = new JsonArray(Entries
                .Select(e => (JsonNode)new JsonObject
                {
                    ["path"] = e.Path,
                    ["kind"] = e.Kind.ToString().ToLowerInvariant(),
                    ["type"] = TypeName(e.ClrType),
                })
                .ToArray()),
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Builds data in which every path of the catalogue exists: values are the text <c>"x"</c>, collections hold one
    /// item, and an open shape is an <see cref="OpenWitness"/> with the given names.
    /// </summary>
    /// <param name="openNames">The names an open shape has (at any depth below it); null for none.</param>
    internal IReadOnlyDictionary<string, object?> CreateWitness(IEnumerable<string>? openNames = null)
    {
        OpenWitness open = new OpenWitness(openNames ?? []);
        Dictionary<string, DataCatalogEntry> byPath = Entries.ToDictionary(e => e.Path, StringComparer.Ordinal);
        ILookup<string, DataCatalogEntry> children = Entries
            .Where(e => !e.Path.EndsWith("[]", StringComparison.Ordinal))
            .ToLookup(e => e.Path.LastIndexOf('.') is var dot and >= 0 ? e.Path[..dot] : string.Empty, StringComparer.Ordinal);
        return ObjectWitness(string.Empty);

        object? Witness(DataCatalogEntry entry) => entry.Kind switch
        {
            DataKind.Value => "x",
            DataKind.Open => open,
            DataKind.Collection => new List<object?> { byPath.TryGetValue(entry.Path + "[]", out DataCatalogEntry? item) ? Witness(item) : "x" },
            _ => ObjectWitness(entry.Path),
        };

        Dictionary<string, object?> ObjectWitness(string path) =>
            children[path].ToDictionary(e => path.Length == 0 ? e.Path : e.Path[(path.Length + 1)..], Witness, StringComparer.Ordinal);
    }

    /// <summary>
    /// Validates a template against witness data of the catalogue (<see cref="TemplateProcessor.ValidateTemplate(Stream, IReadOnlyDictionary{string, object?})"/>).
    /// Names below open shapes cannot be listed in advance, so the names the first pass misses become names of the
    /// open shapes and the template is validated again, until nothing changes: a missing name that remains is not in the
    /// data. Paths below closed types never gain names this way.
    /// </summary>
    /// <param name="template">The template package.</param>
    /// <returns>The last validation.</returns>
    internal ValidationResult ValidateTemplate(byte[] template)
    {
        HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
        bool open = Entries.Any(e => e.Kind == DataKind.Open);
        for (int pass = 0; ; pass++)
        {
            ValidationResult result;
            using (MemoryStream stream = new MemoryStream(template, writable: false))
            {
                result = new TemplateProcessor().ValidateTemplate(stream, CreateWitness(names));
            }

            int before = names.Count;
            if (open && pass < MaximumDepth)
            {
                foreach (string missing in MissingNames(result))
                {
                    names.UnionWith(missing.Split('.', '[', ']').Where(n => n.Length > 0 && !char.IsAsciiDigit(n[0])));
                }
            }

            if (names.Count == before)
            {
                return result;
            }
        }
    }

    /// <summary>The variables a validation reports missing: values and loops, and variables of conditions.</summary>
    internal static IEnumerable<string> MissingNames(ValidationResult result) =>
        result.MissingVariables.Concat(result.Warnings
            .Where(w => w.Type == ValidationWarningType.MissingConditionVariable)
            .Select(w => ConditionVariable().Match(w.Message))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value));

    /// <summary>The kind of a path: its entry's kind, <see cref="DataKind.Open"/> below an open shape, or null.</summary>
    internal DataKind? KindOf(string path)
    {
        // items[0].name reads an item like items[].name.
        path = System.Text.RegularExpressions.Regex.Replace(path, @"\[[0-9]+\]", "[]");
        DataCatalogEntry? entry = Entries.FirstOrDefault(e => e.Path == path);
        if (entry != null)
        {
            return entry.Kind;
        }

        return Contains(path) ? DataKind.Open : null;
    }

    /// <summary>The kind a value of a C# type has (for typed component parameters).</summary>
    internal static DataKind KindOfType(Type type)
    {
        Type bare = Nullable.GetUnderlyingType(type) ?? type;
        return IsValue(bare) ? DataKind.Value : IsOpen(bare) ? DataKind.Open : ItemType(bare) is not null ? DataKind.Collection : DataKind.Object;
    }

    private static void Properties(Type type, string prefix, JsonNamingPolicy naming, List<Type> branch, List<DataCatalogEntry> entries, int depth)
    {
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0 || !property.CanRead
                || property.GetCustomAttribute<JsonIgnoreAttribute>() is { Condition: JsonIgnoreCondition.Always })
            {
                continue;
            }

            string name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? naming.ConvertName(property.Name);
            Add(property.PropertyType, prefix.Length == 0 ? name : prefix + "." + name, naming, branch, entries, depth + 1);
        }
    }

    private static void Add(Type type, string path, JsonNamingPolicy naming, List<Type> branch, List<DataCatalogEntry> entries, int depth)
    {
        Type bare = Nullable.GetUnderlyingType(type) ?? type;
        if (IsValue(bare))
        {
            entries.Add(new DataCatalogEntry(path, DataKind.Value, type));
            return;
        }

        if (IsOpen(bare))
        {
            entries.Add(new DataCatalogEntry(path, DataKind.Open, type));
            return;
        }

        if (ItemType(bare) is { } item)
        {
            entries.Add(new DataCatalogEntry(path, DataKind.Collection, type));
            Add(item, path + "[]", naming, branch, entries, depth);
            return;
        }

        entries.Add(new DataCatalogEntry(path, DataKind.Object, type));

        // A recursive type is expanded once more (children[].name), then cut; the depth cap is a second guard.
        if (depth >= MaximumDepth || branch.Count(t => t == bare) >= 2)
        {
            return;
        }

        branch.Add(bare);
        Properties(bare, path, naming, branch, entries, depth);
        branch.RemoveAt(branch.Count - 1);
    }

    private static bool IsValue(Type type) =>
        type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateTimeOffset)
        || type == typeof(DateOnly) || type == typeof(TimeOnly) || type == typeof(TimeSpan) || type == typeof(Guid);

    private static bool IsOpen(Type type) =>
        type == typeof(object) || type == typeof(JsonElement) || typeof(JsonNode).IsAssignableFrom(type) || type == typeof(System.Dynamic.ExpandoObject)
        || typeof(System.Collections.IDictionary).IsAssignableFrom(type)
        || type.GetInterfaces().Append(type).Any(IsDictionaryInterface);

    private static bool IsDictionaryInterface(Type type) =>
        type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(IDictionary<,>) || type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));

    /// <summary>A readable C# type name: <c>decimal</c>, <c>int?</c>, <c>IReadOnlyList&lt;Line&gt;</c>.</summary>
    private static string TypeName(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return TypeName(underlying) + "?";
        }

        if (type.IsArray)
        {
            return TypeName(type.GetElementType()!) + "[]";
        }

        if (type.IsGenericType)
        {
            string name = type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)];
            return name + "<" + string.Join(", ", type.GetGenericArguments().Select(TypeName)) + ">";
        }

        return type == typeof(string) ? "string" : type == typeof(int) ? "int" : type == typeof(long) ? "long"
            : type == typeof(bool) ? "bool" : type == typeof(decimal) ? "decimal" : type == typeof(double) ? "double"
            : type == typeof(float) ? "float" : type == typeof(object) ? "object" : type.Name;
    }

    // Until the engine exposes the variable of a MissingConditionVariable warning (TriasDev/templify#275), its name is
    // read from the message; a test fails if the wording changes.
    [GeneratedRegex(@"uses variable '([^']+)'", RegexOptions.CultureInvariant)]
    private static partial Regex ConditionVariable();

    private static Type? ItemType(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        Type? enumerable = type.GetInterfaces().Append(type).FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable?.GetGenericArguments()[0];
    }
}

/// <summary>
/// Witness data below an open shape: every given name exists and is the witness again; it prints as <c>x</c>, and as
/// a collection it holds one item (itself).
/// </summary>
internal sealed class OpenWitness : Dictionary<string, object?>, System.Collections.IEnumerable
{
    public OpenWitness(IEnumerable<string> names)
        : base(StringComparer.Ordinal)
    {
        foreach (string name in names)
        {
            this[name] = this;
        }
    }

    // A loop enumerates without the generic interface: one item, which has the same names.
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => new object?[] { this }.GetEnumerator();

    public override string ToString() => "x";
}
