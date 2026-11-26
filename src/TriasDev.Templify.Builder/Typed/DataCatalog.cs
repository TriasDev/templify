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
    private const int MaximumItems = 1000;
    private const int MaximumPasses = 24;

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
    /// Builds data in which every path of the catalogue exists: values are the text <c>"x"</c>, collections hold
    /// <paramref name="items"/> items, and an open shape is an <see cref="OpenWitness"/> with the given names.
    /// </summary>
    /// <param name="openNames">The names an open shape has (at any depth below it); null for none.</param>
    /// <param name="items">How many items every collection holds (an index <c>[n]</c> needs n + 1).</param>
    internal IReadOnlyDictionary<string, object?> CreateWitness(IEnumerable<string>? openNames = null, int items = 1)
    {
        OpenWitness open = new OpenWitness(openNames ?? []);
        Dictionary<string, DataCatalogEntry> byPath = new Dictionary<string, DataCatalogEntry>(StringComparer.Ordinal);
        foreach (DataCatalogEntry entry in Entries)
        {
            // A property hidden with `new` lists its path twice; the first entry wins.
            byPath.TryAdd(entry.Path, entry);
        }

        ILookup<string, DataCatalogEntry> children = byPath.Values
            .Where(e => !e.Path.EndsWith("[]", StringComparison.Ordinal))
            .ToLookup(e => e.Path.LastIndexOf('.') is var dot and >= 0 ? e.Path[..dot] : string.Empty, StringComparer.Ordinal);
        return ObjectWitness(string.Empty);

        object? Witness(DataCatalogEntry entry)
        {
            switch (entry.Kind)
            {
                case DataKind.Value:
                    return "x";
                case DataKind.Open:
                    return open;
                case DataKind.Collection:
                    object? item = byPath.TryGetValue(entry.Path + "[]", out DataCatalogEntry? itemEntry) ? Witness(itemEntry) : "x";
                    return Enumerable.Repeat(item, items).ToList();
                default:
                    return ObjectWitness(entry.Path);
            }
        }

        Dictionary<string, object?> ObjectWitness(string path)
        {
            Dictionary<string, object?> result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (DataCatalogEntry child in children[path])
            {
                result.TryAdd(path.Length == 0 ? child.Path : child.Path[(path.Length + 1)..], Witness(child));
            }

            return result;
        }
    }

    /// <summary>Validates a template against witness data of the catalogue (see <see cref="CheckTemplate"/>).</summary>
    internal ValidationResult ValidateTemplate(byte[] template) => CheckTemplate(template).Validation;

    /// <summary>
    /// Validates a template against witness data of the catalogue and fills it once with that data. The engine's
    /// validation checks every branch but, inside loops, only the first segment of an item's path; the fill checks
    /// whole paths in the branches the witness takes. Names below open shapes and indexes cannot be known in advance:
    /// they start from the template's placeholders, and every name or index a pass misses is added (to the open shapes,
    /// or as more collection items) until nothing changes. A name that stays missing is not in the data; paths below
    /// closed types never gain names this way.
    /// </summary>
    /// <param name="template">The template package.</param>
    /// <returns>The last validation, the names only the fill missed, and the fill's error.</returns>
    internal CatalogueCheck CheckTemplate(byte[] template)
    {
        HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
        int items = 1;
        bool open = Entries.Any(e => e.Kind == DataKind.Open);

        // An open loop item with a root name (order) would resolve order.… through the item inside the loop.
        HashSet<string> roots = Entries.Select(e => e.Path.Split('.', '[')[0]).ToHashSet(StringComparer.Ordinal);
        for (int pass = 0; ; pass++)
        {
            IReadOnlyDictionary<string, object?> witness = CreateWitness(names, items);
            ValidationResult validation;
            using (MemoryStream stream = new MemoryStream(template, writable: false))
            {
                validation = new TemplateProcessor().ValidateTemplate(stream, witness);
            }

            ProcessingResult? fill = validation.Errors.All(e => e.Type == ValidationErrorType.MissingVariable) ? Fill(template, witness) : null;
            List<string> missing = MissingNames(validation).Concat(fill?.MissingVariables ?? []).ToList();

            int namesBefore = names.Count;
            int itemsBefore = items;
            foreach (string name in missing)
            {
                foreach (Match index in IndexPattern().Matches(name))
                {
                    items = Math.Min(MaximumItems, Math.Max(items, int.Parse(index.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) + 1));
                }
            }

            if (open)
            {
                foreach (string name in validation.AllPlaceholders.Concat(missing))
                {
                    names.UnionWith(name.Split('.', '[', ']', ':', '(', ')', ' ').Where(n => n.Length > 0 && !n.StartsWith('@') && !roots.Contains(n)));
                }
            }

            if ((names.Count == namesBefore && items == itemsBefore) || pass >= MaximumPasses)
            {
                HashSet<string> reported = MissingNames(validation).ToHashSet(StringComparer.Ordinal);
                return new CatalogueCheck(
                    validation,
                    (fill?.MissingVariables ?? []).Where(reported.Add).ToList(),
                    fill is { IsSuccess: false } ? fill.ErrorMessage ?? "Filling the template failed." : null);
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

    private static ProcessingResult Fill(byte[] template, IReadOnlyDictionary<string, object?> witness)
    {
        using MemoryStream input = new MemoryStream(template, writable: false);
        using MemoryStream output = new MemoryStream();
        return new TemplateProcessor().ProcessTemplate(input, output, witness);
    }

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

        if (type.IsGenericType && type.Name.Contains('`', StringComparison.Ordinal))
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

    [GeneratedRegex(@"\[([0-9]{1,3})\]", RegexOptions.CultureInvariant)]
    private static partial Regex IndexPattern();

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

/// <summary>What <see cref="DataCatalog.CheckTemplate"/> found.</summary>
/// <param name="Validation">The last validation against the witness.</param>
/// <param name="FillMissing">The names only the trial fill found missing (whole paths of loop items).</param>
/// <param name="FillError">The trial fill's error (for example a loop over a value); null when it succeeded or did not run.</param>
internal sealed record CatalogueCheck(ValidationResult Validation, IReadOnlyList<string> FillMissing, string? FillError);

