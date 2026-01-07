// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Collections;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;

namespace TriasDev.Templify.Visitors;

/// <summary>
/// Visitor that processes loop blocks ({{#foreach}}/{{/foreach}}).
/// Expands loops by cloning content for each collection item.
/// </summary>
/// <remarks>
/// This visitor wraps the loop expansion logic from LoopProcessor into the visitor pattern.
/// Benefits:
/// - Works with DocumentWalker for unified traversal
/// - Context-aware (uses IEvaluationContext from Phase 1)
/// - Can be composed with other visitors
/// - Eliminates duplication by using DocumentWalker for nested processing
///
/// Architecture:
/// 1. Resolves collection from context
/// 2. For each item: Creates LoopEvaluationContext
/// 3. Clones content elements for this iteration
/// 4. Uses DocumentWalker to process cloned content (handles nested loops, conditionals, placeholders)
/// 5. Inserts all cloned iterations and removes original loop block
/// </remarks>
internal sealed class LoopVisitor : ITemplateElementVisitor
{
    private readonly DocumentWalker _walker;
    private ITemplateElementVisitor _nestedVisitor;
    private readonly IWarningCollector _warningCollector;

    public LoopVisitor(DocumentWalker walker, ITemplateElementVisitor nestedVisitor, IWarningCollector warningCollector)
    {
        _walker = walker ?? throw new ArgumentNullException(nameof(walker));
        _nestedVisitor = nestedVisitor ?? throw new ArgumentNullException(nameof(nestedVisitor));
        _warningCollector = warningCollector ?? throw new ArgumentNullException(nameof(warningCollector));
    }

    /// <summary>
    /// Updates the nested visitor. Used to create circular reference for deep nesting support.
    /// </summary>
    internal void SetNestedVisitor(ITemplateElementVisitor nestedVisitor)
    {
        _nestedVisitor = nestedVisitor ?? throw new ArgumentNullException(nameof(nestedVisitor));
    }

    /// <summary>
    /// Processes a loop block by expanding it with cloned content for each collection item.
    /// </summary>
    /// <param name="loop">The loop block to process.</param>
    /// <param name="context">The evaluation context for resolving variables.</param>
    public void VisitLoop(LoopBlock loop, IEvaluationContext context)
    {
        // Resolve the collection from context
        if (!context.TryResolveVariable(loop.CollectionName, out object? collectionObj))
        {
            // Collection not found - remove loop block and report warning
            _warningCollector.AddWarning(ProcessingWarning.MissingLoopCollection(loop.CollectionName));
            RemoveLoopBlock(loop);
            return;
        }

        // Handle null collection - treat same as missing (silent removal) and report warning
        if (collectionObj == null)
        {
            _warningCollector.AddWarning(ProcessingWarning.NullLoopCollection(loop.CollectionName));
            RemoveLoopBlock(loop);
            return;
        }

        // Ensure it's actually a collection (but not a string, which is IEnumerable<char>)
        if (collectionObj is string || collectionObj is not IEnumerable collection)
        {
            throw new InvalidOperationException(
                $"Variable '{loop.CollectionName}' is not a collection. Cannot iterate.");
        }

        // Create loop contexts for each item
        IReadOnlyList<LoopContext> contexts = LoopContext.CreateContexts(
            collection,
            loop.CollectionName,
            loop.IterationVariableName,
            parent: null);

        // Handle empty collection
        if (contexts.Count == 0)
        {
            RemoveLoopBlock(loop);
            return;
        }

        // Process each iteration (in reverse order to maintain document order during insertion)
        OpenXmlElement? insertionPoint = loop.EndMarker;

        for (int i = contexts.Count - 1; i >= 0; i--)
        {
            LoopContext loopContext = contexts[i];

            // Create loop evaluation context that chains to global context
            LoopEvaluationContext loopEvalContext = new LoopEvaluationContext(loopContext, context);

            // Clone content elements for this iteration
            List<OpenXmlElement> clonedElements = TemplateElementHelper.CloneElements(loop.ContentElements);

            // Insert cloned elements after the end marker FIRST
            // This gives them parents so SafeRemove works when processing nested constructs
            // Track the last inserted element to maintain correct order
            OpenXmlElement lastInserted = insertionPoint;
            foreach (OpenXmlElement clonedElement in clonedElements)
            {
                lastInserted.InsertAfterSelf(clonedElement);
                lastInserted = clonedElement;
            }

            // Process nested constructs in cloned content using DocumentWalker
            // This handles nested loops, conditionals, and placeholders with proper context
            // Note: After insertion, clonedElements now have parents and can be processed
            _walker.WalkElements(clonedElements, _nestedVisitor, loopEvalContext);
        }

        // Remove the original loop block (markers and content)
        RemoveLoopBlock(loop);
    }

    /// <summary>
    /// Not implemented - LoopVisitor only processes loops.
    /// </summary>
    public void VisitConditional(ConditionalBlock conditional, IEvaluationContext context)
    {
        // LoopVisitor doesn't process conditionals
        // This method is no-op to satisfy the interface
    }

    /// <summary>
    /// Not implemented - LoopVisitor only processes loops.
    /// </summary>
    public void VisitPlaceholder(PlaceholderMatch placeholder, Paragraph paragraph, IEvaluationContext context)
    {
        // LoopVisitor doesn't process placeholders
        // This method is no-op to satisfy the interface
    }

    /// <summary>
    /// Not implemented - LoopVisitor only processes loops.
    /// </summary>
    public void VisitParagraph(Paragraph paragraph, IEvaluationContext context)
    {
        // LoopVisitor doesn't process regular paragraphs
        // This method is no-op to satisfy the interface
    }

    /// <summary>
    /// Removes a loop block from the document (markers and content).
    /// </summary>
    private void RemoveLoopBlock(LoopBlock loop)
    {
        TemplateElementHelper.SafeRemove(loop.StartMarker);
        TemplateElementHelper.SafeRemoveRange(loop.ContentElements);
        TemplateElementHelper.SafeRemove(loop.EndMarker);
    }
}
