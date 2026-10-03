// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Packaging;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Visitors;

/// <summary>
/// The visitor graph that processes a Word document: conditionals, loops and placeholders,
/// walked by one <see cref="DocumentWalker"/>.
/// </summary>
/// <remarks>
/// The loop visitor processes the content of each iteration with the complete composite (which
/// contains the loop visitor itself), so loops and conditionals nest to any depth. That cycle is
/// wired here, in one place.
/// </remarks>
internal sealed class TemplatePipeline
{
    private TemplatePipeline(
        DocumentWalker walker,
        CompositeVisitor visitor,
        PlaceholderVisitor placeholderVisitor)
    {
        Walker = walker;
        Visitor = visitor;
        PlaceholderVisitor = placeholderVisitor;
    }

    /// <summary>Gets the document walker.</summary>
    public DocumentWalker Walker { get; }

    /// <summary>Gets the composite of all visitors (conditional, loop, placeholder).</summary>
    public CompositeVisitor Visitor { get; }

    /// <summary>Gets the placeholder visitor (replacement count).</summary>
    public PlaceholderVisitor PlaceholderVisitor { get; }

    /// <summary>
    /// Creates the visitor graph.
    /// </summary>
    /// <param name="options">The replacement options.</param>
    /// <param name="missingVariables">Receives the names of missing variables.</param>
    /// <param name="warningCollector">Receives processing warnings.</param>
    public static TemplatePipeline Create(
        PlaceholderReplacementOptions options,
        HashSet<string> missingVariables,
        IWarningCollector warningCollector)
    {
        PlaceholderVisitor placeholderVisitor = new PlaceholderVisitor(options, missingVariables, warningCollector);
        DocumentWalker walker = new DocumentWalker();
        ConditionalVisitor conditionalVisitor = new ConditionalVisitor(warningCollector);

        // The loop visitor needs the final composite, which contains the loop visitor itself:
        // create it with a temporary composite, then point it at the final one.
        CompositeVisitor tempComposite = new CompositeVisitor(conditionalVisitor, placeholderVisitor);
        LoopVisitor loopVisitor = new LoopVisitor(walker, tempComposite, warningCollector);
        CompositeVisitor composite = new CompositeVisitor(conditionalVisitor, loopVisitor, placeholderVisitor);
        loopVisitor.SetNestedVisitor(composite);

        return new TemplatePipeline(walker, composite, placeholderVisitor);
    }

    /// <summary>
    /// Processes the document body, then headers and footers, then footnotes and endnotes.
    /// Comments are intentionally not processed: they are reviewer notes, not document content.
    /// </summary>
    public void Process(WordprocessingDocument document, IEvaluationContext context)
    {
        Walker.Walk(document, Visitor, context);
        Walker.WalkHeadersAndFooters(document, Visitor, context);
        Walker.WalkFootnotesAndEndnotes(document, Visitor, context);
    }
}
