// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using System.Xml.Linq;

namespace TriasDev.Templify.OpenDocument;

/// <summary>
/// Kind of a piece of paragraph text.
/// </summary>
internal enum OdtTextPieceKind
{
    /// <summary>A text node; every character is editable.</summary>
    Text,

    /// <summary>A <c>text:s</c> element contributing <c>text:c</c> spaces.</summary>
    Spaces,

    /// <summary>A <c>text:tab</c> element contributing <c>\t</c>.</summary>
    Tab,

    /// <summary>A <c>text:line-break</c> element contributing <c>\n</c>.</summary>
    LineBreak,
}

/// <summary>
/// A node that contributes characters to a paragraph's text.
/// </summary>
/// <param name="Node">The text node (<see cref="XText"/>) or atom element (<c>text:s</c>, <c>text:tab</c>, <c>text:line-break</c>).</param>
/// <param name="Kind">The kind of the piece.</param>
/// <param name="Start">The offset of the piece's first character in the paragraph text.</param>
/// <param name="Length">The number of characters the piece contributes.</param>
internal sealed record OdtTextPiece(XNode Node, OdtTextPieceKind Kind, int Start, int Length)
{
    /// <summary>Gets the exclusive end offset of the piece in the paragraph text.</summary>
    public int End => Start + Length;
}

/// <summary>
/// An element of a paragraph that contributes no text (bookmark, field, frame, note, annotation, …),
/// located between two characters of the paragraph text.
/// </summary>
/// <param name="Element">The element.</param>
/// <param name="Offset">The number of paragraph-text characters that precede the element.</param>
internal sealed record OdtContentAnchor(XElement Element, int Offset);

/// <summary>
/// A snapshot of an OpenDocument paragraph's (<c>text:p</c>, <c>text:h</c>) own text: the text of its
/// text nodes and space/tab/line-break elements, where each character comes from, and the
/// non-text content between the characters.
/// </summary>
/// <remarks>
/// <para>
/// Inline containers (<c>text:span</c>, <c>text:a</c>, <c>text:meta</c>) are entered; every other
/// element is an anchor. In particular, fields (their element holds the displayed value), notes,
/// frames and annotations contribute no text, so their content (which may hold paragraphs of its
/// own) is never part of this paragraph's text.
/// </para>
/// <para>
/// Text nodes map 1:1 to the text; tab, CR and LF characters inside text nodes (which ODF renders as
/// spaces) appear as spaces. White space is not collapsed.
/// </para>
/// <para>
/// The snapshot is invalidated by any change to the paragraph; build a new one after editing.
/// </para>
/// </remarks>
internal sealed class OdtParagraphTextModel
{
    private OdtParagraphTextModel(
        XElement paragraph,
        string text,
        IReadOnlyList<OdtTextPiece> pieces,
        IReadOnlyList<OdtContentAnchor> anchors)
    {
        Paragraph = paragraph;
        Text = text;
        Pieces = pieces;
        Anchors = anchors;
    }

    /// <summary>Gets the paragraph this model describes.</summary>
    public XElement Paragraph { get; }

    /// <summary>Gets the paragraph's own text.</summary>
    public string Text { get; }

    /// <summary>Gets the text pieces in document order (empty text nodes excluded).</summary>
    public IReadOnlyList<OdtTextPiece> Pieces { get; }

    /// <summary>Gets the non-text content in document order.</summary>
    public IReadOnlyList<OdtContentAnchor> Anchors { get; }

    /// <summary>
    /// Builds the text model of a paragraph.
    /// </summary>
    public static OdtParagraphTextModel Build(XElement paragraph)
    {
        Builder builder = new Builder();
        builder.VisitContainer(paragraph);
        return new OdtParagraphTextModel(paragraph, builder.Text.ToString(), builder.Pieces, builder.Anchors);
    }

    /// <summary>
    /// Gets the paragraph's own text (see <see cref="OdtParagraphTextModel"/> for what is included).
    /// </summary>
    public static string GetText(XElement paragraph) => Build(paragraph).Text;

    /// <summary>
    /// Returns the index of the piece containing the character at <paramref name="offset"/>, or -1.
    /// </summary>
    public int FindPieceIndex(int offset)
    {
        for (int i = 0; i < Pieces.Count; i++)
        {
            OdtTextPiece piece = Pieces[i];
            if (offset >= piece.Start && offset < piece.End)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Checks whether the character at <paramref name="offset"/> is literal white space in a text
    /// node, which ODF collapses with adjacent literal white space.
    /// </summary>
    public bool IsCollapsibleSpaceAt(int offset)
    {
        int index = FindPieceIndex(offset);
        return index >= 0 && Pieces[index].Kind == OdtTextPieceKind.Text && Text[offset] == ' ';
    }

    /// <summary>
    /// Checks whether an element is an inline container whose content belongs to the enclosing paragraph.
    /// </summary>
    internal static bool IsInlineContainer(XElement element) =>
        element.Name == OdfNames.Span || element.Name == OdfNames.Link || element.Name == OdfNames.TextMeta;

    /// <summary>
    /// Reads the space count of a <c>text:s</c> element (default 1).
    /// </summary>
    internal static int GetSpaceCount(XElement space)
    {
        string? value = space.Attribute(OdfNames.SpaceCount)?.Value;
        return value != null && int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int count) && count > 0
            ? count
            : 1;
    }

    private sealed class Builder
    {
        public StringBuilder Text { get; } = new StringBuilder();

        public List<OdtTextPiece> Pieces { get; } = new List<OdtTextPiece>();

        public List<OdtContentAnchor> Anchors { get; } = new List<OdtContentAnchor>();

        public void VisitContainer(XElement container)
        {
            foreach (XNode node in container.Nodes())
            {
                switch (node)
                {
                    case XText text:
                        AddText(text);
                        break;
                    case XElement element when IsInlineContainer(element):
                        VisitContainer(element);
                        break;
                    case XElement element when element.Name == OdfNames.Space:
                        int count = GetSpaceCount(element);
                        AddPiece(element, OdtTextPieceKind.Spaces, new string(' ', count));
                        break;
                    case XElement element when element.Name == OdfNames.Tab:
                        AddPiece(element, OdtTextPieceKind.Tab, "\t");
                        break;
                    case XElement element when element.Name == OdfNames.LineBreak:
                        AddPiece(element, OdtTextPieceKind.LineBreak, "\n");
                        break;
                    case XElement element:
                        Anchors.Add(new OdtContentAnchor(element, Text.Length));
                        break;
                }

                // Comments and processing instructions carry no text and are left alone.
            }
        }

        private void AddText(XText text)
        {
            string value = text.Value;
            if (value.Length == 0)
            {
                return;
            }

            Pieces.Add(new OdtTextPiece(text, OdtTextPieceKind.Text, Text.Length, value.Length));
            foreach (char c in value)
            {
                Text.Append(c is '\t' or '\r' or '\n' ? ' ' : c);
            }
        }

        private void AddPiece(XElement element, OdtTextPieceKind kind, string text)
        {
            Pieces.Add(new OdtTextPiece(element, kind, Text.Length, text.Length));
            Text.Append(text);
        }
    }
}
