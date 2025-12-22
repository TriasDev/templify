// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Expressions;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Markdown;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Visitors;

/// <summary>
/// Visitor that processes placeholders ({{VariableName}}).
/// Replaces placeholders with values from the evaluation context.
/// </summary>
/// <remarks>
/// This visitor wraps placeholder replacement logic into the visitor pattern.
/// Benefits:
/// - Works with DocumentWalker for unified traversal
/// - Context-aware (uses IEvaluationContext for variable resolution)
/// - Can be composed with other visitors
/// - Handles both global variables and loop-scoped variables
///
/// Note: DocumentWalker calls VisitPlaceholder for EACH placeholder in a paragraph.
/// This visitor processes them individually, unlike DocumentBodyReplacer which
/// processes all placeholders in a paragraph at once.
/// </remarks>
internal sealed class PlaceholderVisitor : ITemplateElementVisitor
{
    private readonly PlaceholderReplacementOptions _options;
    private readonly HashSet<string> _missingVariables;
    private int _replacementCount;

    /// <summary>
    /// Gets the total number of placeholder replacements made by this visitor.
    /// </summary>
    public int ReplacementCount => _replacementCount;

    public PlaceholderVisitor(PlaceholderReplacementOptions options, HashSet<string> missingVariables)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _missingVariables = missingVariables ?? throw new ArgumentNullException(nameof(missingVariables));
        _replacementCount = 0;
    }

    /// <summary>
    /// Processes a placeholder by replacing it with the resolved value from context.
    /// </summary>
    /// <param name="placeholder">The placeholder match to process.</param>
    /// <param name="paragraph">The paragraph containing the placeholder.</param>
    /// <param name="context">The evaluation context for resolving variables.</param>
    public void VisitPlaceholder(PlaceholderMatch placeholder, Paragraph paragraph, IEvaluationContext context)
    {
        object? value;
        bool resolved;

        // Check if this is an expression
        if (placeholder.IsExpression)
        {
            // Parse and evaluate the expression
            var parser = new BooleanExpressionParser();
            BooleanExpression? expression = parser.Parse(placeholder.VariableName);

            if (expression != null)
            {
                try
                {
                    var dataContext = new EvaluationContextAdapter(context);
                    bool result = expression.Evaluate(dataContext);
                    value = result;
                    resolved = true;
                }
                catch (ArgumentException)
                {
                    resolved = false;
                    value = null;
                }
                catch (InvalidOperationException)
                {
                    resolved = false;
                    value = null;
                }
                catch (InvalidCastException)
                {
                    resolved = false;
                    value = null;
                }
            }
            else
            {
                resolved = false;
                value = null;
            }
        }
        else
        {
            // Try to resolve the variable from the context
            resolved = context.TryResolveVariable(placeholder.VariableName, out value);
        }

        if (!resolved)
        {
            // Variable/expression not found - handle based on options
            _missingVariables.Add(placeholder.VariableName);

            switch (_options.MissingVariableBehavior)
            {
                case MissingVariableBehavior.ReplaceWithEmpty:
                    ReplacePlaceholderInParagraph(paragraph, placeholder, string.Empty);
                    _replacementCount++;
                    break;

                case MissingVariableBehavior.ThrowException:
                    throw new InvalidOperationException($"Missing variable or invalid expression: {placeholder.VariableName}");

                case MissingVariableBehavior.LeaveUnchanged:
                default:
                    // Leave the placeholder as-is
                    break;
            }
        }
        else
        {
            // Variable/expression found - convert to string with optional format and replace
            string replacementValue = ValueConverter.ConvertToString(
                value,
                _options.Culture,
                placeholder.Format,
                _options.BooleanFormatterRegistry);
            ReplacePlaceholderInParagraph(paragraph, placeholder, replacementValue);
            _replacementCount++;
        }
    }

    /// <summary>
    /// Not implemented - PlaceholderVisitor only processes placeholders.
    /// </summary>
    public void VisitConditional(ConditionalBlock conditional, IEvaluationContext context)
    {
        // PlaceholderVisitor doesn't process conditionals
        // This method is no-op to satisfy the interface
    }

    /// <summary>
    /// Not implemented - PlaceholderVisitor only processes placeholders.
    /// </summary>
    public void VisitLoop(LoopBlock loop, IEvaluationContext context)
    {
        // PlaceholderVisitor doesn't process loops
        // This method is no-op to satisfy the interface
    }

    /// <summary>
    /// Not implemented - PlaceholderVisitor only processes placeholders.
    /// </summary>
    public void VisitParagraph(Paragraph paragraph, IEvaluationContext context)
    {
        // PlaceholderVisitor doesn't process regular paragraphs
        // This method is no-op to satisfy the interface
    }

    /// <summary>
    /// Replaces a single placeholder in the paragraph with the replacement value.
    /// Supports markdown formatting in replacement values.
    /// Preserves per-run formatting when placeholder is contained within a single run.
    /// </summary>
    /// <param name="paragraph">The paragraph containing the placeholder.</param>
    /// <param name="placeholder">The placeholder to replace.</param>
    /// <param name="replacementValue">The value to replace it with.</param>
    private void ReplacePlaceholderInParagraph(
        Paragraph paragraph,
        PlaceholderMatch placeholder,
        string replacementValue)
    {
        // Get all text runs in the paragraph
        List<Run> runs = paragraph.Descendants<Run>().ToList();

        if (runs.Count == 0)
        {
            return;
        }

        // Build run boundaries for mapping placeholder indices to runs
        List<(Run Run, int StartIndex, int EndIndex)> runBoundaries = BuildRunBoundaries(runs);

        // Find which runs the placeholder spans
        int placeholderStart = placeholder.StartIndex;
        int placeholderEnd = placeholder.StartIndex + placeholder.Length;

        (int startRunIndex, int endRunIndex) = FindRunsForPlaceholder(runBoundaries, placeholderStart, placeholderEnd);

        // Check if placeholder is contained within a single run.
        // Single-run replacement preserves the run's complete formatting (highlight, shading, etc.)
        // which would otherwise be lost when merging multiple runs into one.
        if (startRunIndex == endRunIndex && startRunIndex >= 0)
        {
            // Placeholder is within a single run - replace in place, preserving that run's formatting
            ReplacePlaceholderInSingleRun(runBoundaries[startRunIndex], placeholderStart, placeholderEnd, replacementValue);
        }
        else
        {
            // Placeholder spans multiple runs - replace only the affected runs, preserving others
            ReplacePlaceholderAcrossRuns(
                runBoundaries,
                startRunIndex,
                endRunIndex,
                placeholderStart,
                placeholderEnd,
                replacementValue);
        }
    }

    /// <summary>
    /// Builds a list of run boundaries (start and end character indices) for mapping.
    /// </summary>
    private static List<(Run Run, int StartIndex, int EndIndex)> BuildRunBoundaries(List<Run> runs)
    {
        List<(Run, int, int)> boundaries = new List<(Run, int, int)>();
        int currentIndex = 0;

        foreach (Run run in runs)
        {
            int runLength = run.InnerText.Length;
            boundaries.Add((run, currentIndex, currentIndex + runLength));
            currentIndex += runLength;
        }

        return boundaries;
    }

    /// <summary>
    /// Finds the run indices that contain the placeholder's start and end positions.
    /// </summary>
    private static (int startRunIndex, int endRunIndex) FindRunsForPlaceholder(
        List<(Run Run, int StartIndex, int EndIndex)> runBoundaries,
        int placeholderStart,
        int placeholderEnd)
    {
        int startRunIndex = -1;
        int endRunIndex = -1;

        for (int i = 0; i < runBoundaries.Count; i++)
        {
            (Run _, int runStart, int runEnd) = runBoundaries[i];

            // Check if placeholder start is within this run
            if (startRunIndex == -1 && placeholderStart >= runStart && placeholderStart < runEnd)
            {
                startRunIndex = i;
            }

            // Check if placeholder end is within this run
            // Note: placeholderEnd is exclusive, so we check <= runEnd
            if (placeholderEnd > runStart && placeholderEnd <= runEnd)
            {
                endRunIndex = i;
            }

            // Only break once both indices are found to avoid returning (-1, someIndex)
            if (startRunIndex >= 0 && endRunIndex >= 0)
            {
                break;
            }
        }

        return (startRunIndex, endRunIndex);
    }

    /// <summary>
    /// Replaces a placeholder within a single run, preserving that run's formatting.
    /// </summary>
    private void ReplacePlaceholderInSingleRun(
        (Run Run, int StartIndex, int EndIndex) runInfo,
        int placeholderStart,
        int placeholderEnd,
        string replacementValue)
    {
        Run run = runInfo.Run;
        string runText = run.InnerText;

        // Calculate local indices within this run
        int localStart = placeholderStart - runInfo.StartIndex;
        int localEnd = placeholderEnd - runInfo.StartIndex;

        // Build new text with replacement
        string textBefore = runText.Substring(0, localStart);
        string textAfter = runText.Substring(localEnd);

        // Check if replacement value contains markdown
        if (MarkdownParser.ContainsMarkdown(replacementValue))
        {
            // Parse markdown and replace this single run with multiple runs
            List<MarkdownSegment> segments = MarkdownParser.Parse(replacementValue);
            ReplaceRunWithMarkdownSegments(run, textBefore, segments, textAfter);
        }
        else
        {
            // Simple replacement - update run text in place
            string newText = textBefore + replacementValue + textAfter;
            ReplaceRunText(run, newText);
        }
    }

    /// <summary>
    /// Replaces the text content of a run while preserving its formatting.
    /// </summary>
    private static void ReplaceRunText(Run run, string newText)
    {
        // Remove all text elements from the run
        run.RemoveAllChildren<Text>();

        // Add new text element
        Text text = new Text(newText);
        text.Space = SpaceProcessingModeValues.Preserve;
        run.AppendChild(text);
    }

    /// <summary>
    /// Replaces a single run with multiple runs for markdown formatting.
    /// Preserves the original run's formatting as the base.
    /// </summary>
    private static void ReplaceRunWithMarkdownSegments(
        Run originalRun,
        string textBefore,
        List<MarkdownSegment> segments,
        string textAfter)
    {
        // Get the parent element (typically a Paragraph) to insert new runs.
        // A null parent would indicate a detached/corrupted run, which should not occur
        // in normal document processing. We silently return as there's nothing to replace into.
        OpenXmlElement? parent = originalRun.Parent;
        if (parent == null) return;

        // Clone the original run's properties as base formatting
        RunProperties? baseProperties = FormattingPreserver.CloneRunProperties(originalRun.RunProperties);

        // Create list of new runs to insert
        List<Run> newRuns = new List<Run>();

        // Add text before placeholder (if any)
        if (!string.IsNullOrEmpty(textBefore))
        {
            Text text = new Text(textBefore);
            text.Space = SpaceProcessingModeValues.Preserve;
            Run beforeRun = new Run(text);
            FormattingPreserver.ApplyRunProperties(beforeRun, FormattingPreserver.CloneRunProperties(baseProperties));
            newRuns.Add(beforeRun);
        }

        // Add markdown segments with appropriate formatting
        foreach (MarkdownSegment segment in segments)
        {
            Text text = new Text(segment.Text);
            text.Space = SpaceProcessingModeValues.Preserve;
            Run segmentRun = new Run(text);

            RunProperties? mergedProperties = FormattingPreserver.ApplyMarkdownFormatting(
                FormattingPreserver.CloneRunProperties(baseProperties),
                segment.IsBold,
                segment.IsItalic,
                segment.IsStrikethrough);

            FormattingPreserver.ApplyRunProperties(segmentRun, mergedProperties);
            newRuns.Add(segmentRun);
        }

        // Add text after placeholder (if any)
        if (!string.IsNullOrEmpty(textAfter))
        {
            Text text = new Text(textAfter);
            text.Space = SpaceProcessingModeValues.Preserve;
            Run afterRun = new Run(text);
            FormattingPreserver.ApplyRunProperties(afterRun, FormattingPreserver.CloneRunProperties(baseProperties));
            newRuns.Add(afterRun);
        }

        // Insert new runs before the original run, then remove the original
        foreach (Run newRun in newRuns)
        {
            parent.InsertBefore(newRun, originalRun);
        }

        originalRun.Remove();
    }

    /// <summary>
    /// Replaces a placeholder that spans multiple runs, preserving formatting of each run
    /// and keeping unaffected runs intact.
    /// </summary>
    /// <remarks>
    /// This method only modifies the runs that contain the placeholder text.
    /// Other runs in the paragraph (before or after the placeholder) are preserved
    /// with their original formatting. This is critical for paragraphs that contain
    /// multiple differently-formatted sections (e.g., different background colors).
    /// </remarks>
    private void ReplacePlaceholderAcrossRuns(
        List<(Run Run, int StartIndex, int EndIndex)> runBoundaries,
        int startRunIndex,
        int endRunIndex,
        int placeholderStart,
        int placeholderEnd,
        string replacementValue)
    {
        // Safety check - if we couldn't find the runs, bail out
        if (startRunIndex < 0 || endRunIndex < 0 || startRunIndex > endRunIndex)
        {
            return;
        }

        // Get the runs that contain the placeholder
        List<(Run Run, int StartIndex, int EndIndex)> affectedRuns = runBoundaries
            .Skip(startRunIndex)
            .Take(endRunIndex - startRunIndex + 1)
            .ToList();

        // Extract formatting from the first affected run (this run has the placeholder's intended formatting)
        RunProperties? baseProperties = FormattingPreserver.CloneRunProperties(
            affectedRuns[0].Run.RunProperties);

        // Calculate text before placeholder in the first affected run
        (Run firstRun, int firstStart, int _) = affectedRuns[0];
        int localStart = placeholderStart - firstStart;
        string textBeforeInFirstRun = firstRun.InnerText.Substring(0, localStart);

        // Calculate text after placeholder in the last affected run
        (Run lastRun, int lastStart, int _) = affectedRuns[^1];
        int localEnd = placeholderEnd - lastStart;
        string textAfterInLastRun = lastRun.InnerText.Substring(localEnd);

        // Build replacement runs
        List<Run> newRuns = new List<Run>();

        // Text before placeholder in first run (if any) - preserves first run's formatting
        if (!string.IsNullOrEmpty(textBeforeInFirstRun))
        {
            newRuns.Add(CreateRunWithText(textBeforeInFirstRun, baseProperties));
        }

        // Replacement value (with or without markdown)
        if (MarkdownParser.ContainsMarkdown(replacementValue))
        {
            List<MarkdownSegment> segments = MarkdownParser.Parse(replacementValue);
            foreach (MarkdownSegment segment in segments)
            {
                RunProperties? props = FormattingPreserver.ApplyMarkdownFormatting(
                    FormattingPreserver.CloneRunProperties(baseProperties),
                    segment.IsBold,
                    segment.IsItalic,
                    segment.IsStrikethrough);
                newRuns.Add(CreateRunWithText(segment.Text, props));
            }
        }
        else
        {
            newRuns.Add(CreateRunWithText(replacementValue, baseProperties));
        }

        // Text after placeholder in last run (if any) - preserves last run's formatting
        if (!string.IsNullOrEmpty(textAfterInLastRun))
        {
            RunProperties? lastProps = FormattingPreserver.CloneRunProperties(lastRun.RunProperties);
            newRuns.Add(CreateRunWithText(textAfterInLastRun, lastProps));
        }

        // Insert new runs before the first affected run
        Run insertBefore = firstRun;
        foreach (Run newRun in newRuns)
        {
            insertBefore.Parent?.InsertBefore(newRun, insertBefore);
        }

        // Remove only the affected runs (preserving other runs in the paragraph)
        foreach ((Run run, int _, int _) in affectedRuns)
        {
            run.Remove();
        }
    }

    /// <summary>
    /// Creates a new Run element with the specified text and formatting properties.
    /// </summary>
    private static Run CreateRunWithText(string text, RunProperties? properties)
    {
        Text textElement = new Text(text);
        textElement.Space = SpaceProcessingModeValues.Preserve;
        Run run = new Run(textElement);

        if (properties != null)
        {
            run.RunProperties = FormattingPreserver.CloneRunProperties(properties);
        }

        return run;
    }

}
