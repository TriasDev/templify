// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;

namespace TriasDev.Templify.Visitors;

/// <summary>
/// Walks through a document tree, detects template elements, and dispatches them to a visitor.
/// Implements the traversal logic of the Visitor Pattern.
/// </summary>
/// <remarks>
/// The DocumentWalker is responsible for:
/// - Traversing the document structure (body, tables, cells, paragraphs)
/// - Detecting template constructs (conditionals, loops, placeholders)
/// - Dispatching detected elements to the appropriate visitor methods
/// - Handling element removal (skipping already-processed elements)
///
/// The walker uses existing detectors for consistency:
/// - ConditionalDetector for {{#if}}/{{#else}}/{{/if}}
/// - LoopDetector for {{#foreach}}/{{/foreach}}
/// - PlaceholderFinder for {{VariableName}}
/// </remarks>
internal sealed class DocumentWalker
{
    private readonly PlaceholderFinder _placeholderFinder;

    public DocumentWalker()
    {
        _placeholderFinder = new PlaceholderFinder();
    }

    /// <summary>
    /// Walks through the document body and visits all template elements.
    /// </summary>
    /// <param name="document">The Word document to walk.</param>
    /// <param name="visitor">The visitor that will process detected elements.</param>
    /// <param name="context">The evaluation context for variable resolution.</param>
    public void Walk(
        WordprocessingDocument document,
        ITemplateElementVisitor visitor,
        IEvaluationContext context)
    {
        if (document?.MainDocumentPart?.Document?.Body == null)
        {
            return;
        }

        Body body = document.MainDocumentPart.Document.Body;
        List<OpenXmlElement> elements = body.Elements<OpenXmlElement>().ToList();

        WalkElements(elements, visitor, context);
    }

    /// <summary>
    /// Walks through all headers and footers in the document and visits template elements.
    /// </summary>
    /// <param name="document">The Word document to walk.</param>
    /// <param name="visitor">The visitor that will process detected elements.</param>
    /// <param name="context">The evaluation context for variable resolution.</param>
    public void WalkHeadersAndFooters(
        WordprocessingDocument document,
        ITemplateElementVisitor visitor,
        IEvaluationContext context)
    {
        if (document?.MainDocumentPart == null)
        {
            return;
        }

        foreach (HeaderPart headerPart in document.MainDocumentPart.HeaderParts)
        {
            if (headerPart.Header != null)
            {
                List<OpenXmlElement> elements = headerPart.Header.Elements<OpenXmlElement>().ToList();
                WalkElements(elements, visitor, context);
            }
        }

        foreach (FooterPart footerPart in document.MainDocumentPart.FooterParts)
        {
            if (footerPart.Footer != null)
            {
                List<OpenXmlElement> elements = footerPart.Footer.Elements<OpenXmlElement>().ToList();
                WalkElements(elements, visitor, context);
            }
        }
    }

