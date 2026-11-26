// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>
/// Containers stay well-formed once Templify removes the markers (#291): a cell or box still ends with a paragraph,
/// and a table never touches the next one.
/// </summary>
public sealed class RenderSafetyTests
{
    private static readonly XNamespace _w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace _table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace _text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace _office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly BoxStyleKey _box = new BoxStyleKey("Framed");

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private static StyleSheet Sheet() => StyleSheet.Create(s => s.Box(_box, b => b.Border(1.Pt(), Color.FromHex("000000"))));

    private static void Inner(BlockComposer c) => c.Table(t =>
    {
        t.Columns(cols => cols.Relative(1));
        t.Row(r => r.Cell("inner"));
    });

    private static XElement Body(TemplateFormat format, byte[] package) =>
        format == TemplateFormat.Docx
            ? BuilderHarness.ReadXml(package, "word/document.xml").Descendants(_w + "body").Single()
            : BuilderHarness.ReadXml(package, "content.xml").Descendants(_office + "text").Single();

    private static XName TableName(TemplateFormat format) => format == TemplateFormat.Docx ? _w + "tbl" : _table + "table";

    private static XName ParagraphName(TemplateFormat format) => format == TemplateFormat.Docx ? _w + "p" : _text + "p";

    private static XElement FirstCell(TemplateFormat format, XElement table) =>
        format == TemplateFormat.Docx ? table.Descendants(_w + "tc").First() : table.Descendants(_table + "table-cell").First();

    /// <summary>The last content element of a cell (DOCX: not the cell properties).</summary>
    private static XElement LastContent(XElement cell) => cell.Elements().Last(e => e.Name.LocalName is not "tcPr");

