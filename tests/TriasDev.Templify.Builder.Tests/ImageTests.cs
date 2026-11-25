// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Odt;

namespace TriasDev.Templify.Builder.Tests;

public sealed class ImageTests
{
    private static readonly XNamespace _w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace _wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private static readonly XNamespace _draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private static readonly XNamespace _svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
    private static readonly XNamespace _xlink = "http://www.w3.org/1999/xlink";
    private static readonly XNamespace _manifest = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    /// <summary>40 × 20 px PNG, no resolution (96 dpi).</summary>
    internal static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAACgAAAAUCAIAAABwJOjsAAAAJElEQVR42mOQ96scEMQwavGoxaMWj1o8avGoxaMWj1o8ciwGAO6wzt9DGVp0AAAAAElFTkSuQmCC");

    /// <summary>40 × 20 px PNG at 192 dpi (pHYs).</summary>
    private static readonly byte[] HiDpiPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAACgAAAAUCAIAAABwJOjsAAAACXBIWXMAAB2HAAAdhwGP5fFlAAAAJElEQVR42mM4ISc3IIhh1OJRi0ctHrV41OJRi0ctHrV45FgMAFLhLK5O4QWwAAAAAElFTkSuQmCC");

    /// <summary>40 × 20 px JPEG, JFIF without units (96 dpi).</summary>
    private static readonly byte[] Jpeg = Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQAASABIAAD/4QBMRXhpZgAATU0AKgAAAAgAAYdpAAQAAAABAAAAGgAAAAAAA6ABAAMAAAABAAEAAKACAAQAAAABAAAAKKADAAQAAAABAAAAFAAAAAD/7QA4UGhvdG9zaG9wIDMuMAA4QklNBAQAAAAAAAA4QklNBCUAAAAAABDUHYzZjwCyBOmACZjs+EJ+/8AAEQgAFAAoAwEiAAIRAQMRAf/EAB8AAAEFAQEBAQEBAAAAAAAAAAABAgMEBQYHCAkKC//EALUQAAIBAwMCBAMFBQQEAAABfQECAwAEEQUSITFBBhNRYQcicRQygZGhCCNCscEVUtHwJDNicoIJChYXGBkaJSYnKCkqNDU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6g4SFhoeIiYqSk5SVlpeYmZqio6Slpqeoqaqys7S1tre4ubrCw8TFxsfIycrS09TV1tfY2drh4uPk5ebn6Onq8fLz9PX29/j5+v/EAB8BAAMBAQEBAQEBAQEAAAAAAAABAgMEBQYHCAkKC//EALURAAIBAgQEAwQHBQQEAAECdwABAgMRBAUhMQYSQVEHYXETIjKBCBRCkaGxwQkjM1LwFWJy0QoWJDThJfEXGBkaJicoKSo1Njc4OTpDREVGR0hJSlNUVVZXWFlaY2RlZmdoaWpzdHV2d3h5eoKDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uLj5OXm5+jp6vLz9PX29/j5+v/bAEMAAgICAgICAwICAwUDAwMFBgUFBQUGCAYGBgYGCAoICAgICAgKCgoKCgoKCgwMDAwMDA4ODg4ODw8PDw8PDw8PD//bAEMBAgICBAQEBwQEBxALCQsQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEP/dAAQAA//aAAwDAQACEQMRAD8A+E6KKK/dD8TCiiigAooooA//0PhOiiiv3Q/EwooooAKKKKAP/9k=");

    [Fact]
    public void ImageSource_ReadsSizeAndResolution()
    {
        ImageSource png = ImageSource.Png(Png);
        Assert.Equal((40, 20), (png.PixelWidth, png.PixelHeight));
        Assert.Equal((30.Pt(), 15.Pt()), (png.NaturalWidth, png.NaturalHeight));

        ImageSource hiDpi = ImageSource.FromBytes(HiDpiPng);
        Assert.Equal(15, hiDpi.NaturalWidth.Points, 2);

        ImageSource jpeg = ImageSource.Jpeg(Jpeg);
        Assert.Equal((40, 20), (jpeg.PixelWidth, jpeg.PixelHeight));
        Assert.Equal(30.Pt(), jpeg.NaturalWidth);
        Assert.Equal(20, ImageSource.FromBytes(Jpeg).PixelHeight);

        Assert.Throws<ArgumentException>(() => ImageSource.Png(Jpeg));
        Assert.Throws<ArgumentException>(() => ImageSource.Jpeg(Png));
        Assert.Throws<ArgumentException>(() => ImageSource.FromBytes(new byte[] { 1, 2, 3, 4, 5 }));
    }

