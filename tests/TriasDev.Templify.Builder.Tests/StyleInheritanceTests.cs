// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Json;
using TriasDev.Templify.Core;
using TriasDev.Templify.Builder.Testing;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>Inherited paragraph styles: the style sheet's default, table text styles and a row's text style.</summary>
public sealed class StyleInheritanceTests
{
    private static readonly ParagraphStyleKey _body = new ParagraphStyleKey("Body", "Body Text Style");
    private static readonly ParagraphStyleKey _heading = new ParagraphStyleKey("Heading", "Heading Style");
    private static readonly ParagraphStyleKey _cell = new ParagraphStyleKey("CellText", "Cell Text");
    private static readonly ParagraphStyleKey _head = new ParagraphStyleKey("HeadText", "Head Text");
    private static readonly ParagraphStyleKey _total = new ParagraphStyleKey("TotalText", "Total Text");
    private static readonly ParagraphStyleKey _other = new ParagraphStyleKey("OtherText", "Other Text");
    private static readonly ParagraphStyleKey _inner = new ParagraphStyleKey("InnerText", "Inner Text");
    private static readonly ParagraphStyleKey _item = new ParagraphStyleKey("ItemText", "Item Text");
    private static readonly TableStyleKey _grid = new TableStyleKey("Grid");
    private static readonly TableStyleKey _innerGrid = new TableStyleKey("InnerGrid");
    private static readonly BoxStyleKey _note = new BoxStyleKey("Note");
    private static readonly ListStyleKey _bullets = new ListStyleKey("Bullets");

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private static StyleSheet Sheet(bool withDefault = true, bool withTableText = true) => StyleSheet.Create(s =>
    {
        if (withDefault)
        {
            s.Defaults(d => d.Paragraph(_body));
        }

        foreach (ParagraphStyleKey key in new[] { _body, _heading, _cell, _head, _total, _other, _inner, _item })
        {
            s.Paragraph(key, p => p.SpaceAfter(1.Pt()));
        }

        s.Table(_grid, t =>
        {
            t.RowRule(0.5.Pt(), Color.FromHex("D4D4D4"));
            if (withTableText)
            {
                t.TextStyle(_cell).HeaderTextStyle(_head);
            }
        });
        s.Table(_innerGrid, t => t.TextStyle(_inner));
        s.Box(_note, b => b.Background(Color.FromHex("EEEEEE")));
        s.List(_bullets, l => l.Level(0, "•", 6.Mm(), 4.Mm()).ItemStyle(_item));
    });

