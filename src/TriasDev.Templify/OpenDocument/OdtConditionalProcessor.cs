// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.OpenDocument;

/// <summary>
/// Evaluates OpenDocument conditional blocks and removes the markers and the non-matching branches.
/// The counterpart of <c>ConditionalVisitor</c>.
/// </summary>
internal sealed class OdtConditionalProcessor
{
    private readonly ConditionalEvaluator _evaluator = new ConditionalEvaluator();
    private readonly IWarningCollector _warningCollector;

    public OdtConditionalProcessor(IWarningCollector warningCollector)
    {
        _warningCollector = warningCollector ?? throw new ArgumentNullException(nameof(warningCollector));
    }

    /// <summary>
    /// Processes a conditional block: an inline conditional (all markers in one paragraph) is resolved
    /// within the paragraph text; a block conditional keeps the content of the first matching branch and
    /// removes all markers and the other branches.
    /// </summary>
    public void Process(OdtConditionalBlock conditional, IEvaluationContext context)
    {
        if (conditional.StartMarker == conditional.EndMarker && OdfNames.IsParagraph(conditional.StartMarker))
        {
            ProcessInlineConditionals(conditional.StartMarker, context);
            return;
        }

        OdtConditionalBranch? match = conditional.Branches.FirstOrDefault(
            b => b.IsElseBranch || _evaluator.Evaluate(b.ConditionExpression!, context, _warningCollector));

        foreach (OdtConditionalBranch branch in conditional.Branches)
        {
            SafeRemove(branch.Marker);
        }

        foreach (OdtConditionalBranch branch in conditional.Branches.Where(b => b != match))
        {
            foreach (XElement element in branch.ContentElements)
            {
                SafeRemove(element);
            }
        }

        SafeRemove(conditional.EndMarker);
    }

    /// <summary>
    /// Resolves all inline conditionals in a paragraph. Only the text of markers and of non-matching
    /// branches is removed; the kept text retains its spans and formatting.
    /// </summary>
    public void ProcessInlineConditionals(XElement paragraph, IEvaluationContext context)
    {
        string text = OdtParagraphTextModel.GetText(paragraph);
        List<(int Start, int End)> removals = new List<(int, int)>();
        CollectInlineRemovals(text, 0, text.Length, context, removals);

        if (removals.Count > 0)
        {
            OdtParagraphTextRewriter.Remove(paragraph, removals);
        }
    }

    private void CollectInlineRemovals(
        string text,
        int start,
        int end,
        IEvaluationContext context,
        List<(int Start, int End)> removals)
    {
        List<InlineConditional> conditionals = InlineConditionalParser.Parse(text.Substring(start, end - start));

        // Right to left, as for Word documents.
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

    private static void SafeRemove(XElement element)
    {
        if (element.Parent != null)
        {
            element.Remove();
        }
    }
}