    [Fact]
    public void ImageFit_KeepsTheAspectRatio()
    {
        ImageSource png = ImageSource.Png(Png);
        Assert.Equal((60.Pt(), 30.Pt()), ImageFit.Width(60.Pt()).Resolve(png));
        Assert.Equal((20.Pt(), 10.Pt()), ImageFit.Height(10.Pt()).Resolve(png));
        Assert.Equal((40.Pt(), 20.Pt()), ImageFit.Within(100.Pt(), 20.Pt()).Resolve(png));
        Assert.Equal((20.Pt(), 10.Pt()), ImageFit.Within(20.Pt(), 100.Pt()).Resolve(png));
        Assert.Equal((30.Pt(), 15.Pt()), ImageFit.Natural.Resolve(png));
    }

    private static TestDefinition Document() => new TestDefinition(t =>
    {
        ImageSource logo = ImageSource.Png(Png);
        ImageSource photo = ImageSource.Jpeg(Jpeg);
        t.Page(p => p.Header(h => h.Paragraph(x =>
        {
            x.Text("Company");
            x.TabRight();
            x.Image(logo, "Company logo", ImageFit.Within(30.Mm(), 10.Mm()));
        })));
        t.Content(c =>
        {
            c.Paragraph(x =>
            {
                x.Text("Signed ");
                x.Image(logo, "Signature", ImageFit.Height(12.Pt()));
                x.Text(" by ");
                x.Value(t.Data["signer"]);
            });
            c.Image(photo, "Product photo", ImageFit.Width(80.Mm()));
            c.Table(tb =>
            {
                tb.Columns(cols =>
                {
                    cols.Fixed(20.Mm());
                    cols.Relative(1);
                });
                tb.ForEachRow(t.Data["items"], "item", (item, rows) => rows.Row(r =>
                {
                    r.Cell(cell => cell.Image(logo, "Item icon", ImageFit.Height(10.Pt())));
                    r.Cell(item["name"]);
                }));
            });
        });
    });

    private const string Json = """{ "signer": "Alice", "items": [ { "name": "one" }, { "name": "two" }, { "name": "three" } ] }""";

    [Fact]
    public void Docx_StoresEachImageOnce_WithPerPartRelationships()
    {
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Document(), TemplateFormat.Docx, Json);
        byte[] package = template.Content.ToArray();

        Assert.Equal(new[] { "word/media/image1.png", "word/media/image2.jpeg" }, Entries(package).Where(e => e.StartsWith("word/media/", StringComparison.Ordinal)));
        string contentTypes = Entry(package, "[Content_Types].xml");
        Assert.Contains("Extension=\"png\" ContentType=\"image/png\"", contentTypes, StringComparison.Ordinal);
        Assert.Contains("Extension=\"jpeg\" ContentType=\"image/jpeg\"", contentTypes, StringComparison.Ordinal);
        Assert.Contains("Target=\"media/image1.png\"", Entry(package, "word/_rels/header1.xml.rels"), StringComparison.Ordinal);
        Assert.Contains("Target=\"media/image2.jpeg\"", Entry(package, "word/_rels/document.xml.rels"), StringComparison.Ordinal);

        List<XElement> docPr = BuilderHarness.ReadXml(package, "word/document.xml").Descendants(_wp + "docPr").ToList();
        Assert.Equal(new[] { "Signature", "Product photo", "Item icon" }, docPr.Select(d => d.Attribute("descr")!.Value));
        XElement headerDocPr = BuilderHarness.ReadXml(package, "word/header1.xml").Descendants(_wp + "docPr").Single();
        Assert.Equal(4, docPr.Select(d => d.Attribute("id")!.Value).Append(headerDocPr.Attribute("id")!.Value).Distinct().Count());

        // 80 mm wide photo: 226.772 pt → 2 880 000 EMU; height half.
        XElement extent = BuilderHarness.ReadXml(package, "word/document.xml").Descendants(_wp + "extent").ElementAt(1);
        Assert.Equal("2880004", extent.Attribute("cx")!.Value);

