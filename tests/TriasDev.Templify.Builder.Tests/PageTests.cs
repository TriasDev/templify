// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;
using TriasDev.Templify.Tests.Odt;

namespace TriasDev.Templify.Builder.Tests;

public sealed class PageTests
{
    private static readonly XNamespace _w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace _r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace _style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace _fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
    private static readonly XNamespace _text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace _office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private static readonly ParagraphStyleKey _indented = new ParagraphStyleKey("Indented", "Indented");

    [Fact]
    public void DefaultPage_IsA4WithEqualMargins()
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Paragraph("x")));

        XElement section = BuilderHarness.ReadXml(BuilderHarness.Build(definition, TemplateFormat.Docx).Content.ToArray(), "word/document.xml")
            .Descendants(_w + "sectPr").Single();
        Assert.Equal("11906", section.Element(_w + "pgSz")!.Attribute(_w + "w")!.Value);
        Assert.Equal("16838", section.Element(_w + "pgSz")!.Attribute(_w + "h")!.Value);
        Assert.Equal("1417", section.Element(_w + "pgMar")!.Attribute(_w + "top")!.Value);
        Assert.Equal("1417", section.Element(_w + "pgMar")!.Attribute(_w + "left")!.Value);
        Assert.Null(section.Element(_w + "titlePg"));

        XElement layout = BuilderHarness.ReadXml(BuilderHarness.Build(definition, TemplateFormat.Odt).Content.ToArray(), "styles.xml")
            .Descendants(_style + "page-layout-properties").Single();
        Assert.Equal("595.276pt", layout.Attribute(_fo + "page-width")!.Value);
        Assert.Equal("841.89pt", layout.Attribute(_fo + "page-height")!.Value);
        Assert.Equal("70.866pt", layout.Attribute(_fo + "margin-top")!.Value);
    }

    [Fact]
    public void CustomPage_SizeAndMargins()
    {
        TestDefinition definition = new TestDefinition(t =>
        {
            t.Page(p => p.Size(PageSize.Letter).Margins(top: 20.Mm(), right: 15.Mm(), bottom: 18.Mm(), left: 30.Mm()).HeaderFooterDistance(8.Mm(), 6.Mm()));
            t.Content(c => c.Paragraph("x"));
        });

        XElement margins = BuilderHarness.ReadXml(BuilderHarness.Build(definition, TemplateFormat.Docx).Content.ToArray(), "word/document.xml")
            .Descendants(_w + "pgMar").Single();
        Assert.Equal(new[] { "1134", "850", "1020", "1701", "454", "340" },
            new[] { "top", "right", "bottom", "left", "header", "footer" }.Select(a => margins.Attribute(_w + a)!.Value));

        XElement layout = BuilderHarness.ReadXml(BuilderHarness.Build(definition, TemplateFormat.Odt).Content.ToArray(), "styles.xml")
            .Descendants(_style + "page-layout-properties").Single();
        Assert.Equal("612pt", layout.Attribute(_fo + "page-width")!.Value);
        Assert.Equal("85.039pt", layout.Attribute(_fo + "margin-left")!.Value);
    }

    private static TestDefinition Letter() => new TestDefinition(t =>
    {
        t.Styles(StyleSheet.Create(s => s.Paragraph(_indented, p => p.IndentStart(10.Mm()))));
        t.Page(p => p
            .Header(h =>
            {
                h.Paragraph(x =>
                {
                    x.Value(t.Data["company"]);
                    x.TabRight();
                    x.Text("Confidential");
                });
                h.If(t.Data["draft"], d => d.Paragraph("DRAFT"));
            })
            .Footer(f => f.Paragraph(x =>
            {
                x.Text("Page ");
                x.PageNumber();
                x.Text(" of ");
                x.PageCount();
            }))
            .FirstPage(first =>
            {
                first.NoHeader();
                first.Footer(f => f.Paragraph(x =>
                {
                    x.Text("Cover of ");
                    x.Value(t.Data["company"]);
                }));
            }));
        t.Content(c =>
        {
            c.Paragraph("Cover");
            c.PageBreak();
            c.Paragraph(x =>
            {
                x.Text("Body");
                x.TabRight();
                x.Text("right");
            });
            c.Paragraph(_indented, x =>
            {
                x.Text("Indented");
                x.TabRight();
                x.Text("end");
            });
            c.PageBreak();
            c.Paragraph("Last");
        });
    });

    private const string LetterJson = """{ "company": "Acme Ltd", "draft": true }""";

    [Fact]
    public void Docx_WritesHeaderAndFooterParts_WithAFirstPage()
    {
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Letter(), TemplateFormat.Docx, LetterJson);
        byte[] built = template.Content.ToArray();

        XElement section = BuilderHarness.ReadXml(built, "word/document.xml").Descendants(_w + "sectPr").Single();
        Assert.NotNull(section.Element(_w + "titlePg"));
        Assert.Equal(
            new[] { "headerReference default rId2", "headerReference first rId3", "footerReference default rId4", "footerReference first rId5" },
            section.Elements().Where(e => e.Name.LocalName.EndsWith("Reference", StringComparison.Ordinal))
                .Select(e => $"{e.Name.LocalName} {e.Attribute(_w + "type")!.Value} {e.Attribute(_r + "id")!.Value}"));

        Assert.Equal(new[] { "Acme Ltd\tConfidential", "DRAFT" }, PartTexts(filled, "word/header1.xml"));
        Assert.Equal(new[] { string.Empty }, PartTexts(filled, "word/header2.xml"));
        Assert.Equal(new[] { "Cover of Acme Ltd" }, PartTexts(filled, "word/footer2.xml"));

        XElement footer = BuilderHarness.ReadXml(filled, "word/footer1.xml").Root!;
        Assert.Equal(new[] { " PAGE ", " NUMPAGES " }, footer.Descendants(_w + "instrText").Select(i => i.Value));

        XElement tab = BuilderHarness.ReadXml(built, "word/document.xml").Descendants(_w + "tab").First(e => e.Parent!.Name == _w + "tabs");
        Assert.Equal("right", tab.Attribute(_w + "val")!.Value);
        Assert.Equal("9071", tab.Attribute(_w + "pos")!.Value);
    }

    [Fact]
    public void Odt_WritesTheMasterPage_WithAFirstPage()
    {
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Letter(), TemplateFormat.Odt, LetterJson);

        XElement master = BuilderHarness.ReadXml(filled, "styles.xml").Descendants(_style + "master-page").Single();
        Assert.Equal("Standard", master.Attribute(_style + "name")!.Value);
        Assert.Equal(new[] { "header", "header-first", "footer", "footer-first" }, master.Elements().Select(e => e.Name.LocalName));
        Assert.Equal(new[] { "Acme Ltd\tConfidential", "DRAFT" }, master.Element(_style + "header")!.Elements(_text + "p").Select(OdtDocumentVerifier.RenderText));
        Assert.Equal(new[] { "Cover of Acme Ltd" }, master.Element(_style + "footer-first")!.Elements(_text + "p").Select(OdtDocumentVerifier.RenderText));
        Assert.NotNull(master.Element(_style + "footer")!.Descendants(_text + "page-number").SingleOrDefault());
        Assert.NotNull(master.Element(_style + "footer")!.Descendants(_text + "page-count").SingleOrDefault());

        XDocument styles = BuilderHarness.ReadXml(template.Content.ToArray(), "styles.xml");
        XElement layout = styles.Descendants(_style + "page-layout").Single();
        Assert.Equal("35.433pt", layout.Element(_style + "page-layout-properties")!.Attribute(_fo + "margin-top")!.Value);
        Assert.Equal("35.433pt", layout.Element(_style + "header-style")!.Element(_style + "header-footer-properties")!.Attribute(_fo + "min-height")!.Value);

        // Right tab stops: from the frame's start in the header and body (453.544pt), from the indent for the indented style.
        XDocument content = BuilderHarness.ReadXml(template.Content.ToArray(), "content.xml");
        Dictionary<string, string> tabs = content.Root!.Element(_office + "automatic-styles")!.Elements(_style + "style")
            .Where(s => s.Descendants(_style + "tab-stop").Any())
            .ToDictionary(s => s.Attribute(_style + "parent-style-name")!.Value, s => s.Descendants(_style + "tab-stop").Single().Attribute(_style + "position")!.Value);
        Assert.Equal("453.544pt", tabs["Standard"]);
        Assert.Equal("425.198pt", tabs["Indented"]);
        Assert.Contains(styles.Descendants(_style + "style"), s => (string?)s.Attribute(_style + "name") == "TemplifyHfTab1");
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Contract_IncludesHeaderAndFooterPaths(TemplateFormat format)
    {
        GeneratedTemplate template = BuilderHarness.Build(Letter(), format);

        Assert.Equal(new[] { "company", "draft" }, template.Contract.Entries.Select(e => e.Path));
    }

    [Fact]
    public void Errors_AreReported()
    {
        TestDefinition twice = new TestDefinition(t =>
        {
            t.Page(p => p.Size(PageSize.A5));
            t.Page(p => p.Size(PageSize.A4));
        });
        TestDefinition noFrame = new TestDefinition(t => t.Page(p => p.Size(PageSize.A5).Margins(100.Mm())));
        TestDefinition undefinedStyle = new TestDefinition(t => t.Page(p => p.Header(h => h.Paragraph(_indented, "x"))));

        Assert.Equal("TB6001", Assert.Single(Assert.Throws<TemplateBuildException>(() => BuilderHarness.Build(twice, TemplateFormat.Docx)).Errors).Code);
        Assert.Equal("TB6002", Assert.Single(Assert.Throws<TemplateBuildException>(() => BuilderHarness.Build(noFrame, TemplateFormat.Docx)).Errors).Code);
        Assert.Equal("TB2010", Assert.Single(Assert.Throws<TemplateBuildException>(() => BuilderHarness.Build(undefinedStyle, TemplateFormat.Odt)).Errors).Code);
        Assert.Throws<ArgumentOutOfRangeException>(() => BuilderHarness.Build(new TestDefinition(t => t.Page(p => p.Margins(-1.Mm()))), TemplateFormat.Docx));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PageSize(Length.Zero, 10.Mm()));

        PageComposer? kept = null;
        BuilderHarness.Build(new TestDefinition(t => t.Page(p => kept = p)), TemplateFormat.Docx);
        Assert.Throws<InvalidOperationException>(() => kept!.Size(PageSize.A4));
    }

    // ── LibreOffice: what a reader sees ─────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Formats))]
    [Trait("Category", "LibreOffice")]
    public void LibreOffice_ShowsTheHeadersFootersAndPageNumbersPerPage(TemplateFormat format)
    {
        LibreOfficeRunner.RequireExecutable();
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Letter(), format, LetterJson);
        byte[] pdf = LibreOfficeRunner.Convert(filled, template.FileExtension.TrimStart('.'), "pdf", "pdf");

        string[] pages = Enumerable.Range(1, 3).Select(n => PdfText(pdf, n)).ToArray();

        Assert.DoesNotContain("Acme Ltd", pages[0].Split("Cover of")[0], StringComparison.Ordinal);
        Assert.Contains("Cover of Acme Ltd", pages[0], StringComparison.Ordinal);
        Assert.DoesNotContain("Page 1 of", pages[0], StringComparison.Ordinal);
        Assert.Contains("Acme Ltd", pages[1], StringComparison.Ordinal);
        Assert.Contains("DRAFT", pages[1], StringComparison.Ordinal);
        Assert.Contains("Page 2 of 3", pages[1], StringComparison.Ordinal);
        Assert.Contains("Page 3 of 3", pages[2], StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    [Trait("Category", "LibreOffice")]
    public void LibreOffice_RightTabEndsAtTheRightMargin(TemplateFormat format)
    {
        LibreOfficeRunner.RequireExecutable();
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Letter(), format, LetterJson);
        byte[] pdf = LibreOfficeRunner.Convert(filled, template.FileExtension.TrimStart('.'), "pdf", "pdf");

        // A4, 25 mm margins: the frame ends at 595.276 − 70.866 = 524.41 pt.
        Dictionary<string, double> wordEnds = PdfWordEnds(pdf, 2);
        Assert.InRange(wordEnds["right"], 523.0, 525.0);
        Assert.InRange(wordEnds["end"], 523.0, 525.0);
        Assert.InRange(wordEnds["Confidential"], 523.0, 525.0);
    }

    private static List<string> PartTexts(byte[] package, string part)
    {
        return BuilderHarness.ReadXml(package, part).Root!.Elements(_w + "p").Select(p =>
            string.Concat(p.Descendants().Select(e =>
                e.Name == _w + "t" ? e.Value
                : e.Name == _w + "tab" && e.Parent!.Name == _w + "r" ? "\t"
                : string.Empty)))
            .ToList();
    }

    private static string PdfText(byte[] pdf, int page) => RunPdfTool("pdftotext", pdf, "-f", page.ToString(CultureInfo.InvariantCulture), "-l", page.ToString(CultureInfo.InvariantCulture), "-layout");

    /// <summary>The right edge (xMax) of each word on a page, from pdftotext's bounding-box output.</summary>
    private static Dictionary<string, double> PdfWordEnds(byte[] pdf, int page)
    {
        string html = RunPdfTool("pdftotext", pdf, "-f", page.ToString(CultureInfo.InvariantCulture), "-l", page.ToString(CultureInfo.InvariantCulture), "-bbox");
        Dictionary<string, double> ends = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(html, "xMax=\"([0-9.]+)\"[^>]*>([^<]+)</word>"))
        {
            ends[match.Groups[2].Value] = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        return ends;
    }

    private static string RunPdfTool(string tool, byte[] pdf, params string[] arguments)
    {
        string? executable = new[] { "/opt/homebrew/bin/" + tool, "/usr/local/bin/" + tool, "/usr/bin/" + tool }.FirstOrDefault(File.Exists);
        Assert.SkipWhen(executable == null, $"{tool} (poppler) is not installed.");

        string directory = Path.Combine(Path.GetTempPath(), "templify-pdf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string input = Path.Combine(directory, "document.pdf");
            File.WriteAllBytes(input, pdf);
            ProcessStartInfo start = new ProcessStartInfo(executable!) { RedirectStandardOutput = true, UseShellExecute = false };
            foreach (string argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            start.ArgumentList.Add(input);
            start.ArgumentList.Add("-");
            using Process process = Process.Start(start)!;
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
