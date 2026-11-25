// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using System.Xml.Linq;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;
using TriasDev.Templify.Tests.Odt;

namespace TriasDev.Templify.Builder.Tests;

public sealed class RowLoopTests
{
    private static readonly XNamespace _w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace _table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace _text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace _office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private static TestDefinition Invoice() => new TestDefinition(t =>
    {
        TemplatePath order = t.Data["order"];
        t.Content(c => c.Table(tb =>
        {
            tb.Columns(cols =>
            {
                cols.Relative(3);
                cols.Relative(1);
            });
            tb.HeaderRow(r =>
            {
                r.Cell("Item");
                r.Cell("Amount");
            });
            tb.ForEachRow(order["lines"], "line", (line, rows) =>
            {
                rows.Row(r =>
                {
                    r.Cell(cell =>
                    {
                        cell.Paragraph(x =>
                        {
                            x.Value(line.Number);
                            x.Text(". ");
                            x.Value(line["description"]);
                        });
                        cell.ForEach(line["tags"], "tag", (tag, tb2) => tb2.Paragraph(x =>
                        {
                            x.Text("#");
                            x.Value(tag.Self);
                        }));
                    });
                    r.Cell(line["amount"]);
                });
                rows.RowsIf(line["discount"], d => d.Row(r =>
                {
                    r.Cell("  discount");
                    r.Cell(line["discount"]);
                }));
            });
            tb.RowsIf(order["vat"].Gt(0), v => v.Row(r =>
            {
                r.Cell("VAT");
                r.Cell(order["vat"]);
            })).Else(e => e.Row(r =>
            {
                r.Cell("no VAT");
                r.Cell("–");
            }));
            tb.Row(r =>
            {
                r.Cell("Total");
                r.Cell(order["total"]);
            });
        }));
    });

    private const string OrderJson = """
    {
      "order": {
        "lines": [
          { "description": "Laptop", "amount": "1499", "tags": [ "hw", "3y" ], "discount": "-50" },
          { "description": "Dock", "amount": "175", "tags": [ "acc" ], "discount": "" }
        ],
        "vat": 0,
        "total": "1624"
      }
    }
    """;

    [Theory]
    [MemberData(nameof(Formats))]
    public void RowLoop_WithConditionalRowsAndCellLoops_IsFilled(TemplateFormat format)
    {
        (_, byte[] filled) = BuilderHarness.BuildAndFill(Invoice(), format, OrderJson);

        Assert.Equal(
            new[]
            {
                "Item | Amount",
                "1. Laptop\n#hw\n#3y | 1499",
                "  discount | -50",
                "2. Dock\n#acc | 175",
                "no VAT | –",
                "Total | 1624",
            },
            Rows(format, filled));
    }

