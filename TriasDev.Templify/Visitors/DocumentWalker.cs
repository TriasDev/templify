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
/// - ConditionalDetector for {{#if}}/{{else}}/{{/if}}
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
    /// </remarks>
    public void WalkElements(
        List<OpenXmlElement> elements,
        ITemplateElementVisitor visitor,
        IEvaluationContext context)
    {
        // Determine if we're walking an actual document (with parents) or cloned content (without parents)
        // This affects whether we skip removed elements
        bool isDocumentWalk = elements.Any(e => e.Parent != null);

        // Step 1: Detect and visit conditionals
        // Conditionals are processed first because they can contain loops
        // Process from deepest to shallowest (same as ConditionalProcessor)
        IReadOnlyList<ConditionalBlock> conditionals = ConditionalDetector.DetectConditionalsInElements(elements);
        foreach (ConditionalBlock conditional in conditionals.OrderByDescending(c => c.NestingLevel))
        {
            // Skip if already removed by nested conditional processing
            // Only check Parent if walking an actual document (not cloned content)
            if (isDocumentWalk && (conditional.StartMarker.Parent == null || conditional.EndMarker.Parent == null))
            {
                continue;
            }

            visitor.VisitConditional(conditional, context);
        }

        // Step 2: Detect and visit loops
        // Note: After conditionals are processed, some elements may have been removed
        // Loops are processed after conditionals because conditionals can affect loop content
        IReadOnlyList<LoopBlock> loops = LoopDetector.DetectLoopsInElements(elements);
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
        // Step 1: Detect and process table row loops
        // Table row loops have markers in separate rows (e.g., row 1: {{#foreach Items}}, row 3: {{/foreach}})
        // These must be detected at the table level before walking individual cells
        IReadOnlyList<LoopBlock> tableRowLoops = LoopDetector.DetectTableRowLoops(table);
        foreach (LoopBlock loop in tableRowLoops)
        {
            // Skip if already removed by nested loop processing
            if (loop.StartMarker.Parent == null || loop.EndMarker.Parent == null)
            {
                continue;
            }

            visitor.VisitLoop(loop, context);
        }

        // Step 2: Walk remaining rows and cells
        // After table row loops are processed, walk the remaining cells
        foreach (TableRow row in table.Elements<TableRow>().ToList())
        {
            // Skip if row was removed by loop processing
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

            // Step 3: Process row-level paragraphs (malformed structure, but handle gracefully)
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
    }

    /// <summary>
    /// Checks if a paragraph is a template marker (conditional or loop marker).
    /// </summary>
    /// <param name="paragraph">The paragraph to check.</param>
    /// <returns>True if the paragraph contains a template marker, false otherwise.</returns>
    /// <remarks>
    /// Marker paragraphs include:
    /// - {{#if ...}}, {{else}}, {{/if}}
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
        if (text.Contains("{{#if") || text.Contains("{{else}}") || text.Contains("{{/if}}"))
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
}
