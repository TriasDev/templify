// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Markdown;

namespace TriasDev.Templify.Utilities;

/// <summary>
/// One piece of replacement content: either a run of text with optional markdown formatting,
/// or a line break.
/// </summary>
internal readonly record struct ReplacementPiece(
    string Text,
    bool IsBold = false,
    bool IsItalic = false,
    bool IsStrikethrough = false,
    bool IsLineBreak = false)
{
    /// <summary>Gets a line break piece.</summary>
    public static ReplacementPiece LineBreak { get; } = new ReplacementPiece(string.Empty, IsLineBreak: true);

    /// <summary>Gets whether the piece carries formatting of its own.</summary>
    public bool HasFormatting => IsBold || IsItalic || IsStrikethrough;
}

/// <summary>
/// Format-agnostic description of the text that replaces a range of a paragraph's text:
/// a sequence of formatted text pieces and line breaks.
/// </summary>
internal sealed class ReplacementContent
{
    /// <summary>
    /// Newline separators for splitting text. Order matters: \r\n must come before \r and \n.
    /// </summary>
    private static readonly string[] _newlineSeparators = { "\r\n", "\r", "\n" };

    private ReplacementContent(IReadOnlyList<ReplacementPiece> pieces)
    {
        Pieces = pieces;
    }

    /// <summary>Gets an empty replacement (the range is removed).</summary>
    public static ReplacementContent Empty { get; } = new ReplacementContent(Array.Empty<ReplacementPiece>());

    /// <summary>Gets the pieces in order.</summary>
    public IReadOnlyList<ReplacementPiece> Pieces { get; }

    /// <summary>
    /// Gets whether the content is (at most) a single unformatted text piece, which can be
    /// written into the existing text element without creating new runs.
    /// </summary>
    public bool IsPlainText => Pieces.Count == 0 || (Pieces.Count == 1 && !Pieces[0].IsLineBreak && !Pieces[0].HasFormatting);

    /// <summary>Gets the concatenated text of all pieces (line breaks excluded).</summary>
    public string PlainText => string.Concat(Pieces.Select(p => p.Text));

    /// <summary>Creates content consisting of a single unformatted text piece.</summary>
    public static ReplacementContent FromText(string text) =>
        string.IsNullOrEmpty(text) ? Empty : new ReplacementContent(new[] { new ReplacementPiece(text) });

    /// <summary>
    /// Creates content from a placeholder value, optionally turning newlines into line breaks
    /// and rendering markdown (<c>**bold**</c>, <c>*italic*</c>, <c>~~strike~~</c>) as formatting.
    /// </summary>
    /// <param name="value">The replacement value.</param>
    /// <param name="splitNewlines">Whether \r\n, \r and \n become line breaks.</param>
    /// <param name="parseMarkdown">Whether markdown syntax becomes formatting.</param>
    public static ReplacementContent FromValue(string value, bool splitNewlines, bool parseMarkdown)
    {
        bool hasNewlines = splitNewlines && (value.Contains('\n') || value.Contains('\r'));
        bool hasMarkdown = parseMarkdown && MarkdownParser.ContainsMarkdown(value);

        if (!hasNewlines && !hasMarkdown)
        {
            return FromText(value);
        }

        List<ReplacementPiece> pieces = new List<ReplacementPiece>();

        if (!hasNewlines)
        {
            AddMarkdownPieces(pieces, value);
            return new ReplacementContent(pieces);
        }

        // Split on newlines first; with markdown enabled, each line is parsed on its own.
        string[] lines = value.Split(_newlineSeparators, StringSplitOptions.None);
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                pieces.Add(ReplacementPiece.LineBreak);
            }

            string line = lines[i];
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (hasMarkdown && MarkdownParser.ContainsMarkdown(line))
            {
                AddMarkdownPieces(pieces, line);
            }
            else
            {
                pieces.Add(new ReplacementPiece(line));
            }
        }

        return new ReplacementContent(pieces);
    }

    private static void AddMarkdownPieces(List<ReplacementPiece> pieces, string text)
    {
        foreach (MarkdownSegment segment in MarkdownParser.Parse(text))
        {
            pieces.Add(new ReplacementPiece(segment.Text, segment.IsBold, segment.IsItalic, segment.IsStrikethrough));
        }
    }
}