        // The engine cloned the row's image three times with unique ids (schema validation ran in the harness).
        List<string> filledIds = BuilderHarness.ReadXml(filled, "word/document.xml").Descendants(_wp + "docPr").Select(d => d.Attribute("id")!.Value).ToList();
        Assert.Equal(5, filledIds.Count);
        Assert.Equal(filledIds.Count, filledIds.Distinct().Count());
    }

    [Fact]
    public void Odt_StoresPicturesWithManifestEntries_AndFramesWithAltText()
    {
        (GeneratedTemplate template, _) = BuilderHarness.BuildAndFill(Document(), TemplateFormat.Odt, Json);
        byte[] package = template.Content.ToArray();

        List<string> pictures = Entries(package).Where(e => e.StartsWith("Pictures/", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, pictures.Count);
        XDocument manifest = BuilderHarness.ReadXml(package, "META-INF/manifest.xml");
        Assert.All(pictures, p => Assert.Contains(manifest.Descendants(_manifest + "file-entry"), e => (string?)e.Attribute(_manifest + "full-path") == p));

        List<XElement> frames = BuilderHarness.ReadXml(package, "content.xml").Descendants(_draw + "frame").ToList();
        Assert.Equal(new[] { "Signature", "Product photo", "Item icon" }, frames.Select(f => f.Element(_svg + "title")!.Value));
        Assert.Equal(new[] { "TemplifyImage1", "TemplifyImage2", "TemplifyImage3" }, frames.Select(f => f.Attribute(_draw + "name")!.Value));
        Assert.Equal("226.772pt", frames[1].Attribute(_svg + "width")!.Value);
        Assert.Equal(frames[0].Element(_draw + "image")!.Attribute(_xlink + "href")!.Value, frames[2].Element(_draw + "image")!.Attribute(_xlink + "href")!.Value);

        XElement logo = BuilderHarness.ReadXml(package, "styles.xml").Descendants(_draw + "frame").Single();
        Assert.Equal("TemplifyHfImage1", logo.Attribute(_draw + "name")!.Value);
        Assert.Equal("Company logo", logo.Element(_svg + "desc")!.Value);
    }

    [Fact]
    public void AltText_IsRequired()
    {
        ImageSource png = ImageSource.Png(Png);

        Assert.Throws<ArgumentException>(() => BuilderHarness.Build(new TestDefinition(t => t.Content(c => c.Image(png, " "))), TemplateFormat.Docx));
        Assert.Throws<ArgumentException>(() => BuilderHarness.Build(new TestDefinition(t => t.Content(c => c.Paragraph(x => x.Image(png, string.Empty)))), TemplateFormat.Docx));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    [Trait("Category", "LibreOffice")]
    public void LibreOffice_RendersEveryImage(TemplateFormat format)
    {
        LibreOfficeRunner.RequireExecutable();
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Document(), format, Json);
        byte[] pdf = LibreOfficeRunner.Convert(filled, template.FileExtension.TrimStart('.'), "pdf", "pdf");

        // Header logo, signature, photo and three row icons.
        Assert.Equal(6, PdfImageCount(pdf));
    }

    private static int PdfImageCount(byte[] pdf)
    {
        string? executable = new[] { "/opt/homebrew/bin/pdfimages", "/usr/local/bin/pdfimages", "/usr/bin/pdfimages" }.FirstOrDefault(File.Exists);
        Assert.SkipWhen(executable == null, "pdfimages (poppler) is not installed.");
        string file = Path.Combine(Path.GetTempPath(), "templify-images-" + Guid.NewGuid().ToString("N") + ".pdf");
        File.WriteAllBytes(file, pdf);
        try
        {
            ProcessStartInfo start = new ProcessStartInfo(executable!) { RedirectStandardOutput = true, UseShellExecute = false };
            start.ArgumentList.Add("-list");
            start.ArgumentList.Add(file);
            using Process process = Process.Start(start)!;
            string[] lines = process.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            process.WaitForExit();

            // Two header lines; one line per image ("smask" lines are the alpha channel of the image before them).
            return lines.Skip(2).Count(l => !l.Contains(" smask ", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static List<string> Entries(byte[] package)
    {
        using ZipArchive archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        return archive.Entries.Select(e => e.FullName).ToList();
    }

    private static string Entry(byte[] package, string name)
    {
        using ZipArchive archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        using StreamReader reader = new StreamReader(archive.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }
}
