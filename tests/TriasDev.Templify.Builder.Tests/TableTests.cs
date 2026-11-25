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

public sealed partial class TableTests
{
    private static readonly XNamespace _w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace _table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace _style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace _fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
    private static readonly XNamespace _text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace _office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private static readonly TableStyleKey _grid = new TableStyleKey("Grid");

    private static StyleSheet Sheet() => StyleSheet.Create(s => s.Table(_grid, t => t
        .OuterBorder(0.75.Pt(), Color.FromHex("404040"))
        .RowRule(0.5.Pt(), Color.FromHex("D4D4D4"))
        .ColumnRule(0.5.Pt(), Color.FromHex("D4D4D4"))
        .HeaderRule(1.5.Pt(), Color.FromHex("1F4E79"))
        .HeaderBackground(Color.FromHex("EEF3F8"))
        .CellPadding(2.Pt(), 4.Pt())));

    private static TestDefinition Invoice(int extraRows = 0) => new TestDefinition(t =>
    {
        TemplatePath order = t.Data["order"];
        t.Styles(Sheet());
        t.Content(c =>
        {
            c.Table(tb =>
            {
                tb.Style(_grid);
                tb.Columns(cols =>
                {
                    cols.Fixed(15.Mm());
                    cols.Relative(2);
                    cols.Relative(1);
                });
                tb.SpaceAfter(6.Pt());
                tb.HeaderRow(r =>
                {
                    r.Cell("Pos.");
                    r.Cell("Description");
                    r.Cell("Amount");
                });
                tb.Row(r =>
                {
                    r.Cell("1");
                    r.Cell(cell =>
                    {
                        cell.Paragraph(order["item"]);
                        cell.If(order["express"], b => b.Paragraph("express"));
                    });
                    r.Cell(cell => cell.VerticalAlignment(CellVerticalAlignment.Bottom).Paragraph(x => x.Value(order["amount"], ValueFormat.Number("F2"))));
                });
                for (int i = 0; i < extraRows; i++)
                {
                    string n = (i + 2).ToString(CultureInfo.InvariantCulture);
                    tb.Row(r =>
                    {
                        r.Cell(n);
                        r.Cell("Filler line " + n);
                        r.Cell("0.00");
                    });
                }

                tb.Row(r =>
                {
                    r.Cell(cell => cell.ColumnSpan(2).Background(Color.FromHex("F5F5F5")).Paragraph("Total"));
                    r.Cell(order["total"]);
                });
            });
            c.Paragraph("after");
        });
    });

    private const string OrderJson = """{ "order": { "item": "Laptop", "express": true, "amount": 1499, "total": "1,499.00" } }""";

    [Theory]
    [MemberData(nameof(Formats))]
    public void Table_IsFilledByTheEngine(TemplateFormat format)
    {
        (_, byte[] filled) = BuilderHarness.BuildAndFill(Invoice(), format, OrderJson);

        Assert.Equal(
            new[]
            {
                new[] { "Pos.", "Description", "Amount" },
                new[] { "1", "Laptop\nexpress", "1499.00" },
                new[] { "Total", "1,499.00" },
            },
            TableTexts(format, filled).Single());
    }

