// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Core;

/// <summary>
/// The structural template lint for Word templates (#255). Runs the processing detectors over exactly the block
/// containers the <c>DocumentWalker</c> visits — block lists (body, header, footer, note, table cell, content control,
/// text box), row lists of tables, and loop content (walked once, as processing detects it again per iteration) — and
/// reports, each problem once and located:
/// detector syntax errors (R7), text next to block markers (R2, #253), more than one block marker in a marker element
/// (R3), orphan end and branch markers (R9) and unterminated tags (R1). Marker rows and elements are not descended
/// into, because processing removes them.
/// </summary>
internal sealed class TemplateLintWalker
{
    private const int MaxQuotedTagLength = 40;

    private readonly WordTemplateLocator _locator;
    private readonly List<ValidationError> _errors;
    private readonly HashSet<string> _allPlaceholders;
    private readonly HashSet<(OpenXmlElement? Element, ValidationErrorType Type, string Message)> _reportedSyntaxErrors =
        new HashSet<(OpenXmlElement?, ValidationErrorType, string)>();

    /// <summary>Elements a detector error was reported for; problems inside them are follow-ups and not reported.</summary>
    private readonly HashSet<OpenXmlElement> _failedElements = new HashSet<OpenXmlElement>();
    private readonly HashSet<(OpenXmlElement Element, string Rule)> _reported = new HashSet<(OpenXmlElement, string)>();

    public TemplateLintWalker(WordTemplateLocator locator, List<ValidationError> errors, HashSet<string> allPlaceholders)
    {
        _locator = locator;
        _errors = errors;
        _allPlaceholders = allPlaceholders;
    }

