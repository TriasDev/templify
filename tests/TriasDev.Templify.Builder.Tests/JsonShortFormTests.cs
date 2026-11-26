// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json;
using TriasDev.Templify.Builder.Json;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>The compact JSON forms: text with placeholders, short blocks, inherited styles (#274).</summary>
public sealed class JsonShortFormTests
{
    private static readonly CharacterStyleKey _strong = new CharacterStyleKey("Strong", "Strong Text");

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private static string Definition(string content, string styles = "{ \"character\": { \"Strong\": { \"name\": \"Strong Text\", \"bold\": true } } }") => $$"""
        {
          "templifyDefinition": 1,
          "identity": { "id": "short", "version": 1 },
          "language": "en-US",
          "styles": {{styles}},
          "content": {{content}}
        }
        """;

    private sealed record Order(string Number);

    private sealed record Data(Order Order);

    // ── text with placeholders ───────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Formats))]
    public void Import_StringWithPlaceholders(TemplateFormat format)
    {
        ITemplateDefinition imported = TemplateJson.Import(Definition("""[ { "paragraph": "Order {{order.number}} of {{order.date:date:yyyy}}" } ]"""));

        (_, byte[] filled) = BuilderHarness.BuildAndFill(imported, format, """{ "order": { "number": "42", "date": "2026-11-26" } }""");

        Assert.Equal(new[] { "Order 42 of 2026" }, BuilderHarness.ParagraphTexts(format, filled));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Import_StyledTextWithValue(TemplateFormat format)
    {
        ITemplateDefinition imported = TemplateJson.Import(Definition("""[ { "paragraph": [ "No. ", { "text": "{{order.number}}", "style": "Strong" } ] } ]"""));
        TestDefinition expected = new TestDefinition(t =>
        {
            t.Styles(StyleSheet.Create(s => s.Character(_strong, c => c.Bold())));
            t.Content(c => c.Paragraph(x =>
            {
                x.Text("No. ");
                x.Value(t.Data["order"]["number"], _strong);
            }));
        }, new TemplateIdentity("short", 1));

        Assert.Equal(BuilderHarness.Build(expected, format).ContentHash, BuilderHarness.Build(imported, format).ContentHash);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Import_Expression(TemplateFormat format)
    {
        ITemplateDefinition imported = TemplateJson.Import(Definition("""[ { "paragraph": "Free: {{(a and b):yesno}}" } ]"""));

        (_, byte[] filled) = BuilderHarness.BuildAndFill(imported, format, """{ "a": true, "b": false }""");

        Assert.Equal(new[] { "Free: No" }, BuilderHarness.ParagraphTexts(format, filled));
    }

    [Theory]
    [InlineData("""[ { "paragraph": "x {{order.number" } ]""", "/content/0/paragraph")]
    [InlineData("""[ { "paragraph": [ "ok", "x {{}}" ] } ]""", "/content/0/paragraph/1")]
    [InlineData("""[ { "paragraph": "{{(a and b)}}" } ]""", "/content/0/paragraph")]
    [InlineData("""[ { "paragraph": [ { "text": "{{(a):yesno}}", "style": "Strong" } ] } ]""", "/content/0/paragraph/0")]
    [InlineData("""[ { "paragraph": [ { "if": "a", "then": "{{(b):yesno}}" } ] } ]""", "/content/0/paragraph/0/then")]
    [InlineData("""[ { "paragraph": [ { "if": "a", "then": [ { "text": "{{v:number:F2}}", "style": "Strong" } ] } ] } ]""", "/content/0/paragraph/0/then/0")]
    [InlineData("""[ { "paragraph": [ { "value": "a" } ] } ]""", "/content/0/paragraph/0")]
    [InlineData("""[ { "paragraph": [ { "expression": "a", "format": "yesno" } ] } ]""", "/content/0/paragraph/0/expression")]
    public void Import_Errors(string content, string location)
    {
        IReadOnlyList<BuildDiagnostic> errors = Assert.Throws<TemplateBuildException>(() => TemplateJson.Import(Definition(content))).Errors;

        BuildDiagnostic error = Assert.Single(errors);
        Assert.Equal("TB9200", error.Code);
        Assert.Equal(location, error.Location);
    }

    [Fact]
    public void Import_UnknownPathInString_IsTB9203()
    {
        TemplateJsonImportOptions options = new TemplateJsonImportOptions { Data = DataCatalog.From<Data>() };

        IReadOnlyList<BuildDiagnostic> errors = Assert.Throws<TemplateBuildException>(() =>
            TemplateJson.Import(Definition("""[ { "paragraph": "{{order.number}} {{order.nmber}}" } ]"""), options)).Errors;

        BuildDiagnostic error = Assert.Single(errors);
        Assert.Equal("TB9203", error.Code);
        Assert.Equal("/content/0/paragraph", error.Location);
        Assert.Contains("order.nmber", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_MergesValuesIntoText()
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Paragraph(x =>
        {
            x.Text("Order ");
            x.Value(t.Data["order"]["number"]);
            x.Text(" of ");
            x.Value(t.Data["order"]["date"], ValueFormat.Date("yyyy"));
        })));

        Assert.Equal("Order {{order.number}} of {{order.date:date:yyyy}}", ContentOf(definition).GetString());
    }

    [Fact]
    public void Export_KeepsAdjacentTextsApart()
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Paragraph(x =>
        {
            x.Text("a");
            x.Text("b");
            x.Value(t.Data["v"]);
            x.Tab();
            x.Text("c");
        })));

        Assert.Equal("""["a","b{{v}}",{"tab":true},"c"]""", Compact(ContentOf(definition)));
        AssertRoundTrip(definition);
    }

    [Fact]
    public void Export_DoesNotMergeABraceIntoAPlaceholder()
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Paragraph(x =>
        {
            x.Text("set {");
            x.Value(t.Data["v"]);
            x.Text("}");
        })));

        Assert.Equal("""["set {","{{v}}}"]""", Compact(ContentOf(definition)));
        AssertRoundTrip(definition);
    }

    [Fact]
    public void Export_StyledRun()
    {
        TestDefinition definition = new TestDefinition(t =>
        {
            t.Styles(StyleSheet.Create(s => s.Character(_strong, c => c.Bold())));
            t.Content(c => c.Paragraph(x =>
            {
                x.Text("x", _strong);
                x.Value(t.Data["p"], _strong);
                x.Value(t.Data["q"]);
            }));
        });

        Assert.Equal("""[{"text":"x{{p}}","style":"Strong"},"{{q}}"]""", Compact(ContentOf(definition)));
        AssertRoundTrip(definition);
    }

    [Fact]
    public void Export_InlineConditionBranchesAreText()
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Paragraph(x =>
        {
            x.Value(t.Data["name"]);
            x.If(t.Data["isNew"], b => b.Text(" (new, ").Value(t.Data["since"]).Text(")")).Else(b => b.Text(""));
        })));

        Assert.Equal("""["{{name}}",{"if":"isNew","then":" (new, {{since}})","else":[]}]""", Compact(ContentOf(definition)));
        AssertRoundTrip(definition);
    }

    // ── block short forms ────────────────────────────────────────────────────

    private static readonly ParagraphStyleKey _body = new ParagraphStyleKey("Body", "Body Text Style");
    private static readonly ParagraphStyleKey _heading = new ParagraphStyleKey("Heading", "Heading Style");
    private static readonly ParagraphStyleKey _cell = new ParagraphStyleKey("CellText", "Cell Text");
    private static readonly ParagraphStyleKey _head = new ParagraphStyleKey("HeadText", "Head Text");
    private static readonly ParagraphStyleKey _total = new ParagraphStyleKey("TotalText", "Total Text");
    private static readonly ParagraphStyleKey _item = new ParagraphStyleKey("ItemText", "Item Text");
    private static readonly TableStyleKey _grid = new TableStyleKey("Grid");
    private static readonly BoxStyleKey _success = new BoxStyleKey("SuccessBox");
    private static readonly ListStyleKey _bullets = new ListStyleKey("Bullets");

    private const string ShortStyles = """
        {
          "defaults": { "paragraph": "Body" },
          "paragraph": {
            "Body": { "name": "Body Text Style" }, "Heading": { "name": "Heading Style" }, "CellText": { "name": "Cell Text" },
            "HeadText": { "name": "Head Text" }, "TotalText": { "name": "Total Text" }, "ItemText": { "name": "Item Text" }
          },
          "table": { "Grid": { "rowRule": "0.5pt #D4D4D4", "textStyle": "CellText", "headerTextStyle": "HeadText" } },
          "box": { "SuccessBox": { "background": "#EFF6F1" } },
          "list": { "Bullets": { "itemStyle": "ItemText", "levels": [ { "level": 0, "bullet": "•", "indent": "6mm", "hanging": "4mm" }, { "level": 1, "bullet": "–", "indent": "12mm", "hanging": "4mm" } ] } }
        }
        """;

    private const string ShortContent = """
        [
          "Plain block {{name}}",
          { "style": "Heading", "paragraph": "Title" },
          { "style": "SuccessBox", "box": [ "Paid" ] },
          { "style": "Bullets", "list": [ "first", { "level": 1, "item": "second {{name}}" } ] },
          { "style": "Grid", "columns": [ "20mm", 1 ], "table": [
            { "header": true, "row": [ "#", "Item" ] },
            { "row": [ "1", [ "a", { "style": "Heading", "paragraph": "b" } ] ] },
            { "textStyle": "TotalText", "row": [ { "span": 2, "content": "Total {{total}}" } ] }
          ] }
        ]
        """;

    private static TestDefinition ShortFormsInCSharp() => new TestDefinition(t =>
    {
        t.Styles(StyleSheet.Create(s =>
        {
            s.Defaults(d => d.Paragraph(_body));
            s.Paragraph(_body, p => { });
            s.Paragraph(_heading, p => { });
            s.Paragraph(_cell, p => { });
            s.Paragraph(_head, p => { });
            s.Paragraph(_total, p => { });
            s.Paragraph(_item, p => { });
            s.Table(_grid, g => g.RowRule(0.5.Pt(), Color.FromHex("D4D4D4")).TextStyle(_cell).HeaderTextStyle(_head));
            s.Box(_success, b => b.Background(Color.FromHex("EFF6F1")));
            s.List(_bullets, l => l.ItemStyle(_item).Level(0, "•", 6.Mm(), 4.Mm()).Level(1, "–", 12.Mm(), 4.Mm()));
        }));
        t.Content(c =>
        {
            c.Paragraph(x =>
            {
                x.Text("Plain block ");
                x.Value(t.Data["name"]);
            });
            c.Paragraph(_heading, "Title");
            c.Box(_success, b => b.Paragraph("Paid"));
            c.List(_bullets, l =>
            {
                l.Item("first");
                l.Item(1, x =>
                {
                    x.Text("second ");
                    x.Value(t.Data["name"]);
                });
            });
            c.Table(tb =>
            {
                tb.Style(_grid).Columns(cols =>
                {
                    cols.Fixed(20.Mm());
                    cols.Relative(1);
                });
                tb.HeaderRow(r =>
                {
                    r.Cell("#");
                    r.Cell("Item");
                });
                tb.Row(r =>
                {
                    r.Cell("1");
                    r.Cell(cell =>
                    {
                        cell.Paragraph("a");
                        cell.Paragraph(_heading, "b");
                    });
                });
                tb.Row(r =>
                {
                    r.TextStyle(_total);
                    r.Cell(cell => cell.ColumnSpan(2).Paragraph(x =>
                    {
                        x.Text("Total ");
                        x.Value(t.Data["total"]);
                    }));
                });
            });
        });
    }, new TemplateIdentity("short", 1));

    [Theory]
    [MemberData(nameof(Formats))]
    public void Import_ShortForms(TemplateFormat format)
    {
        ITemplateDefinition imported = TemplateJson.Import(Definition(ShortContent, ShortStyles));

        Assert.Equal(BuilderHarness.Build(ShortFormsInCSharp(), format).ContentHash, BuilderHarness.Build(imported, format).ContentHash);
    }

    [Fact]
    public void Export_ShortForms()
    {
        string json = TemplateJson.Export(ShortFormsInCSharp(), BuilderHarness.Options(TemplateFormat.Docx)).Json;
        using JsonDocument document = JsonDocument.Parse(json);
        using JsonDocument expected = JsonDocument.Parse(ShortContent);

        Assert.Equal(Compact(expected.RootElement), Compact(document.RootElement.GetProperty("content")));
        AssertRoundTrip(ShortFormsInCSharp());
    }

    [Fact]
    public void Export_OmitsInheritedStyle_KeepsOthers()
    {
        TestDefinition definition = new TestDefinition(t =>
        {
            t.Styles(StyleSheet.Create(s =>
            {
                s.Defaults(d => d.Paragraph(_body));
                s.Paragraph(_body, p => { });
                s.Paragraph(_heading, p => { });
            }));
            t.Content(c =>
            {
                c.Paragraph(_body, "explicit body");
                c.Paragraph(_heading, "heading");
            });
        });

        string json = TemplateJson.Export(definition, BuilderHarness.Options(TemplateFormat.Docx)).Json;
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.Equal("""["explicit body",{"style":"Heading","paragraph":"heading"}]""", Compact(document.RootElement.GetProperty("content")));
        AssertRoundTrip(definition);
    }

    [Fact]
    public void Export_ExplicitInheritedStyleInNestedCell_RoundTrips()
    {
        TestDefinition definition = new TestDefinition(t =>
        {
            t.Styles(StyleSheet.Create(s =>
            {
                s.Defaults(d => d.Paragraph(_body));
                s.Paragraph(_body, p => { });
                s.Paragraph(_cell, p => { });
                s.Table(_grid, g => g.TextStyle(_cell));
                s.Box(_success, b => b.Background(Color.FromHex("EFF6F1")));
            }));
            t.Content(c => c.Table(tb =>
            {
                tb.Style(_grid).Columns(cols => cols.Relative(1));
                tb.Row(r => r.Cell(cell =>
                {
                    cell.Paragraph(_cell, "explicit cell style");
                    cell.If(t.Data["flag"], b => b.Box(_success, box => box.Paragraph(_body, "explicit body in box")));
                    cell.Table(inner =>
                    {
                        inner.Columns(cols => cols.Relative(1));
                        inner.Row(ir => ir.Cell(_cell, "unstyled table, explicit cell style"));
                    });
                }));
            }));
        });

        string json = TemplateJson.Export(definition, BuilderHarness.Options(TemplateFormat.Docx)).Json;
        Assert.DoesNotContain("\"style\": \"Body\"", json, StringComparison.Ordinal);
        Assert.Contains("unstyled table, explicit cell style", json, StringComparison.Ordinal);
        AssertRoundTrip(definition);
    }

    [Fact]
    public void Import_OldShape_IsRejected()
    {
        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() =>
            TemplateJson.Import(Definition("""[ { "paragraph": { "style": "Body", "content": "x" } } ]"""))).Errors);

        Assert.Equal("TB9200", error.Code);
        Assert.Equal("/content/0/paragraph", error.Location);
    }

    [Fact]
    public void Import_CellOptionsWithoutContent_IsTB9200()
    {
        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() =>
            TemplateJson.Import(Definition("""[ { "columns": [ 1 ], "table": [ { "row": [ { "span": 1 } ] } ] } ]"""))).Errors);

        Assert.Equal("TB9200", error.Code);
        Assert.Equal("/content/0/table/0/row/0", error.Location);
    }

    private static JsonElement ContentOf(ITemplateDefinition definition)
    {
        string json = TemplateJson.Export(definition, BuilderHarness.Options(TemplateFormat.Docx)).Json;
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement block = document.RootElement.GetProperty("content")[0];
        return (block.ValueKind == JsonValueKind.String ? block : block.GetProperty("paragraph")).Clone();
    }

    private static string Compact(JsonElement element) =>
        JsonSerializer.Serialize(element, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static void AssertRoundTrip(ITemplateDefinition definition)
    {
        foreach (TemplateFormat format in BuilderHarness.Formats)
        {
            BuilderHarness.AssertJsonRoundTrip(definition, BuilderHarness.Build(definition, format), BuilderHarness.Options(format));
        }
    }
}