    /// <summary>
    /// Walks through a list of elements and visits template constructs.
    /// </summary>
    /// <param name="elements">The elements to walk.</param>
    /// <param name="visitor">The visitor to dispatch to.</param>
    /// <param name="context">The evaluation context.</param>
    /// <remarks>
    /// Detection priority (from highest to lowest):
    /// 1. Conditionals (can contain loops and placeholders)
    /// 2. Loops (can contain conditionals and placeholders)
    /// 3. Placeholders (leaf-level constructs)
    /// 4. Regular paragraphs (no template constructs)
    ///
    /// Conditionals and loops are processed from deepest to shallowest nesting
    /// to ensure inner constructs are handled before outer ones.
    ///
    /// IMPORTANT: Conditionals that are inside loop blocks must NOT be processed
    /// at the outer level. They will be processed when the loop is expanded with
    /// the correct evaluation context (LoopEvaluationContext).
    /// </remarks>
    public void WalkElements(
        List<OpenXmlElement> elements,
        ITemplateElementVisitor visitor,
        IEvaluationContext context)
    {
        // Determine if we're walking an actual document (with parents) or cloned content (without parents)
        // This affects whether we skip removed elements
        bool isDocumentWalk = elements.Any(e => e.Parent != null);

        // The cloned content of a table row loop is a list of rows. Rows need row-aware detection:
        // markers confined to a single cell are cell-level constructs, not row-level blocks.
        List<TableRow>? rows = elements.Count > 0 && elements.All(e => e is TableRow)
            ? elements.Cast<TableRow>().ToList()
            : null;

        // Pre-detect loops to know which elements are inside loop blocks
        // This is needed to filter out conditionals that are inside loops
        IReadOnlyList<LoopBlock> loops = rows != null
            ? LoopDetector.DetectTableRowLoops(rows)
            : LoopDetector.DetectLoopsInElements(elements);
        HashSet<OpenXmlElement> elementsInsideLoops = GetElementsInsideLoops(loops);

        // Step 1: Detect and visit conditionals
        // Conditionals are processed first because they can contain loops
        // BUT: conditionals inside loops must be skipped - they will be processed
        // when the loop is expanded with LoopEvaluationContext
        IReadOnlyList<ConditionalBlock> conditionals = rows != null
            ? ConditionalDetector.DetectTableRowConditionals(rows)
            : ConditionalDetector.DetectConditionalsInElements(elements);
        VisitConditionals(conditionals, elementsInsideLoops, isDocumentWalk, visitor, context);

        // Step 2: Detect and visit loops
        // Note: After conditionals are processed, some elements may have been removed
        // Loops are processed after conditionals because conditionals can affect loop content
        foreach (LoopBlock loop in loops)
        {
            // Skip if already removed by conditional processing or nested loop processing
            // Only check Parent if walking an actual document (not cloned content)
            if (isDocumentWalk && (loop.StartMarker.Parent == null || loop.EndMarker.Parent == null))
            {
                continue;
            }

            visitor.VisitLoop(loop, context);
        }

        // Step 3: Visit paragraphs for placeholder replacement
        // After blocks are processed, walk remaining paragraphs

        foreach (OpenXmlElement element in elements.ToList())
        {
            // Skip if element was removed by conditional/loop processing
            // But only check Parent if we're walking an actual document
            if (isDocumentWalk && element.Parent == null)
            {
                continue;
            }

            if (element is Paragraph paragraph)
            {
                // Skip marker paragraphs (they're already processed by block visitors)
                if (IsMarkerParagraph(paragraph))
                {
                    continue;
                }

                // Detect placeholders in the paragraph
                string text = paragraph.InnerText;
                IReadOnlyList<PlaceholderMatch> placeholders = _placeholderFinder.FindPlaceholdersAsList(text);

                if (placeholders.Count > 0)
                {
                    // Visit each placeholder in reverse order (highest index first)
                    // This prevents earlier replacements from invalidating later placeholder indices
                    foreach (PlaceholderMatch placeholder in placeholders.OrderByDescending(p => p.StartIndex))
                    {
                        visitor.VisitPlaceholder(placeholder, paragraph, context);
                    }
                }
                else
                {
                    // Regular paragraph (no template constructs)
                    visitor.VisitParagraph(paragraph, context);
                }
            }
            else if (element is Table table)
            {
                // Recursively walk table rows and cells
                WalkTable(table, visitor, context);
            }
            else if (element is TableRow row)
            {
                // Handle TableRow elements (e.g., from cloned table row loops)
                // Walk cells in the row
                foreach (TableCell cell in row.Elements<TableCell>())
                {
                    List<OpenXmlElement> cellElements = cell.Elements<OpenXmlElement>().ToList();
                    WalkElements(cellElements, visitor, context);
                }
            }
        }
    }

    /// <summary>
    /// Walks through table rows and cells.
    /// </summary>
    /// <param name="table">The table to walk.</param>
    /// <param name="visitor">The visitor to dispatch to.</param>
    /// <param name="context">The evaluation context.</param>
    private void WalkTable(
        Table table,
        ITemplateElementVisitor visitor,
        IEvaluationContext context)
    {
        // Snapshot the rows before loop expansion. Rows produced by a table row loop are
        // already walked by the LoopVisitor with the loop's evaluation context; walking them
        // again here (with this outer context) would re-process data values as template
        // syntax (template injection) and duplicate warnings. See issue #140.
        List<TableRow> originalRows = table.Elements<TableRow>().ToList();

        // Table row loops and conditionals have markers in separate rows
        // (e.g., row 1: {{#foreach Items}} / {{#if Show}}, row 3: {{/foreach}} / {{/if}}).
        // They must be detected at the table level before walking individual cells.
        // Loops are pre-detected so that conditionals inside loop rows can be skipped here:
        // they are processed when the loop expands, with the loop's evaluation context.
        IReadOnlyList<LoopBlock> tableRowLoops = LoopDetector.DetectTableRowLoops(originalRows);
        HashSet<OpenXmlElement> rowsInsideLoops = GetElementsInsideLoops(tableRowLoops);

        // Step 1: Detect and process table row conditionals (deepest first, before loops)
        IReadOnlyList<ConditionalBlock> tableRowConditionals = ConditionalDetector.DetectTableRowConditionals(originalRows);
        VisitConditionals(tableRowConditionals, rowsInsideLoops, isDocumentWalk: true, visitor, context);

        // Step 2: Process table row loops
        foreach (LoopBlock loop in tableRowLoops)
        {
            // Skip if already removed by nested loop processing
            if (loop.StartMarker.Parent == null || loop.EndMarker.Parent == null)
            {
                continue;
            }

            visitor.VisitLoop(loop, context);
        }

        // Step 3: Walk remaining rows and cells
        // After table row conditionals and loops are processed, walk the remaining original cells
        foreach (TableRow row in originalRows)
        {
            // Skip if row was removed by loop processing (loop markers and loop content rows)
            if (row.Parent == null)
            {
                continue;
            }

            foreach (TableCell cell in row.Elements<TableCell>())
            {
                // Walk paragraphs in each cell
                List<OpenXmlElement> cellElements = cell.Elements<OpenXmlElement>().ToList();
                WalkElements(cellElements, visitor, context);
            }

            // Process row-level paragraphs (malformed structure, but handle gracefully)
            // Some templates may have paragraphs as direct children of rows instead of cells
            // This can happen when SDT controls wrapping cells are unwrapped incorrectly
            List<Paragraph> rowLevelParagraphs = row.Elements<Paragraph>().ToList();
            foreach (Paragraph paragraph in rowLevelParagraphs)
            {
                // Skip marker paragraphs (they should be processed by block visitors)
                if (IsMarkerParagraph(paragraph))
                {
                    continue;
                }

                // Visit placeholder in the paragraph
                visitor.VisitParagraph(paragraph, context);
            }
        }

        // Step 4: A table whose rows were all removed (false row conditionals, empty row loops)
        // is not a valid table - Word rejects a <w:tbl> without <w:tr>. Remove it entirely.
        if (originalRows.Count > 0 && !table.Elements<TableRow>().Any())
        {
            RemoveEmptyTable(table);
        }
    }

