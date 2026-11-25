// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;
using TriasDev.Templify.Tests.Odt;

namespace TriasDev.Templify.Builder.Tests;

public sealed partial class BoxRuleTests
{
    private static readonly XNamespace _w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace _style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace _fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
    private static readonly XNamespace _text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace _table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace _office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private static readonly BoxStyleKey _success = new BoxStyleKey("Success");
    private static readonly BoxStyleKey _danger = new BoxStyleKey("Danger");
    private static readonly BoxStyleKey _neutral = new BoxStyleKey("Neutral");

    private static StyleSheet Sheet() => StyleSheet.Create(s =>
    {
        s.Box(_success, b => b.Border(1.Pt(), Color.FromHex("96C6A8")).Background(Color.FromHex("EFF6F1")).Padding(8.Pt()).SpaceAfter(6.Pt()));
        s.Box(_danger, b => b.Border(1.Pt(), Color.FromHex("E09999")).Background(Color.FromHex("F8E8E8")).Padding(8.Pt()));
        s.Box(_neutral, b => b.Border(0.75.Pt(), Color.FromHex("C0C0C0")).Padding(4.Pt(), 10.Pt()));
    });

    private sealed class Verdict(TemplatePath control) : ITemplateComponent
    {
        public void Compose(BlockComposer content) => content.Switch(control["answer"], s =>
        {
            s.Case(new[] { "yes", "yesCertificate" }, b => Body(b, _success, "Met"));
            s.Case("no", b => Body(b, _danger, "Not met"));
            s.Default(b => Body(b, _neutral, "Not assessed"));
        });

        private void Body(BlockComposer c, BoxStyleKey style, string verdict) => c.Box(style, b =>
        {
            b.Paragraph(x =>
            {
                x.Text(verdict + ": ");
                x.Value(control["title"]);
            });
            b.If(control["comment"], k => k.Paragraph(control["comment"]));
            b.ForEach(control["evidence"], "e", (e, eb) => eb.Paragraph(x =>
            {
                x.Text("- ");
                x.Value(e.Self);
            }));
            b.Table(t =>
            {
                t.Columns(cols =>
                {
                    cols.Relative(1);
                    cols.Relative(1);
                });
                t.Row(r =>
                {
                    r.Cell("Owner");
                    r.Cell(control["owner"]);
                });
            });
        });
    }

    private static TestDefinition Controls() => new TestDefinition(t =>
    {
        t.Styles(Sheet());
        t.Content(c =>
        {
            c.Paragraph("Controls");
            c.Rule(0.5.Pt(), Color.FromHex("D4D4D4"), 4.Pt(), 8.Pt());
            c.ForEach(t.Data["controls"], "control", (control, b) => b.Component(new Verdict(control.Self)));
        });
    });

    private const string Json = """
    {
      "controls": [
        { "answer": "yes", "title": "Encryption", "comment": "AES-256", "evidence": [ "Policy", "Audit" ], "owner": "IT" },
        { "answer": "no", "title": "Backups", "comment": "", "evidence": [], "owner": "Ops" },
        { "answer": "later", "title": "Training", "comment": "", "evidence": [ "Plan" ], "owner": "HR" }
      ]
    }
    """;

    [Theory]
    [MemberData(nameof(Formats))]
    public void VerdictBoxes_ChooseTheirStyleByData(TemplateFormat format)
    {
        GeneratedTemplate template = BuilderHarness.Build(Controls(), format);
        using Stream input = template.OpenRead();
        using MemoryStream output = new MemoryStream();
        // Empty evidence lists are expected here: no warning for them.
        ProcessingResult result = new TemplateProcessor(new PlaceholderReplacementOptions { WarnOnEmptyLoopCollections = false, Culture = CultureInfo.InvariantCulture })
            .ProcessTemplate(input, output, Json);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        BuilderHarness.AssertPackage(format, output.ToArray());

        List<string[]> boxes = Boxes(format, output.ToArray());
        Assert.Equal(3, boxes.Count);
        Assert.Equal(new[] { "Met: Encryption", "AES-256", "- Policy", "- Audit", "Owner | IT", string.Empty }, boxes[0]);
        Assert.Equal(new[] { "Not met: Backups", "Owner | Ops", string.Empty }, boxes[1]);
        Assert.Equal(new[] { "Not assessed: Training", "- Plan", "Owner | HR", string.Empty }, boxes[2]);
    }