    [Fact]
    public void Docx_Table_HasTheGridBordersAndRowOptions()
    {
        byte[] package = BuilderHarness.Build(Invoice(), TemplateFormat.Docx).Content.ToArray();
        XElement table = BuilderHarness.ReadXml(package, "word/document.xml").Descendants(_w + "tbl").Single();

        // Frame 160 mm = 453.544 pt; 15 mm fixed; the rest 2 : 1.
        Assert.Equal(new[] { "850", "5480", "2740" }, table.Element(_w + "tblGrid")!.Elements(_w + "gridCol").Select(g => g.Attribute(_w + "w")!.Value));
        XElement properties = table.Element(_w + "tblPr")!;
        Assert.Equal("fixed", properties.Element(_w + "tblLayout")!.Attribute(_w + "type")!.Value);
        Assert.Equal("6", properties.Element(_w + "tblBorders")!.Element(_w + "top")!.Attribute(_w + "sz")!.Value);
        Assert.Equal("D4D4D4", properties.Element(_w + "tblBorders")!.Element(_w + "insideH")!.Attribute(_w + "color")!.Value);
        Assert.Equal("80", properties.Element(_w + "tblCellMar")!.Element(_w + "left")!.Attribute(_w + "w")!.Value);

        List<XElement> rows = table.Elements(_w + "tr").ToList();
        Assert.NotNull(rows[0].Element(_w + "trPr")!.Element(_w + "tblHeader"));
        Assert.All(rows, r => Assert.NotNull(r.Element(_w + "trPr")!.Element(_w + "cantSplit")));
        XElement headerCell = rows[0].Elements(_w + "tc").First().Element(_w + "tcPr")!;
        Assert.Equal("EEF3F8", headerCell.Element(_w + "shd")!.Attribute(_w + "fill")!.Value);
        Assert.Equal("12", headerCell.Element(_w + "tcBorders")!.Element(_w + "bottom")!.Attribute(_w + "sz")!.Value);
        XElement totalCell = rows[2].Elements(_w + "tc").First().Element(_w + "tcPr")!;
        Assert.Equal("2", totalCell.Element(_w + "gridSpan")!.Attribute(_w + "val")!.Value);
        Assert.Equal("6330", totalCell.Element(_w + "tcW")!.Attribute(_w + "w")!.Value);
        Assert.Equal("bottom", rows[1].Elements(_w + "tc").Last().Element(_w + "tcPr")!.Element(_w + "vAlign")!.Attribute(_w + "val")!.Value);

        // SpaceAfter: a 1 pt paragraph after the table.
        XElement spacer = (XElement)table.NextNode!;
        Assert.Equal("120", spacer.Descendants(_w + "spacing").Single().Attribute(_w + "after")!.Value);
    }

    [Fact]
    public void Odt_Table_HasColumnsHeaderRowsSpansAndCellStyles()
    {
        byte[] package = BuilderHarness.Build(Invoice(), TemplateFormat.Odt).Content.ToArray();
        XDocument content = BuilderHarness.ReadXml(package, "content.xml");
        XElement table = content.Descendants(_table + "table").Single();
        Dictionary<string, XElement> styles = content.Root!.Element(_office + "automatic-styles")!.Elements(_style + "style")
            .ToDictionary(s => s.Attribute(_style + "name")!.Value);

        Assert.Equal("TemplifyTable1", table.Attribute(_table + "name")!.Value);
        Assert.Equal(
            new[] { "42.52pt", "274.016pt", "137.008pt" },
            table.Elements(_table + "table-column").Select(c => styles[c.Attribute(_table + "style-name")!.Value].Descendants().Single().Attribute(_style + "column-width")!.Value));
        Assert.Single(table.Element(_table + "table-header-rows")!.Elements(_table + "table-row"));
        Assert.Equal("6pt", styles["TemplifyTable1"].Element(_style + "table-properties")!.Attribute(_fo + "margin-bottom")!.Value);

        List<XElement> bodyRows = table.Elements(_table + "table-row").ToList();
        XElement total = bodyRows[1];
        Assert.Equal("2", total.Elements(_table + "table-cell").First().Attribute(_table + "number-columns-spanned")!.Value);
        Assert.Single(total.Elements(_table + "covered-table-cell"));

        XElement headerCell = table.Element(_table + "table-header-rows")!.Descendants(_table + "table-cell").First();
        XElement headerProps = styles[headerCell.Attribute(_table + "style-name")!.Value].Element(_style + "table-cell-properties")!;
        Assert.Equal("#eef3f8", headerProps.Attribute(_fo + "background-color")!.Value);
        Assert.Equal("1.5pt solid #1f4e79", headerProps.Attribute(_fo + "border-bottom")!.Value);
        Assert.Equal("0.75pt solid #404040", headerProps.Attribute(_fo + "border-top")!.Value);
        XElement firstBodyCell = bodyRows[0].Elements(_table + "table-cell").First();
        Assert.Equal("1.5pt solid #1f4e79", styles[firstBodyCell.Attribute(_table + "style-name")!.Value].Element(_style + "table-cell-properties")!.Attribute(_fo + "border-top")!.Value);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void NestedTable_ResolvesItsColumnsAgainstTheCell(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c =>
        {
            c.Table(outer =>
            {
                outer.Columns(cols =>
                {
                    cols.Fixed(100.Mm());
                    cols.Relative(1);
                });
                outer.Row(r =>
                {
                    r.Cell(cell => cell.Table(inner =>
                    {
                        inner.Columns(cols =>
                        {
                            cols.Relative(1);
                            cols.Relative(1);
                        });
                        inner.Row(ir =>
                        {
                            ir.Cell("a");
                            ir.Cell("b");
                        });
                    }));
                    r.Cell("right");
                });
            });
            c.Table(second =>
            {
                second.Columns(cols => cols.Relative(1));
                second.Row(r => r.Cell("second table"));
            });
        }));

