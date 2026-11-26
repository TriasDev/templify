// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Builder.Json;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>Findings of the final review of the JSON format round (#293).</summary>
public sealed class ReviewFixes293Tests
{
    private static readonly ListStyleKey _bullets = new ListStyleKey("Bullets");

    // ── 1: line breaks and tabs, composed either way, export ─────────────────

    public static TheoryData<string> BreakDefinitions => new() { "inline-break", "inline-tab", "paragraph-string", "cell", "list-item" };

    private static TestDefinition Breaks(string kind) => new TestDefinition(t =>
    {
        t.Styles(StyleSheet.Create(s => s.List(_bullets, l => l.Level(0, "•", 6.Mm(), 4.Mm()))));
        t.Content(c =>
        {
            _ = kind switch
            {
                "inline-break" => c.Paragraph(x => x.Text("a").LineBreak().Text("b")),
                "inline-tab" => c.Paragraph(x => x.Text("a").Tab().Text("b")),
                "paragraph-string" => c.Paragraph("a\nb\tc"),
                "cell" => c.Table(table => table.Columns(k => k.Relative(1)).Row(r => r.Cell("a\nb"))),
                _ => c.List(_bullets, l => l.Item("a\tb")),
            };
        });
    });

    [Theory]
    [MemberData(nameof(BreakDefinitions))]
    public void LineBreaksAndTabs_RoundTripThroughJson(string kind)
    {
        foreach (TemplateFormat format in new[] { TemplateFormat.Docx, TemplateFormat.Odt })
        {
            TestDefinition definition = Breaks(kind);
            BuilderHarness.AssertInvariants(definition, BuilderHarness.Build(definition, format));
        }
    }

    [Fact]
    public void ParagraphString_WithABreak_BuildsLikeTheInlineForm()
    {
        TestDefinition text = new TestDefinition(t => t.Content(c => c.Paragraph("a\nb")));
        TestDefinition inline = new TestDefinition(t => t.Content(c => c.Paragraph(x => x.Text("a").LineBreak().Text("b"))));

        Assert.Equal(BuilderHarness.Build(inline, TemplateFormat.Docx).ContentHash, BuilderHarness.Build(text, TemplateFormat.Docx).ContentHash);
    }

    // ── 2: a host value format exports; the import still asks for it ─────────

