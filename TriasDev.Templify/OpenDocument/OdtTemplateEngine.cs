// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Xml.Linq;
using TriasDev.Templify.Core;
using TriasDev.Templify.Loops;

namespace TriasDev.Templify.OpenDocument;

/// <summary>
/// Walks the block containers of an OpenDocument Text document and processes the template
/// constructs in them, in the order of the Word pipeline: conditionals (deepest first), then
/// placeholders.
/// </summary>
/// <remarks>
/// <para>
/// Walked are the body (<c>office:body/office:text</c> in <c>content.xml</c>) and the headers and
/// footers of the master pages (<c>styles.xml</c>), including their left, center and right regions.
/// Within them: paragraphs and headings, tables (including header rows and row groups, cells and
/// covered cells), lists, sections, numbered paragraphs, index bodies, page-anchored frames, and the
/// text boxes, shapes and notes anchored in paragraphs. Annotations (comments) and tracked deletions are not walked.
/// </para>
/// </remarks>
internal sealed class OdtTemplateEngine
{
    private static readonly XName _xmlId = XNamespace.Xml + "id";
    private static readonly XName _continueList = OdfNames.Text + "continue-list";
    private static readonly XName _continueNumbering = OdfNames.Text + "continue-numbering";

    private readonly OdtPlaceholderProcessor _placeholders;
    private readonly OdtConditionalProcessor _conditionals;
    private readonly IWarningCollector _warningCollector;

    /// <summary>Whether a loop cloned a list (see <see cref="ContinueListNumbering"/>).</summary>
    private bool _listsCloned;

    public OdtTemplateEngine(
        PlaceholderReplacementOptions options,
        HashSet<string> missingVariables,
        IWarningCollector warningCollector)
    {
        _placeholders = new OdtPlaceholderProcessor(options, missingVariables, warningCollector);
        _conditionals = new OdtConditionalProcessor(warningCollector);
        _warningCollector = warningCollector;
    }

    /// <summary>Gets the number of placeholders replaced.</summary>
    public int ReplacementCount => _placeholders.ReplacementCount;

    /// <summary>
    /// Processes the body and the headers and footers of a package.
    /// </summary>
    /// <exception cref="InvalidOdtPackageException">The content part has no text body.</exception>
    public void Process(OdtPackage package, IEvaluationContext context)
    {
        XDocument content = package.GetXml(OdtPackage.ContentEntry)!;
        XElement? body = content.Root?.Element(OdfNames.OfficeBody)?.Element(OdfNames.OfficeText);
        if (body == null)
        {
            throw new InvalidOdtPackageException("Invalid document: content.xml has no text body (office:text).");
        }

        XDocument? styles = package.GetXml(OdtPackage.StylesEntry);

        // content.xml can reference the common styles of styles.xml by name; markdown styles must not shadow them.
        _placeholders.Styles.ReserveNames(
            styles?.Root?.Elements(OdfNames.Office + "styles").Descendants()
                .Select(e => (string?)e.Attribute(OdfNames.Style + "name")).OfType<string>()
            ?? Enumerable.Empty<string>());

        ProcessBlocks(body.Elements().ToList(), context);

        List<XElement> headersAndFooters = styles?.Root != null
            ? GetHeadersAndFooters(styles.Root).ToList()
            : new List<XElement>();
        foreach (XElement headerOrFooter in headersAndFooters)
        {
            ProcessContainer(headerOrFooter, context);
        }

        // Loop cloning copies frame, table and section names and note ids; make them unique again.
        List<XElement> roots = new List<XElement> { body };
        roots.AddRange(headersAndFooters);
        OdtUniqueNames.EnsureUnique(roots);

        ContinueListNumbering(new[] { content, styles });
    }

    /// <summary>
    /// Gets the header and footer containers of all master pages (<c>style:header</c>, <c>style:footer</c>,
    /// their <c>-left</c> and <c>-first</c> variants, also in the LibreOffice extension namespace).
    /// </summary>
    internal static IEnumerable<XElement> GetHeadersAndFooters(XElement stylesRoot) =>
        stylesRoot.Elements(OdfNames.OfficeMasterStyles)
            .Elements(OdfNames.MasterPage)
            .Elements()
            .Where(e => (e.Name.Namespace == OdfNames.Style || e.Name.Namespace == OdfNames.LibreOfficeExtension)
                        && (e.Name.LocalName.StartsWith("header", StringComparison.Ordinal)
                            || e.Name.LocalName.StartsWith("footer", StringComparison.Ordinal)))
            .ToList();