    /// <summary>
    /// Visits conditional blocks from deepest to shallowest nesting level.
    /// </summary>
    /// <param name="conditionals">The detected conditional blocks.</param>
    /// <param name="elementsInsideLoops">Elements inside loop blocks; conditionals starting there are skipped.</param>
    /// <param name="isDocumentWalk">Whether the elements are attached to a document (enables removed-element checks).</param>
    /// <param name="visitor">The visitor to dispatch to.</param>
    /// <param name="context">The evaluation context.</param>
    private static void VisitConditionals(
        IReadOnlyList<ConditionalBlock> conditionals,
        HashSet<OpenXmlElement> elementsInsideLoops,
        bool isDocumentWalk,
        ITemplateElementVisitor visitor,
        IEvaluationContext context)
    {
        foreach (ConditionalBlock conditional in conditionals.OrderByDescending(c => c.NestingLevel))
        {
            // Skip if already removed by nested conditional processing
            // Only check Parent if walking an actual document (not cloned content)
            if (isDocumentWalk && (conditional.StartMarker.Parent == null || conditional.EndMarker.Parent == null))
            {
                continue;
            }

            // Skip conditionals that are inside loop blocks
            // They will be processed when the loop expands with the correct context
            if (elementsInsideLoops.Contains(conditional.StartMarker))
            {
                continue;
            }

            visitor.VisitConditional(conditional, context);
        }
    }

    /// <summary>
    /// Removes a table that has no rows left, keeping its container valid.
    /// </summary>
    /// <remarks>
    /// A table cell, header or footer must contain at least one block-level element
    /// (and a cell must end with a paragraph, ECMA-376 §17.4.66); an empty paragraph is
    /// appended where removing the table would violate that.
    /// </remarks>
    private static void RemoveEmptyTable(Table table)
    {
        OpenXmlElement? parent = table.Parent;
        if (parent == null)
        {
            return;
        }

        table.Remove();

        switch (parent)
        {
            case TableCell cell when cell.LastChild is not Paragraph:
                cell.AppendChild(new Paragraph());
                break;
            case Header or Footer when !parent.Elements<Paragraph>().Any() && !parent.Elements<Table>().Any():
                parent.AppendChild(new Paragraph());
                break;
        }
    }

    /// <summary>
    /// Checks if a paragraph is a template marker (conditional or loop marker).
    /// </summary>
    /// <param name="paragraph">The paragraph to check.</param>
    /// <returns>True if the paragraph contains a template marker, false otherwise.</returns>
    /// <remarks>
    /// Marker paragraphs include:
    /// - {{#if ...}}, {{#else}}, {{/if}}
    /// - {{#foreach ...}}, {{/foreach}}
    /// - {{#empty}}, {{/empty}}
    ///
    /// These paragraphs are processed by block visitors and should not be
    /// visited as regular paragraphs or placeholders.
    /// </remarks>
    private bool IsMarkerParagraph(Paragraph paragraph)
    {
        string text = paragraph.InnerText;

        // Check for conditional markers
        if (text.Contains("{{#if") || text.Contains("{{#else}}") || text.Contains("{{/if}}"))
        {
            return true;
        }

        // Check for loop markers
        if (text.Contains("{{#foreach") || text.Contains("{{/foreach}}") ||
            text.Contains("{{#empty}}") || text.Contains("{{/empty}}"))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets all elements that are inside loop content blocks.
    /// </summary>
    /// <param name="loops">The detected loop blocks.</param>
    /// <returns>A set of elements that are inside loop blocks.</returns>
    /// <remarks>
    /// This is used to filter out conditionals that are inside loops.
    /// Such conditionals should not be processed at the outer level because
    /// they need to be evaluated with the loop's evaluation context.
    /// </remarks>
    private static HashSet<OpenXmlElement> GetElementsInsideLoops(IReadOnlyList<LoopBlock> loops)
    {
        HashSet<OpenXmlElement> elementsInsideLoops = new HashSet<OpenXmlElement>();

        foreach (LoopBlock loop in loops)
        {
            // Add all content elements of the loop
            foreach (OpenXmlElement element in loop.ContentElements)
            {
                elementsInsideLoops.Add(element);
            }
        }

        return elementsInsideLoops;
    }
}