        (GeneratedTemplate template, _) = BuilderHarness.BuildAndFill(definition, format, "{}");
        byte[] package = template.Content.ToArray();

        // 100 mm = 283.465 pt, minus 2 × 5.4 pt default padding = 272.665 pt, halved.
        if (format == TemplateFormat.Docx)
        {
            XElement body = BuilderHarness.ReadXml(package, "word/document.xml").Root!.Element(_w + "body")!;
            XElement inner = body.Descendants(_w + "tbl").Skip(1).First();
            Assert.Equal(new[] { "2727", "2727" }, inner.Element(_w + "tblGrid")!.Elements(_w + "gridCol").Select(g => g.Attribute(_w + "w")!.Value));
            Assert.Equal(new[] { "tbl", "p", "tbl", "sectPr" }, body.Elements().Select(e => e.Name.LocalName));
        }
        else
        {
            XDocument content = BuilderHarness.ReadXml(package, "content.xml");
            Dictionary<string, XElement> styles = content.Root!.Element(_office + "automatic-styles")!.Elements(_style + "style")
                .ToDictionary(s => s.Attribute(_style + "name")!.Value);
            XElement inner = content.Descendants(_table + "table").Skip(1).First();
            Assert.Equal(
                new[] { "136.333pt", "136.332pt" },
                inner.Elements(_table + "table-column").Select(c => styles[c.Attribute(_table + "style-name")!.Value].Descendants().Single().Attribute(_style + "column-width")!.Value));
        }
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Contract_IncludesCellPaths(TemplateFormat format)
    {
        Assert.Equal(
            new[] { "order.amount", "order.express", "order.item", "order.total" },
            BuilderHarness.Build(Invoice(), format).Contract.Entries.Select(e => e.Path));
    }

    [Fact]
    public void Errors_AreReported()
    {
        TestDefinition noColumns = new TestDefinition(t => t.Content(c => c.Table(tb => tb.Row(r => r.Cell("x")))));
        TestDefinition tooWide = new TestDefinition(t => t.Content(c => c.Table(tb =>
        {
            tb.Columns(cols =>
            {
                cols.Fixed(100.Mm());
                cols.Fixed(100.Mm());
            });
            tb.Row(r =>
            {
                r.Cell("a");
                r.Cell("b");
            });
        })));
        TestDefinition wrongCells = new TestDefinition(t => t.Content(c => c.Table(tb =>
        {
            tb.Columns(cols =>
            {
                cols.Relative(1);
                cols.Relative(1);
            });
            tb.Row(r => r.Cell("only one"));
        })));
        TestDefinition undefinedStyle = new TestDefinition(t => t.Content(c => c.Table(tb =>
        {
            tb.Style(_grid);
            tb.Columns(cols => cols.Relative(1));
            tb.Row(r => r.Cell("x"));
        })));

        Assert.Equal("TB7001", Single(noColumns));
        Assert.Equal("TB7002", Single(tooWide));
        Assert.Equal("TB7003", Single(wrongCells));
        Assert.Equal("TB2010", Single(undefinedStyle));
        Assert.Throws<InvalidOperationException>(() => BuilderHarness.Build(new TestDefinition(t => t.Content(c => c.Table(tb =>
        {
            tb.Row(r => r.Cell("x"));
            tb.HeaderRow(r => r.Cell("late header"));
        }))), TemplateFormat.Docx));
        Assert.Throws<ArgumentOutOfRangeException>(() => BuilderHarness.Build(new TestDefinition(t => t.Content(c => c.Table(tb =>
            tb.Row(r => r.Cell(cell => cell.ColumnSpan(0)))))), TemplateFormat.Docx));

        static string Single(TestDefinition definition) =>
            Assert.Single(Assert.Throws<TemplateBuildException>(() => BuilderHarness.Build(definition, TemplateFormat.Odt)).Errors).Code;
    }

