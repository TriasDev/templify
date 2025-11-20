// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;

namespace TriasDev.Templify.Visitors;

/// <summary>
/// Visitor interface for processing template elements in a document.
/// Implementations can process conditionals, loops, placeholders, or other template constructs.
/// </summary>
/// <remarks>
/// This interface follows the Visitor Pattern to enable clean separation of concerns:
/// - DocumentWalker handles traversal and detection
/// - Visitors handle processing logic
/// - IEvaluationContext provides data resolution
///
/// Key benefits:
/// - Eliminates code duplication (GetElementText, detection logic, etc.)
/// - Clear separation: traversal vs. processing
/// - Extensible: new visitors can be added without changing the walker
/// - Testable: visitors can be mocked for testing
///
/// Note: This is internal for Phase 2. May become public in Phase 3 for extensibility.
/// </remarks>
internal interface ITemplateElementVisitor
{
    /// <summary>
    /// Visits a conditional block ({{#if}}/{{else}}/{{/if}}).
    /// </summary>
    /// <param name="conditional">The conditional block to process.</param>
    /// <param name="context">The evaluation context for resolving variables.</param>
    /// <remarks>
    /// The visitor should:
    /// - Evaluate the condition using the provided context
    /// - Keep the true branch or false branch based on evaluation
    /// - Remove the conditional markers and unwanted branch
    /// </remarks>
    void VisitConditional(ConditionalBlock conditional, IEvaluationContext context);

    /// <summary>
    /// Visits a loop block ({{#foreach}}/{{/foreach}}).
    /// </summary>
    /// <param name="loop">The loop block to process.</param>
    /// <param name="context">The evaluation context for resolving variables.</param>
    /// <remarks>
    /// The visitor should:
    /// - Resolve the collection from the context
    /// - Clone the loop content for each iteration
    /// - Create a LoopEvaluationContext for each iteration
    /// - Process nested template constructs within each iteration
    /// - Remove the loop markers
    /// </remarks>
    void VisitLoop(LoopBlock loop, IEvaluationContext context);

    /// <summary>
    /// Visits a placeholder ({{VariableName}}).
    /// </summary>
    /// <param name="placeholder">The placeholder match information.</param>
    /// <param name="paragraph">The paragraph containing the placeholder.</param>
    /// <param name="context">The evaluation context for resolving variables.</param>
    /// <remarks>
    /// The visitor should:
    /// - Resolve the variable value from the context
    /// - Replace the placeholder text with the resolved value
    /// - Handle missing variables according to configuration
    /// - Preserve formatting where possible
    /// </remarks>
    void VisitPlaceholder(PlaceholderMatch placeholder, Paragraph paragraph, IEvaluationContext context);

    /// <summary>
    /// Visits a regular paragraph (no template constructs detected).
    /// Allows visitors to perform custom processing on all paragraphs.
    /// </summary>
    /// <param name="paragraph">The paragraph to visit.</param>
    /// <param name="context">The evaluation context.</param>
    /// <remarks>
    /// This method is called for paragraphs that don't contain:
    /// - Conditional markers ({{#if}}, {{else}}, {{/if}})
    /// - Loop markers ({{#foreach}}, {{/foreach}})
    /// - Placeholders ({{VariableName}})
    ///
    /// Most visitors will leave these paragraphs unchanged, but custom
    /// processing (e.g., formatting, analytics) can be performed here.
    /// </remarks>
    void VisitParagraph(Paragraph paragraph, IEvaluationContext context);
}
