// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using System.Xml.Linq;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.OpenDocument;

/// <summary>
/// Rewrites ranges of an OpenDocument paragraph's own text (see <see cref="OdtParagraphTextModel"/>) in place.
/// </summary>
/// <remarks>
/// <para>
/// Only the nodes that overlap a range are changed; spans outside the range, their formatting and all
/// non-text content (links, fields, frames, notes, bookmarks, …) stay untouched. Content located strictly
/// inside a replaced range (fields, frames, notes, …) is removed together with the text around it;
/// bookmarks, annotations, change marks and other markup are kept.
/// </para>
/// <para>
/// Replacement content is inserted where the first replaced character was, inside the same spans, so it
/// takes that character's formatting; text after the range keeps its own. The inserted text is encoded
/// so that it renders exactly as given: spaces that ODF would collapse become <c>text:s</c>, tabs become
/// <c>text:tab</c> and line breaks <c>text:line-break</c>.
/// </para>
/// </remarks>
internal static class OdtParagraphTextRewriter
{
    private static readonly XName _insertionMarker = XNamespace.Get("urn:triasdev:templify:internal") + "insert";

    private static readonly HashSet<XName> _keptMarkup = new HashSet<XName>
    {
        OdfNames.Text + "bookmark",
        OdfNames.Text + "bookmark-start",
        OdfNames.Text + "bookmark-end",
        OdfNames.Text + "change",
        OdfNames.Text + "change-start",
        OdfNames.Text + "change-end",
        OdfNames.SoftPageBreak,
        OdfNames.Number,
        OdfNames.OfficeAnnotation,
        OdfNames.OfficeAnnotationEnd,
    };

    /// <summary>
    /// Replaces the paragraph text in [<paramref name="start"/>, <paramref name="end"/>) with
    /// <paramref name="replacement"/>.
    /// </summary>
    /// <param name="paragraph">The paragraph (<c>text:p</c> or <c>text:h</c>).</param>
    /// <param name="start">The inclusive start offset in the paragraph text.</param>
    /// <param name="end">The exclusive end offset in the paragraph text.</param>
    /// <param name="replacement">The replacement content.</param>
    /// <param name="styleNameFor">
    /// Optional: returns the automatic text style that renders a formatted piece (markdown), or null to
    /// insert the piece unformatted.
    /// </param>
    /// <returns>True if the range was found and replaced; otherwise false.</returns>
    public static bool Replace(
        XElement paragraph,
        int start,
        int end,
        ReplacementContent replacement,
        Func<ReplacementPiece, string?>? styleNameFor = null)
    {
        OdtParagraphTextModel model = OdtParagraphTextModel.Build(paragraph);
        if (start < 0 || end > model.Text.Length || start >= end)
        {
            return false;
        }

        int firstIndex = model.FindPieceIndex(start);
        int lastIndex = model.FindPieceIndex(end - 1);
        if (firstIndex < 0 || lastIndex < 0)
        {
            return false;
        }

        bool precededByCollapsible = start == 0 || model.IsCollapsibleSpaceAt(start - 1);
        bool followedByCollapsible = end < model.Text.Length && model.IsCollapsibleSpaceAt(end);

        HashSet<XElement> touched = new HashSet<XElement>();
        XElement marker = new XElement(_insertionMarker);

        // The first piece is split at the start of the range; the marker goes in between.
        OdtTextPiece first = model.Pieces[firstIndex];
        AddParent(touched, first.Node);
        int firstLocalStart = start - first.Start;
        int firstLocalEnd = Math.Min(first.Length, end - first.Start);
        switch (first.Kind)
        {
            case OdtTextPieceKind.Text:
                XText text = (XText)first.Node;
                string value = text.Value;
                string suffix = value.Substring(firstLocalEnd);
                text.Value = value.Substring(0, firstLocalStart);
                text.AddAfterSelf(marker);
                if (suffix.Length > 0)
                {
                    marker.AddAfterSelf(new XText(suffix));
                }

                break;

            case OdtTextPieceKind.Spaces:
                XElement spaces = (XElement)first.Node;
                int trailingCount = first.Length - firstLocalEnd;
                if (firstLocalStart > 0)
                {
                    spaces.AddBeforeSelf(CreateSpaces(firstLocalStart));
                }

                spaces.AddBeforeSelf(marker);
                if (trailingCount > 0)
                {
                    spaces.AddBeforeSelf(CreateSpaces(trailingCount));
                }

                spaces.Remove();
                break;

            default:
                // Tabs and line breaks are single characters, entirely inside the range.
                first.Node.AddBeforeSelf(marker);
                first.Node.Remove();
                break;
        }

        // The other pieces overlapping the range lose their covered characters.
        for (int i = firstIndex + 1; i <= lastIndex; i++)
        {
            OdtTextPiece piece = model.Pieces[i];
            AddParent(touched, piece.Node);
            int localEnd = Math.Min(piece.Length, end - piece.Start);

            switch (piece.Kind)
            {
                case OdtTextPieceKind.Text:
                    XText text = (XText)piece.Node;
                    text.Value = text.Value.Substring(localEnd);
                    break;

                case OdtTextPieceKind.Spaces:
                    int remaining = piece.Length - localEnd;
                    if (remaining > 0)
                    {
                        SetSpaceCount((XElement)piece.Node, remaining);
                    }
                    else
                    {
                        piece.Node.Remove();
                    }

                    break;

                default:
                    piece.Node.Remove();
                    break;
            }
        }

        RemoveContentInside(model, start, end, touched);

        List<XNode> nodes = Encode(replacement, precededByCollapsible, followedByCollapsible, styleNameFor);
        AddParent(touched, marker);
        marker.ReplaceWith(nodes.ToArray());

        Prune(touched);
        return true;
    }

