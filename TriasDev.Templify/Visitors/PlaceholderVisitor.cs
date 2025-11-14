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

        // Check if replacement value contains markdown
        if (MarkdownParser.ContainsMarkdown(replacementValue))
        {
            // Parse markdown and create multiple runs with formatting
            List<MarkdownSegment> segments = MarkdownParser.Parse(replacementValue);
            UpdateParagraphTextWithMarkdown(paragraph, runs, textBefore, segments, textAfter);
        }
        else
        {
            // No markdown - use simple text replacement
            string replacedText = textBefore + replacementValue + textAfter;
            UpdateParagraphText(paragraph, runs, replacedText);
        }
    }

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
        // Extract base formatting from the original runs
        RunProperties? baseProperties = FormattingPreserver.ExtractAndCloneRunProperties(runs);

        // Remove all existing runs
        foreach (Run run in runs)
        {
            run.Remove();
        }

        // Add text before placeholder (if any)
        if (!string.IsNullOrEmpty(textBefore))
        {
            Text text = new Text(textBefore);
            text.Space = SpaceProcessingModeValues.Preserve;
            Run beforeRun = new Run(text);
            FormattingPreserver.ApplyRunProperties(beforeRun, FormattingPreserver.CloneRunProperties(baseProperties));
            paragraph.AppendChild(beforeRun);
        }

        // Add markdown segments with appropriate formatting
        // Note: Empty segments are filtered by the parser
        foreach (MarkdownSegment segment in segments)
        {
            Text text = new Text(segment.Text);
            text.Space = SpaceProcessingModeValues.Preserve;
            Run segmentRun = new Run(text);

            // Apply base formatting merged with markdown formatting
            RunProperties? mergedProperties = FormattingPreserver.ApplyMarkdownFormatting(
                FormattingPreserver.CloneRunProperties(baseProperties),
                segment.IsBold,
                segment.IsItalic,
                segment.IsStrikethrough);

            FormattingPreserver.ApplyRunProperties(segmentRun, mergedProperties);
            paragraph.AppendChild(segmentRun);
        }

        // Add text after placeholder (if any)
        if (!string.IsNullOrEmpty(textAfter))
        {
            Text text = new Text(textAfter);
            text.Space = SpaceProcessingModeValues.Preserve;
            Run afterRun = new Run(text);
            FormattingPreserver.ApplyRunProperties(afterRun, FormattingPreserver.CloneRunProperties(baseProperties));
            paragraph.AppendChild(afterRun);
        }
    }
}
