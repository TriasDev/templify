// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.PropertyPaths;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Placeholders;

/// <summary>
/// The <c>:join</c> format specifier: renders a collection as one text, e.g. <c>{{Assets:join:Name:"; "}}</c>.
/// Arguments: an optional item property path (unquoted), then an optional separator in double quotes
/// (default <c>", "</c>; <c>""</c> joins without separator). <c>null</c> and empty items are skipped.
/// </summary>
internal sealed partial class JoinFormat
{
    private const string Keyword = "join";
    private const string DefaultSeparator = ", ";

    private readonly PropertyPath? _path;

    private JoinFormat(string? itemPath, PropertyPath? path, string separator)
    {
        ItemPath = itemPath;
        _path = path;
        Separator = separator;
    }

    /// <summary>The item property path as written (e.g. <c>Owner.Name</c>), or <c>null</c> to join the items.</summary>
    public string? ItemPath { get; }

    /// <summary>The separator between items.</summary>
    public string Separator { get; }

    [GeneratedRegex(@"^[\w.\[\]]+$", RegexOptions.CultureInvariant)]
    private static partial Regex PathRegex();

    /// <summary>
    /// Parses a placeholder format (the text after the first colon, e.g. <c>join:Name:"; "</c>). Returns <c>false</c>
    /// for any other format and for malformed join arguments, which then render as if no format were given.
    /// </summary>
    public static bool TryParse(string? format, [NotNullWhen(true)] out JoinFormat? join)
    {
        join = null;
        if (format == null || !format.StartsWith(Keyword, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string rest = ConditionLexer.NormalizeQuotes(format.Substring(Keyword.Length));
        if (rest.Length == 0)
        {
            join = new JoinFormat(null, null, DefaultSeparator);
            return true;
        }

        if (rest[0] != ':' || rest.Length == 1)
        {
            return false;
        }

        rest = rest.Substring(1);
        string? itemPath = null;
        PropertyPath? path = null;
        if (rest[0] != '"')
        {
            int colon = rest.IndexOf(':', StringComparison.Ordinal);
            itemPath = colon < 0 ? rest : rest.Substring(0, colon);
            if (!PathRegex().IsMatch(itemPath) || !PropertyPath.TryParse(itemPath, out path))
            {
                return false;
            }

            if (colon < 0)
            {
                join = new JoinFormat(itemPath, path, DefaultSeparator);
                return true;
            }

            rest = rest.Substring(colon + 1);
        }

        if (!TryReadQuoted(rest, out string? separator))
        {
            return false;
        }

        join = new JoinFormat(itemPath, path, separator);
        return true;
    }

    /// <summary>
    /// Whether <c>:join</c> applies to the value: any <see cref="IEnumerable"/> except strings and dictionaries
    /// (which includes <c>ExpandoObject</c>), or a <see cref="JsonElement"/> array.
    /// </summary>
    public static bool IsCollection(object? value) => value switch
    {
        null or string => false,
        JsonElement json => json.ValueKind == JsonValueKind.Array,
        IDictionary => false,
        _ when IsGenericDictionary(value.GetType()) => false,
        IEnumerable => true,
        _ => false,
    };

    /// <summary>Joins the items (or their <see cref="ItemPath"/> values), skipping <c>null</c> and empty texts.</summary>
    /// <param name="collection">A value for which <see cref="IsCollection"/> is <c>true</c>.</param>
    /// <param name="culture">The culture numbers and dates are formatted with.</param>
    public string Join(object collection, CultureInfo culture)
    {
        List<string> parts = new List<string>();
        foreach (object? item in GetItems(collection))
        {
            if (item == null || !TryGetItemValue(item, out object? value))
            {
                continue;
            }

            string text = ValueConverter.ConvertToString(value, culture);
            if (text.Length > 0)
            {
                parts.Add(text);
            }
        }

        return string.Join(Separator, parts);
    }

    /// <summary>
    /// Whether the collection has at least one non-null item and <see cref="ItemPath"/> resolves on none of them
    /// (likely a misspelled property). <c>false</c> when there is no item path.
    /// </summary>
    public bool IsItemPathMissingOnAllItems(object collection)
    {
        if (_path == null)
        {
            return false;
        }

        bool hasItem = false;
        foreach (object? item in GetItems(collection))
        {
            if (item == null)
            {
                continue;
            }

            hasItem = true;
            if (PropertyPathResolver.TryResolvePath(item, _path, out _))
            {
                return false;
            }
        }

        return hasItem;
    }

    /// <summary>
    /// For a placeholder <paramref name="variableName"/> with <paramref name="format"/> and resolved
    /// <paramref name="value"/>: returns the qualified name (<c>Assets.Name</c>) when the value is joined by an item
    /// property that no item has.
    /// </summary>
    public static bool TryGetMissingItemVariable(
        string variableName,
        object? value,
        string? format,
        [NotNullWhen(true)] out string? missingVariable)
    {
        missingVariable = null;
        if (!TryParse(format, out JoinFormat? join) || !IsCollection(value) || !join.IsItemPathMissingOnAllItems(value!))
        {
            return false;
        }

        missingVariable = $"{variableName}.{join.ItemPath}";
        return true;
    }

    private bool TryGetItemValue(object item, out object? value)
    {
        if (_path == null)
        {
            value = item;
            return true;
        }

        return PropertyPathResolver.TryResolvePath(item, _path, out value);
    }

    private static IEnumerable GetItems(object collection) =>
        collection is JsonElement json
            ? (IEnumerable)JsonDataParser.ConvertJsonElementToObject(json)
            : (IEnumerable)collection;

    private static bool IsGenericDictionary(Type type) =>
        type.GetInterfaces().Any(i => i.IsGenericType
            && (i.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                || i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));

    /// <summary>Reads <c>"..."</c> spanning the whole text, with <c>\"</c> and <c>\\</c> escapes.</summary>
    private static bool TryReadQuoted(string text, [NotNullWhen(true)] out string? value)
    {
        value = null;
        if (text.Length < 2 || text[0] != '"')
        {
            return false;
        }

        StringBuilder result = new StringBuilder();
        for (int i = 1; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\\' && i + 1 < text.Length && text[i + 1] is '"' or '\\')
            {
                result.Append(text[++i]);
            }
            else if (c == '"')
            {
                if (i != text.Length - 1)
                {
                    return false;
                }

                value = result.ToString();
                return true;
            }
            else
            {
                result.Append(c);
            }
        }

        return false;
    }
}