    private static string Snapshot(ITemplateDefinition definition, TemplateFormat format)
    {
        GeneratedTemplate template = BuilderHarness.Build(definition, format);
        BuilderHarness.AssertInvariants(definition, template);
        return TemplateSnapshot.Create(template);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void UnstyledParagraph_GetsTheDefault(TemplateFormat format)
    {
        string snapshot = Snapshot(new TestDefinition(t =>
        {
            t.Styles(Sheet());
            t.Content(c => c.Paragraph("plain"));
        }), format);

        Assert.Contains("Body | plain", snapshot, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void ExplicitStyle_Wins(TemplateFormat format)
    {
        string snapshot = Snapshot(new TestDefinition(t =>
        {
            t.Styles(Sheet());
            t.Content(c => c.Paragraph(_heading, "title"));
        }), format);

        Assert.Contains("Heading | title", snapshot, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void NoDefault_StaysUnstyled(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(t =>
        {
            t.Styles(Sheet(withDefault: false));
            t.Content(c => c.Paragraph("plain"));
        });
        GeneratedTemplate template = BuilderHarness.Build(definition, format);
        BuilderHarness.AssertInvariants(definition, template);

        string part = BuilderHarness.ReadXml(template.Content.ToArray(), BuilderHarness.MainPart(format)).ToString();
        Assert.DoesNotContain("\"Body\"", part, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void TableCells_UseTextStyles(TemplateFormat format)
    {
        string snapshot = Snapshot(new TestDefinition(t =>
        {
            t.Styles(Sheet());
            t.Content(c => c.Table(tb =>
            {
                tb.Style(_grid).Columns(cols =>
                {
                    cols.Relative(1);
                    cols.Relative(1);
                });
                tb.HeaderRow(r =>
                {
                    r.Cell("h1");
                    r.Cell("h2");
                });
                tb.Row(r =>
                {
                    r.Cell("b1");
                    r.Cell(_other, "b2");
                });
                tb.Row(r =>
                {
                    r.TextStyle(_total);
                    r.Cell("t1");
                    r.Cell("t2");
                });
            }));
        }), format);

        Assert.Contains("HeadText | h1", snapshot, StringComparison.Ordinal);
        Assert.Contains("CellText | b1", snapshot, StringComparison.Ordinal);
        Assert.Contains("OtherText | b2", snapshot, StringComparison.Ordinal);
        Assert.Contains("TotalText | t1", snapshot, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void NestedTable_UsesItsOwnStyle(TemplateFormat format)
    {
        string snapshot = Snapshot(new TestDefinition(t =>
        {
            t.Styles(Sheet());
            t.Content(c => c.Table(tb =>
            {
                tb.Style(_grid).Columns(cols => cols.Relative(1));
                tb.Row(r => r.Cell(cell =>
                {
                    cell.Paragraph("outer");
                    cell.Table(inner =>
                    {
                        inner.Style(_innerGrid).Columns(cols => cols.Relative(1));
                        inner.Row(ir => ir.Cell("nested"));
                    });
                }));
            }));
        }), format);

        Assert.Contains("CellText | outer", snapshot, StringComparison.Ordinal);
        Assert.Contains("InnerText | nested", snapshot, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void BoxInCell_GetsTheDocumentDefault(TemplateFormat format)
    {
        string snapshot = Snapshot(new TestDefinition(t =>
        {
            t.Styles(Sheet());
            t.Content(c => c.Table(tb =>
            {
                tb.Style(_grid).Columns(cols => cols.Relative(1));
                tb.Row(r => r.Cell(cell => cell.Box(_note, b => b.Paragraph("boxed"))));
            }));
        }), format);

        Assert.Contains("Body | boxed", snapshot, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void ConditionalsAndLoops_Inherit(TemplateFormat format)
    {
        string snapshot = Snapshot(new TestDefinition(t =>
        {
            TemplatePath order = t.Data["order"];
            t.Styles(Sheet());
            t.Content(c =>
            {
                c.If(order["flag"], b => b.Paragraph("in if"));
                c.Table(tb =>
                {
                    tb.Style(_grid).Columns(cols => cols.Relative(1));
                    tb.Row(r => r.Cell(cell => cell.If(order["flag"], b => b.Paragraph("in cell if"))));
                });
            });
        }), format);

        Assert.Contains("Body | in if", snapshot, StringComparison.Ordinal);
        Assert.Contains("CellText | in cell if", snapshot, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void ListItems_KeepTheirItemStyle(TemplateFormat format)
    {
        string snapshot = Snapshot(new TestDefinition(t =>
        {
            t.Styles(Sheet());
            t.Content(c => c.List(_bullets, l => l.Item("first")));
        }), format);

        Assert.Contains("ItemText [list 1] | first", snapshot, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void HeaderAndFooter_GetTheDefault(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(t =>
        {
            t.Styles(Sheet());
            t.Page(p => p.Header(h => h.Paragraph("running head")));
            t.Content(c => c.Paragraph(_heading, "body"));
        });
        GeneratedTemplate template = BuilderHarness.Build(definition, format);
        BuilderHarness.AssertInvariants(definition, template);

        string part = format == TemplateFormat.Docx
            ? BuilderHarness.ReadXml(template.Content.ToArray(), "word/header1.xml").ToString()
            : BuilderHarness.ReadXml(template.Content.ToArray(), "styles.xml").ToString();
        Assert.Matches("(w:val|text:style-name)=\"Body\"[^<]*>(.|\\n)*running head", part);
    }

    [Fact]
    public void UndefinedDefault_IsTB2010()
    {
        ParagraphStyleKey missing = new ParagraphStyleKey("Missing", "Missing Style");
        TestDefinition definition = new TestDefinition(t =>
        {
            t.Styles(StyleSheet.Create(s =>
            {
                s.Defaults(d => d.Paragraph(missing));
                s.Paragraph(_heading, p => p.Bold());
            }));
            t.Content(c => c.Paragraph(_heading, "styled only"));
        });

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(() => BuilderHarness.Build(definition, TemplateFormat.Docx));
        BuildDiagnostic error = Assert.Single(exception.Errors);
        Assert.Equal("TB2010", error.Code);
        Assert.Contains("Missing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UndefinedDefault_UsedByParagraphs_IsReportedOnce()
    {
        ParagraphStyleKey missing = new ParagraphStyleKey("Missing", "Missing Style");
        TestDefinition definition = new TestDefinition(t =>
        {
            t.Styles(StyleSheet.Create(s => s.Defaults(d => d.Paragraph(missing))));
            t.Content(c =>
            {
                c.Paragraph("a");
                c.Paragraph("b");
            });
        });

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(() => BuilderHarness.Build(definition, TemplateFormat.Docx));
        Assert.Equal("TB2010", Assert.Single(exception.Errors).Code);
    }

    [Fact]
    public void UndefinedTableTextStyle_IsTB2010()
    {
        ParagraphStyleKey missing = new ParagraphStyleKey("Missing", "Missing Style");
        TestDefinition definition = new TestDefinition(t =>
        {
            t.Styles(StyleSheet.Create(s => s.Table(_grid, tb => tb.HeaderTextStyle(missing))));
            t.Content(c => c.Paragraph("no table at all"));
        });

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(() => BuilderHarness.Build(definition, TemplateFormat.Docx));
        BuildDiagnostic error = Assert.Single(exception.Errors);
        Assert.Equal("TB2010", error.Code);
        Assert.Contains("Grid", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_CarriesTheInheritanceSettings()
    {
        TestDefinition definition = new TestDefinition(t =>
        {
            t.Styles(Sheet());
            t.Content(c => c.Table(tb =>
            {
                tb.Style(_grid).Columns(cols => cols.Relative(1));
                tb.Row(r =>
                {
                    r.TextStyle(_total);
                    r.Cell("t");
                });
            }));
        });
        TemplateBuildOptions options = BuilderHarness.Options(TemplateFormat.Docx);

        string json = TemplateJson.Export(definition, options).Json;
        Assert.Contains("\"paragraph\": \"Body\"", json, StringComparison.Ordinal);
        Assert.Contains("\"textStyle\": \"CellText\"", json, StringComparison.Ordinal);
        Assert.Contains("\"headerTextStyle\": \"HeadText\"", json, StringComparison.Ordinal);
        Assert.Contains("\"textStyle\": \"TotalText\"", json, StringComparison.Ordinal);
        BuilderHarness.AssertJsonRoundTrip(definition, TemplateBuilder.Build(definition, options), options);
    }

    [Fact]
    public void Json_UndefinedDefault_IsTB2010()
    {
        const string json = """
            {
              "templifyDefinition": 1,
              "identity": { "id": "t", "version": 1 },
              "language": "en-US",
              "styles": { "defaults": { "paragraph": "Nope" } },
              "content": [ "x" ]
            }
            """;

        TemplateBuildException exception = Assert.ThrowsAny<TemplateBuildException>(() =>
            TemplateBuilder.Build(TemplateJson.Import(json, new TemplateJsonImportOptions()), BuilderHarness.Options(TemplateFormat.Docx)));
        Assert.Equal("TB2010", Assert.Single(exception.Errors).Code);
    }
}
