// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Globalization;
using TriasDev.Templify.Builder.Json;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>Theme tokens and the context components see (#293, style review A5; port finding F5).</summary>
public sealed class JsonThemeTests
{
    private static readonly ParagraphStyleKey _heading = new ParagraphStyleKey("Heading", "Heading Style");
    private static readonly TableStyleKey _grid = new TableStyleKey("Grid");
    private static readonly Color _accent = Color.Token("accent", Color.FromHex("1F4E79"));
    private static readonly Color _line = Color.Token("line", Color.FromHex("D4D4D4"));
    private static readonly FontFace _headingFont = FontFace.Token("heading", new FontFace("Liberation Serif"));

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private static TestDefinition Themed() => new TestDefinition(t =>
    {
        t.Styles(StyleSheet.Create(s =>
        {
            s.Paragraph(_heading, p => p.Font(_headingFont).Color(_accent));
            s.Table(_grid, g => g.HeaderRule(1.Pt(), _accent).RowRule(0.5.Pt(), _line));
        }));
        t.Content(c =>
        {
            c.Paragraph(_heading, "Title");
            c.Table(tb =>
            {
                tb.Style(_grid).Columns(cols => cols.Relative(1));
                tb.Row(r => r.Cell(cell => cell.Background(_line).Paragraph("x")));
            });
            c.Rule(0.5.Pt(), _line);
        });
    });

    private static JsonElement Root(string json) => JsonDocument.Parse(json).RootElement;

    private static string Compact(JsonElement element) => JsonSerializer.Serialize(element);

    [Fact]
    public void Export_WritesTokens_AndATheme()
    {
        string json = TemplateJson.Export(Themed(), BuilderHarness.Options(TemplateFormat.Docx)).Json;
        JsonElement root = Root(json);

        Assert.Equal("""{"colors":{"accent":"#1F4E79","line":"#D4D4D4"},"fonts":{"heading":"Liberation Serif"}}""", Compact(root.GetProperty("theme")));
        JsonElement heading = root.GetProperty("styles").GetProperty("paragraph").GetProperty("Heading");
        Assert.Equal("$heading", heading.GetProperty("font").GetString());
        Assert.Equal("$accent", heading.GetProperty("color").GetString());
        JsonElement grid = root.GetProperty("styles").GetProperty("table").GetProperty("Grid");
        Assert.Equal("1pt $accent", grid.GetProperty("headerRule").GetString());
        Assert.Contains("\"background\": \"$line\"", json, StringComparison.Ordinal);
        Assert.Contains("\"line\": \"0.5pt $line\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Import_ResolvesTokens_SameBytes(TemplateFormat format)
    {
        TemplateJsonDocument exported = TemplateJson.Export(Themed(), BuilderHarness.Options(format));
        ITemplateDefinition imported = TemplateJson.Import(exported.Json);

        Assert.Equal(BuilderHarness.Build(Themed(), format).ContentHash, BuilderHarness.Build(imported, format).ContentHash);
        Assert.Equal(exported.Json, TemplateJson.Export(imported, BuilderHarness.Options(format)).Json);
    }

    [Fact]
    public void HostTheme_OverridesTheFile()
    {
        string json = TemplateJson.Export(Themed(), BuilderHarness.Options(TemplateFormat.Docx)).Json;
        ITemplateDefinition imported = TemplateJson.Import(json, new TemplateJsonImportOptions
        {
            Theme = new Dictionary<string, string> { ["accent"] = "#00A000", ["heading"] = "Liberation Sans" },
        });

        GeneratedTemplate template = BuilderHarness.Build(imported, TemplateFormat.Docx);
        string styles = BuilderHarness.ReadXml(template.Content.ToArray(), "word/styles.xml").ToString();
        Assert.Contains("00A000", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("1F4E79", styles, StringComparison.Ordinal);
        Assert.Contains("Liberation Sans", styles, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "styles": { "paragraph": { "H": { "name": "H", "color": "$nope" } } } }""", "/styles/paragraph/H/color")]
    [InlineData("""{ "theme": { "colors": { "a": "blue" } } }""", "/theme/colors/a")]
    [InlineData("""{ "theme": { "colors": { "1a": "#000000" } } }""", "/theme/colors/1a")]
    public void ThemeErrors(string extra, string location)
    {
        string body = extra.Trim()[1..^1];
        string json = $$"""{ "templifyDefinition": 1, "identity": { "id": "t", "version": 1 }, {{body}}, "content": [ "x" ] }""";

        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => TemplateJson.Import(json)).Errors);
        Assert.Equal("TB9200", error.Code);
        Assert.Equal(location, error.Location);
    }

    [Fact]
    public void Token_RejectsBadNames()
    {
        Assert.Throws<ArgumentException>(() => Color.Token("has space", Color.FromHex("000000")));
        Assert.Throws<ArgumentException>(() => FontFace.Token("", new FontFace("Arial")));
    }

    // ── component context ────────────────────────────────────────────────────

    private sealed record Brand(string Greeting);

    private sealed class Greeting : ITemplateComponent
    {
        public void Compose(BlockComposer c) => c.Paragraph(c.Shared<Brand>().Greeting + " " + c.Texts["name"]);
    }

    [Fact]
    public void Components_SeeTheTextsAndSharedObjects()
    {
        ITextCatalog catalog = TextCatalog.FromDictionary(new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["name"] = new Dictionary<string, string> { ["en"] = "World" },
        });
        TestDefinition definition = new TestDefinition(t =>
        {
            t.Share(new Brand("Hello"));
            t.Content(c => c.Component(new Greeting()));
        });

        (_, byte[] filled) = BuilderHarness.BuildAndFill(definition, BuilderHarness.Options(TemplateFormat.Docx) with { Texts = catalog }, "{}");

        Assert.Equal(new[] { "Hello World" }, BuilderHarness.ParagraphTexts(TemplateFormat.Docx, filled));
        Assert.Throws<InvalidOperationException>(() => BuilderHarness.Build(new TestDefinition(t => t.Content(c => c.Component(new Greeting()))), TemplateFormat.Docx));
    }

    [Fact]
    public void RegisteredComponent_ReadingSharedObjects_Exports_AndImportsWithTheHostsObjects()
    {
        ITextCatalog catalog = TextCatalog.FromDictionary(new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["name"] = new Dictionary<string, string> { ["en"] = "World" },
        });
        TestDefinition definition = new TestDefinition(t =>
        {
            t.Share(new Brand("Hello"));
            t.Content(c => c.Component(new Greeting()));
        });
        ComponentCatalog components = new ComponentCatalog().Register<Greeting>("greeting");
        TemplateBuildOptions options = BuilderHarness.Options(TemplateFormat.Docx) with { Texts = catalog };

        TemplateJsonDocument exported = TemplateJson.Export(definition, options, new TemplateJsonExportOptions { Components = components, TextLanguages = [CultureInfo.GetCultureInfo("en")] });

        // The component's own text is in the JSON's texts, so the JSON builds without the host's catalog.
        Assert.Contains("\"component\": \"greeting\"", exported.Json, StringComparison.Ordinal);
        Assert.Contains("\"texts\": {\"name\": {\"en\": \"World\"}}", exported.Json, StringComparison.Ordinal);
        ITemplateDefinition imported = TemplateJson.Import(exported.Json, new TemplateJsonImportOptions { Components = components, Shared = [new Brand("Hello")] });
        Assert.Equal(TemplateBuilder.Build(definition, options).ContentHash, TemplateBuilder.Build(imported, options with { Texts = TextCatalog.Empty }).ContentHash);
    }
}
