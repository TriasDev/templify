// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

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
}