    /// <summary>
    /// Processes the blocks of a container (cell, list item, section, text box, note, header, …). A container
    /// whose content was removed entirely gets an empty paragraph, so it stays editable in LibreOffice.
    /// </summary>
    internal void ProcessContainer(XElement container, IEvaluationContext context)
    {
        bool hadContent = HasBlockContent(container);

        ProcessBlocks(container.Elements().ToList(), context);

        EnsureContent(container, hadContent);
    }

    /// <summary>
    /// Appends an empty paragraph to a container that had content before processing and has none now.
    /// </summary>
    private static void EnsureContent(XElement container, bool hadContent)
    {
        if (hadContent && container.Parent != null && !HasBlockContent(container) && container.Name != OdfNames.CoveredTableCell)
        {
            container.Add(new XElement(OdfNames.Paragraph));
        }
    }

    private static bool HasBlockContent(XElement container) =>
        container.Elements().Any(e => e.Name != OdfNames.SoftPageBreak && e.Name != OdfNames.Number);

    /// <summary>
    /// Processes a sequence of sibling blocks: conditionals (deepest first), then the remaining blocks
    /// (placeholders, tables, nested containers).
    /// </summary>
    /// <remarks>
    /// A sequence consisting only of table rows uses row-aware detection: markers confined to a single
    /// cell are cell-level constructs, not row-level blocks.
    /// </remarks>
    internal void ProcessBlocks(IReadOnlyList<XElement> blocks, IEvaluationContext context)
    {
        if (blocks.Count > 0 && blocks.All(OdtMarkerText.IsRow))
        {
            ProcessRows(blocks, context);
            return;
        }

        if (blocks.Count > 0 && blocks.All(IsListItem))
        {
            ProcessListItems(blocks, context);
            return;
        }

        // Loops are detected first, so conditionals inside loop content are left for the iterations
        // (they are evaluated with the loop's context).
        IReadOnlyList<OdtLoopBlock> loops = OdtLoopDetector.DetectLoops(blocks);
        IReadOnlyList<OdtConditionalBlock> conditionals = OdtConditionalDetector.DetectConditionals(blocks);
        ProcessConditionals(conditionals, GetElementsInsideLoops(loops), context);
        ProcessLoops(loops, context);

        foreach (XElement block in blocks)
        {
            if (block.Parent == null)
            {
                continue;
            }

            ProcessBlock(block, context);
        }
    }

    /// <summary>
    /// Processes conditional blocks from the deepest nesting level up, skipping blocks already removed by
    /// an enclosing block and blocks inside loops (those are processed with the loop's context).
    /// </summary>
    private void ProcessConditionals(
        IReadOnlyList<OdtConditionalBlock> conditionals,
        HashSet<XElement> elementsInsideLoops,
        IEvaluationContext context)
    {
        foreach (OdtConditionalBlock conditional in conditionals.OrderByDescending(c => c.NestingLevel))
        {
            if (conditional.StartMarker.Parent == null || conditional.EndMarker.Parent == null)
            {
                continue;
            }

            if (elementsInsideLoops.Contains(conditional.StartMarker))
            {
                continue;
            }

            _conditionals.Process(conditional, context);
        }
    }

    private static HashSet<XElement> GetElementsInsideLoops(IReadOnlyList<OdtLoopBlock> loops) =>
        new HashSet<XElement>(loops.SelectMany(l => l.ContentElements));

    /// <summary>
    /// Expands loops that are still in the document (a conditional may have removed them).
    /// </summary>
    private void ProcessLoops(IReadOnlyList<OdtLoopBlock> loops, IEvaluationContext context)
    {
        foreach (OdtLoopBlock loop in loops)
        {
            if (loop.StartMarker.Parent == null || loop.EndMarker.Parent == null)
            {
                continue;
            }

            ExpandLoop(loop, context);
        }
    }

