namespace TriasDev.Templify.Markdown;

/// <summary>
/// Represents a segment of text with associated markdown formatting.
/// </summary>
internal sealed class MarkdownSegment
{
    /// <summary>
    /// Gets the text content of this segment.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets whether this segment should be rendered in bold.
    /// </summary>
    public bool IsBold { get; }

    /// <summary>
    /// Gets whether this segment should be rendered in italic.
    /// </summary>
    public bool IsItalic { get; }

    /// <summary>
    /// Gets whether this segment should be rendered with strikethrough.
    /// </summary>
    public bool IsStrikethrough { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownSegment"/> class.
    /// </summary>
    /// <param name="text">The text content.</param>
    /// <param name="isBold">Whether the text is bold.</param>
    /// <param name="isItalic">Whether the text is italic.</param>
    /// <param name="isStrikethrough">Whether the text has strikethrough.</param>
    public MarkdownSegment(string text, bool isBold = false, bool isItalic = false, bool isStrikethrough = false)
    {
        Text = text;
        IsBold = isBold;
        IsItalic = isItalic;
        IsStrikethrough = isStrikethrough;
    }
}