    [Theory]
    [MemberData(nameof(Formats))]
    [Trait("Category", "LibreOffice")]
    public void LibreOffice_RepeatsTheHeaderRowAndPlacesTheColumns(TemplateFormat format)
    {
        LibreOfficeRunner.RequireExecutable();
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Invoice(extraRows: 60), format, OrderJson);
        byte[] pdf = LibreOfficeRunner.Convert(filled, template.FileExtension.TrimStart('.'), "pdf", "pdf");

        string page2 = Pdf(pdf, 2, "-layout");
        Assert.Contains("Description", page2, StringComparison.Ordinal);

        // Column starts: frame starts at 70.866 pt; column 2 at + 42.52 pt; column 3 at + 274.016 pt more; plus 4 pt padding.
        Dictionary<string, double> starts = WordStarts(Pdf(pdf, 1, "-bbox"));
        Assert.InRange(starts["Description"], 70.866 + 42.52 + 4 - 1.5, 70.866 + 42.52 + 4 + 1.5);
        Assert.InRange(starts["Amount"], 70.866 + 42.52 + 274.016 + 4 - 1.5, 70.866 + 42.52 + 274.016 + 4 + 1.5);
    }

    /// <summary>Rows of every top-level table: cell texts, paragraphs joined with a newline.</summary>
    private static List<string[][]> TableTexts(TemplateFormat format, byte[] package)
    {
        if (format == TemplateFormat.Docx)
        {
            return BuilderHarness.ReadXml(package, "word/document.xml").Root!.Element(_w + "body")!.Elements(_w + "tbl")
                .Select(t => t.Elements(_w + "tr").Select(r => r.Elements(_w + "tc")
                    .Select(c => string.Join("\n", c.Elements(_w + "p").Select(p => string.Concat(p.Descendants(_w + "t").Select(x => x.Value))))).ToArray()).ToArray())
                .ToList();
        }

        XElement body = BuilderHarness.ReadXml(package, "content.xml").Descendants(_office + "text").First();
        return body.Elements(_table + "table")
            .Select(t => t.Descendants(_table + "table-row").Select(r => r.Elements(_table + "table-cell")
                .Select(c => string.Join("\n", c.Elements(_text + "p").Select(OdtDocumentVerifier.RenderText))).ToArray()).ToArray())
            .ToList();
    }

    private static Dictionary<string, double> WordStarts(string bbox)
    {
        Dictionary<string, double> starts = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (Match match in WordBox().Matches(bbox))
        {
            starts.TryAdd(match.Groups[2].Value, double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
        }

        return starts;
    }

    [GeneratedRegex("xMin=\"([0-9.]+)\"[^>]*>([^<]+)</word>")]
    private static partial Regex WordBox();

    private static string Pdf(byte[] pdf, int page, string mode)
    {
        string? executable = new[] { "/opt/homebrew/bin/pdftotext", "/usr/local/bin/pdftotext", "/usr/bin/pdftotext" }.FirstOrDefault(File.Exists);
        Assert.SkipWhen(executable == null, "pdftotext (poppler) is not installed.");
        string directory = Path.Combine(Path.GetTempPath(), "templify-pdf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string input = Path.Combine(directory, "document.pdf");
            File.WriteAllBytes(input, pdf);
            ProcessStartInfo start = new ProcessStartInfo(executable!) { RedirectStandardOutput = true, UseShellExecute = false };
            foreach (string argument in new[] { "-f", page.ToString(CultureInfo.InvariantCulture), "-l", page.ToString(CultureInfo.InvariantCulture), mode, input, "-" })
            {
                start.ArgumentList.Add(argument);
            }

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
