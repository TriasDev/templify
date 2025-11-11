using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Loops;
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
        // Try to resolve the variable from the context
        if (!context.TryResolveVariable(placeholder.VariableName, out object? value))
        {
            // Variable not found - handle based on options
            _missingVariables.Add(placeholder.VariableName);

            switch (_options.MissingVariableBehavior)
            {
                case MissingVariableBehavior.ReplaceWithEmpty:
                    ReplacePlaceholderInParagraph(paragraph, placeholder, string.Empty);
                    _replacementCount++;
                    break;

                case MissingVariableBehavior.ThrowException:
                    throw new InvalidOperationException($"Missing variable: {placeholder.VariableName}");

                case MissingVariableBehavior.LeaveUnchanged:
                default:
                    // Leave the placeholder as-is
                    break;
            }
        }
        else
        {
            // Variable found - convert to string and replace
            string replacementValue = ValueConverter.ConvertToString(value, _options.Culture);
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

        // Replace the placeholder in the full text
        string replacedText = fullText.Remove(placeholder.StartIndex, placeholder.Length)
                                      .Insert(placeholder.StartIndex, replacementValue);

        // Update the paragraph with the new text
        UpdateParagraphText(paragraph, runs, replacedText);
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
}
