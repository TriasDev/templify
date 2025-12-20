// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;

namespace TriasDev.Templify.Visitors;

/// <summary>
/// Visitor that processes conditional blocks ({{#if}}/{{else}}/{{/if}}).
/// Evaluates conditions and removes false branches.
/// </summary>
/// <remarks>
/// This visitor wraps the logic from ConditionalProcessor into the visitor pattern.
/// Benefits:
/// - Works with DocumentWalker for unified traversal
/// - Context-aware (uses IEvaluationContext from Phase 1)
/// - Can be composed with other visitors
/// - Eliminates duplication between ConditionalProcessor and LoopProcessor
/// </remarks>
internal sealed class ConditionalVisitor : ITemplateElementVisitor
{
    private readonly ConditionalEvaluator _evaluator;

    private static readonly Regex _ifStartPattern = new Regex(
        @"\{\{#if\s+(.+?)\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _elsePattern = new Regex(
        @"\{\{else\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _ifEndPattern = new Regex(
        @"\{\{/if\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public ConditionalVisitor()
    {
        _evaluator = new ConditionalEvaluator();
    }

    /// <summary>
    /// Processes a conditional block by evaluating the condition and keeping the appropriate branch.
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
            // Evaluate the condition using the provided context
            bool conditionResult = _evaluator.Evaluate(conditional.ConditionExpression, context);

            if (conditionResult)
            {
                // Condition is TRUE: Keep IF branch, remove ELSE branch
                ProcessTrueBranch(conditional);
            }
            else
            {
                // Condition is FALSE: Remove IF branch, keep ELSE branch
                ProcessFalseBranch(conditional);
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
    /// Processes conditional when condition is TRUE.
    /// Keeps IF content, removes ELSE content and all markers.
    /// </summary>
    /// <remarks>
    /// This method is extracted from ConditionalProcessor.ProcessTrueBranch()
    /// to eliminate duplication.
    /// </remarks>
    private void ProcessTrueBranch(ConditionalBlock conditional)
    {
        // Remove the start marker (if it still has a parent)
        TemplateElementHelper.SafeRemove(conditional.StartMarker);

        // Remove ELSE content elements (if any)
        TemplateElementHelper.SafeRemoveRange(conditional.ElseContentElements);

        // Remove the else marker (if any)
        if (conditional.ElseMarker != null)
        {
            TemplateElementHelper.SafeRemove(conditional.ElseMarker);
        }

        // Remove the end marker
        TemplateElementHelper.SafeRemove(conditional.EndMarker);

        // IF content elements remain in the document
    }

    /// <summary>
    /// Processes conditional when condition is FALSE.
    /// Removes IF content, keeps ELSE content (if any), removes all markers.
    /// </summary>
    /// <remarks>
    /// This method is extracted from ConditionalProcessor.ProcessFalseBranch()
    /// to eliminate duplication.
    /// </remarks>
    private void ProcessFalseBranch(ConditionalBlock conditional)
    {
        // Remove the start marker
        TemplateElementHelper.SafeRemove(conditional.StartMarker);

        // Remove IF content elements
        TemplateElementHelper.SafeRemoveRange(conditional.IfContentElements);

        // Remove the else marker (if any)
        if (conditional.ElseMarker != null)
        {
            TemplateElementHelper.SafeRemove(conditional.ElseMarker);
        }

        // Remove the end marker
        TemplateElementHelper.SafeRemove(conditional.EndMarker);

        // ELSE content elements (if any) remain in the document
        // If there's no else branch, nothing remains (which is correct)
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
    /// Processes all inline conditionals in a paragraph by working on the combined text.
    /// </summary>
    private void ProcessAllInlineConditionalsInParagraph(Paragraph paragraph, IEvaluationContext context)
    {
        // Get the full text of the paragraph
        string fullText = paragraph.InnerText;

        // Find all conditionals and process them from right to left
        // This preserves text positions as we make replacements
        List<InlineConditionalInfo> conditionals = FindAllInlineConditionals(fullText);

        if (conditionals.Count == 0)
        {
            return;
        }

        // Process from right to left to preserve positions
        conditionals.Reverse();

        // Use StringBuilder for efficient string manipulation
        StringBuilder result = new StringBuilder(fullText);

        foreach (InlineConditionalInfo info in conditionals)
        {
            // Evaluate the condition
            bool conditionResult = _evaluator.Evaluate(info.Condition, context);

            // Calculate the replacement - recursively process nested conditionals in content
            string rawContent = conditionResult ? info.IfContent : (info.ElseContent ?? "");
            string replacement = ProcessNestedInlineConditionals(rawContent, context);

            // Replace in the result (right to left preserves positions)
            result.Remove(info.StartIndex, info.EndIndex - info.StartIndex);
            result.Insert(info.StartIndex, replacement);
        }

        // Now rebuild the paragraph with the new text
        RebuildParagraphText(paragraph, result.ToString());
    }

    /// <summary>
    /// Recursively processes nested inline conditionals within text content.
    /// </summary>
    /// <param name="text">The text that may contain nested conditionals.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The text with all nested conditionals processed.</returns>
    private string ProcessNestedInlineConditionals(string text, IEvaluationContext context)
    {
        // Find conditionals in this text
        List<InlineConditionalInfo> conditionals = FindAllInlineConditionals(text);

        if (conditionals.Count == 0)
        {
            return text;
        }

        // Process from right to left
        conditionals.Reverse();

        StringBuilder result = new StringBuilder(text);

        foreach (InlineConditionalInfo info in conditionals)
        {
            bool conditionResult = _evaluator.Evaluate(info.Condition, context);

            // Recursively process nested conditionals in the chosen branch
            string rawContent = conditionResult ? info.IfContent : (info.ElseContent ?? "");
            string replacement = ProcessNestedInlineConditionals(rawContent, context);

            result.Remove(info.StartIndex, info.EndIndex - info.StartIndex);
            result.Insert(info.StartIndex, replacement);
        }

        return result.ToString();
    }

    /// <summary>
    /// Information about an inline conditional found in text.
    /// </summary>
    private class InlineConditionalInfo
    {
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public string Condition { get; set; } = "";
        public string IfContent { get; set; } = "";
        public string? ElseContent { get; set; }
    }

    /// <summary>
    /// Finds all inline conditionals in text, properly handling nesting.
    /// </summary>
    private List<InlineConditionalInfo> FindAllInlineConditionals(string text)
    {
        List<InlineConditionalInfo> result = new List<InlineConditionalInfo>();

        int searchStart = 0;
        while (searchStart < text.Length)
        {
            // Find next {{#if
            Match ifMatch = _ifStartPattern.Match(text, searchStart);
            if (!ifMatch.Success)
            {
                break;
            }

            // Find the matching {{/if}} considering nesting
            int depth = 1;
            int pos = ifMatch.Index + ifMatch.Length;
            int elsePos = -1;
            int elseLength = 0;

            while (pos < text.Length && depth > 0)
            {
                // Look for next marker
                Match nextIfMatch = _ifStartPattern.Match(text, pos);
                Match nextEndMatch = _ifEndPattern.Match(text, pos);
                Match nextElseMatch = _elsePattern.Match(text, pos);

                // Find which comes first
                int nextIfPos = nextIfMatch.Success ? nextIfMatch.Index : int.MaxValue;
                int nextEndPos = nextEndMatch.Success ? nextEndMatch.Index : int.MaxValue;
                int nextElsePos = nextElseMatch.Success ? nextElseMatch.Index : int.MaxValue;

                if (nextEndPos <= nextIfPos && nextEndPos <= nextElsePos && nextEndMatch.Success)
                {
                    depth--;
                    if (depth == 0)
                    {
                        // Found the matching end
                        int endIndex = nextEndMatch.Index + nextEndMatch.Length;

                        // Extract content
                        string condition = ifMatch.Groups[1].Value.Trim();
                        string ifContent;
                        string? elseContent = null;

                        if (elsePos >= 0)
                        {
                            // Use stored elseLength instead of redundant Match operation
                            ifContent = text.Substring(ifMatch.Index + ifMatch.Length, elsePos - (ifMatch.Index + ifMatch.Length));
                            elseContent = text.Substring(elsePos + elseLength, nextEndMatch.Index - (elsePos + elseLength));
                        }
                        else
                        {
                            ifContent = text.Substring(ifMatch.Index + ifMatch.Length, nextEndMatch.Index - (ifMatch.Index + ifMatch.Length));
                        }

                        result.Add(new InlineConditionalInfo
                        {
                            StartIndex = ifMatch.Index,
                            EndIndex = endIndex,
                            Condition = condition,
                            IfContent = ifContent,
                            ElseContent = elseContent
                        });

                        searchStart = endIndex;
                        break;
                    }
                    pos = nextEndMatch.Index + nextEndMatch.Length;
                }
                else if (nextIfPos <= nextEndPos && nextIfPos <= nextElsePos && nextIfMatch.Success)
                {
                    depth++;
                    pos = nextIfMatch.Index + nextIfMatch.Length;
                }
                else if (nextElsePos <= nextIfPos && nextElsePos <= nextEndPos && nextElseMatch.Success && depth == 1)
                {
                    // Found else at our level - store position and length
                    elsePos = nextElseMatch.Index;
                    elseLength = nextElseMatch.Length;
                    pos = nextElseMatch.Index + nextElseMatch.Length;
                }
                else
                {
                    // No more markers found
                    break;
                }
            }

            if (depth > 0)
            {
                // Unmatched conditional, skip it
                searchStart = ifMatch.Index + ifMatch.Length;
            }
        }

        return result;
    }

    /// <summary>
    /// Rebuilds a paragraph's text content while preserving formatting of the first run.
    /// </summary>
    private void RebuildParagraphText(Paragraph paragraph, string newText)
    {
        // Get current runs and their properties
        List<Run> runs = paragraph.Elements<Run>().ToList();

        // Get formatting from first run with text
        RunProperties? formatting = null;
        foreach (Run run in runs)
        {
            if (run.RunProperties != null)
            {
                formatting = (RunProperties)run.RunProperties.CloneNode(true);
                break;
            }
        }

        // Remove all runs
        foreach (Run run in runs)
        {
            run.Remove();
        }

        // Add new run with the processed text
        Run newRun = new Run();
        if (formatting != null)
        {
            newRun.RunProperties = formatting;
        }
        Text textElement = new Text(newText);
        textElement.Space = SpaceProcessingModeValues.Preserve;
        newRun.Append(textElement);
        paragraph.Append(newRun);
    }
}