    [Fact]
    public void Odt_RowLoopUnderAHeader_DoesNotRepeatTheHeaderRule()
    {
        XNamespace style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
        XNamespace fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
        TableStyleKey grid = new TableStyleKey("Grid");
        TestDefinition definition = new TestDefinition(t =>
        {
            t.Styles(StyleSheet.Create(s => s.Table(grid, g => g.HeaderRule(1.5.Pt(), Color.FromHex("1F4E79")).RowRule(0.5.Pt(), Color.FromHex("D4D4D4")))));
            t.Content(c => c.Table(tb =>
            {
                tb.Style(grid);
                tb.Columns(cols => cols.Relative(1));
                tb.HeaderRow(r => r.Cell("Item"));
                tb.ForEachRow(t.Data["lines"], "line", (line, rows) => rows.Row(r => r.Cell(line["name"])));
            }));
        });

        (_, byte[] filled) = BuilderHarness.BuildAndFill(definition, TemplateFormat.Odt, """{ "lines": [ { "name": "a" }, { "name": "b" }, { "name": "c" } ] }""");

        XDocument content = BuilderHarness.ReadXml(filled, "content.xml");
        Dictionary<string, XElement> styles = content.Descendants(style + "style").ToDictionary(e => e.Attribute(style + "name")!.Value);
        string? Border(XElement cell, string side) =>
            styles[cell.Attribute(_table + "style-name")!.Value].Element(style + "table-cell-properties")?.Attribute(fo + "border-" + side)?.Value;

        XElement header = content.Descendants(_table + "table-header-rows").Single().Descendants(_table + "table-cell").Single();
        Assert.Equal("1.5pt solid #1f4e79", Border(header, "bottom"));
        XElement[] body = content.Descendants(_table + "table").Single().Elements(_table + "table-row").Select(r => r.Elements(_table + "table-cell").Single()).ToArray();
        Assert.Equal(3, body.Length);
        // Every clone's top is the row rule; the header's wider bottom wins the first edge (collapsing model).
        Assert.All(body, cell => Assert.Equal("0.5pt solid #d4d4d4", Border(cell, "top")));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void RowLoop_EmptyCollection_LeavesNoRows(TemplateFormat format)
    {
        GeneratedTemplate template = BuilderHarness.Build(Invoice(), format);
        using Stream input = template.OpenRead();
        using MemoryStream output = new MemoryStream();
        ProcessingResult result = new TemplateProcessor(new PlaceholderReplacementOptions { WarnOnEmptyLoopCollections = false })
            .ProcessTemplate(input, output, """{ "order": { "lines": [], "vat": 19, "total": "0" } }""");

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new[] { "Item | Amount", "VAT | 19", "Total | 0" }, Rows(format, output.ToArray()));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void MarkerRows_SpanTheWholeGrid(TemplateFormat format)
    {
        byte[] package = BuilderHarness.Build(Invoice(), format).Content.ToArray();

        List<string> rows = Rows(format, package);
        Assert.Contains("{{#foreach line in order.lines}}", rows);
        Assert.Contains("{{#if line.discount}}", rows);
        Assert.Contains("{{#if order.vat > 0}}", rows);
        Assert.Contains("{{#else}}", rows);
        Assert.Equal(2, rows.Count(r => r == "{{/if}}"));

        if (format == TemplateFormat.Docx)
        {
            XElement marker = BuilderHarness.ReadXml(package, "word/document.xml").Descendants(_w + "tr")
                .First(r => r.Value == "{{#foreach line in order.lines}}");
            Assert.Equal("2", marker.Descendants(_w + "gridSpan").Single().Attribute(_w + "val")!.Value);
        }
        else
        {
            XElement marker = BuilderHarness.ReadXml(package, "content.xml").Descendants(_table + "table-row")
                .First(r => r.Value == "{{#foreach line in order.lines}}");
            Assert.Equal("2", marker.Element(_table + "table-cell")!.Attribute(_table + "number-columns-spanned")!.Value);
            Assert.Single(marker.Elements(_table + "covered-table-cell"));
        }
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void NestedRowLoops_AndTablesInsideParagraphLoops(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c =>
        {
            c.Table(tb =>
            {
                tb.Columns(cols =>
                {
                    cols.Relative(1);
                    cols.Relative(1);
                });
                tb.ForEachRow(t.Data["groups"], "group", (group, rows) =>
                {
                    rows.Row(r => r.Cell(cell => cell.ColumnSpan(2).Paragraph(group["name"])));
                    rows.ForEachRow(group["items"], "item", (item, inner) => inner.Row(r =>
                    {
                        r.Cell(item["name"]);
                        r.Cell(group["name"]);
                    }));
                });
            });
            c.ForEach(t.Data["groups"], "g", (g, b) =>
            {
                b.Paragraph(g["name"]);
                b.Table(tb =>
                {
                    tb.Columns(cols => cols.Relative(1));
                    tb.ForEachRow(g["items"], "it", (it, rows) => rows.Row(r => r.Cell(it["name"])));
                });
            });
        }));
        string json = """{ "groups": [ { "name": "A", "items": [ { "name": "a1" }, { "name": "a2" } ] }, { "name": "B", "items": [ { "name": "b1" } ] } ] }""";

        (_, byte[] filled) = BuilderHarness.BuildAndFill(definition, format, json);

        List<string> rows = Rows(format, filled);
        Assert.Equal(new[] { "A", "a1 | A", "a2 | A", "B", "b1 | B", "a1", "a2", "b1" }, rows);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Contract_IncludesRowLoopsAndRowConditions(TemplateFormat format)
    {
        Assert.Equal(
            new[]
            {
                "order.lines (Collection)", "order.lines[].amount (Value)", "order.lines[].description (Value)",
                "order.lines[].discount (Value)", "order.lines[].discount (Condition)", "order.lines[].tags (Collection)",
                "order.lines[].tags[] (Value)", "order.total (Value)", "order.vat (Value)", "order.vat (Condition)",
            },
            BuilderHarness.Build(Invoice(), format).Contract.Entries.Select(e => $"{e.Path} ({e.Usage})"));
    }

    [Fact]
    public void Scope_And_CellCount_AreChecked()
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Table(tb =>
        {
            LoopItem? escaped = null;
            tb.Columns(cols =>
            {
                cols.Relative(1);
                cols.Relative(1);
            });
            tb.ForEachRow(t.Data["lines"], "line", (line, rows) =>
            {
                escaped = line;
                rows.Row(r => r.Cell("only one cell"));
            });
            tb.Row(r =>
            {
                r.Cell(escaped!["name"]);
                r.Cell("x");
            });
        })));

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(() => BuilderHarness.Build(definition, TemplateFormat.Docx));

        Assert.Equal(new[] { "TB4001", "TB7003" }, exception.Errors.Select(e => e.Code));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    [Trait("Category", "LibreOffice")]
    public void LibreOffice_RendersTheFilledRows(TemplateFormat format)
    {
        LibreOfficeRunner.RequireExecutable();
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Invoice(), format, OrderJson);

        string text = Encoding.UTF8.GetString(LibreOfficeRunner.Convert(filled, template.FileExtension.TrimStart('.'), "txt:Text (encoded):UTF8", "txt"));

        foreach (string expected in new[] { "1. Laptop", "#3y", "discount", "2. Dock", "no VAT", "Total" })
        {
            Assert.Contains(expected, text, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("{{", text, StringComparison.Ordinal);
    }

    /// <summary>Every table row of the body (nested tables' rows included in order), cells joined with " | ".</summary>
    private static List<string> Rows(TemplateFormat format, byte[] package)
    {
        if (format == TemplateFormat.Docx)
        {
            return BuilderHarness.ReadXml(package, "word/document.xml").Descendants(_w + "tr")
                .Select(r => string.Join(" | ", r.Elements(_w + "tc").Select(c =>
                    string.Join("\n", c.Elements(_w + "p").Select(p => string.Concat(p.Descendants(_w + "t").Select(x => x.Value)))))))
                .ToList();
        }

        return BuilderHarness.ReadXml(package, "content.xml").Descendants(_office + "text").First().Descendants(_table + "table-row")
            .Select(r => string.Join(" | ", r.Elements(_table + "table-cell").Select(c =>
                string.Join("\n", c.Elements(_text + "p").Select(OdtDocumentVerifier.RenderText)))))
            .ToList();
    }
}
