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
    /// <param name="Condition">The condition expression, or null for else branch.</param>
    /// <param name="Content">The content of the branch.</param>
    private sealed record InlineBranch(string? Condition, string Content);

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
            Match ifMatch = ConditionalPatterns.IfStart.Match(text, searchStart);
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
            bool hasElseAtOurLevel = false;

            while (pos < text.Length && depth > 0)
            {
                // Look for next marker
                Match nextIfMatch = ConditionalPatterns.IfStart.Match(text, pos);
                Match nextEndMatch = ConditionalPatterns.IfEnd.Match(text, pos);
                Match nextElseIfMatch = ConditionalPatterns.ElseIf.Match(text, pos);
                Match nextElseMatch = ConditionalPatterns.Else.Match(text, pos);

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
                    // Validate: elseif cannot appear after else
                    if (hasElseAtOurLevel)
                    {
                        throw new InvalidOperationException(
                            "Invalid conditional structure: '{{#elseif}}' cannot appear after '{{else}}'. " +
                            "The '{{else}}' branch must be the last branch before '{{/if}}'.");
                    }

                    // elseif at our level
                    branchMarkers.Add((nextElseIfMatch.Index, nextElseIfMatch.Length, nextElseIfMatch.Groups[1].Value.Trim()));
                    pos = nextElseIfMatch.Index + nextElseIfMatch.Length;
                }
                else if (minPos == nextElsePos && depth == 1)
                {
                    // else at our level
                    hasElseAtOurLevel = true;
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

                    info.Branches.Add(new InlineBranch(marker.Condition, content));
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
    /// Compares all relevant formatting properties to ensure segments with different
    /// formatting are not incorrectly merged.
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

        // Shading fill
        var aShadeFill = a.Shading?.Fill?.Value;
        var bShadeFill = b.Shading?.Fill?.Value;

        // Text color
        var aColor = a.Color?.Val?.Value;
        var bColor = b.Color?.Val?.Value;

        // Highlight color
        var aHighlight = a.Highlight?.Val?.Value;
        var bHighlight = b.Highlight?.Val?.Value;

        // Bold / Italic (OnOffValue semantics: treat null as false)
        var aBold = a.Bold?.Val ?? false;
        var bBold = b.Bold?.Val ?? false;

        var aItalic = a.Italic?.Val ?? false;
        var bItalic = b.Italic?.Val ?? false;

        // Underline style
        var aUnderline = a.Underline?.Val?.Value;
        var bUnderline = b.Underline?.Val?.Value;

        // Font family (RunFonts)
        var aAsciiFont = a.RunFonts?.Ascii?.Value;
        var bAsciiFont = b.RunFonts?.Ascii?.Value;

        var aHighAnsiFont = a.RunFonts?.HighAnsi?.Value;
        var bHighAnsiFont = b.RunFonts?.HighAnsi?.Value;

        var aEastAsiaFont = a.RunFonts?.EastAsia?.Value;
        var bEastAsiaFont = b.RunFonts?.EastAsia?.Value;

        var aComplexFont = a.RunFonts?.ComplexScript?.Value;
        var bComplexFont = b.RunFonts?.ComplexScript?.Value;

        // Font size (half-points as string, e.g. "24")
        var aFontSize = a.FontSize?.Val?.Value;
        var bFontSize = b.FontSize?.Val?.Value;

        var aFontSizeCs = a.FontSizeComplexScript?.Val?.Value;
        var bFontSizeCs = b.FontSizeComplexScript?.Val?.Value;

        return
            aShadeFill == bShadeFill &&
            aColor == bColor &&
            aHighlight == bHighlight &&
            aBold == bBold &&
            aItalic == bItalic &&
            aUnderline == bUnderline &&
            aAsciiFont == bAsciiFont &&
            aHighAnsiFont == bHighAnsiFont &&
            aEastAsiaFont == bEastAsiaFont &&
            aComplexFont == bComplexFont &&
            aFontSize == bFontSize &&
            aFontSizeCs == bFontSizeCs;
    }
}
