// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace TriasDev.Templify.Converter.Converters;

/// <summary>
/// Helper methods for working with OpenXML content controls (SDTs).
/// </summary>
public static class OpenXmlHelpers
{
    /// <summary>
    /// Get the tag value from a content control.
    /// </summary>
    public static string? GetContentControlTag(SdtElement sdt)
    {
        return sdt.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;
    }

    /// <summary>
    /// Get the content element of any kind of content control
    /// (<see cref="SdtBlock"/>, <see cref="SdtRun"/>, <see cref="SdtCell"/>, <see cref="SdtRow"/>).
    /// </summary>
    public static OpenXmlCompositeElement? GetSdtContent(SdtElement sdt)
    {
        return sdt switch
        {
            SdtBlock block => block.SdtContentBlock,
            SdtRun run => run.SdtContentRun,
            SdtCell cell => cell.SdtContentCell,
            SdtRow row => row.SdtContentRow,
            _ => sdt.ChildElements.OfType<OpenXmlCompositeElement>()
                .FirstOrDefault(e => e.LocalName == "sdtContent"),
        };
    }

    /// <summary>
    /// Returns the root elements of every part that can contain content controls:
    /// the main document, headers, footers, footnotes and endnotes.
    /// </summary>
    public static IEnumerable<OpenXmlPartRootElement> GetContentRoots(WordprocessingDocument document)
    {
        MainDocumentPart? main = document.MainDocumentPart;
        if (main == null)
        {
            yield break;
        }

        if (main.Document != null)
        {
            yield return main.Document;
        }

        foreach (HeaderPart header in main.HeaderParts)
        {
            if (header.Header != null)
            {
                yield return header.Header;
            }
        }

        foreach (FooterPart footer in main.FooterParts)
        {
            if (footer.Footer != null)
            {
                yield return footer.Footer;
            }
        }

        if (main.FootnotesPart?.Footnotes != null)
        {
            yield return main.FootnotesPart.Footnotes;
        }

        if (main.EndnotesPart?.Endnotes != null)
        {
            yield return main.EndnotesPart.Endnotes;
        }
    }

    /// <summary>
    /// Returns a short, human-readable name of the part that contains the element.
    /// </summary>
    public static string GetPartName(OpenXmlElement element)
    {
        OpenXmlElement? root = element.Ancestors().LastOrDefault() ?? element;
        return root switch
        {
            Document => "Body",
            Header => "Header",
            Footer => "Footer",
            Footnotes => "Footnotes",
            Endnotes => "Endnotes",
            _ => root.LocalName,
        };
    }

    /// <summary>
    /// Unwrap a content control: its children are moved (not cloned) to replace the control itself.
    /// All children are preserved (runs, hyperlinks, fields, bookmarks, ...). When a block-level
    /// control ended up inside a paragraph, the paragraph's inline content is moved instead to avoid
    /// nested paragraphs.
    /// </summary>
    /// <param name="sdt">The content control to unwrap.</param>
    /// <returns>The last element that was moved, or null if no elements were moved.</returns>
    public static OpenXmlElement? UnwrapContentControl(SdtElement sdt)
    {
        if (sdt.Parent == null)
        {
            return null;
        }

        OpenXmlCompositeElement? sdtContent = GetSdtContent(sdt);
        if (sdtContent == null)
        {
            sdt.Remove();
            return null;
        }

        bool insideParagraph = sdt.Ancestors<Paragraph>().Any();
        OpenXmlElement? lastMovedElement = null;

        foreach (OpenXmlElement child in sdtContent.ChildElements.ToList())
        {
            if (insideParagraph && child is Paragraph childParagraph)
            {
                foreach (OpenXmlElement inline in childParagraph.ChildElements.ToList())
                {
                    if (inline is ParagraphProperties)
                    {
                        continue;
                    }

                    inline.Remove();
                    sdt.InsertBeforeSelf(inline);
                    lastMovedElement = inline;
                }

                continue;
            }

            child.Remove();
            sdt.InsertBeforeSelf(child);
            lastMovedElement = child;
        }

        sdt.Remove();
        return lastMovedElement;
    }