    [Fact]
    public void Docx_Box_IsAOneCellTableWithBorderBackgroundAndPadding()
    {
        byte[] package = BuilderHarness.Build(Controls(), TemplateFormat.Docx).Content.ToArray();
        XElement body = BuilderHarness.ReadXml(package, "word/document.xml").Root!.Element(_w + "body")!;
        XElement box = body.Descendants(_w + "tbl").First();

        XElement properties = box.Element(_w + "tblPr")!;
        Assert.Equal("9071", properties.Element(_w + "tblW")!.Attribute(_w + "w")!.Value);
        Assert.Equal(new[] { "8", "8", "8", "8" }, new[] { "top", "left", "bottom", "right" }.Select(e => properties.Element(_w + "tblBorders")!.Element(_w + e)!.Attribute(_w + "sz")!.Value));
        Assert.Equal("96C6A8", properties.Element(_w + "tblBorders")!.Element(_w + "top")!.Attribute(_w + "color")!.Value);
        Assert.Equal("160", properties.Element(_w + "tblCellMar")!.Element(_w + "left")!.Attribute(_w + "w")!.Value);
        Assert.Equal("EFF6F1", box.Descendants(_w + "shd").First().Attribute(_w + "fill")!.Value);
        Assert.Null(box.Element(_w + "tr")!.Element(_w + "trPr")?.Element(_w + "cantSplit"));

        // The nested two-column table shares the box's inner width: 453.544 − 2 × 8 pt.
        XElement nested = box.Descendants(_w + "tbl").First();
        Assert.Equal(new[] { "4375", "4375" }, nested.Element(_w + "tblGrid")!.Elements(_w + "gridCol").Select(g => g.Attribute(_w + "w")!.Value));

        XElement rule = body.Elements(_w + "p").ElementAt(1);
        Assert.Equal("4", rule.Descendants(_w + "top").Single().Attribute(_w + "sz")!.Value);
        Assert.Equal("80", rule.Descendants(_w + "spacing").Single().Attribute(_w + "before")!.Value);
        Assert.Equal("160", rule.Descendants(_w + "spacing").Single().Attribute(_w + "after")!.Value);
    }

    [Fact]
    public void Odt_Box_And_Rule_Styles()
    {
        byte[] package = BuilderHarness.Build(Controls(), TemplateFormat.Odt).Content.ToArray();
        XDocument content = BuilderHarness.ReadXml(package, "content.xml");
        Dictionary<string, XElement> styles = content.Root!.Element(_office + "automatic-styles")!.Elements(_style + "style")
            .ToDictionary(s => s.Attribute(_style + "name")!.Value);

        XElement boxCell = content.Descendants(_table + "table").First().Descendants(_table + "table-cell").First();
        XElement cellProperties = styles[boxCell.Attribute(_table + "style-name")!.Value].Element(_style + "table-cell-properties")!;
        Assert.Equal("#eff6f1", cellProperties.Attribute(_fo + "background-color")!.Value);
        Assert.Equal(new[] { "1pt solid #96c6a8", "1pt solid #96c6a8", "1pt solid #96c6a8", "1pt solid #96c6a8" },
            new[] { "border-top", "border-bottom", "border-left", "border-right" }.Select(a => cellProperties.Attribute(_fo + a)!.Value));
        Assert.Equal("8pt", cellProperties.Attribute(_fo + "padding-left")!.Value);
        Assert.Equal("6pt", styles[content.Descendants(_table + "table").First().Attribute(_table + "style-name")!.Value].Element(_style + "table-properties")!.Attribute(_fo + "margin-bottom")!.Value);

        XElement rule = content.Descendants(_text + "p").ElementAt(1);
        XElement ruleProperties = styles[rule.Attribute(_text + "style-name")!.Value].Element(_style + "paragraph-properties")!;
        Assert.Equal("0.5pt solid #d4d4d4", ruleProperties.Attribute(_fo + "border-top")!.Value);
        Assert.Equal("4pt", ruleProperties.Attribute(_fo + "margin-top")!.Value);
        Assert.Equal("1pt", ruleProperties.Attribute(_fo + "line-height")!.Value);
    }

