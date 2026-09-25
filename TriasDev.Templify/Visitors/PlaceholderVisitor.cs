// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.Replacements;
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
/// This visitor processes them individually.
/// </remarks>
internal sealed class PlaceholderVisitor : ITemplateElementVisitor
{
    private readonly PlaceholderReplacementOptions _options;
    private readonly HashSet<string> _missingVariables;
    private readonly IWarningCollector _warningCollector;
    private int _replacementCount;

    /// <summary>
    /// Gets the total number of placeholder replacements made by this visitor.
    /// </summary>
    public int ReplacementCount => _replacementCount;

    public PlaceholderVisitor(
        PlaceholderReplacementOptions options,
        HashSet<string> missingVariables,
        IWarningCollector warningCollector)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _missingVariables = missingVariables ?? throw new ArgumentNullException(nameof(missingVariables));
        _warningCollector = warningCollector ?? throw new ArgumentNullException(nameof(warningCollector));
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
            resolved = ExpressionPlaceholderEvaluator.TryEvaluate(placeholder.VariableName, context, _warningCollector, out value);
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
            _warningCollector.AddWarning(ProcessingWarning.MissingVariable(placeholder.VariableName));

            switch (_options.MissingVariableBehavior)
            {
                case MissingVariableBehavior.ReplaceWithEmpty:
                    ReplacePlaceholderInParagraph(paragraph, placeholder, string.Empty, applyMarkdown: false);
                    _replacementCount++;
                    break;

                case MissingVariableBehavior.ThrowException:
                    throw new MissingVariableException(
                        placeholder.VariableName,
                        $"Missing variable or invalid expression: {placeholder.VariableName}");

                case MissingVariableBehavior.LeaveUnchanged:
                default:
                    // Leave the placeholder as-is
                    break;
            }
        }
        else
        {
            // Variable/expression found - convert to string with optional format and replace.
            // The :raw specifier only disables markdown; the value itself uses default conversion.
            bool isRaw = IsRawFormat(placeholder.Format);
            string replacementValue = ValueConverter.ConvertToString(
                value,
                _options.Culture,
                isRaw ? null : placeholder.Format,
                _options.BooleanFormatterRegistry);

            // Apply text replacements (e.g., HTML entities to Word-compatible text)
            // Note: Apply returns null only if input is null, which won't happen here
            replacementValue = TextReplacements.Apply(replacementValue, _options.TextReplacements)!;

            // Remove characters invalid in XML 1.0 (e.g., 0x02 STX from JSON data)
            // to prevent OpenXML serialization failures
            replacementValue = XmlCharacterSanitizer.Sanitize(replacementValue)!;

            bool applyMarkdown = _options.EnableMarkdown && !isRaw;
            ReplacePlaceholderInParagraph(paragraph, placeholder, replacementValue, applyMarkdown);
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
    /// Newlines become line breaks (if enabled) and markdown becomes formatting (if requested);
    /// the replacement takes the formatting of the run where the placeholder starts.
    /// </summary>
    /// <param name="paragraph">The paragraph containing the placeholder.</param>
    /// <param name="placeholder">The placeholder to replace.</param>
    /// <param name="replacementValue">The value to replace it with.</param>
    /// <param name="applyMarkdown">Whether markdown syntax in the value is rendered as formatting.</param>
    private void ReplacePlaceholderInParagraph(
        Paragraph paragraph,
        PlaceholderMatch placeholder,
        string replacementValue,
        bool applyMarkdown)
    {
        ReplacementContent content = ReplacementContent.FromValue(
            replacementValue,
            _options.EnableNewlineSupport,
            applyMarkdown);

        ParagraphTextRewriter.Replace(
            paragraph,
            placeholder.StartIndex,
            placeholder.StartIndex + placeholder.Length,
            content);
    }

    /// <summary>
    /// Checks whether the format specifier is <c>raw</c>, which disables markdown for the placeholder.
    /// </summary>
    private static bool IsRawFormat(string? format) =>
        string.Equals(format, "raw", StringComparison.OrdinalIgnoreCase);
}
