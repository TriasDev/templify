// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace TriasDev.Templify.Converter.Converters;

/// <summary>
/// Places Templify block markers (<c>{{#if}}</c>/<c>{{/if}}</c>, <c>{{#foreach}}</c>/<c>{{/foreach}}</c>)
/// around a content control in the layout the core library understands, and unwraps the control.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Inline controls (inside a paragraph): marker runs in the same paragraph.</item>
/// <item>Block controls: marker paragraphs before and after the content.</item>
/// <item>Row controls: separate marker rows before and after the rows (table row loops/conditionals).</item>
/// <item>Cell controls (single cell): marker paragraphs at the start and end of the cell content.</item>
/// </list>
/// All checks happen before the document is modified, so a <see cref="ControlConversionException"/>
/// leaves the control untouched.
/// </remarks>
internal static class MarkerPlacement
{
    public static void Wrap(SdtElement sdt, string startMarker, string endMarker, HighlightColorValues highlight)
    {
        OpenXmlCompositeElement content = OpenXmlHelpers.GetSdtContent(sdt)
            ?? throw new ControlConversionException("The control has no content");

        if (sdt is SdtRun || sdt.Ancestors<Paragraph>().Any())
        {
            sdt.InsertBeforeSelf(OpenXmlHelpers.CreateMarkerRun(startMarker, highlight));
            sdt.InsertAfterSelf(OpenXmlHelpers.CreateMarkerRun(endMarker, highlight));
        }
        else if (sdt is SdtRow)
        {
            List<TableRow> rows = content.Elements<TableRow>().ToList();
            if (rows.Count == 0)
            {
                throw new ControlConversionException("The row control contains no table rows");
            }

            sdt.InsertBeforeSelf(OpenXmlHelpers.CreateMarkerRow(rows[0], startMarker, highlight));
            sdt.InsertAfterSelf(OpenXmlHelpers.CreateMarkerRow(rows[^1], endMarker, highlight));
        }
        else if (sdt is SdtCell)
        {
            List<TableCell> cells = content.Elements<TableCell>().ToList();
            if (cells.Count != 1)
            {
                throw new ControlConversionException(
                    $"The cell control wraps {cells.Count} cells; only single-cell controls can be converted");
            }

            TableCell cell = cells[0];
            Paragraph start = OpenXmlHelpers.CreateMarkerParagraph(startMarker, highlight);
            if (cell.TableCellProperties != null)
            {
                cell.TableCellProperties.InsertAfterSelf(start);
            }
            else
            {
                cell.PrependChild(start);
            }

            cell.AppendChild(OpenXmlHelpers.CreateMarkerParagraph(endMarker, highlight));
        }
        else
        {
            sdt.InsertBeforeSelf(OpenXmlHelpers.CreateMarkerParagraph(startMarker, highlight));
            sdt.InsertAfterSelf(OpenXmlHelpers.CreateMarkerParagraph(endMarker, highlight));
        }

        OpenXmlHelpers.UnwrapContentControl(sdt);
    }
}
