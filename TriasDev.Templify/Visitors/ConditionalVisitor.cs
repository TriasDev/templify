// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Visitors;

/// <summary>
/// Visitor that processes conditional blocks ({{#if}}/{{#elseif}}/{{#else}}/{{/if}}).
/// Evaluates conditions and removes non-matching branches.
/// </summary>
/// <remarks>
/// Benefits:
/// - Works with DocumentWalker for unified traversal
/// - Context-aware (uses IEvaluationContext from Phase 1)
/// - Can be composed with other visitors
/// </remarks>
internal sealed class ConditionalVisitor : ITemplateElementVisitor
{
    private readonly ConditionalEvaluator _evaluator;
    private readonly IWarningCollector _warningCollector;

    public ConditionalVisitor(IWarningCollector warningCollector)
    {
        _evaluator = new ConditionalEvaluator();
        _warningCollector = warningCollector ?? throw new ArgumentNullException(nameof(warningCollector));
    }

    /// <summary>
    /// Processes a conditional block by evaluating branches in order and keeping the first matching one.
    /// </summary>
    /// <param name="conditional">The conditional block to process.</param>
    /// <param name="context">The evaluation context for resolving variables.</param>
    public void VisitConditional(ConditionalBlock conditional, IEvaluationContext context)
    {
        // Check if this is an inline conditional (start and end in same element)
        bool isInlineConditional = conditional.StartMarker == conditional.EndMarker
                                   && conditional.StartMarker is Paragraph;

        if (isInlineConditional)
        {
            // Process at Run level to preserve surrounding content
            // This handles multiple inline conditionals in the same paragraph
            ProcessInlineConditional(conditional, context);
        }
        else
        {
            // Find the first matching branch
            ConditionalBranch? matchingBranch = FindMatchingBranch(conditional, context);

            // Process branches: keep matching branch content, remove everything else
            ProcessBranches(conditional, matchingBranch);
        }
    }

    /// <summary>
    /// Finds the first branch whose condition evaluates to true, or the else branch.
    /// </summary>
    private ConditionalBranch? FindMatchingBranch(ConditionalBlock conditional, IEvaluationContext context)
    {
        foreach (ConditionalBranch branch in conditional.Branches)
        {
            if (branch.IsElseBranch)
            {
                // Else branch - always matches as fallback
                return branch;
            }

            if (_evaluator.Evaluate(branch.ConditionExpression!, context, _warningCollector))
            {
                return branch;
            }
        }

        // No branch matched (all conditions false and no else)
        return null;
    }

    /// <summary>
    /// Processes conditional branches: removes all markers and non-matching branch content.
    /// </summary>
    private void ProcessBranches(ConditionalBlock conditional, ConditionalBranch? matchingBranch)
    {
        // Table cells that could be emptied by this block's removals.
        // Captured BEFORE mutating the tree, so ancestor lookups still resolve.
        HashSet<TableCell> affectedCells = CollectAncestorCells(conditional);

        // Remove all branch markers
        foreach (ConditionalBranch branch in conditional.Branches)
        {
            TemplateElementHelper.SafeRemove(branch.Marker);
        }

        // Remove content of non-matching branches
        foreach (ConditionalBranch branch in conditional.Branches)
        {
            if (branch != matchingBranch)
            {
                TemplateElementHelper.SafeRemoveRange(branch.ContentElements);
            }
        }

        // Remove the end marker
        TemplateElementHelper.SafeRemove(conditional.EndMarker);

        // Matching branch content (if any) remains in the document.

        // ECMA-376 §17.4.66: a <w:tc> must contain at least one block-level element and must end
        // with a <w:p>. Removing a branch can empty a cell whose entire content was the conditional
        // block; top such cells back up so the produced OOXML stays valid.
        EnsureCellsEndWithParagraph(affectedCells);
    }

    /// <summary>
    /// Collects the table cells that contain any of the conditional's markers or content elements.
    /// Must be called before the tree is mutated so ancestor lookups still resolve.
    /// </summary>
    private static HashSet<TableCell> CollectAncestorCells(ConditionalBlock conditional)
    {
        HashSet<TableCell> cells = new HashSet<TableCell>();

        void AddCellOf(OpenXmlElement? node)
        {
            TableCell? cell = node?.Ancestors<TableCell>().FirstOrDefault();
            if (cell is not null)
            {
                cells.Add(cell);
            }
        }

        foreach (ConditionalBranch branch in conditional.Branches)
        {
            AddCellOf(branch.Marker);
            foreach (OpenXmlElement element in branch.ContentElements)
            {
                AddCellOf(element);
            }
        }

        AddCellOf(conditional.EndMarker);

        return cells;
    }

