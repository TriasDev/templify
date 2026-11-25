// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Core;

/// <summary>
/// Reports block markers whose paragraph or row holds other text, which processing removes with the marker (#253).
/// Runs the processing detectors over the same block containers the <c>DocumentWalker</c> visits: block lists (body,
/// header, footer, note, table cell, content control, text box), row lists of tables, and the content of loops
/// (detected again per iteration). Marker rows are not descended into, because processing removes them.
/// </summary>
/// <remarks>
/// Syntax errors of the detectors are ignored here: they are reported by the other validation steps.
/// </remarks>
internal sealed class BlockMarkerValidator
{
    private readonly List<ValidationError> _errors;
    private readonly HashSet<OpenXmlElement> _reported = new HashSet<OpenXmlElement>();

    public BlockMarkerValidator(List<ValidationError> errors)
    {
        _errors = errors;
    }

    public void ValidateBlocks(List<OpenXmlElement> elements)
    {
        if (elements.Count == 0)
        {
            return;
        }

        IReadOnlyList<LoopBlock> loops = Detect(() => LoopDetector.DetectLoopsInElements(elements));
        IReadOnlyList<ConditionalBlock> conditionals = Detect(() => ConditionalDetector.DetectConditionalsInElements(elements));
        HashSet<OpenXmlElement> markers = Report(loops, conditionals);

        foreach (LoopBlock loop in loops)
        {
            ValidateBlocks(loop.ContentElements.ToList());
        }

        foreach (OpenXmlElement element in elements)
        {
            if (markers.Contains(element))
            {
                continue;
            }

            switch (element)
            {
                case Paragraph paragraph:
                    foreach (TextBoxContent textBox in paragraph.Descendants<TextBoxContent>()
                        .Where(box => box.Ancestors<Paragraph>().FirstOrDefault() == paragraph))
                    {
                        ValidateBlocks(textBox.Elements<OpenXmlElement>().ToList());
                    }

                    break;
                case Table table:
                    ValidateRowContainer(table);
                    break;
                case SdtBlock { SdtContentBlock: not null } contentControl:
                    ValidateBlocks(contentControl.SdtContentBlock.Elements<OpenXmlElement>().ToList());
                    break;
            }
        }
    }

    private void ValidateRowContainer(OpenXmlCompositeElement container)
    {
        ValidateRows(container.Elements<TableRow>().ToList());

        foreach (SdtRow rowContentControl in container.Elements<SdtRow>())
        {
            if (rowContentControl.SdtContentRow != null)
            {
                ValidateRowContainer(rowContentControl.SdtContentRow);
            }
        }
    }

    private void ValidateRows(List<TableRow> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        IReadOnlyList<LoopBlock> loops = Detect(() => LoopDetector.DetectTableRowLoops(rows));
        IReadOnlyList<ConditionalBlock> conditionals = Detect(() => ConditionalDetector.DetectTableRowConditionals(rows));
        HashSet<OpenXmlElement> markers = Report(loops, conditionals);

        foreach (LoopBlock loop in loops)
        {
            ValidateRows(loop.ContentElements.OfType<TableRow>().ToList());
        }

        foreach (TableRow row in rows.Where(r => !markers.Contains(r)))
        {
            foreach (TableCell cell in TemplateElementText.GetRowCells(row))
            {
                ValidateBlocks(cell.Elements<OpenXmlElement>().ToList());
            }
        }
    }

    /// <summary>Reports the lost text of each block's markers (once per marker element); returns the removed markers.</summary>
    private HashSet<OpenXmlElement> Report(IReadOnlyList<LoopBlock> loops, IReadOnlyList<ConditionalBlock> conditionals)
    {
        IEnumerable<(OpenXmlElement Marker, string Message)> losses =
            loops.SelectMany(BlockMarkerLoss.Find).Concat(conditionals.SelectMany(BlockMarkerLoss.Find));
        foreach ((OpenXmlElement marker, string message) in losses)
        {
            if (_reported.Add(marker))
            {
                _errors.Add(ValidationError.Create(
                    ValidationErrorType.BlockMarkerWithText,
                    message,
                    BlockMarkerText.GetLocation(BlockMarkerLoss.GetText(marker))));
            }
        }

        // The elements processing removes. An inline conditional's paragraph stays (only its markers are removed).
        return new HashSet<OpenXmlElement>(
            loops.SelectMany(l => new[] { l.StartMarker, l.EndMarker })
                .Concat(conditionals
                    .Where(c => !(c.StartMarker == c.EndMarker && c.StartMarker is Paragraph))
                    .SelectMany(c => c.Branches.Select(b => b.Marker).Append(c.EndMarker))));
    }

    private static IReadOnlyList<T> Detect<T>(Func<IReadOnlyList<T>> detect)
    {
        try
        {
            return detect();
        }
        catch (TemplateSyntaxException)
        {
            return Array.Empty<T>();
        }
    }
}
