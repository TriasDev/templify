// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.OpenDocument;

/// <summary>
/// Walks the block containers of an OpenDocument Text document and processes the template
/// constructs in them.
/// </summary>
/// <remarks>
/// <para>
/// Walked are the body (<c>office:body/office:text</c> in <c>content.xml</c>) and the headers and
/// footers of the master pages (<c>styles.xml</c>). Within them: paragraphs and headings, tables
/// (including header rows and row groups, cells and covered cells), lists, sections, numbered
/// paragraphs, index bodies, page-anchored frames, and the text boxes, shapes and notes anchored in
/// paragraphs. Annotations (comments) and tracked deletions are not walked.
/// </para>
/// </remarks>
internal sealed class OdtTemplateEngine
{
    private readonly OdtPlaceholderProcessor _placeholders;

    public OdtTemplateEngine(
        PlaceholderReplacementOptions options,
        HashSet<string> missingVariables,
        IWarningCollector warningCollector)
    {
        _placeholders = new OdtPlaceholderProcessor(options, missingVariables, warningCollector);
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

        ProcessContainer(body, context);

        XDocument? styles = package.GetXml(OdtPackage.StylesEntry);
        if (styles?.Root != null)
        {
            foreach (XElement headerOrFooter in GetHeadersAndFooters(styles.Root))
            {
                ProcessContainer(headerOrFooter, context);
            }
        }
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
    /// Processes the blocks of a container (body, cell, list item, section, text box, note, header, …).
    /// </summary>
    internal void ProcessContainer(XElement container, IEvaluationContext context)
    {
        ProcessBlocks(container.Elements().ToList(), context);
    }

    /// <summary>
    /// Processes a sequence of sibling blocks.
    /// </summary>
    internal void ProcessBlocks(IReadOnlyList<XElement> blocks, IEvaluationContext context)
    {
        foreach (XElement block in blocks)
        {
            if (block.Parent == null)
            {
                continue;
            }

            ProcessBlock(block, context);
        }
    }

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
            foreach (XElement item in block.Elements()
                .Where(e => e.Name == OdfNames.ListItem || e.Name == OdfNames.ListHeader)
                .ToList())
            {
                ProcessContainer(item, context);
            }
        }
        else if (block.Name == OdfNames.Section
                 || block.Name == OdfNames.NumberedParagraph
                 || block.Name == OdfNames.IndexBody
                 || block.Name == OdfNames.IndexTitle)
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

    private void ProcessParagraph(XElement paragraph, IEvaluationContext context)
    {
        // Text boxes, shapes and notes anchored in the paragraph hold their own blocks.
        ProcessNestedContainers(paragraph, context);

        _placeholders.ProcessParagraph(paragraph, context);
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

    private void ProcessTable(XElement table, IEvaluationContext context)
    {
        foreach (XElement row in GetRows(table).ToList())
        {
            foreach (XElement cell in row.Elements()
                .Where(e => e.Name == OdfNames.TableCell || e.Name == OdfNames.CoveredTableCell)
                .ToList())
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
            else if (child.Name == OdfNames.TableHeaderRows
                     || child.Name == OdfNames.TableRows
                     || child.Name == OdfNames.TableRowGroup)
            {
                foreach (XElement row in GetRows(child))
                {
                    yield return row;
                }
            }
        }
    }
}
