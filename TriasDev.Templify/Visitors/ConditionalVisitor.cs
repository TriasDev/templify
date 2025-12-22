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
    /// Rebuilds a paragraph's text content while preserving formatting from original runs.
    /// Uses a run-level approach: keeps runs that don't contain conditional markers,
    /// and only rebuilds runs that contain conditional text.
    /// </summary>
    private void RebuildParagraphText(Paragraph paragraph, string newText)
    {
        // Get current runs
        List<Run> originalRuns = paragraph.Elements<Run>().ToList();

        // Categorize runs: those with conditional markers vs those without
        List<(Run Run, string Text, bool HasConditional, int Index)> runInfo =
            new List<(Run, string, bool, int)>();

        for (int i = 0; i < originalRuns.Count; i++)
        {
            Run run = originalRuns[i];
            string text = run.InnerText;
            bool hasConditional = text.Contains("{{#if") || text.Contains("{{/if") ||
                                  text.Contains("{{#") || text.Contains("{{/") ||
                                  text.Contains("{{else}}");
            runInfo.Add((run, text, hasConditional, i));
        }

        // If no runs have conditionals, the new text should match and we can use
        // the simpler formatting preservation approach
        bool anyConditionalRuns = runInfo.Any(r => r.HasConditional);

        if (!anyConditionalRuns)
        {
            // No conditional markers in runs - use simple text replacement
            RebuildParagraphTextSimple(paragraph, newText, originalRuns);
            return;
        }

        // Build mapping of original text positions to runs (including non-text runs like tabs)
        List<(int StartPos, int EndPos, Run Run, bool IsTab)> runPositions =
            new List<(int, int, Run, bool)>();

        int currentPos = 0;
        foreach (Run run in originalRuns)
        {
            bool isTab = run.Elements<TabChar>().Any();
            string text = run.InnerText;
            int length = text.Length;

            if (isTab || length > 0)
            {
                runPositions.Add((currentPos, currentPos + Math.Max(length, isTab ? 1 : 0), run, isTab));
            }

            currentPos += length;
            if (isTab && length == 0)
            {
                currentPos += 1; // Account for tab as 1 character position
            }
        }

        // Find which runs should be preserved (non-conditional runs that exist in new text)
        // and which should be rebuilt
        string originalText = string.Concat(originalRuns.Select(r => r.InnerText));

        // Remove all original runs
        foreach (Run run in originalRuns)
        {
            run.Remove();
        }

        // Rebuild using segment matching with tab preservation
        RebuildWithTabPreservation(paragraph, newText, originalRuns, runPositions, originalText);
    }

    /// <summary>
    /// Simple paragraph rebuild when no conditional markers are present.
    /// </summary>
    private void RebuildParagraphTextSimple(Paragraph paragraph, string newText, List<Run> originalRuns)
    {
        // Build text segments with formatting
        List<(string Text, RunProperties? Properties)> originalSegments =
            new List<(string, RunProperties?)>();

        foreach (Run run in originalRuns)
        {
            string runText = run.InnerText;
            if (!string.IsNullOrEmpty(runText))
            {
                RunProperties? props = run.RunProperties != null
                    ? (RunProperties)run.RunProperties.CloneNode(true)
                    : null;
                originalSegments.Add((runText, props));
            }
        }

        List<(string Text, RunProperties? Properties)> newSegments =
            MapTextToFormattedSegments(newText, originalSegments);

        // Remove original runs
        foreach (Run run in originalRuns)
        {
            run.Remove();
        }

        // Create new runs
        foreach ((string text, RunProperties? props) in newSegments)
        {
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            Run newRun = new Run();
            if (props != null)
            {
                newRun.RunProperties = (RunProperties)props.CloneNode(true);
            }
            Text textElement = new Text(text);
            textElement.Space = SpaceProcessingModeValues.Preserve;
            newRun.Append(textElement);
            paragraph.Append(newRun);
        }
    }

    /// <summary>
    /// Rebuilds paragraph with tab preservation by tracking which original runs
    /// map to which portions of the new text.
    /// </summary>
    private void RebuildWithTabPreservation(
        Paragraph paragraph,
        string newText,
        List<Run> originalRuns,
        List<(int StartPos, int EndPos, Run Run, bool IsTab)> runPositions,
        string originalText)
    {
        // Build segments from non-conditional original runs
        List<(string Text, RunProperties? Properties)> textSegments =
            new List<(string, RunProperties?)>();

        foreach (Run run in originalRuns)
        {
            string text = run.InnerText;
            if (!string.IsNullOrEmpty(text) &&
                !text.Contains("{{#if") && !text.Contains("{{/if") &&
                !text.Contains("{{#") && !text.Contains("{{/") &&
                !text.Contains("{{else}}"))
            {
                RunProperties? props = run.RunProperties != null
                    ? (RunProperties)run.RunProperties.CloneNode(true)
                    : null;
                textSegments.Add((text, props));
            }
        }

        // Get formatted segments for new text
        List<(string Text, RunProperties? Properties)> newSegments =
            MapTextToFormattedSegments(newText, textSegments);

        // Track tab runs from original
        List<(Run TabRun, int OriginalPos)> tabRuns = new List<(Run, int)>();
        int pos = 0;
        foreach (Run run in originalRuns)
        {
            if (run.Elements<TabChar>().Any())
            {
                RunProperties? props = run.RunProperties != null
                    ? (RunProperties)run.RunProperties.CloneNode(true)
                    : null;
                Run clonedTab = new Run();
                if (props != null)
                {
                    clonedTab.RunProperties = props;
                }
                clonedTab.Append(new TabChar());
                tabRuns.Add((clonedTab, pos));
            }
            pos += run.InnerText.Length;
        }

        // Calculate where each tab should go in the new text
        // Tabs that were between text segments that still exist should be preserved
        List<(Run TabRun, int NewPos)> newTabPositions = new List<(Run, int)>();

        foreach ((Run tabRun, int origPos) in tabRuns)
        {
            // Find where this position maps to in new text
            int newPos = MapOriginalPositionToNew(origPos, originalText, newText);
            if (newPos >= 0)
            {
                newTabPositions.Add((tabRun, newPos));
            }
        }

        // Sort tabs by position
        newTabPositions = newTabPositions.OrderBy(t => t.NewPos).ToList();

        // Output runs, interleaving tabs
        int currentPos = 0;
        int tabIdx = 0;

        foreach ((string text, RunProperties? props) in newSegments)
        {
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            // Insert tabs that should come before this position
            while (tabIdx < newTabPositions.Count && newTabPositions[tabIdx].NewPos <= currentPos)
            {
                paragraph.Append(newTabPositions[tabIdx].TabRun);
                tabIdx++;
            }

            // Create text run
            Run newRun = new Run();
            if (props != null)
            {
                newRun.RunProperties = (RunProperties)props.CloneNode(true);
            }
            Text textElement = new Text(text);
            textElement.Space = SpaceProcessingModeValues.Preserve;
            newRun.Append(textElement);
            paragraph.Append(newRun);

            currentPos += text.Length;
        }

        // Append remaining tabs
        while (tabIdx < newTabPositions.Count)
        {
            paragraph.Append(newTabPositions[tabIdx].TabRun);
            tabIdx++;
        }
    }

    /// <summary>
    /// Maps a position from original text to new text.
    /// Uses the text content before and after the position to find the mapping.
    /// </summary>
    private int MapOriginalPositionToNew(int origPos, string originalText, string newText)
    {
        // Get text before this position in original (excluding conditional markers)
        string textBefore = "";
        int searchPos = 0;
        while (searchPos < origPos && searchPos < originalText.Length)
        {
            // Skip conditional markers
            if (originalText.Substring(searchPos).StartsWith("{{#if"))
            {
                int endMarker = originalText.IndexOf("}}", searchPos);
                if (endMarker >= 0)
                {
                    searchPos = endMarker + 2;
                }
                else
                {
                    break;
                }
                continue;
            }
            if (originalText.Substring(searchPos).StartsWith("{{/if"))
            {
                int endMarker = originalText.IndexOf("}}", searchPos);
                if (endMarker >= 0)
                {
                    searchPos = endMarker + 2;
                }
                else
                {
                    break;
                }
                continue;
            }
            if (originalText.Substring(searchPos).StartsWith("{{else}}"))
            {
                searchPos += 8;
                continue;
            }

            textBefore += originalText[searchPos];
            searchPos++;
        }

        // Find where this "textBefore" ends in newText
        if (string.IsNullOrEmpty(textBefore))
        {
            return 0;
        }

        // Try to find the end of textBefore in newText
        // Start from the end of textBefore and work backwards
        for (int matchLen = Math.Min(textBefore.Length, 20); matchLen > 0; matchLen--)
        {
            string suffix = textBefore.Substring(textBefore.Length - matchLen);
            int idx = newText.IndexOf(suffix);
            if (idx >= 0)
            {
                return idx + suffix.Length;
            }
        }

        return -1;
    }

    /// <summary>
    /// Maps new text to formatted segments by using position-aware matching.
    /// Builds a mapping from original character positions to formatting, then applies to new text.
    /// </summary>
    private List<(string Text, RunProperties? Properties)> MapTextToFormattedSegments(
        string newText,
        List<(string Text, RunProperties? Properties)> originalSegments)
    {
        if (string.IsNullOrEmpty(newText) || originalSegments.Count == 0)
        {
            // Fall back to using first available formatting
            RunProperties? defaultProps = originalSegments.FirstOrDefault().Properties;
            return new List<(string, RunProperties?)> { (newText, defaultProps) };
        }

        // Build a character-level formatting map from original segments
        // This maps each character position in the original text to its RunProperties
        List<RunProperties?> originalCharFormatting = new List<RunProperties?>();
        foreach ((string text, RunProperties? props) in originalSegments)
        {
            for (int i = 0; i < text.Length; i++)
            {
                originalCharFormatting.Add(props);
            }
        }

        string originalFullText = string.Concat(originalSegments.Select(s => s.Text));

        // Build new text's formatting by finding where each character came from in original
        // This handles the case where conditionals are removed but other text stays in place
        List<RunProperties?> newCharFormatting = new List<RunProperties?>();

        for (int i = 0; i < newText.Length; i++)
        {
            // Find this character's position in the original text
            // Use context-aware matching to handle duplicate characters
            int origPos = FindOriginalPosition(newText, i, originalFullText, originalCharFormatting);

            if (origPos >= 0 && origPos < originalCharFormatting.Count)
            {
                newCharFormatting.Add(originalCharFormatting[origPos]);
            }
            else
            {
                // Fallback: use first available non-conditional formatting
                newCharFormatting.Add(GetDefaultFormatting(originalSegments));
            }
        }

        // Merge consecutive characters with same formatting into segments
        List<(string Text, RunProperties? Properties)> result = new List<(string, RunProperties?)>();
        if (newCharFormatting.Count == 0)
        {
            return result;
        }

        int segmentStart = 0;
        RunProperties? currentProps = newCharFormatting[0];

        for (int i = 1; i <= newCharFormatting.Count; i++)
        {
            bool endOfText = i == newCharFormatting.Count;
            bool formattingChanged = !endOfText && !FormattingMatches(currentProps, newCharFormatting[i]);

            if (endOfText || formattingChanged)
            {
                string segment = newText.Substring(segmentStart, i - segmentStart);
                result.Add((segment, currentProps));

                if (!endOfText)
                {
                    segmentStart = i;
                    currentProps = newCharFormatting[i];
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Finds where a character at position in newText came from in originalText.
    /// Uses context matching to handle cases with duplicate characters.
    /// </summary>
    private int FindOriginalPosition(
        string newText,
        int newPos,
        string originalText,
        List<RunProperties?> originalFormatting)
    {
        // Strategy: find the position in original text that has matching surrounding context
        // Look for context before and after the character

        int contextSize = 5;
        string targetChar = newText[newPos].ToString();

        // Build context around the character in new text
        int contextStart = Math.Max(0, newPos - contextSize);
        int contextEnd = Math.Min(newText.Length, newPos + contextSize + 1);
        string newContext = newText.Substring(contextStart, contextEnd - contextStart);
        int charPosInContext = newPos - contextStart;

        // Find all occurrences of the target character in original text
        List<int> candidates = new List<int>();
        for (int i = 0; i < originalText.Length; i++)
        {
            if (originalText[i] == newText[newPos])
            {
                candidates.Add(i);
            }
        }

        if (candidates.Count == 0)
        {
            return -1;
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        // Find the candidate with best context match
        int bestCandidate = candidates[0];
        int bestScore = -1;

        foreach (int candidate in candidates)
        {
            // Build context around this candidate in original text
            int origContextStart = Math.Max(0, candidate - contextSize);
            int origContextEnd = Math.Min(originalText.Length, candidate + contextSize + 1);
            string origContext = originalText.Substring(origContextStart, origContextEnd - origContextStart);

            // Score based on how many context characters match
            int score = 0;
            int minLen = Math.Min(newContext.Length, origContext.Length);
            for (int i = 0; i < minLen; i++)
            {
                if (i < newContext.Length && i < origContext.Length &&
                    newContext[i] == origContext[i])
                {
                    score++;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = candidate;
            }
        }

        return bestCandidate;
    }

    /// <summary>
    /// Gets default formatting from non-conditional segments.
    /// </summary>
    private RunProperties? GetDefaultFormatting(List<(string Text, RunProperties? Properties)> segments)
    {
        foreach ((string text, RunProperties? props) in segments)
        {
            if (!string.IsNullOrEmpty(text) &&
                !text.StartsWith("{{#") && !text.StartsWith("{{/") &&
                !text.Contains("{{#if") && !text.Contains("{{/if") &&
                !text.Contains("{{else}}"))
            {
                return props;
            }
        }
        return segments.FirstOrDefault().Properties;
    }

    /// <summary>
    /// Checks if two RunProperties are equivalent for the purpose of merging segments.
    /// </summary>
    private bool FormattingMatches(RunProperties? a, RunProperties? b)
    {
        if (a == null && b == null)
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        // Compare key formatting elements
        var aShadeFill = a.Shading?.Fill?.Value;
        var bShadeFill = b.Shading?.Fill?.Value;

        var aColor = a.Color?.Val?.Value;
        var bColor = b.Color?.Val?.Value;

        return aShadeFill == bShadeFill && aColor == bColor;
    }
}
