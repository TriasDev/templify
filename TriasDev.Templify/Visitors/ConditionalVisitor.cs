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
/// Visitor that processes conditional blocks ({{#if}}/{{#elseif}}/{{else}}/{{/if}}).
/// Evaluates conditions and removes non-matching branches.
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

    private static readonly Regex _elseIfPattern = new Regex(
        @"\{\{#elseif\s+(.+?)\}\}",
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

            if (_evaluator.Evaluate(branch.ConditionExpression!, context))
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

        // Matching branch content (if any) remains in the document
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
            // Find the first matching branch
            string replacement = "";
            foreach (var branch in info.Branches)
            {
                if (branch.Condition == null)
                {
                    // Else branch - always matches
                    replacement = ProcessNestedInlineConditionals(branch.Content, context);
                    break;
                }

                if (_evaluator.Evaluate(branch.Condition, context))
                {
                    replacement = ProcessNestedInlineConditionals(branch.Content, context);
                    break;
                }
            }

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
            // Find the first matching branch
            string replacement = "";
            foreach (var branch in info.Branches)
            {
                if (branch.Condition == null)
                {
                    replacement = ProcessNestedInlineConditionals(branch.Content, context);
                    break;
                }

                if (_evaluator.Evaluate(branch.Condition, context))
                {
                    replacement = ProcessNestedInlineConditionals(branch.Content, context);
                    break;
                }
            }

            result.Remove(info.StartIndex, info.EndIndex - info.StartIndex);
            result.Insert(info.StartIndex, replacement);
        }

        return result.ToString();
    }

    /// <summary>
    /// Represents a branch within an inline conditional.
    /// </summary>
    private class InlineBranch
    {
        public string? Condition { get; set; }  // null for else branch
        public string Content { get; set; } = "";
    }

    /// <summary>
    /// Information about an inline conditional found in text.
    /// </summary>
    private class InlineConditionalInfo
    {
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public List<InlineBranch> Branches { get; set; } = new List<InlineBranch>();
    }

    /// <summary>
    /// Finds all inline conditionals in text, properly handling nesting and elseif branches.
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

            // Find all branch markers and the matching {{/if}} considering nesting
            int depth = 1;
            int pos = ifMatch.Index + ifMatch.Length;

            // Track branch markers at our level (depth 1)
            List<(int Index, int Length, string? Condition)> branchMarkers = new List<(int, int, string?)>();
            branchMarkers.Add((ifMatch.Index, ifMatch.Length, ifMatch.Groups[1].Value.Trim())); // if marker

            int endMatchIndex = -1;
            int endMatchLength = 0;

            while (pos < text.Length && depth > 0)
            {
                // Look for next marker
                Match nextIfMatch = _ifStartPattern.Match(text, pos);
                Match nextEndMatch = _ifEndPattern.Match(text, pos);
                Match nextElseIfMatch = _elseIfPattern.Match(text, pos);
                Match nextElseMatch = _elsePattern.Match(text, pos);

                // Find which comes first
                int nextIfPos = nextIfMatch.Success ? nextIfMatch.Index : int.MaxValue;
                int nextEndPos = nextEndMatch.Success ? nextEndMatch.Index : int.MaxValue;
                int nextElseIfPos = nextElseIfMatch.Success ? nextElseIfMatch.Index : int.MaxValue;
                int nextElsePos = nextElseMatch.Success ? nextElseMatch.Index : int.MaxValue;

                int minPos = Math.Min(Math.Min(nextIfPos, nextEndPos), Math.Min(nextElseIfPos, nextElsePos));

                if (minPos == int.MaxValue)
                {
                    // No more markers found
                    break;
                }

                if (minPos == nextEndPos)
                {
                    depth--;
                    if (depth == 0)
                    {
                        endMatchIndex = nextEndMatch.Index;
                        endMatchLength = nextEndMatch.Length;
                        break;
                    }
                    pos = nextEndMatch.Index + nextEndMatch.Length;
                }
                else if (minPos == nextIfPos)
                {
                    depth++;
                    pos = nextIfMatch.Index + nextIfMatch.Length;
                }
                else if (minPos == nextElseIfPos && depth == 1)
                {
                    // elseif at our level
                    branchMarkers.Add((nextElseIfMatch.Index, nextElseIfMatch.Length, nextElseIfMatch.Groups[1].Value.Trim()));
                    pos = nextElseIfMatch.Index + nextElseIfMatch.Length;
                }
                else if (minPos == nextElsePos && depth == 1)
                {
                    // else at our level
                    branchMarkers.Add((nextElseMatch.Index, nextElseMatch.Length, null)); // null condition for else
                    pos = nextElseMatch.Index + nextElseMatch.Length;
                }
                else
                {
                    // Marker at deeper level, just skip
                    pos = minPos + 1;
                }
            }

            if (endMatchIndex >= 0)
            {
                // Successfully found matching end
                int endIndex = endMatchIndex + endMatchLength;

                // Build branches from markers
                InlineConditionalInfo info = new InlineConditionalInfo
                {
                    StartIndex = ifMatch.Index,
                    EndIndex = endIndex,
                    Branches = new List<InlineBranch>()
                };

                for (int m = 0; m < branchMarkers.Count; m++)
                {
                    var marker = branchMarkers[m];
                    int contentStart = marker.Index + marker.Length;
                    int contentEnd = (m + 1 < branchMarkers.Count)
                        ? branchMarkers[m + 1].Index
                        : endMatchIndex;

                    string content = text.Substring(contentStart, contentEnd - contentStart);

                    info.Branches.Add(new InlineBranch
                    {
                        Condition = marker.Condition,
                        Content = content
                    });
                }

                result.Add(info);
                searchStart = endIndex;
            }
            else
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