    /// <summary>
    /// Create a run containing the given text, optionally highlighted.
    /// </summary>
    public static Run CreateMarkerRun(string text, HighlightColorValues? highlightColor)
    {
        Run run = new Run();
        if (highlightColor.HasValue && highlightColor.Value != HighlightColorValues.None)
        {
            run.RunProperties = new RunProperties(new Highlight { Val = highlightColor.Value });
        }

        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    /// <summary>
    /// Create a paragraph that contains only a Templify marker (e.g. <c>{{#if x}}</c>).
    /// </summary>
    public static Paragraph CreateMarkerParagraph(string text, HighlightColorValues? highlightColor)
    {
        return new Paragraph(CreateMarkerRun(text, highlightColor));
    }

    /// <summary>
    /// Create a table row that contains only a Templify marker, mirroring the cell layout
    /// (cell properties such as widths and spans) of <paramref name="templateRow"/>.
    /// The marker text is placed in the first cell; other cells get an empty paragraph.
    /// </summary>
    public static TableRow CreateMarkerRow(TableRow? templateRow, string text, HighlightColorValues? highlightColor)
    {
        TableRow markerRow = new TableRow();
        List<TableCell> templateCells = templateRow?.Elements<TableCell>().ToList() ?? new List<TableCell>();

        if (templateCells.Count == 0)
        {
            markerRow.AppendChild(new TableCell(CreateMarkerParagraph(text, highlightColor)));
            return markerRow;
        }

        for (int i = 0; i < templateCells.Count; i++)
        {
            TableCell cell = new TableCell();
            if (templateCells[i].TableCellProperties is TableCellProperties properties)
            {
                cell.AppendChild(properties.CloneNode(true));
            }

            cell.AppendChild(i == 0 ? CreateMarkerParagraph(text, highlightColor) : new Paragraph());
            markerRow.AppendChild(cell);
        }

        return markerRow;
    }

    /// <summary>
    /// Replace the content of a content control with new text, preserving the formatting of the
    /// first run. Word's placeholder-text style is dropped.
    /// </summary>
    /// <param name="sdt">The content control to replace text in.</param>
    /// <param name="newText">The new text to insert.</param>
    /// <returns>True if the text could be placed; false for control kinds that cannot hold text.</returns>
    public static bool ReplaceContentControlText(SdtElement sdt, string newText)
    {
        OpenXmlCompositeElement? sdtContent = GetSdtContent(sdt);
        if (sdtContent == null || sdt is SdtRow)
        {
            return false;
        }

        Run? existingRun = sdtContent.Descendants<Run>().FirstOrDefault();
        RunProperties? runProps = existingRun?.RunProperties?.CloneNode(true) as RunProperties;
        if (runProps?.RunStyle?.Val?.Value == "PlaceholderText")
        {
            runProps.RunStyle.Remove();
        }

        Run newRun = new Run();
        if (runProps != null && runProps.HasChildren)
        {
            newRun.RunProperties = runProps;
        }

        newRun.AppendChild(new Text(newText) { Space = SpaceProcessingModeValues.Preserve });

        switch (sdtContent)
        {
            case SdtContentRun runContent:
                runContent.RemoveAllChildren();
                runContent.AppendChild(newRun);
                return true;

            case SdtContentBlock blockContent:
                ReplaceParagraphContent(blockContent, newRun);
                return true;

            case SdtContentCell cellContent:
                TableCell? cell = cellContent.GetFirstChild<TableCell>();
                if (cell == null)
                {
                    return false;
                }

                ReplaceParagraphContent(cell, newRun);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Keep only the first paragraph of <paramref name="container"/> (with its paragraph properties)
    /// and make <paramref name="newRun"/> its only content.
    /// </summary>
    private static void ReplaceParagraphContent(OpenXmlCompositeElement container, Run newRun)
    {
        Paragraph? paragraph = container.Elements<Paragraph>().FirstOrDefault();
        if (paragraph == null)
        {
            paragraph = new Paragraph();
            container.AppendChild(paragraph);
        }

        foreach (OpenXmlElement sibling in container.ChildElements.ToList())
        {
            if (sibling != paragraph && sibling is not TableCellProperties)
            {
                sibling.Remove();
            }
        }

        foreach (OpenXmlElement child in paragraph.ChildElements.ToList())
        {
            if (child is not ParagraphProperties)
            {
                child.Remove();
            }
        }

        paragraph.AppendChild(newRun);
    }
}
