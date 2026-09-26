// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using System.Xml.Linq;

namespace TriasDev.Templify.OpenDocument;

/// <summary>
/// Extracts the text that loop and conditional marker detection sees for OpenDocument block elements,
/// consistently with the paragraph text used for replacement. The counterpart of <c>TemplateElementText</c>.
/// </summary>
internal static class OdtMarkerText
{
    /// <summary>
    /// Gets the own text of a paragraph or of a block container (table row, table cell, list item, …):
    /// the concatenated own text (see <see cref="OdtParagraphTextModel"/>) of the paragraphs it contains.
    /// Text boxes, notes and annotations anchored in those paragraphs are excluded, as they are walked
    /// as separate containers (or not at all).
    /// </summary>
    public static string GetOwnText(XElement element)
    {
        if (OdfNames.IsParagraph(element))
        {
            return OdtParagraphTextModel.GetText(element);
        }

        StringBuilder text = new StringBuilder();
        AppendOwnText(element, text);
        return text.ToString();
    }

    /// <summary>
    /// Gets the text in which loop and conditional markers are detected for an element of a block
    /// sequence: the own text of paragraphs, headings, table rows, table cells, list items and lists with a
    /// single item (in ODF a lone bullet between paragraphs is a list of its own, which in Word is just a
    /// paragraph); <see langword="null"/> for other elements (tables, longer lists, sections, …), whose markers
    /// are detected inside them.
    /// </summary>
    public static string? GetMarkerText(XElement element) =>
        OdfNames.IsParagraph(element) || IsRow(element) || IsCell(element) || element.Name == OdfNames.ListItem
        || IsSingleItemList(element)
            ? GetOwnText(element)
            : null;

    /// <summary>Checks whether an element is a list with exactly one item (or header).</summary>
    public static bool IsSingleItemList(XElement element) =>
        element.Name == OdfNames.List
        && element.Elements().Count(e => e.Name == OdfNames.ListItem || e.Name == OdfNames.ListHeader) == 1;

    /// <summary>Checks whether an element is a table row.</summary>
    public static bool IsRow(XElement element) => element.Name == OdfNames.TableRow;

    /// <summary>Checks whether an element is a table cell or a covered table cell.</summary>
    public static bool IsCell(XElement element) =>
        element.Name == OdfNames.TableCell || element.Name == OdfNames.CoveredTableCell;

    /// <summary>Gets the cells (including covered cells) of a row in document order.</summary>
    public static IEnumerable<XElement> GetRowCells(XElement row) => row.Elements().Where(IsCell);

    private static void AppendOwnText(XElement container, StringBuilder text)
    {
        foreach (XElement child in container.Elements())
        {
            if (OdfNames.IsParagraph(child))
            {
                text.Append(OdtParagraphTextModel.GetText(child));
            }
            else if (child.Name != OdfNames.OfficeAnnotation && child.Name.Namespace != OdfNames.Draw)
            {
                AppendOwnText(child, text);
            }
        }
    }
}
