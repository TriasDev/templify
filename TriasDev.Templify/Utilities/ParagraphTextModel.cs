// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace TriasDev.Templify.Utilities;

/// <summary>
/// A text element (<c>w:t</c>) that contributes to a paragraph's text.
/// </summary>
/// <param name="Element">The text element.</param>
/// <param name="Run">The run that owns the text element (its formatting source).</param>
/// <param name="Start">The offset of the element's first character in the paragraph text.</param>
/// <param name="Length">The number of characters the element contributes.</param>
internal sealed record ParagraphTextSegment(Text Element, Run Run, int Start, int Length)
{
    /// <summary>Gets the exclusive end offset of the segment in the paragraph text.</summary>
    public int End => Start + Length;
}

/// <summary>
/// A non-text element of a paragraph (tab, break, drawing, field character, bookmark, …)
/// located between two characters of the paragraph text.
/// </summary>
/// <param name="Element">The non-text element.</param>
/// <param name="Offset">The number of paragraph-text characters that precede the element.</param>
internal sealed record ParagraphContentAnchor(OpenXmlElement Element, int Offset);

/// <summary>
/// A snapshot of a paragraph's own text-bearing content: the text of its runs, where each
/// character comes from, and the non-text content between the characters.
/// </summary>
/// <remarks>
/// <para>
/// The paragraph text is the concatenation of the <c>w:t</c> elements of the paragraph's own runs,
/// including runs nested in inline containers (hyperlinks, simple fields, inline content controls,
/// custom XML, tracked insertions). It deliberately excludes field instructions
/// (<c>w:instrText</c>), deleted text and the content of nested paragraphs (e.g. text boxes),
/// so a range of this text can always be mapped back to exactly one set of text elements.
/// </para>
/// <para>
/// The snapshot is invalidated by any change to the paragraph; build a new one after editing.
/// </para>
/// </remarks>
internal sealed class ParagraphTextModel
{
    private ParagraphTextModel(
        Paragraph paragraph,
        string text,
        IReadOnlyList<ParagraphTextSegment> segments,
        IReadOnlyList<ParagraphContentAnchor> anchors)
    {
        Paragraph = paragraph;
        Text = text;
        Segments = segments;
        Anchors = anchors;
    }

    /// <summary>Gets the paragraph this model describes.</summary>
    public Paragraph Paragraph { get; }

    /// <summary>Gets the paragraph's own text.</summary>
    public string Text { get; }

    /// <summary>Gets the text segments in document order.</summary>
    public IReadOnlyList<ParagraphTextSegment> Segments { get; }

    /// <summary>Gets the non-text content in document order.</summary>
    public IReadOnlyList<ParagraphContentAnchor> Anchors { get; }

    /// <summary>
    /// Builds the text model of a paragraph.
    /// </summary>
    public static ParagraphTextModel Build(Paragraph paragraph)
    {
        Builder builder = new Builder();
        builder.VisitContainer(paragraph);
        return new ParagraphTextModel(paragraph, builder.Text.ToString(), builder.Segments, builder.Anchors);
    }

    /// <summary>
    /// Gets the paragraph's own text (see <see cref="ParagraphTextModel"/> for what is included).
    /// </summary>
    public static string GetText(Paragraph paragraph) => Build(paragraph).Text;

    /// <summary>
    /// Returns the index of the segment containing the character at <paramref name="offset"/>, or -1.
    /// </summary>
    public int FindSegmentIndex(int offset)
    {
        for (int i = 0; i < Segments.Count; i++)
        {
            ParagraphTextSegment segment = Segments[i];
            if (offset >= segment.Start && offset < segment.End)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Checks whether an element is an inline container whose runs belong to the enclosing paragraph.
    /// </summary>
    internal static bool IsRunContainer(OpenXmlElement element) =>
        element is Hyperlink
            or SimpleField
            or SdtRun
            or SdtContentRun
            or InsertedRun
            or MoveToRun
            or CustomXmlRun;

    private sealed class Builder
    {
        public StringBuilder Text { get; } = new StringBuilder();

        public List<ParagraphTextSegment> Segments { get; } = new List<ParagraphTextSegment>();

        public List<ParagraphContentAnchor> Anchors { get; } = new List<ParagraphContentAnchor>();

        public void VisitContainer(OpenXmlElement container)
        {
            foreach (OpenXmlElement child in container.ChildElements)
            {
                switch (child)
                {
                    case ParagraphProperties:
                        break;
                    case Run run:
                        VisitRun(run);
                        break;
                    case OpenXmlElement inline when IsRunContainer(inline):
                        VisitContainer(inline);
                        break;
                    default:
                        // Bookmarks, comment ranges, proofing marks, deleted runs, math, …
                        Anchors.Add(new ParagraphContentAnchor(child, Text.Length));
                        break;
                }
            }
        }

        private void VisitRun(Run run)
        {
            foreach (OpenXmlElement child in run.ChildElements)
            {
                switch (child)
                {
                    case RunProperties:
                        break;
                    case Text text:
                        string value = text.Text;
                        Segments.Add(new ParagraphTextSegment(text, run, Text.Length, value.Length));
                        Text.Append(value);
                        break;
                    default:
                        // Tabs, breaks, drawings, pictures (text boxes), field characters,
                        // field instructions, footnote references, symbols, …
                        Anchors.Add(new ParagraphContentAnchor(child, Text.Length));
                        break;
                }
            }
        }
    }
}