    /// <summary>
    /// Removes the given ranges of the paragraph text. Ranges must not overlap.
    /// </summary>
    public static void Remove(XElement paragraph, IEnumerable<(int Start, int End)> ranges)
    {
        // Right to left, so offsets of the remaining ranges stay valid.
        foreach ((int start, int end) in ranges.OrderByDescending(r => r.Start))
        {
            Replace(paragraph, start, end, ReplacementContent.Empty);
        }
    }

    /// <summary>
    /// Encodes replacement content as ODF nodes that render the text exactly.
    /// </summary>
    /// <param name="replacement">The content.</param>
    /// <param name="precededByCollapsible">
    /// Whether the character before the insertion point is literal white space (or the paragraph start),
    /// which would swallow a leading literal space.
    /// </param>
    /// <param name="followedByCollapsible">
    /// Whether the character after the insertion point is literal white space, which a trailing literal
    /// space would swallow.
    /// </param>
    /// <param name="styleNameFor">Optional style lookup for formatted pieces.</param>
    internal static List<XNode> Encode(
        ReplacementContent replacement,
        bool precededByCollapsible,
        bool followedByCollapsible,
        Func<ReplacementPiece, string?>? styleNameFor = null)
    {
        List<XNode> result = new List<XNode>();
        TextEncoder encoder = new TextEncoder(precededByCollapsible);
        bool endsWithPlainText = false;

        for (int i = 0; i < replacement.Pieces.Count; i++)
        {
            ReplacementPiece piece = replacement.Pieces[i];
            bool isLast = i == replacement.Pieces.Count - 1;

            if (piece.IsLineBreak)
            {
                encoder.Flush(result);
                result.Add(new XElement(OdfNames.LineBreak));
                encoder.ResetAfterElement();
                endsWithPlainText = false;
                continue;
            }

            if (piece.Text.Length == 0)
            {
                continue;
            }

            string? styleName = piece.HasFormatting ? styleNameFor?.Invoke(piece) : null;
            if (styleName == null)
            {
                encoder.Append(piece.Text, result);
                endsWithPlainText = true;
                continue;
            }

            encoder.Flush(result);
            List<XNode> content = new List<XNode>();
            encoder.Append(piece.Text, content);
            encoder.Flush(content);
            TrailingFixup(content, followedByCollapsible && isLast);
            result.Add(new XElement(OdfNames.Span, new XAttribute(OdfNames.StyleName, styleName), content));
            endsWithPlainText = false;
        }

        encoder.Flush(result);
        if (endsWithPlainText)
        {
            TrailingFixup(result, followedByCollapsible);
        }

        return result;
    }

    /// <summary>
    /// Turns a trailing literal space into <c>text:s</c> when literal white space follows, so neither is lost.
    /// </summary>
    private static void TrailingFixup(List<XNode> nodes, bool followedByCollapsible)
    {
        if (!followedByCollapsible || nodes.Count == 0 || nodes[^1] is not XText last || !last.Value.EndsWith(' '))
        {
            return;
        }

        last.Value = last.Value.Substring(0, last.Value.Length - 1);
        nodes.Add(CreateSpaces(1));
        if (last.Value.Length == 0)
        {
            nodes.Remove(last);
        }
    }

