// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace TriasDev.Templify.Utilities;

/// <summary>
/// Extracts the text that template marker detection (loops, conditionals) sees for block-level
/// elements, consistently with the paragraph text used for replacement.
/// </summary>
internal static class TemplateElementText
{
    /// <summary>
    /// Gets the own text of a paragraph or of a block container (table row, table cell,
    /// content control): the concatenated own text (see <see cref="ParagraphTextModel"/>)
    /// of the paragraphs it contains.
    /// </summary>
    /// <remarks>
    /// Text box content (<c>w:txbxContent</c>) is excluded: text boxes are walked as separate
    /// block containers, so markers inside a text box must not affect the detection of the
    /// row, cell or paragraph the text box is anchored in. Field instructions and deleted
    /// text are excluded as in <see cref="ParagraphTextModel"/>.
    /// </remarks>
    public static string GetOwnText(OpenXmlElement element)
    {
        if (element is Paragraph paragraph)
        {
            return ParagraphTextModel.GetText(paragraph);
        }

        StringBuilder text = new StringBuilder();
        AppendOwnText(element, text);
        return text.ToString();
    }

    /// <summary>
    /// Gets the cells of a row in document order, including cells wrapped in cell-level
    /// content controls (<c>w:sdt</c> around <c>w:tc</c>); cells of nested tables belong
    /// to their own rows and are not included.
    /// </summary>
    public static IEnumerable<TableCell> GetRowCells(TableRow row) =>
        row.Descendants<TableCell>().Where(c => c.Ancestors<TableRow>().FirstOrDefault() == row);

    private static void AppendOwnText(OpenXmlElement container, StringBuilder text)
    {
        foreach (OpenXmlElement child in container.ChildElements)
        {
            switch (child)
            {
                case Paragraph paragraph:
                    // Paragraphs never contain paragraphs except inside text boxes,
                    // which ParagraphTextModel already excludes.
                    text.Append(ParagraphTextModel.GetText(paragraph));
                    break;
                case TextBoxContent:
                    break;
                default:
                    AppendOwnText(child, text);
                    break;
            }
        }
    }
}