    /// <summary>
    /// Expands a loop: the content is cloned once per item, inserted after the end marker and processed with
    /// the item's loop context; then the original markers and content are removed. The counterpart of
    /// <c>LoopVisitor.VisitLoop</c>, with the same warnings and errors.
    /// </summary>
    private void ExpandLoop(OdtLoopBlock loop, IEvaluationContext context)
    {
        if (!context.TryResolveVariable(loop.CollectionName, out object? collectionObject))
        {
            _warningCollector.AddWarning(ProcessingWarning.MissingLoopCollection(loop.CollectionName));
            RemoveLoop(loop);
            return;
        }

        if (collectionObject == null)
        {
            _warningCollector.AddWarning(ProcessingWarning.NullLoopCollection(loop.CollectionName));
            RemoveLoop(loop);
            return;
        }

        if (collectionObject is string || collectionObject is not System.Collections.IEnumerable collection)
        {
            throw new TemplateDataException($"Variable '{loop.CollectionName}' is not a collection. Cannot iterate.");
        }

        IReadOnlyList<LoopContext> iterations = LoopContext.CreateContexts(
            collection,
            loop.CollectionName,
            loop.IterationVariableName,
            parent: null);

        // Each clone of a list is marked with the template list it comes from, also through nested loops.
        // Lists inside a list (list-item loops, loops in a list item) are sub-lists numbered by their parent list.
        List<ListSource> listSources = loop.EndMarker.Ancestors(OdfNames.List).Any()
            ? new List<ListSource>()
            : loop.ContentElements.SelectMany(FindOutermostLists).Select(ListSource.Of).ToList();
        _listsCloned |= listSources.Count > 0;

        // Last item first: each iteration is inserted directly after the end marker, so the
        // iterations end up in document order.
        for (int i = iterations.Count - 1; i >= 0; i--)
        {
            LoopEvaluationContext iterationContext = new LoopEvaluationContext(iterations[i], context);
            List<XElement> clones = loop.ContentElements.Select(e => new XElement(e)).ToList();
            if (listSources.Count > 0)
            {
                List<XElement> clonedLists = clones.SelectMany(FindOutermostLists).ToList();
                for (int l = 0; l < clonedLists.Count; l++)
                {
                    clonedLists[l].AddAnnotation(listSources[l]);
                }
            }

            XElement insertAfter = loop.EndMarker;
            foreach (XElement clone in clones)
            {
                insertAfter.AddAfterSelf(clone);
                insertAfter = clone;
            }

            ProcessBlocks(clones, iterationContext);
        }

        RemoveLoop(loop);
    }

    /// <summary>
    /// Finds the lists in an element that are not nested in another list (nor in an annotation).
    /// </summary>
    private static IEnumerable<XElement> FindOutermostLists(XElement element)
    {
        if (element.Name == OdfNames.List)
        {
            yield return element;
            yield break;
        }

        foreach (XElement child in element.Elements())
        {
            if (child.Name == OdfNames.OfficeAnnotation)
            {
                continue;
            }

            foreach (XElement list in FindOutermostLists(child))
            {
                yield return list;
            }
        }
    }

    /// <summary>
    /// Makes the loop copies of a list continue the numbering of the first copy, as in the Word pipeline (cloned
    /// numbered paragraphs keep their numbering instance, so a loop over a numbered item produces 1., 2., 3.).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each copy is a separate <c>text:list</c>, which LibreOffice numbers from 1 again. For the copies of one
    /// template list (also across nested loops, where Word numbering continues as well), the first copy keeps or
    /// gets an <c>xml:id</c>, and every later copy gets <c>text:continue-list</c> referring to it. This links the
    /// copies even when other lists come between them. Other lists are not changed, so lists meant to restart
    /// still do.
    /// </para>
    /// <para>
    /// Runs after <see cref="OdtUniqueNames.EnsureUnique"/>, which removes the duplicate <c>xml:id</c> values
    /// copied by the cloning, so the ids referred to here are unique.
    /// </para>
    /// </remarks>
    private void ContinueListNumbering(IReadOnlyList<XDocument?> documents)
    {
        if (!_listsCloned)
        {
            return;
        }

        List<XElement> elements = documents.OfType<XDocument>().SelectMany(d => d.Descendants()).ToList();
        HashSet<string> usedIds = new HashSet<string>(
            elements.Select(e => (string?)e.Attribute(_xmlId)).OfType<string>(),
            StringComparer.Ordinal);
        int nextId = 1;

        // Copies removed again (by a conditional, as an emptied list, or as an outer loop's source content)
        // are no longer in a document and not found here.
        IEnumerable<IGrouping<ListSource, XElement>> copiesBySource = elements
            .Where(e => e.Name == OdfNames.List && e.Annotation<ListSource>() != null)
            .GroupBy(e => e.Annotation<ListSource>()!);

        foreach (List<XElement> copies in copiesBySource.Select(g => g.ToList()).Where(g => g.Count > 1))
        {
            XElement first = copies[0];
            string? id = (string?)first.Attribute(_xmlId);
            if (id == null)
            {
                do
                {
                    id = "templify-list" + nextId.ToString(CultureInfo.InvariantCulture);
                    nextId++;
                }
                while (!usedIds.Add(id));

                first.SetAttributeValue(_xmlId, id);
            }

            foreach (XElement copy in copies.Skip(1))
            {
                copy.Attribute(_xmlId)?.Remove();
                copy.Attribute(_continueNumbering)?.Remove();
                copy.SetAttributeValue(_continueList, id);
            }
        }

        _listsCloned = false;
    }

