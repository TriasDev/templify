// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace TriasDev.Templify.Placeholders;

/// <summary>
/// Finds and extracts placeholder patterns in document text.
/// </summary>
public sealed class PlaceholderFinder
{
    // Pattern: {{variableName}} or {{variableName:format}} or {{(expression):format}}
    // where variableName can be:
    // - Simple: Name, OrderId
    // - Nested with dots: Customer.Address.City
    // - Nested with brackets: Items[0], Settings[Theme]
    // - Mixed: Orders[0].Customer.Name
    // - Loop metadata: @index, @first, @last, @count
    // - Current item: . or this (for primitive collections)
    // - Expression: (var1 and var2), (not IsActive), (Count > 0), ((var1 or var2) and var3)
    // Optional format specifier: :checkbox, :yesno, :checkmark, etc.
    private static readonly Regex PlaceholderPattern = new(
        @"\{\{(\.|this|@?[\w\.\[\]]+|\([^\}]+\))(?::(\w+))?\}\}",
        RegexOptions.Compiled);

    /// <summary>
    /// Finds all placeholders in the specified text.
    /// </summary>
    /// <param name="text">The text to search for placeholders.</param>
    /// <returns>A collection of placeholder matches found in the text.</returns>
    public IEnumerable<PlaceholderMatch> FindPlaceholders(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        MatchCollection matches = PlaceholderPattern.Matches(text);

        foreach (Match match in matches)
        {
            if (match.Success && match.Groups.Count >= 2)
            {
                // Group 1: Variable name
                // Group 2: Optional format specifier (captured by second group if present)
                string? format = match.Groups.Count >= 3 && match.Groups[2].Success
                    ? match.Groups[2].Value
                    : null;

                yield return new PlaceholderMatch
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
    /// <param name="text">The text to search for placeholders.</param>
    /// <returns>A list of placeholder matches found in the text.</returns>
    public IReadOnlyList<PlaceholderMatch> FindPlaceholdersAsList(string text)
    {
        return FindPlaceholders(text).ToList();
    }

    /// <summary>
    /// Checks if the specified text is a valid placeholder.
    /// </summary>
    /// <param name="text">The text to validate.</param>
    /// <returns>True if the text is a valid placeholder; otherwise, false.</returns>
    public bool IsValidPlaceholder(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return PlaceholderPattern.IsMatch(text);
    }

    /// <summary>
    /// Extracts the variable name from a placeholder string.
    /// </summary>
    /// <param name="placeholder">The placeholder text (e.g., "{{VariableName}}").</param>
    /// <returns>The variable name if valid; otherwise, null.</returns>
    public string? ExtractVariableName(string placeholder)
    {
        if (string.IsNullOrEmpty(placeholder))
        {
            return null;
        }

        Match match = PlaceholderPattern.Match(placeholder);

        if (match.Success && match.Groups.Count >= 2)
        {
            return match.Groups[1].Value;
        }

        return null;
    }

    /// <summary>
    /// Gets all unique variable names found in the text.
    /// </summary>
    /// <param name="text">The text to search.</param>
    /// <returns>A distinct collection of variable names.</returns>
    public IEnumerable<string> GetUniqueVariableNames(string text)
    {
        return FindPlaceholders(text)
            .Select(m => m.VariableName)
            .Distinct()
            .OrderBy(name => name);
    }
}
