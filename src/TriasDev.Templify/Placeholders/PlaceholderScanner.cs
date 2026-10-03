// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace TriasDev.Templify.Placeholders;

/// <summary>
/// Finds placeholder patterns in text. Used by the library itself; the obsolete public
/// <c>PlaceholderFinder</c> delegates to it.
/// </summary>
internal static partial class PlaceholderScanner
{
    // Pattern: {{variableName}} or {{variableName:format}} or {{(expression):format}}
    // where variableName can be:
    // - Simple: Name, OrderId
    // - Nested with dots: Customer.Address.City
    // - Nested with brackets: Items[0], Settings[Theme]
    // - Mixed: Orders[0].Customer.Name
    // - Loop metadata: @index, @number, @first, @last, @count
    // - Current item: . or this (for primitive collections)
    // - Expression: (var1 and var2), (not IsActive), (Count > 0), ((var1 or var2) and var3)
    // Optional format specifier: :checkbox, :yesno, :currency, :number:N2, :date:yyyy-MM-dd, etc.
    private static readonly Regex _placeholderPattern = PlaceholderRegex();

    [GeneratedRegex(@"\{\{(\.|this|@?[\w\.\[\]]+|\([^\}]+\))(?::(\w+(?::[^\}]+)?))?\}\}")]
    private static partial Regex PlaceholderRegex();

    /// <summary>
    /// Finds all placeholders in the specified text, in order of appearance.
    /// </summary>
    public static IEnumerable<PlaceholderToken> FindPlaceholders(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        MatchCollection matches = _placeholderPattern.Matches(text);

        foreach (Match match in matches)
        {
            if (match.Success && match.Groups.Count >= 2)
            {
                // Group 1: Variable name
                // Group 2: Optional format specifier (captured by second group if present)
                string? format = match.Groups.Count >= 3 && match.Groups[2].Success
                    ? match.Groups[2].Value
                    : null;

                yield return new PlaceholderToken
                {
                    FullMatch = match.Value,
                    VariableName = match.Groups[1].Value,
                    Format = format,
                    StartIndex = match.Index,
                    Length = match.Length
                };
            }
        }
    }

    /// <summary>
    /// Finds all placeholders in the specified text and returns them as a list.
    /// </summary>
    public static IReadOnlyList<PlaceholderToken> FindPlaceholdersAsList(string text)
    {
        return FindPlaceholders(text).ToList();
    }

    /// <summary>
    /// Checks whether the specified text contains a valid placeholder.
    /// </summary>
    public static bool IsValidPlaceholder(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return _placeholderPattern.IsMatch(text);
    }

    /// <summary>
    /// Extracts the variable name of the first placeholder in the specified text; null if there is none.
    /// </summary>
    public static string? ExtractVariableName(string placeholder)
    {
        if (string.IsNullOrEmpty(placeholder))
        {
            return null;
        }

        Match match = _placeholderPattern.Match(placeholder);

        if (match.Success && match.Groups.Count >= 2)
        {
            return match.Groups[1].Value;
        }

        return null;
    }

    /// <summary>
    /// Gets all distinct variable names found in the text, sorted.
    /// </summary>
    public static IEnumerable<string> GetUniqueVariableNames(string text)
    {
        return FindPlaceholders(text)
            .Select(m => m.VariableName)
            .Distinct()
            .OrderBy(name => name);
    }
}
