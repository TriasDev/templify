// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace TriasDev.Templify.Utilities;

/// <summary>
/// Rewrites ranges of a paragraph's own text (see <see cref="ParagraphTextModel"/>) in place.
/// </summary>
/// <remarks>
/// <para>
/// Only the text elements that overlap a range are changed; runs outside the range, their
/// formatting and all non-text content (hyperlinks, fields, drawings, bookmarks, …) stay untouched.
/// Simple inline content located strictly inside a replaced range (tabs, breaks, symbols) is
/// removed together with the text around it.
/// </para>
/// <para>
/// Replacement text takes the formatting of the run holding the first replaced character;
/// text after the range keeps the formatting of its own run.
/// </para>
/// </remarks>
internal static class ParagraphTextRewriter
{
    /// <summary>
    /// Replaces the paragraph text in [<paramref name="start"/>, <paramref name="end"/>) with
    /// <paramref name="replacement"/>.
    /// </summary>
    /// <returns>True if the range was found and replaced; otherwise false.</returns>
    public static bool Replace(Paragraph paragraph, int start, int end, ReplacementContent replacement)
    {
        ParagraphTextModel model = ParagraphTextModel.Build(paragraph);
        if (start < 0 || end > model.Text.Length || start >= end)
        {
            return false;
        }

        int firstIndex = model.FindSegmentIndex(start);
        int lastIndex = model.FindSegmentIndex(end - 1);
        if (firstIndex < 0 || lastIndex < 0)
        {
            return false;
        }

        ParagraphTextSegment first = model.Segments[firstIndex];
        ParagraphTextSegment last = model.Segments[lastIndex];
        string prefix = first.Element.Text.Substring(0, start - first.Start);
        string suffix = last.Element.Text.Substring(end - last.Start);
        RunProperties? baseProperties = first.Run.RunProperties;

        HashSet<Run> touchedRuns = new HashSet<Run> { first.Run, last.Run };

        RemoveContentInside(model, start, end, touchedRuns);

        // Segments fully inside the range lose their text.
        for (int i = firstIndex + 1; i < lastIndex; i++)
        {
            ParagraphTextSegment middle = model.Segments[i];
            touchedRuns.Add(middle.Run);
            middle.Element.Remove();
        }

        bool sameSegment = firstIndex == lastIndex;

        if (replacement.IsPlainText)
        {
            string text = replacement.PlainText;
            if (sameSegment)
            {
                SetText(first.Element, prefix + text + suffix);
            }
            else
            {
                SetText(first.Element, prefix + text);
                SetText(last.Element, suffix);
            }
        }
        else
        {
            SetText(first.Element, prefix);
            if (!sameSegment)
            {
                SetText(last.Element, suffix);
            }

            // Split the first run after its text element, so the new runs can go in between.
            Run tail = SplitRunAfter(first.Run, first.Element);
            touchedRuns.Add(tail);
            if (sameSegment && suffix.Length > 0)
            {
                Text suffixElement = new Text(suffix) { Space = SpaceProcessingModeValues.Preserve };
                if (tail.RunProperties != null)
                {
                    tail.RunProperties.InsertAfterSelf(suffixElement);
                }
                else
                {
                    tail.PrependChild(suffixElement);
                }
            }

            OpenXmlElement insertAfter = first.Run;
            foreach (Run run in CreateRuns(replacement, baseProperties))
            {
                insertAfter.InsertAfterSelf(run);
                insertAfter = run;
            }
        }

        RemoveEmptyText(touchedRuns);
        PruneEmptyRuns(touchedRuns);
        return true;
    }

    /// <summary>
    /// Removes the given ranges of the paragraph text. Ranges must not overlap.
    /// </summary>
    public static void Remove(Paragraph paragraph, IEnumerable<(int Start, int End)> ranges)
    {
        // Right to left, so offsets of the remaining ranges stay valid.
        foreach ((int start, int end) in ranges.OrderByDescending(r => r.Start))
        {
            Replace(paragraph, start, end, ReplacementContent.Empty);
        }
    }

