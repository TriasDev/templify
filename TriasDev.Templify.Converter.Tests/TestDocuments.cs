// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;

namespace TriasDev.Templify.Converter.Tests;

/// <summary>
/// Builds synthetic OpenXMLTemplates documents (content controls with OpenXMLTemplates tags).
/// </summary>
internal static class TestDocuments
{
    public static SdtProperties Props(string tag) => new SdtProperties(new Tag { Val = tag });

    public static Paragraph Para(params OpenXmlElement[] children) => new Paragraph(children);

    public static Paragraph Para(string text) => new Paragraph(TextRun(text));

    public static Run TextRun(string text) => new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

    /// <summary>Inline (run-level) control.</summary>
    public static SdtRun InlineControl(string tag, params OpenXmlElement[] content)
        => new SdtRun(Props(tag), new SdtContentRun(content));

    /// <summary>Inline variable control with placeholder text.</summary>
    public static SdtRun InlineVariable(string path, string placeholderText = "Click here")
        => InlineControl("variable_" + path, TextRun(placeholderText));

    /// <summary>Block-level control.</summary>
    public static SdtBlock BlockControl(string tag, params OpenXmlElement[] content)
        => new SdtBlock(Props(tag), new SdtContentBlock(content));

    /// <summary>Row-level control (repeating table rows).</summary>
    public static SdtRow RowControl(string tag, params TableRow[] rows)
        => new SdtRow(Props(tag), new SdtContentRow(rows));

    public static TableCell Cell(params OpenXmlElement[] content)
        => new TableCell(new TableCellProperties(new TableCellWidth { Width = "2000", Type = TableWidthUnitValues.Dxa }), new Paragraph(content));

    public static TableRow Row(params TableCell[] cells) => new TableRow(cells);

    public static Table Table(params OpenXmlElement[] rows)
    {
        Table table = new Table(
            new TableProperties(new TableWidth { Width = "0", Type = TableWidthUnitValues.Auto }),
            new TableGrid(new GridColumn { Width = "2000" }, new GridColumn { Width = "2000" }));
        table.Append(rows);
        return table;
    }

    /// <summary>
    /// Create a .docx with the given body content and optional header content.
    /// </summary>
    public static void Create(string path, IEnumerable<OpenXmlElement> body, IEnumerable<OpenXmlElement>? header = null, IEnumerable<OpenXmlElement>? footer = null)
    {
        using WordprocessingDocument doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        MainDocumentPart main = doc.AddMainDocumentPart();
        Body documentBody = new Body(body);
        SectionProperties section = new SectionProperties();

        if (header != null)
        {
            HeaderPart headerPart = main.AddNewPart<HeaderPart>();
            headerPart.Header = new Header(header);
            section.Append(new HeaderReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(headerPart) });
        }

        if (footer != null)
        {
            FooterPart footerPart = main.AddNewPart<FooterPart>();
            footerPart.Footer = new Footer(footer);
            section.Append(new FooterReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(footerPart) });
        }

        documentBody.Append(section);
        main.Document = new Document(documentBody);
    }

    /// <summary>
    /// Paragraph texts of the body (one entry per paragraph, including paragraphs in tables).
    /// </summary>
    public static List<string> BodyParagraphs(string path)
    {
        using WordprocessingDocument doc = WordprocessingDocument.Open(path, false);
        return doc.MainDocumentPart!.Document!.Body!.Descendants<Paragraph>().Select(p => p.InnerText).ToList();
    }

    public static List<string> BodyParagraphs(Stream stream)
    {
        stream.Position = 0;
        using WordprocessingDocument doc = WordprocessingDocument.Open(stream, false);
        return doc.MainDocumentPart!.Document!.Body!.Descendants<Paragraph>().Select(p => p.InnerText).ToList();
    }

    public static List<string> TableRowTexts(Stream stream)
    {
        stream.Position = 0;
        using WordprocessingDocument doc = WordprocessingDocument.Open(stream, false);
        return doc.MainDocumentPart!.Document!.Body!.Descendants<TableRow>().Select(r => r.InnerText).ToList();
    }

    public static string HeaderText(Stream stream)
    {
        stream.Position = 0;
        using WordprocessingDocument doc = WordprocessingDocument.Open(stream, false);
        return string.Concat(doc.MainDocumentPart!.HeaderParts.Select(h => h.Header!.InnerText));
    }

    public static string HeaderText(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return HeaderText(stream);
    }

    public static int CountSdts(string path)
    {
        using WordprocessingDocument doc = WordprocessingDocument.Open(path, false);
        int count = doc.MainDocumentPart!.Document!.Descendants<SdtElement>().Count();
        count += doc.MainDocumentPart.HeaderParts.Sum(h => h.Header!.Descendants<SdtElement>().Count());
        count += doc.MainDocumentPart.FooterParts.Sum(f => f.Footer!.Descendants<SdtElement>().Count());
        return count;
    }

    public static List<string> SchemaErrors(string path)
    {
        using WordprocessingDocument doc = WordprocessingDocument.Open(path, false);
        return new OpenXmlValidator().Validate(doc).Select(e => $"{e.Description} @ {e.Path?.XPath}").ToList();
    }

    public static List<string> SchemaErrors(Stream stream)
    {
        stream.Position = 0;
        using WordprocessingDocument doc = WordprocessingDocument.Open(stream, false);
        return new OpenXmlValidator().Validate(doc).Select(e => $"{e.Description} @ {e.Path?.XPath}").ToList();
    }
}

/// <summary>
/// A temporary directory that is deleted on dispose.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "templify-converter-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // best effort
        }
    }
}
