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
        ITemplateDefinition imported = TemplateJson.Import(Definition("""[ { "paragraph": { "content": "Order {{order.number}} of {{order.date:date:yyyy}}" } } ]"""));

        (_, byte[] filled) = BuilderHarness.BuildAndFill(imported, format, """{ "order": { "number": "42", "date": "2026-11-26" } }""");

        Assert.Equal(new[] { "Order 42 of 2026" }, BuilderHarness.ParagraphTexts(format, filled));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Import_StyledTextWithValue(TemplateFormat format)
    {
        ITemplateDefinition imported = TemplateJson.Import(Definition("""[ { "paragraph": { "content": [ "No. ", { "text": "{{order.number}}", "style": "Strong" } ] } } ]"""));
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
        ITemplateDefinition imported = TemplateJson.Import(Definition("""[ { "paragraph": { "content": "Free: {{(a and b):yesno}}" } } ]"""));

        (_, byte[] filled) = BuilderHarness.BuildAndFill(imported, format, """{ "a": true, "b": false }""");

        Assert.Equal(new[] { "Free: No" }, BuilderHarness.ParagraphTexts(format, filled));
    }

    [Theory]
    [InlineData("""[ { "paragraph": { "content": "x {{order.number" } } ]""", "/content/0/paragraph/content")]
    [InlineData("""[ { "paragraph": { "content": [ "ok", "x {{}}" ] } } ]""", "/content/0/paragraph/content/1")]
    [InlineData("""[ { "paragraph": { "content": "{{(a and b)}}" } } ]""", "/content/0/paragraph/content")]
    [InlineData("""[ { "paragraph": { "content": [ { "text": "{{(a):yesno}}", "style": "Strong" } ] } } ]""", "/content/0/paragraph/content/0")]
    [InlineData("""[ { "paragraph": { "content": [ { "if": "a", "then": "{{(b):yesno}}" } ] } } ]""", "/content/0/paragraph/content/0/then")]
    [InlineData("""[ { "paragraph": { "content": [ { "if": "a", "then": [ { "text": "{{v:number:F2}}", "style": "Strong" } ] } ] } } ]""", "/content/0/paragraph/content/0/then/0")]
    [InlineData("""[ { "paragraph": { "content": [ { "value": "a" } ] } } ]""", "/content/0/paragraph/content/0")]
    [InlineData("""[ { "paragraph": { "content": [ { "expression": "a", "format": "yesno" } ] } } ]""", "/content/0/paragraph/content/0/expression")]
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
            TemplateJson.Import(Definition("""[ { "paragraph": { "content": "{{order.number}} {{order.nmber}}" } } ]"""), options)).Errors;

        BuildDiagnostic error = Assert.Single(errors);
        Assert.Equal("TB9203", error.Code);
        Assert.Equal("/content/0/paragraph/content", error.Location);
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

    private static JsonElement ContentOf(ITemplateDefinition definition)
    {
        string json = TemplateJson.Export(definition, BuilderHarness.Options(TemplateFormat.Docx)).Json;
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement paragraph = document.RootElement.GetProperty("content")[0].GetProperty("paragraph");
        return (paragraph.ValueKind == JsonValueKind.String ? paragraph : paragraph.GetProperty("content")).Clone();
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