    /// <summary>
    /// Removes the non-text content strictly inside (start, end) that belongs to the removed text.
    /// </summary>
    private static void RemoveContentInside(ParagraphTextModel model, int start, int end, HashSet<Run> touchedRuns)
    {
        foreach (ParagraphContentAnchor anchor in model.Anchors)
        {
            if (anchor.Offset <= start || anchor.Offset >= end || !IsRemovableInlineContent(anchor.Element))
            {
                continue;
            }

            if (anchor.Element.Parent is Run run)
            {
                touchedRuns.Add(run);
            }

            anchor.Element.Remove();
        }
    }

    /// <summary>
    /// Checks whether an element is simple inline content that is removed with the text around it.
    /// </summary>
    private static bool IsRemovableInlineContent(OpenXmlElement element) =>
        element is TabChar
            or Break
            or CarriageReturn
            or PositionalTab
            or NoBreakHyphen
            or SoftHyphen
            or SymbolChar
            or LastRenderedPageBreak;

    private static void SetText(Text element, string text)
    {
        element.Text = text;
        element.Space = SpaceProcessingModeValues.Preserve;
    }

    /// <summary>
    /// Moves everything after <paramref name="pivot"/> in <paramref name="run"/> into a new run with the
    /// same properties, inserted right after <paramref name="run"/>.
    /// </summary>
    private static Run SplitRunAfter(Run run, OpenXmlElement pivot)
    {
        Run tail = (Run)run.CloneNode(false);
        if (run.RunProperties != null)
        {
            tail.RunProperties = (RunProperties)run.RunProperties.CloneNode(true);
        }

        List<OpenXmlElement> trailing = new List<OpenXmlElement>();
        for (OpenXmlElement? sibling = pivot.NextSibling(); sibling != null; sibling = sibling.NextSibling())
        {
            trailing.Add(sibling);
        }

        foreach (OpenXmlElement element in trailing)
        {
            element.Remove();
            tail.AppendChild(element);
        }

        run.InsertAfterSelf(tail);
        return tail;
    }

    private static IEnumerable<Run> CreateRuns(ReplacementContent replacement, RunProperties? baseProperties)
    {
        foreach (ReplacementPiece piece in replacement.Pieces)
        {
            if (piece.IsLineBreak)
            {
                yield return new Run(new Break());
                continue;
            }

            if (piece.Text.Length == 0)
            {
                continue;
            }

            Run run = new Run(new Text(piece.Text) { Space = SpaceProcessingModeValues.Preserve });
            RunProperties? properties = FormattingPreserver.CloneRunProperties(baseProperties);
            if (piece.HasFormatting)
            {
                properties = FormattingPreserver.ApplyMarkdownFormatting(
                    properties,
                    piece.IsBold,
                    piece.IsItalic,
                    piece.IsStrikethrough);
            }

            FormattingPreserver.ApplyRunProperties(run, properties);
            yield return run;
        }
    }

    private static void RemoveEmptyText(IEnumerable<Run> runs)
    {
        foreach (Run run in runs)
        {
            foreach (Text text in run.Elements<Text>().Where(t => t.Text.Length == 0).ToList())
            {
                text.Remove();
            }
        }
    }

    /// <summary>
    /// Removes runs that no longer have content, and inline containers left without runs.
    /// </summary>
    private static void PruneEmptyRuns(IEnumerable<Run> runs)
    {
        foreach (Run run in runs)
        {
            if (run.Parent == null || run.ChildElements.Any(c => c is not RunProperties))
            {
                continue;
            }

            OpenXmlElement? container = run.Parent;
            run.Remove();

            // Remove inline wrappers (hyperlinks, custom XML, …) that lost their last child.
            // Content controls are kept: an empty content control is still meaningful.
            while (container is Hyperlink or SimpleField or CustomXmlRun or InsertedRun or MoveToRun
                   && !container.ChildElements.Any(IsContent))
            {
                OpenXmlElement? parent = container.Parent;
                container.Remove();
                container = parent;
            }
        }
    }

    private static bool IsContent(OpenXmlElement element) =>
        element is not CustomXmlProperties;
}