    private static XElement CreateSpaces(int count)
    {
        XElement element = new XElement(OdfNames.Space);
        if (count > 1)
        {
            element.SetAttributeValue(OdfNames.SpaceCount, count.ToString(CultureInfo.InvariantCulture));
        }

        return element;
    }

    private static void SetSpaceCount(XElement element, int count) =>
        element.SetAttributeValue(OdfNames.SpaceCount, count > 1 ? count.ToString(CultureInfo.InvariantCulture) : null);

    /// <summary>
    /// Removes the non-text content strictly inside (start, end): fields, frames, notes and other
    /// content. Bookmarks, annotations, change marks, index marks and other markup are kept.
    /// </summary>
    private static void RemoveContentInside(OdtParagraphTextModel model, int start, int end, HashSet<XElement> touched)
    {
        foreach (OdtContentAnchor anchor in model.Anchors)
        {
            if (anchor.Offset <= start || anchor.Offset >= end || IsKeptMarkup(anchor.Element))
            {
                continue;
            }

            AddParent(touched, anchor.Element);
            anchor.Element.Remove();
        }
    }

    private static bool IsKeptMarkup(XElement element) =>
        _keptMarkup.Contains(element.Name)
        || (element.Name.Namespace == OdfNames.Text
            && (element.Name.LocalName.EndsWith("-mark", StringComparison.Ordinal)
                || element.Name.LocalName.EndsWith("-mark-start", StringComparison.Ordinal)
                || element.Name.LocalName.EndsWith("-mark-end", StringComparison.Ordinal)));

    private static void AddParent(HashSet<XElement> touched, XNode node)
    {
        if (node.Parent != null)
        {
            touched.Add(node.Parent);
        }
    }

    /// <summary>
    /// Removes empty text nodes in the touched containers, and inline containers (spans, links)
    /// left without content.
    /// </summary>
    private static void Prune(IEnumerable<XElement> touched)
    {
        foreach (XElement container in touched)
        {
            foreach (XText empty in container.Nodes().OfType<XText>().Where(t => t.Value.Length == 0).ToList())
            {
                empty.Remove();
            }

            XElement? current = container;
            while (current != null
                   && current.Parent != null
                   && OdtParagraphTextModel.IsInlineContainer(current)
                   && !current.Nodes().Any(n => n is not XText t || t.Value.Length > 0))
            {
                XElement? parent = current.Parent;
                current.Remove();
                current = parent;
            }
        }
    }

    /// <summary>
    /// Encodes characters into text nodes and <c>text:s</c>/<c>text:tab</c> elements, tracking whether
    /// the previous rendered character is collapsible white space.
    /// </summary>
    private sealed class TextEncoder
    {
        private readonly StringBuilder _literal = new StringBuilder();
        private int _pendingSpaces;
        private bool _previousCollapsible;

        public TextEncoder(bool precededByCollapsible)
        {
            _previousCollapsible = precededByCollapsible;
        }

        public void Append(string text, List<XNode> target)
        {
            foreach (char c in text)
            {
                if (c is ' ' or '\r' or '\n')
                {
                    if (_pendingSpaces > 0 || _previousCollapsible)
                    {
                        FlushLiteral(target);
                        _pendingSpaces++;
                    }
                    else
                    {
                        _literal.Append(' ');
                        _previousCollapsible = true;
                    }
                }
                else if (c == '\t')
                {
                    Flush(target);
                    target.Add(new XElement(OdfNames.Tab));
                    ResetAfterElement();
                }
                else
                {
                    FlushSpaces(target);
                    _literal.Append(c);
                    _previousCollapsible = false;
                }
            }
        }

        public void Flush(List<XNode> target)
        {
            FlushLiteral(target);
            FlushSpaces(target);
        }

        public void ResetAfterElement() => _previousCollapsible = false;

        private void FlushLiteral(List<XNode> target)
        {
            if (_literal.Length > 0)
            {
                target.Add(new XText(_literal.ToString()));
                _literal.Clear();
            }
        }

        private void FlushSpaces(List<XNode> target)
        {
            if (_pendingSpaces > 0)
            {
                target.Add(CreateSpaces(_pendingSpaces));
                _pendingSpaces = 0;
                _previousCollapsible = false;
            }
        }
    }
}