    [Fact]
    public void CustomValueFormat_Exports_AndTheImportNeedsItListed()
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Paragraph(x => x.Value(t.Data["ok"], ValueFormat.Custom("janein")))));
        TemplateBuildOptions options = BuilderHarness.Options(TemplateFormat.Docx);

        TemplateJsonDocument exported = TemplateJson.Export(definition, options);

        Assert.Contains("{{ok:janein}}", exported.Json, StringComparison.Ordinal);
        Assert.Equal("TB9200", Assert.Single(Assert.Throws<TemplateBuildException>(() => TemplateJson.Import(exported.Json)).Errors).Code);
        ITemplateDefinition imported = TemplateJson.Import(exported.Json, new TemplateJsonImportOptions { ValueFormats = ["janein"] });
        Assert.Equal(TemplateBuilder.Build(definition, options).ContentHash, TemplateBuilder.Build(imported, options).ContentHash);
    }

    // ── 3: a requested language a text does not have is named at export ──────

    [Fact]
    public void Export_TextLanguages_TextWithoutOneOfThem_IsAClearError()
    {
        ITextCatalog catalog = TextCatalog.FromDictionary(new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["a"] = new Dictionary<string, string> { ["en"] = "A", ["de"] = "A" },
            ["b"] = new Dictionary<string, string> { ["en"] = "B" },
        });
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Paragraph(t.Texts["a"]).Paragraph(t.Texts["b"])));

        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => TemplateJson.Export(
            definition,
            BuilderHarness.Options(TemplateFormat.Docx) with { Texts = catalog },
            new TemplateJsonExportOptions { TextLanguages = [CultureInfo.GetCultureInfo("en"), CultureInfo.GetCultureInfo("de")] })).Errors);

        Assert.Equal("TB9204", error.Code);
        Assert.Contains("'b'", error.Message, StringComparison.Ordinal);
        Assert.Contains("de", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("the same", error.Message, StringComparison.Ordinal);
    }

    // ── 4: host theme values must fit the token ──────────────────────────────

    private const string ThemedJson = """
        {"templifyDefinition": 1, "identity": {"id": "t", "version": 1},
         "theme": {"colors": {"accent": "#1F4E79"}},
         "styles": {"paragraph": {"P": {"name": "P", "color": "$accent"}}},
         "content": [{"style": "P", "paragraph": "x"}]}
        """;

    [Fact]
    public void HostTheme_NonColourForAColourToken_IsAnError()
    {
        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => BuilderHarness.Build(
            TemplateJson.Import(ThemedJson, new TemplateJsonImportOptions { Theme = new Dictionary<string, string> { ["accent"] = "00A000" } }),
            TemplateFormat.Docx)).Errors);

        Assert.Equal("TB9200", error.Code);
        Assert.Contains("accent", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HostTheme_TokenTheDefinitionDoesNotHave_IsAWarning()
    {
        GeneratedTemplate template = BuilderHarness.Build(
            TemplateJson.Import(ThemedJson, new TemplateJsonImportOptions { Theme = new Dictionary<string, string> { ["acent"] = "#00A000" } }),
            TemplateFormat.Docx);

        BuildDiagnostic warning = Assert.Single(template.Warnings, w => w.Code == "TB9205");
        Assert.Contains("acent", warning.Message, StringComparison.Ordinal);
    }

    // ── 5: theme tokens in registered component parameters ───────────────────

    public sealed record Bar(Color Colour) : ITemplateComponent
    {
        public void Compose(BlockComposer c) => c.Rule(1.Pt(), Colour);
    }

    private static TestDefinition Barred(Color accent) => new TestDefinition(t =>
    {
        t.Styles(StyleSheet.Create(s => s.Paragraph(new ParagraphStyleKey("P", "P"), p => p.Color(accent))));
        t.Content(c => c.Paragraph(new ParagraphStyleKey("P", "P"), "x").Component(new Bar(accent)));
    });

    [Fact]
    public void ComponentParameter_ThemeColour_IsWrittenAsToken_AndFollowsTheHostTheme()
    {
        ComponentCatalog components = new ComponentCatalog().Register<Bar>("bar");
        TemplateBuildOptions options = BuilderHarness.Options(TemplateFormat.Docx);

        TemplateJsonDocument exported = TemplateJson.Export(Barred(Color.Token("accent", Color.FromHex("1F4E79"))), options, components);
        ITemplateDefinition imported = TemplateJson.Import(exported.Json, new TemplateJsonImportOptions
        {
            Components = components,
            Theme = new Dictionary<string, string> { ["accent"] = "#FF0000" },
        });

        Assert.Contains("\"colour\": \"$accent\"", exported.Json, StringComparison.Ordinal);
        Assert.Equal(TemplateBuilder.Build(Barred(Color.Token("accent", Color.FromHex("FF0000"))), options).ContentHash, TemplateBuilder.Build(imported, options).ContentHash);
    }

    // ── 6: a generic component's section name ────────────────────────────────

    private sealed class Card<T> : ITemplateComponent
    {
        public void Compose(BlockComposer c) => c.Paragraph(typeof(T).Name);
    }

    [Fact]
    public void UnregisteredGenericComponent_Exports()
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Component(new Card<int>()).Component(new Card<int>())));

        BuilderHarness.AssertInvariants(definition, BuilderHarness.Build(definition, TemplateFormat.Docx));
        Assert.Contains("\"section\": \"Card\"", TemplateJson.Export(definition, BuilderHarness.Options(TemplateFormat.Docx)).Json, StringComparison.Ordinal);
    }

    // ── 7, 8: malformed input is a TemplateBuildException ────────────────────

    [Theory]
    [InlineData("""{"templifyDefinition": 1, "identity": {"id": "t", "version": 1}, "texts": {"a": {"de": "x", "DE": "y"}}, "content": ["[[a]]"]}""", "/texts/a")]
    [InlineData("""{"templifyDefinition": 1, "identity": {"id": "t", "version": "1"}, "content": []}""", "/identity/version")]
    [InlineData("""{"templifyDefinition": "1", "identity": {"id": "t", "version": 1}, "content": []}""", "/templifyDefinition")]
    public void MalformedInput_IsTB9200(string json, string location)
    {
        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => TemplateJson.Import(json)).Errors);

        Assert.Equal("TB9200", error.Code);
        Assert.Equal(location, error.Location);
    }
}
