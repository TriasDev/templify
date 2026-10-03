// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Placeholders;

/// <summary>
/// A placeholder found in text by <see cref="PlaceholderScanner"/>. Internal counterpart of the obsolete
/// public <c>PlaceholderMatch</c>, with the same shape.
/// </summary>
internal sealed class PlaceholderToken
{
    /// <summary>
    /// Gets the full placeholder text including delimiters (e.g., "{{VariableName}}" or "{{VariableName:format}}").
    /// </summary>
    public string FullMatch { get; init; } = string.Empty;

    /// <summary>
    /// Gets the variable name without delimiters and format specifier. For expressions, the full expression text.
    /// </summary>
    public string VariableName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional format specifier (e.g., "checkbox", "yesno"). Null if no format specified.
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// Gets whether this placeholder contains an expression (starts with parenthesis).
    /// </summary>
    public bool IsExpression => VariableName.StartsWith('(');

    /// <summary>
    /// Gets the zero-based starting index of the placeholder in the source text.
    /// </summary>
    public int StartIndex { get; init; }

    /// <summary>
    /// Gets the length of the placeholder in the source text.
    /// </summary>
    public int Length { get; init; }
}
