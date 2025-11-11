namespace TriasDev.Templify.Placeholders;

/// <summary>
/// Represents a placeholder found in the document text.
/// </summary>
public sealed class PlaceholderMatch
{
    /// <summary>
    /// Gets the full placeholder text including delimiters (e.g., "{{VariableName}}").
    /// </summary>
    public string FullMatch { get; init; } = string.Empty;

    /// <summary>
    /// Gets the variable name without delimiters (e.g., "VariableName").
    /// </summary>
    public string VariableName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the zero-based starting index of the placeholder in the source text.
    /// </summary>
    public int StartIndex { get; init; }

    /// <summary>
    /// Gets the length of the placeholder in the source text.
    /// </summary>
    public int Length { get; init; }
}