    [Fact]
    public void Errors_AreReported()
    {
        TestDefinition undefined = new TestDefinition(t => t.Content(c => c.Box(_success, b => b.Paragraph("x"))));
        Assert.Equal("TB2010", Assert.Single(Assert.Throws<TemplateBuildException>(() => BuilderHarness.Build(undefined, TemplateFormat.Docx)).Errors).Code);

        TestDefinition duplicate = new TestDefinition(t =>
        {
            t.Styles(StyleSheet.Create(s =>
            {
                s.Box(_success, b => b.Padding(1.Pt()));
                s.Table(new TableStyleKey("Success"), tb => tb.CellPadding(1.Pt(), 1.Pt()));
            }));
            t.Content(c => c.Paragraph("x"));
        });
        Assert.Equal("TB2001", Assert.Single(Assert.Throws<TemplateBuildException>(() => BuilderHarness.Build(duplicate, TemplateFormat.Docx)).Errors).Code);

        Assert.Throws<ArgumentOutOfRangeException>(() => BuilderHarness.Build(new TestDefinition(t => t.Content(c => c.Rule(Length.Zero, Color.FromHex("000000")))), TemplateFormat.Docx));
        Assert.Throws<ArgumentOutOfRangeException>(() => StyleSheet.Create(s => s.Box(_success, b => b.Border(7.Pt(), Color.FromHex("000000")))));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    [Trait("Category", "LibreOffice")]
    public void LibreOffice_IndentsBoxTextByThePadding(TemplateFormat format)
    {
        LibreOfficeRunner.RequireExecutable();
        GeneratedTemplate template = BuilderHarness.Build(Controls(), format);
        using Stream input = template.OpenRead();
        using MemoryStream output = new MemoryStream();
        new TemplateProcessor(new PlaceholderReplacementOptions { WarnOnEmptyLoopCollections = false }).ProcessTemplate(input, output, Json);
        byte[] pdf = LibreOfficeRunner.Convert(output.ToArray(), template.FileExtension.TrimStart('.'), "pdf", "pdf");

        // Frame starts at 70.866 pt; the success and danger boxes pad 8 pt inside a 1 pt border. The text starts after
        // the padding; LibreOffice adds (part of) the border width, which differs by format (DOCX measures the padding
        // from the border's inner edge, ODT centres collapsed borders on the edge): within 2.5 pt.
        Dictionary<string, double> starts = WordStarts(pdf);
        Assert.InRange(starts["Met:"], 70.866 + 8 - 0.5, 70.866 + 8 + 2.5);
        Assert.InRange(starts["Not"], 70.866 + 8 - 0.5, 70.866 + 8 + 2.5);
    }

    private static List<string[]> Boxes(TemplateFormat format, byte[] package)
    {
        if (format == TemplateFormat.Docx)
        {
            return BuilderHarness.ReadXml(package, "word/document.xml").Root!.Element(_w + "body")!.Elements(_w + "tbl")
                .Select(t => t.Element(_w + "tr")!.Element(_w + "tc")!.Elements()
                    .Where(e => e.Name == _w + "p" || e.Name == _w + "tbl")
                    .Select(e => e.Name == _w + "tbl"
                        ? string.Join(" | ", e.Descendants(_w + "tc").Select(c => string.Concat(c.Descendants(_w + "t").Select(x => x.Value))))
                        : string.Concat(e.Descendants(_w + "t").Select(x => x.Value)))
                    .ToArray())
                .ToList();
        }

        return BuilderHarness.ReadXml(package, "content.xml").Descendants(_office + "text").First().Elements(_table + "table")
            .Select(t => t.Element(_table + "table-row")!.Element(_table + "table-cell")!.Elements()
                .Select(e => e.Name == _table + "table"
                    ? string.Join(" | ", e.Descendants(_table + "table-cell").Select(c => string.Concat(c.Elements(_text + "p").Select(OdtDocumentVerifier.RenderText))))
                    : OdtDocumentVerifier.RenderText(e))
                .ToArray())
            .ToList();
    }

    private static Dictionary<string, double> WordStarts(byte[] pdf)
    {
        string? executable = new[] { "/opt/homebrew/bin/pdftotext", "/usr/local/bin/pdftotext", "/usr/bin/pdftotext" }.FirstOrDefault(File.Exists);
        Assert.SkipWhen(executable == null, "pdftotext (poppler) is not installed.");
        string file = Path.Combine(Path.GetTempPath(), "templify-box-" + Guid.NewGuid().ToString("N") + ".pdf");
        File.WriteAllBytes(file, pdf);
        try
        {
            ProcessStartInfo start = new ProcessStartInfo(executable!) { RedirectStandardOutput = true, UseShellExecute = false };
            foreach (string argument in new[] { "-bbox", file, "-" })
            {
                start.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(start)!;
            string html = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            Dictionary<string, double> starts = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (Match match in WordBox().Matches(html))
            {
                starts.TryAdd(match.Groups[2].Value, double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
            }

            return starts;
        }
        finally
        {
            File.Delete(file);
        }
    }

    [GeneratedRegex("xMin=\"([0-9.]+)\"[^>]*>([^<]+)</word>")]
    private static partial Regex WordBox();
}