    /// <summary>
    /// Identifies the template list that loop copies of a list come from (an annotation on the list elements;
    /// annotations are not copied with an element, so every copy is marked explicitly).
    /// </summary>
    private sealed class ListSource
    {
        /// <summary>Gets the source of a list: the one it was copied from, or the list itself.</summary>
        public static ListSource Of(XElement list)
        {
            ListSource? source = list.Annotation<ListSource>();
            if (source == null)
            {
                source = new ListSource();
                list.AddAnnotation(source);
            }

            return source;
        }
    }

    private static void RemoveLoop(OdtLoopBlock loop)
    {
        SafeRemove(loop.StartMarker);
        foreach (XElement element in loop.ContentElements)
        {
            SafeRemove(element);
        }

        SafeRemove(loop.EndMarker);
    }

    private static void SafeRemove(XElement element)
    {
        if (element.Parent != null)
        {
            element.Remove();
        }
    }

    private static bool IsListItem(XElement element) =>
        element.Name == OdfNames.ListItem || element.Name == OdfNames.ListHeader;

    private void ProcessBlock(XElement block, IEvaluationContext context)
    {
        if (OdfNames.IsParagraph(block))
        {
            ProcessParagraph(block, context);
        }
        else if (block.Name == OdfNames.TableElement)
        {
            ProcessTable(block, context);
        }
        else if (block.Name == OdfNames.List)
        {
            ProcessList(block, context);
        }
        else if (block.Name == OdfNames.Section
                 || block.Name == OdfNames.NumberedParagraph
                 || block.Name == OdfNames.IndexBody
                 || block.Name == OdfNames.IndexTitle
                 || OdfNames.IsHeaderFooterRegion(block))
        {
            ProcessContainer(block, context);
        }
        else if (OdfNames.Indexes.Contains(block.Name))
        {
            // The index source (templates, settings) is configuration; only the cached body is content.
            foreach (XElement indexBody in block.Elements(OdfNames.IndexBody).ToList())
            {
                ProcessContainer(indexBody, context);
            }
        }
        else if (block.Name.Namespace == OdfNames.Draw)
        {
            // Frames and shapes anchored to the page sit directly in the body.
            if (IsNestedBlockContainer(block))
            {
                ProcessContainer(block, context);
            }
            else
            {
                ProcessNestedContainers(block, context);
            }
        }
    }

    /// <summary>
    /// Processes a list: list-item conditionals (markers in their own items, like table rows), then the
    /// content of the remaining items. An item whose content was removed entirely is removed (so no empty
    /// bullet is left behind), and a list left without items is removed.
    /// </summary>
    private void ProcessList(XElement list, IEvaluationContext context)
    {
        List<XElement> items = GetListItems(list).ToList();

        ProcessListItems(items, context);

        if (items.Count > 0 && !GetListItems(list).Any())
        {
            list.Remove();
        }
    }

    /// <summary>
    /// Processes sibling list items (the items of a list, or the cloned items of a list-item loop):
    /// item-level conditionals and loops, then the content of the original items still present.
    /// </summary>
    private void ProcessListItems(IReadOnlyList<XElement> items, IEvaluationContext context)
    {
        IReadOnlyList<OdtLoopBlock> loops = OdtLoopDetector.DetectListItemLoops(items);
        IReadOnlyList<OdtConditionalBlock> conditionals = OdtConditionalDetector.DetectListItemConditionals(items);
        ProcessConditionals(conditionals, GetElementsInsideLoops(loops), context);
        ProcessLoops(loops, context);

        foreach (XElement item in items)
        {
            if (item.Parent == null)
            {
                continue;
            }

            bool hadContent = HasBlockContent(item);
            ProcessBlocks(item.Elements().ToList(), context);
            if (hadContent && !HasBlockContent(item))
            {
                item.Remove();
            }
        }
    }

    private static IEnumerable<XElement> GetListItems(XElement list) =>
        list.Elements().Where(e => e.Name == OdfNames.ListItem || e.Name == OdfNames.ListHeader);

    private void ProcessParagraph(XElement paragraph, IEvaluationContext context)
    {
        // Text boxes, shapes and notes anchored in the paragraph hold their own blocks.
        ProcessNestedContainers(paragraph, context);

        // Marker paragraphs are processed by the block constructs; leftovers are not placeholder text.
        if (IsMarkerParagraph(paragraph))
        {
            return;
        }

        _placeholders.ProcessParagraph(paragraph, context);
    }

