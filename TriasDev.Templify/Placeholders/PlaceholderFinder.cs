// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Placeholders;

/// <summary>
/// Finds and extracts placeholder patterns in document text.
/// </summary>
/// <remarks>
/// This is an internal parsing helper that will become internal in 2.0. To list the placeholders of a
/// template, use <see cref="Core.DocumentTemplateProcessor.ValidateTemplate(Stream)"/> and read
/// <see cref="Core.ValidationResult.AllPlaceholders"/>.
/// </remarks>
[Obsolete("Internal parsing helper; will become internal in 2.0. Use DocumentTemplateProcessor.ValidateTemplate(...) and ValidationResult.AllPlaceholders to list placeholders.")]
public sealed class PlaceholderFinder
{
    /// <summary>
    /// Finds all placeholders in the specified text.
    /// </summary>
    /// <param name="text">The text to search for placeholders.</param>
    /// <returns>A collection of placeholder matches found in the text.</returns>
    public IEnumerable<PlaceholderMatch> FindPlaceholders(string text)
    {
        return PlaceholderScanner.FindPlaceholders(text).Select(ToMatch);
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
        return PlaceholderScanner.IsValidPlaceholder(text);
    }

    /// <summary>
    /// Extracts the variable name from a placeholder string.
    /// </summary>
    /// <param name="placeholder">The placeholder text (e.g., "{{VariableName}}").</param>
    /// <returns>The variable name if valid; otherwise, null.</returns>
    public string? ExtractVariableName(string placeholder)
    {
        return PlaceholderScanner.ExtractVariableName(placeholder);
    }

    /// <summary>
    /// Gets all unique variable names found in the text.
    /// </summary>
    /// <param name="text">The text to search.</param>
    /// <returns>A distinct collection of variable names.</returns>
    public IEnumerable<string> GetUniqueVariableNames(string text)
    {
        return PlaceholderScanner.GetUniqueVariableNames(text);
    }

    private static PlaceholderMatch ToMatch(PlaceholderToken token)
    {
        return new PlaceholderMatch
        {
            FullMatch = token.FullMatch,
            VariableName = token.VariableName,
            Format = token.Format,
            StartIndex = token.StartIndex,
            Length = token.Length
        };
    }
}