    [Theory]
    [MemberData(nameof(Formats))]
    public void CellEndingWithAConditional_StillEndsWithAParagraph(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Table(tb =>
        {
            tb.Columns(cols => cols.Relative(1));
            tb.Row(r => r.Cell(cell =>
            {
                Inner(cell);
                cell.If(t.Data["flag"], b => b.Paragraph("only when flagged"));
            }));
        })));

        (_, byte[] filled) = BuilderHarness.BuildAndFill(definition, format, """{ "flag": false }""");

        XElement outer = Body(format, filled).Elements(TableName(format)).Single();
        Assert.Equal(ParagraphName(format), LastContent(FirstCell(format, outer)).Name);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void BoxEndingWithAnEmptyLoop_StillEndsWithAParagraph(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(t =>
        {
            t.Styles(Sheet());
            t.Content(c => c.Box(_box, b =>
            {
                Inner(b);
                b.ForEach(t.Data["items"], "item", (item, body) => body.Paragraph(item["name"]));
            }));
        });

        (_, byte[] filled) = BuilderHarness.BuildAndFill(definition, format, """{ "items": [] }""");

        XElement box = Body(format, filled).Elements(TableName(format)).Single();
        Assert.Equal(ParagraphName(format), LastContent(FirstCell(format, box)).Name);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void TablesAroundARemovedConditional_StayApart(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c =>
        {
            Inner(c);
            c.If(t.Data["flag"], b => b.Paragraph("between"));
            Inner(c);
        }));

        (_, byte[] filled) = BuilderHarness.BuildAndFill(definition, format, """{ "flag": false }""");

        List<XElement> children = Body(format, filled).Elements().Where(e => e.Name == TableName(format) || e.Name == ParagraphName(format)).ToList();
        int first = children.FindIndex(e => e.Name == TableName(format));
        Assert.Equal(ParagraphName(format), children[first + 1].Name);
        Assert.Equal(TableName(format), children[first + 2].Name);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void NoSpacer_WhenNothingNeedsOne(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c =>
        {
            c.Table(tb =>
            {
                tb.Columns(cols => cols.Relative(1));
                tb.Row(r => r.Cell(cell => cell.If(t.Data["flag"], b => b.Paragraph("x")).Else(b => b.Paragraph("y"))));
            });
            c.Paragraph("after");
        }));

        GeneratedTemplate template = BuilderHarness.Build(definition, format);
        string styles = BuilderHarness.ReadXml(template.Content.ToArray(), format == TemplateFormat.Docx ? "word/styles.xml" : "styles.xml").ToString();
        string withSpacer = BuilderHarness.ReadXml(BuilderHarness.Build(new TestDefinition(t => t.Content(c =>
        {
            Inner(c);
            c.If(t.Data["flag"], b => b.Paragraph("x"));
        })), format).Content.ToArray(), format == TemplateFormat.Docx ? "word/styles.xml" : "styles.xml").ToString();

        Assert.Contains("TemplifyMarker", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("TemplifySpacer", styles, StringComparison.Ordinal);
        Assert.Contains("TemplifySpacer", withSpacer, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void RawBlocks_CountByTheirRoot(TemplateFormat format)
    {
        RawBlock paragraph = new RawBlock
        {
            Docx = _ => new XElement(_w + "p", new XElement(_w + "r", new XElement(_w + "t", "raw paragraph"))),
            Odt = _ => new XElement(_text + "p", "raw paragraph"),
        };
        RawBlock table = new RawBlock
        {
            Docx = ctx => new XElement(_w + "tbl", new XElement(_w + "tblGrid", new XElement(_w + "gridCol", new XAttribute(_w + "w", "2000"))), new XElement(_w + "tr", new XElement(_w + "tc", new XElement(_w + "p")))),
            Odt = _ => new XElement(_table + "table", new XElement(_table + "table-column"), new XElement(_table + "table-row", new XElement(_table + "table-cell", new XElement(_text + "p")))),
        };
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Table(tb =>
        {
            tb.Columns(cols =>
            {
                cols.Relative(1);
                cols.Relative(1);
            });
            tb.Row(r =>
            {
                r.Cell(cell => cell.Raw(paragraph));
                r.Cell(cell => cell.Raw(table));
            });
        })));

        GeneratedTemplate template = BuilderHarness.Build(definition, format);

        XElement outer = Body(format, template.Content.ToArray()).Elements(TableName(format)).Single();
        List<XElement> cells = (format == TemplateFormat.Docx ? outer.Elements(_w + "tr").Single().Elements(_w + "tc") : outer.Elements(_table + "table-row").Single().Elements(_table + "table-cell")).ToList();
        Assert.Single(cells[0].Elements(ParagraphName(format)));
        Assert.Equal(ParagraphName(format), LastContent(cells[1]).Name);
        Assert.Contains("TemplifySpacer", LastContent(cells[1]).ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void NestedBoxEndingWithAConditional_BothCellsEndWithAParagraph(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(t =>
        {
            t.Styles(Sheet());
            t.Content(c => c.Box(_box, outer => outer.Box(_box, inner =>
            {
                Inner(inner);
                inner.If(t.Data["flag"], b => b.Paragraph("x"));
            })));
        });

        (_, byte[] filled) = BuilderHarness.BuildAndFill(definition, format, """{ "flag": false }""");

        XElement outerBox = Body(format, filled).Elements(TableName(format)).Single();
        XElement outerCell = FirstCell(format, outerBox);
        XElement innerBox = outerCell.Elements(TableName(format)).Single();
        Assert.Equal(ParagraphName(format), LastContent(outerCell).Name);
        Assert.Equal(ParagraphName(format), LastContent(FirstCell(format, innerBox)).Name);
    }

    [Fact]
    public void HeaderWithTablesAroundAConditional_StaysApartAndEndsWithAParagraph()
    {
        TestDefinition definition = new TestDefinition(t =>
        {
            t.Page(p => p.Header(h =>
            {
                Inner(h);
                h.If(t.Data["flag"], b => b.Paragraph("between"));
                Inner(h);
                h.If(t.Data["flag"], b => b.Paragraph("after"));
            }));
            t.Content(c => c.Paragraph("body"));
        });

        (_, byte[] filled) = BuilderHarness.BuildAndFill(definition, TemplateFormat.Docx, """{ "flag": false }""");

        List<XElement> header = BuilderHarness.ReadXml(filled, "word/header1.xml").Root!.Elements().ToList();
        Assert.Equal(ParagraphName(TemplateFormat.Docx), header[^1].Name);
        Assert.DoesNotContain(header.Zip(header.Skip(1)), pair => pair.First.Name == _w + "tbl" && pair.Second.Name == _w + "tbl");
    }

    [Fact]
    public void CommaInAStyleName_IsTB2006()
    {
        ParagraphStyleKey key = new ParagraphStyleKey("Body", "Body, main");
        TestDefinition definition = new TestDefinition(t =>
        {
            t.Styles(StyleSheet.Create(s => s.Paragraph(key, p => p.Bold())));
            t.Content(c => c.Paragraph(key, "x"));
        });

        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => BuilderHarness.Build(definition, TemplateFormat.Docx)).Errors);
        Assert.Equal("TB2006", error.Code);
        Assert.Contains("Body, main", error.Message, StringComparison.Ordinal);
    }
}