    /// <summary>
    /// Checks whether a paragraph contains a conditional or loop marker (as the Word pipeline does).
    /// </summary>
    private static bool IsMarkerParagraph(XElement paragraph)
    {
        string text = OdtParagraphTextModel.GetText(paragraph);
        return text.Contains("{{#if", StringComparison.Ordinal)
            || text.Contains("{{#else}}", StringComparison.Ordinal)
            || text.Contains("{{/if}}", StringComparison.Ordinal)
            || text.Contains("{{#foreach", StringComparison.Ordinal)
            || text.Contains("{{/foreach}}", StringComparison.Ordinal);
    }

    /// <summary>
    /// Processes the block containers nested in an element that are not inside another nested container
    /// or paragraph: text boxes and shapes with paragraphs, and note bodies. Annotations are skipped.
    /// </summary>
    private void ProcessNestedContainers(XElement element, IEvaluationContext context)
    {
        foreach (XElement container in FindNestedContainers(element).ToList())
        {
            ProcessContainer(container, context);
        }
    }

    /// <summary>
    /// Finds the block containers nested in <paramref name="element"/> (see <see cref="ProcessNestedContainers"/>).
    /// </summary>
    internal static IEnumerable<XElement> FindNestedContainers(XElement element)
    {
        foreach (XElement child in element.Elements())
        {
            if (child.Name == OdfNames.OfficeAnnotation || OdfNames.IsParagraph(child))
            {
                continue;
            }

            if (IsNestedBlockContainer(child))
            {
                yield return child;
                continue;
            }

            foreach (XElement nested in FindNestedContainers(child))
            {
                yield return nested;
            }
        }
    }

    private static bool IsNestedBlockContainer(XElement element) =>
        element.Name == OdfNames.DrawTextBox
        || element.Name == OdfNames.NoteBody
        || (element.Name.Namespace == OdfNames.Draw
            && element.Elements().Any(c => OdfNames.IsParagraph(c) || c.Name == OdfNames.List));

    /// <summary>
    /// Processes a table: row-level constructs per row container, then the cells. A table whose rows
    /// were all removed is removed, since ODF requires at least one row.
    /// </summary>
    private void ProcessTable(XElement table, IEvaluationContext context)
    {
        bool hadRows = GetRows(table).Any();

        ProcessRowContainer(table, context);

        if (hadRows && !GetRows(table).Any())
        {
            table.Remove();
        }
    }

    /// <summary>
    /// Processes the rows of a row container (a table, header rows, a row group). Nested row containers
    /// are processed on their own; one left without rows is removed.
    /// </summary>
    private void ProcessRowContainer(XElement container, IEvaluationContext context)
    {
        ProcessRows(container.Elements(OdfNames.TableRow).ToList(), context);

        foreach (XElement group in container.Elements().Where(IsRowGroup).ToList())
        {
            bool hadRows = GetRows(group).Any();
            ProcessRowContainer(group, context);
            if (hadRows && !GetRows(group).Any())
            {
                group.Remove();
            }
        }
    }

    private static bool IsRowGroup(XElement element) =>
        element.Name == OdfNames.TableHeaderRows
        || element.Name == OdfNames.TableRows
        || element.Name == OdfNames.TableRowGroup;

    /// <summary>
    /// Processes sibling rows: table-row conditionals (markers in their own rows), then the cells of the
    /// remaining rows.
    /// </summary>
    private void ProcessRows(IReadOnlyList<XElement> rows, IEvaluationContext context)
    {
        // Rows produced by a row loop are processed by the loop with its context and are not in
        // this list, so they are never processed again with the outer context (#140).
        IReadOnlyList<OdtLoopBlock> loops = OdtLoopDetector.DetectTableRowLoops(rows);
        IReadOnlyList<OdtConditionalBlock> conditionals = OdtConditionalDetector.DetectTableRowConditionals(rows);
        ProcessConditionals(conditionals, GetElementsInsideLoops(loops), context);
        ProcessLoops(loops, context);

        foreach (XElement row in rows)
        {
            if (row.Parent == null)
            {
                continue;
            }

            foreach (XElement cell in OdtMarkerText.GetRowCells(row).ToList())
            {
                ProcessContainer(cell, context);
            }
        }
    }

    /// <summary>
    /// Gets the rows of a table in document order, including rows in header-row and row-group
    /// containers; rows of nested tables are not included.
    /// </summary>
    internal static IEnumerable<XElement> GetRows(XElement rowContainer)
    {
        foreach (XElement child in rowContainer.Elements())
        {
            if (child.Name == OdfNames.TableRow)
            {
                yield return child;
            }
            else if (IsRowGroup(child))
            {
                foreach (XElement row in GetRows(child))
                {
                    yield return row;
                }
            }
        }
    }
}
