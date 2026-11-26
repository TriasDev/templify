// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

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
public sealed class DataCatalog
{
    private const int MaximumDepth = 16;

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

    /// <summary>The kind of a path: its entry's kind, <see cref="DataKind.Open"/> below an open shape, or null.</summary>
    internal DataKind? KindOf(string path)
    {
        // items[0].name reads an item like items[].name.
        path = System.Text.RegularExpressions.Regex.Replace(path, @"\[\d+\]", "[]");
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