    /// <summary>Lints a block list (the children of a body, header, footer, note, cell, content control or text box).</summary>
    public void ValidateBlocks(List<OpenXmlElement> elements)
    {
        if (elements.Count == 0)
        {
            return;
        }

        (IReadOnlyList<LoopBlock> loops, bool loopsFailed) = Detect(() => LoopDetector.DetectLoopsInElements(elements));
        (IReadOnlyList<ConditionalBlock> conditionals, bool conditionalsFailed) =
            Detect(() => ConditionalDetector.DetectConditionalsInElements(elements));
        HashSet<OpenXmlElement> removedMarkers = ReportBlocks(loops, conditionals);
        HashSet<OpenXmlElement> loopContent = new HashSet<OpenXmlElement>(loops.SelectMany(l => l.ContentElements));
        HashSet<OpenXmlElement> allMarkers = GetAllMarkers(loops, conditionals);

        // Elements in loop content are linted when the content is walked (below), once.
        foreach (Paragraph paragraph in elements.OfType<Paragraph>().Where(p => !loopContent.Contains(p)))
        {
            ReportUnterminatedTag(paragraph);
            if (!allMarkers.Contains(paragraph) && !IsInsideFailedElement(paragraph))
            {
                ReportOrphanMarkers(paragraph, checkConditionals: !conditionalsFailed, checkLoops: !loopsFailed);
            }
        }

        foreach (LoopBlock loop in loops)
        {
            ValidateBlocks(loop.ContentElements.ToList());
        }

        foreach (OpenXmlElement element in elements)
        {
            if (removedMarkers.Contains(element) || loopContent.Contains(element))
            {
                continue;
            }

            switch (element)
            {
                case Paragraph paragraph:
                    // Processing walks both copies of a DrawingML text box; the lint reports the one the author sees.
                    foreach (TextBoxContent textBox in WordTemplateLocator.GetTextBoxes(paragraph))
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

        (IReadOnlyList<LoopBlock> loops, _) = Detect(() => LoopDetector.DetectTableRowLoops(rows));
        (IReadOnlyList<ConditionalBlock> conditionals, _) = Detect(() => ConditionalDetector.DetectTableRowConditionals(rows));
        HashSet<OpenXmlElement> removedMarkers = ReportBlocks(loops, conditionals);
        HashSet<OpenXmlElement> loopContent = new HashSet<OpenXmlElement>(loops.SelectMany(l => l.ContentElements));

        foreach (LoopBlock loop in loops)
        {
            ValidateRows(loop.ContentElements.OfType<TableRow>().ToList());
        }

        foreach (TableRow row in rows.Where(r => !removedMarkers.Contains(r) && !loopContent.Contains(r)))
        {
            foreach (TableCell cell in TemplateElementText.GetRowCells(row))
            {
                ValidateBlocks(cell.Elements<OpenXmlElement>().ToList());
            }
        }
    }

    /// <summary>
    /// Collects the loop collection names, reports text next to markers (R2) and multiple markers (R3) of the blocks,
    /// and returns the marker elements processing removes (an inline conditional's paragraph stays).
    /// </summary>
    private HashSet<OpenXmlElement> ReportBlocks(IReadOnlyList<LoopBlock> loops, IReadOnlyList<ConditionalBlock> conditionals)
    {
        foreach (LoopBlock loop in loops)
        {
            _allPlaceholders.Add(loop.CollectionName);
        }

        foreach ((OpenXmlElement marker, string message) in loops.SelectMany(BlockMarkerLoss.Find).Concat(conditionals.SelectMany(BlockMarkerLoss.Find)))
        {
            Report(marker, "R2", ValidationErrorType.BlockMarkerWithText, message);
        }

        HashSet<OpenXmlElement> removed = new HashSet<OpenXmlElement>(
            loops.Where(l => l.StartMarker != l.EndMarker).SelectMany(l => new[] { l.StartMarker, l.EndMarker })
                .Concat(conditionals.Where(c => !IsInline(c)).SelectMany(c => c.Branches.Select(b => b.Marker).Append(c.EndMarker))));

        foreach (OpenXmlElement marker in removed)
        {
            IReadOnlyList<string> markers = TemplateTagText.GetBlockMarkersOutsideInlineConditionals(BlockMarkerLoss.GetText(marker));
            if (markers.Count > 1)
            {
                string unit = BlockMarkerLoss.GetUnit(marker);
                Report(marker, "R3", ValidationErrorType.MultipleBlockMarkers,
                    $"More than one block marker in one {unit}: {string.Join(", ", markers.Select(m => $"'{m}'"))}. " +
                    $"Put each block marker in a {unit} of its own.");
            }
        }

        // An inline loop (both markers in one paragraph) is removed too; it is reported as R2.
        removed.UnionWith(loops.Where(l => l.StartMarker == l.EndMarker).Select(l => l.StartMarker));
        return removed;
    }

    private static HashSet<OpenXmlElement> GetAllMarkers(IReadOnlyList<LoopBlock> loops, IReadOnlyList<ConditionalBlock> conditionals) =>
        new HashSet<OpenXmlElement>(
            loops.SelectMany(l => new[] { l.StartMarker, l.EndMarker })
                .Concat(conditionals.SelectMany(c => c.Branches.Select(b => b.Marker).Append(c.EndMarker))));

    private void ReportUnterminatedTag(Paragraph paragraph)
    {
        string? tag = TemplateTagText.FindUnterminatedTag(ParagraphTextModel.GetText(paragraph));
        if (tag != null)
        {
            string quoted = tag.Length <= MaxQuotedTagLength ? tag : tag.Substring(0, MaxQuotedTagLength) + "…";
            Report(paragraph, "R1", ValidationErrorType.UnterminatedTag,
                $"Unterminated tag '{quoted}': a placeholder or marker must open and close in the same paragraph.");
        }
    }

    private void ReportOrphanMarkers(Paragraph paragraph, bool checkConditionals, bool checkLoops)
    {
        foreach (string marker in TemplateTagText.GetEndAndBranchMarkers(ParagraphTextModel.GetText(paragraph)))
        {
            bool isLoopEnd = LoopDetector.ForeachEnd.IsMatch(marker);
            if (isLoopEnd && checkLoops)
            {
                Report(paragraph, "R9:" + marker, ValidationErrorType.UnmatchedLoopEnd,
                    "Loop end marker '{{/foreach}}' has no matching '{{#foreach}}'.");
            }
            else if (!isLoopEnd && checkConditionals)
            {
                Report(paragraph, "R9:" + marker, ValidationErrorType.UnmatchedConditionalEnd,
                    $"Conditional marker '{marker}' has no matching '{{{{#if}}}}'.");
            }
        }
    }

    private void Report(OpenXmlElement element, string rule, ValidationErrorType type, string message)
    {
        if (_reported.Add((element, rule)))
        {
            _errors.Add(ValidationError.Create(type, message, _locator.Describe(element)));
        }
    }

    private static bool IsInline(ConditionalBlock conditional) =>
        conditional.StartMarker == conditional.EndMarker && conditional.StartMarker is Paragraph;

    /// <summary>Runs a detector; a syntax error is reported once (located) and leaves no blocks.</summary>
    private (IReadOnlyList<T> Blocks, bool Failed) Detect<T>(Func<IReadOnlyList<T>> detect)
    {
        try
        {
            return (detect(), false);
        }
        catch (TemplateSyntaxException ex)
        {
            // Processing stops at the outermost error: a row that fails at row level is detected again inside its
            // cells (and loop content again per walk); those follow-ups are not reported. The same problem at another
            // place (body and header) is reported at each place.
            OpenXmlElement? element = ex.SourceElement as OpenXmlElement;
            if (element != null && IsInsideFailedElement(element))
            {
                return (Array.Empty<T>(), true);
            }

            if (_reportedSyntaxErrors.Add((element, ex.ErrorType, ex.Message)))
            {
                _errors.Add(ValidationError.Create(ex.ErrorType, ex.Message, element != null ? _locator.Describe(element) : null));
                if (element != null)
                {
                    _failedElements.Add(element);
                }
            }

            return (Array.Empty<T>(), true);
        }
    }

    private bool IsInsideFailedElement(OpenXmlElement element) =>
        _failedElements.Count > 0 && element.Ancestors().Any(_failedElements.Contains);
}