    /// <summary>
    /// Ensures every given table cell still ends with a paragraph, appending an empty one if needed.
    /// Cells that were themselves removed (e.g. a table-row conditional) are skipped.
    /// </summary>
    private static void EnsureCellsEndWithParagraph(IEnumerable<TableCell> cells)
    {
        foreach (TableCell cell in cells)
        {
            // Skip cells that were removed along with their containing row.
            if (cell.Parent is null)
            {
                continue;
            }

            if (cell.LastChild is not Paragraph)
            {
                cell.AppendChild(new Paragraph());
            }
        }
    }

    /// <summary>
    /// Not implemented - ConditionalVisitor only processes conditionals.
    /// </summary>
    public void VisitLoop(LoopBlock loop, IEvaluationContext context)
    {
        // ConditionalVisitor doesn't process loops
        // This method is no-op to satisfy the interface
    }

    /// <summary>
    /// Not implemented - ConditionalVisitor only processes conditionals.
    /// </summary>
    public void VisitPlaceholder(PlaceholderMatch placeholder, Paragraph paragraph, IEvaluationContext context)
    {
        // ConditionalVisitor doesn't process placeholders
        // This method is no-op to satisfy the interface
    }

    /// <summary>
    /// Not implemented - ConditionalVisitor only processes conditionals.
    /// </summary>
    public void VisitParagraph(Paragraph paragraph, IEvaluationContext context)
    {
        // ConditionalVisitor doesn't process regular paragraphs
        // This method is no-op to satisfy the interface
    }

    /// <summary>
    /// Processes an inline conditional where {{#if}} and {{/if}} are in the same paragraph.
    /// Works at the Run level to preserve content before and after the conditional.
    /// Handles multiple inline conditionals in the same paragraph.
    /// </summary>
    /// <param name="conditional">The inline conditional block.</param>
    /// <param name="context">The evaluation context for resolving variables.</param>
    private void ProcessInlineConditional(ConditionalBlock conditional, IEvaluationContext context)
    {
        Paragraph paragraph = (Paragraph)conditional.StartMarker;

        // Process all inline conditionals in this paragraph by working on the combined text
        // We need to handle multiple conditionals, so we work on the full paragraph text
        ProcessAllInlineConditionalsInParagraph(paragraph, context);
    }

    /// <summary>
    /// Processes all inline conditionals in a paragraph. Only the text of markers and of
    /// non-matching branches is removed; the kept text retains its runs and formatting.
    /// </summary>
    private void ProcessAllInlineConditionalsInParagraph(Paragraph paragraph, IEvaluationContext context)
    {
        string text = ParagraphTextModel.GetText(paragraph);

        List<(int Start, int End)> removals = new List<(int, int)>();
        CollectInlineRemovals(text, 0, text.Length, context, removals);

        if (removals.Count == 0)
        {
            return;
        }

        ParagraphTextRewriter.Remove(paragraph, removals);
    }

    /// <summary>
    /// Collects the text ranges to remove for the inline conditionals in [<paramref name="start"/>,
    /// <paramref name="end"/>) of <paramref name="text"/>: for each conditional, everything except the
    /// content of the matching branch, recursing into that content for nested conditionals.
    /// </summary>
    private void CollectInlineRemovals(
        string text,
        int start,
        int end,
        IEvaluationContext context,
        List<(int Start, int End)> removals)
    {
        List<InlineConditional> conditionals = InlineConditionalParser.Parse(text.Substring(start, end - start));

        // Evaluate right to left, as the previous string-based implementation did.
        for (int c = conditionals.Count - 1; c >= 0; c--)
        {
            InlineConditional conditional = conditionals[c];
            int conditionalStart = start + conditional.StartIndex;
            int conditionalEnd = start + conditional.EndIndex;

            InlineConditionalBranch? match = conditional.Branches.FirstOrDefault(
                b => b.Condition == null || _evaluator.Evaluate(b.Condition, context, _warningCollector));

            if (match == null)
            {
                removals.Add((conditionalStart, conditionalEnd));
                continue;
            }

            int contentStart = start + match.ContentStart;
            int contentEnd = start + match.ContentEnd;

            AddRange(removals, contentEnd, conditionalEnd);
            CollectInlineRemovals(text, contentStart, contentEnd, context, removals);
            AddRange(removals, conditionalStart, contentStart);
        }
    }

    private static void AddRange(List<(int Start, int End)> ranges, int start, int end)
    {
        if (end > start)
        {
            ranges.Add((start, end));
        }
    }
}
