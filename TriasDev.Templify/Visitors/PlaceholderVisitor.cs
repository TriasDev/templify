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
    /// <summary>
    /// Newline separators for splitting text. Order matters: \r\n must come before \r and \n.
    /// </summary>
    private static readonly string[] NewlineSeparators = { "\r\n", "\r", "\n" };

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

        // Concatenate all text from runs
        string fullText = string.Concat(runs.Select(r => r.InnerText));

        // Calculate the text before and after the placeholder
        string textBefore = fullText.Substring(0, placeholder.StartIndex);
        string textAfter = fullText.Substring(placeholder.StartIndex + placeholder.Length);

        // Check for newlines and markdown in the replacement value
        bool hasNewlines = _options.EnableNewlineSupport && ContainsNewlines(replacementValue);
        bool hasMarkdown = MarkdownParser.ContainsMarkdown(replacementValue);

        if (hasNewlines && hasMarkdown)
        {
            // Both markdown and newlines - process each line for markdown
            UpdateParagraphTextWithMarkdownAndBreaks(paragraph, runs, textBefore, replacementValue, textAfter);
        }
        else if (hasNewlines)
        {
            // Only newlines - split and add break elements
            UpdateParagraphTextWithBreaks(paragraph, runs, textBefore, replacementValue, textAfter);
        }
        else if (hasMarkdown)
        {
            // Only markdown - parse and create multiple runs with formatting
            List<MarkdownSegment> segments = MarkdownParser.Parse(replacementValue);
            UpdateParagraphTextWithMarkdown(paragraph, runs, textBefore, segments, textAfter);
        }
        else
        {
            // No markdown or newlines - use simple text replacement
            string replacedText = textBefore + replacementValue + textAfter;
            UpdateParagraphText(paragraph, runs, replacedText);
        }
    }

    /// <summary>
    /// Checks if the text contains newline characters.
    /// </summary>
    private static bool ContainsNewlines(string text) =>
        text.Contains('\n') || text.Contains('\r');

    /// <summary>
    /// Updates the paragraph text by removing old runs and creating a new run with the replaced text.
    /// Preserves formatting (RunProperties) from the original runs.
    /// </summary>
    private static void UpdateParagraphText(Paragraph paragraph, List<Run> runs, string newText)
    {
        // Extract and clone formatting from the original runs before removing them
        RunProperties? clonedProperties = FormattingPreserver.ExtractAndCloneRunProperties(runs);

        // Remove all existing runs
        foreach (Run run in runs)
        {
            run.Remove();
        }

        // Create a new run with the replaced text
        Text text = new Text(newText);
        text.Space = SpaceProcessingModeValues.Preserve;
        Run newRun = new Run(text);

        // Apply the preserved formatting to the new run
        FormattingPreserver.ApplyRunProperties(newRun, clonedProperties);

        // Insert the new run at the beginning of the paragraph
        paragraph.AppendChild(newRun);
    }

    /// <summary>
    /// Prepares a paragraph for content replacement by extracting formatting and removing existing runs.
    /// </summary>
    /// <param name="runs">The original runs to extract formatting from and remove.</param>
    /// <returns>The cloned run properties from the original runs.</returns>
    private static RunProperties? PrepareForReplacement(List<Run> runs)
    {
        // Extract base formatting from the original runs
        RunProperties? baseProperties = FormattingPreserver.ExtractAndCloneRunProperties(runs);

        // Remove all existing runs
        foreach (Run run in runs)
        {
            run.Remove();
        }

        return baseProperties;
    }

    /// <summary>
    /// Adds text before and after the replacement content if they are not empty.
    /// </summary>
    /// <param name="paragraph">The paragraph to add runs to.</param>
    /// <param name="textBefore">Text before the placeholder (added if not empty).</param>
    /// <param name="textAfter">Text after the placeholder (added if not empty).</param>
    /// <param name="baseProperties">The base formatting to apply.</param>
    /// <param name="addContent">Action to add the main content between before/after text.</param>
    private static void AddReplacementContent(
        Paragraph paragraph,
        string textBefore,
        string textAfter,
        RunProperties? baseProperties,
        Action addContent)
    {
        // Add text before placeholder (if any)
        if (!string.IsNullOrEmpty(textBefore))
        {
            AddTextRun(paragraph, textBefore, baseProperties);
        }

        // Add the main replacement content
        addContent();

        // Add text after placeholder (if any)
        if (!string.IsNullOrEmpty(textAfter))
        {
            AddTextRun(paragraph, textAfter, baseProperties);
        }
    }

    /// <summary>
    /// Updates the paragraph text with markdown-formatted segments.
    /// Creates multiple runs with appropriate formatting for each segment.
    /// </summary>
    /// <param name="paragraph">The paragraph to update.</param>
    /// <param name="runs">The original runs in the paragraph.</param>
    /// <param name="textBefore">Text before the placeholder.</param>
    /// <param name="segments">Markdown-parsed segments to insert.</param>
    /// <param name="textAfter">Text after the placeholder.</param>
    private static void UpdateParagraphTextWithMarkdown(
        Paragraph paragraph,
        List<Run> runs,
        string textBefore,
        List<MarkdownSegment> segments,
        string textAfter)
    {
        RunProperties? baseProperties = PrepareForReplacement(runs);

        AddReplacementContent(paragraph, textBefore, textAfter, baseProperties, () =>
        {
            // Add markdown segments with appropriate formatting
            // Note: Empty segments are filtered by the parser
            foreach (MarkdownSegment segment in segments)
            {
                AddMarkdownRun(paragraph, segment, baseProperties);
            }
        });
    }

    /// <summary>
    /// Updates the paragraph text with line breaks for newline characters.
    /// Creates multiple runs with Break elements between lines.
    /// </summary>
    /// <param name="paragraph">The paragraph to update.</param>
    /// <param name="runs">The original runs in the paragraph.</param>
    /// <param name="textBefore">Text before the placeholder.</param>
    /// <param name="textWithNewlines">Text containing newline characters to split.</param>
    /// <param name="textAfter">Text after the placeholder.</param>
    private static void UpdateParagraphTextWithBreaks(
        Paragraph paragraph,
        List<Run> runs,
        string textBefore,
        string textWithNewlines,
        string textAfter)
    {
        RunProperties? baseProperties = PrepareForReplacement(runs);

        AddReplacementContent(paragraph, textBefore, textAfter, baseProperties, () =>
        {
            // Split on newlines and add Break elements between lines
            AddLinesWithBreaks(paragraph, textWithNewlines, line =>
            {
                AddTextRun(paragraph, line, baseProperties);
            });
        });
    }

    /// <summary>
    /// Splits text by newlines and invokes the action for each non-empty line,
    /// inserting Break elements between lines.
    /// </summary>
    /// <param name="paragraph">The paragraph to add breaks to.</param>
    /// <param name="text">The text to split by newlines.</param>
    /// <param name="addLine">Action to invoke for each non-empty line.</param>
    private static void AddLinesWithBreaks(
        Paragraph paragraph,
        string text,
        Action<string> addLine)
    {
        string[] lines = text.Split(NewlineSeparators, StringSplitOptions.None);
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                // Insert Break element for line break
                paragraph.AppendChild(new Run(new Break()));
            }

            if (!string.IsNullOrEmpty(lines[i]))
            {
                addLine(lines[i]);
            }
        }
    }

    /// <summary>
    /// Updates the paragraph text with both markdown formatting and line breaks.
    /// Splits by newlines first, then parses each line for markdown.
    /// </summary>
    /// <param name="paragraph">The paragraph to update.</param>
    /// <param name="runs">The original runs in the paragraph.</param>
    /// <param name="textBefore">Text before the placeholder.</param>
    /// <param name="textWithMarkdownAndNewlines">Text containing both markdown and newlines.</param>
    /// <param name="textAfter">Text after the placeholder.</param>
    private static void UpdateParagraphTextWithMarkdownAndBreaks(
        Paragraph paragraph,
        List<Run> runs,
        string textBefore,
        string textWithMarkdownAndNewlines,
        string textAfter)
    {
        RunProperties? baseProperties = PrepareForReplacement(runs);

        AddReplacementContent(paragraph, textBefore, textAfter, baseProperties, () =>
        {
            // Split by newlines, then parse markdown for each line
            AddLinesWithBreaks(paragraph, textWithMarkdownAndNewlines, line =>
            {
                // Parse markdown for this line
                if (MarkdownParser.ContainsMarkdown(line))
                {
                    List<MarkdownSegment> segments = MarkdownParser.Parse(line);
                    foreach (MarkdownSegment segment in segments)
                    {
                        AddMarkdownRun(paragraph, segment, baseProperties);
                    }
                }
                else
                {
                    AddTextRun(paragraph, line, baseProperties);
                }
            });
        });
    }

    /// <summary>
    /// Helper method to add a text run with preserved formatting.
    /// </summary>
    private static void AddTextRun(Paragraph paragraph, string text, RunProperties? baseProperties)
    {
        Text textElement = new Text(text) { Space = SpaceProcessingModeValues.Preserve };
        Run run = new Run(textElement);
        FormattingPreserver.ApplyRunProperties(run, FormattingPreserver.CloneRunProperties(baseProperties));
        paragraph.AppendChild(run);
    }

    /// <summary>
    /// Helper method to add a markdown segment run with merged formatting.
    /// </summary>
    private static void AddMarkdownRun(Paragraph paragraph, MarkdownSegment segment, RunProperties? baseProperties)
    {
        Text textElement = new Text(segment.Text) { Space = SpaceProcessingModeValues.Preserve };
        Run run = new Run(textElement);

        RunProperties? mergedProperties = FormattingPreserver.ApplyMarkdownFormatting(
            FormattingPreserver.CloneRunProperties(baseProperties),
            segment.IsBold,
            segment.IsItalic,
            segment.IsStrikethrough);

        FormattingPreserver.ApplyRunProperties(run, mergedProperties);
        paragraph.AppendChild(run);
    }
}
