// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using TriasDev.Templify.Builder.Json;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>Text keys in JSON definitions: one JSON for every language (#274).</summary>
public sealed class JsonTextKeyTests
{
    private static readonly XNamespace _w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace _text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

    public static TheoryData<TemplateFormat, string> FormatsAndLanguages => new TheoryData<TemplateFormat, string>
    {
        { TemplateFormat.Docx, "de-DE" }, { TemplateFormat.Odt, "de-DE" }, { TemplateFormat.Docx, "en-GB" }, { TemplateFormat.Odt, "en-GB" },
    };

    private static ITextCatalog Catalog { get; } = TextCatalog.FromDictionary(new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
        ["hello"] = new Dictionary<string, string> { ["de"] = "Hallo", ["en"] = "Hello" },
        ["fmt"] = new Dictionary<string, string> { ["de"] = "d. MMMM yyyy", ["en"] = "d MMMM yyyy" },
        ["title"] = new Dictionary<string, string> { ["de"] = "Titel", ["en"] = "Title" },
        ["logo"] = new Dictionary<string, string> { ["de"] = "Firmenlogo", ["en"] = "Company logo" },
        ["printed"] = new Dictionary<string, string> { ["de"] = "Gedruckt", ["en"] = "Printed" },
        ["braces"] = new Dictionary<string, string> { ["de"] = "a {{x}}", ["en"] = "a {{x}}" },
    });

    private static TemplateBuildOptions Options(TemplateFormat format, string language, ITextCatalog? texts = null) =>
        new TemplateBuildOptions { Format = format, Language = CultureInfo.GetCultureInfo(language), Texts = texts ?? Catalog };

    private static byte[] Png() => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static TestDefinition Translated() => new TestDefinition(t =>
    {
        TemplateTexts texts = t.Texts;
        t.Properties(p => p.Title(texts["title"]));
        t.Content(c =>
        {
            c.Paragraph(x =>
            {
                x.Text(texts["hello"] + " ");
                x.Value(t.Data["date"], ValueFormat.Date(texts["fmt"]));
            });
            c.Image(ImageSource.Png(Png()), texts["logo"], ImageFit.Exact(10.Pt(), 10.Pt()));
            c.Raw(new RawBlock
            {
                Docx = _ => new XElement(_w + "p", new XElement(_w + "r", new XElement(_w + "t", texts["printed"]))),
                Odt = _ => new XElement(_text + "p", texts["printed"]),
            });
        });
    });

    private static JsonElement Root(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Export_WritesKeys_AndNoLanguage()
    {
        TemplateJsonDocument de = TemplateJson.Export(Translated(), Options(TemplateFormat.Docx, "de-DE"));
        TemplateJsonDocument en = TemplateJson.Export(Translated(), Options(TemplateFormat.Odt, "en-GB"));

        Assert.Equal(de.Json, en.Json);
        JsonElement root = Root(de.Json);
        Assert.False(root.TryGetProperty("language", out _));
        Assert.Equal("[[title]]", root.GetProperty("properties").GetProperty("title").GetString());
        Assert.Contains("\"[[hello]] {{date:date:[[fmt]]}}\"", de.Json, StringComparison.Ordinal);
        Assert.Contains("\"alt\": \"[[logo]]\"", de.Json, StringComparison.Ordinal);
        Assert.Contains("[[printed]]", root.GetProperty("content")[2].GetProperty("raw").GetProperty("docx").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Hallo", de.Json, StringComparison.Ordinal);
        Assert.DoesNotContain("", de.Json, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(FormatsAndLanguages))]
    public void Import_TranslatesFromTheCatalog_SameBytesAsCSharp(TemplateFormat format, string language)
    {
        TemplateJsonDocument exported = TemplateJson.Export(Translated(), Options(TemplateFormat.Docx, "en-US"));
        ITemplateDefinition imported = TemplateJson.Import(exported.Json, new TemplateJsonImportOptions { Assets = exported.Assets, AllowRawXml = true });

        Assert.Equal(TemplateBuilder.Build(Translated(), Options(format, language)).ContentHash, TemplateBuilder.Build(imported, Options(format, language)).ContentHash);
        Assert.Equal(exported.Json, TemplateJson.Export(imported, Options(format, language)).Json);
    }

    [Fact]
    public void Import_MissingTranslation_IsTB5001()
    {
        ITemplateDefinition imported = TemplateJson.Import(Definition("""[ "[[hello]] [[nope]]" ]"""));

        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => TemplateBuilder.Build(imported, Options(TemplateFormat.Docx, "de-DE"))).Errors);
        Assert.Equal("TB5001", error.Code);
        Assert.Contains("nope", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""[ "a [[bad key]]" ]""", "/content/0")]
    [InlineData("""[ { "paragraph": [ "ok", "[[unclosed" ] } ]""", "/content/0/paragraph/1")]
    public void Import_MalformedKey_IsTB9200(string content, string location)
    {
        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => TemplateJson.Import(Definition(content))).Errors);

        Assert.Equal("TB9200", error.Code);
        Assert.Equal(location, error.Location);
    }

    [Fact]
    public void Import_LoneClosingBrackets_AreText()
    {
        ITemplateDefinition imported = TemplateJson.Import(Definition("""[ "a ]] b" ]"""));

        (_, byte[] filled) = BuilderHarness.BuildAndFill(imported, TemplateFormat.Docx, "{}");
        Assert.Equal(new[] { "a ]] b" }, BuilderHarness.ParagraphTexts(TemplateFormat.Docx, filled));
    }

    [Fact]
    public void Import_TranslationWithBraces_IsRejectedAtTheString()
    {
        ITemplateDefinition imported = TemplateJson.Import(Definition("""[ "ok", "[[braces]]" ]"""));

        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => TemplateBuilder.Build(imported, Options(TemplateFormat.Docx, "de-DE"))).Errors);
        Assert.Equal("TB9201", error.Code);
        Assert.Equal("/content/1", error.Location);
    }

    [Fact]
    public void Export_LiteralBrackets_IsTB9204()
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c =>
        {
            c.Paragraph("fine");
            c.Paragraph("a [[b");
        }));

        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => TemplateJson.Export(definition, Options(TemplateFormat.Docx, "en-US"))).Errors);
        Assert.Equal("TB9204", error.Code);
        Assert.Equal("/content/1", error.Location);
    }

    [Fact]
    public void Export_KeyOutsideTheSyntax_IsTB9204()
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Paragraph(t.Texts["two words"])));

        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => TemplateJson.Export(definition, Options(TemplateFormat.Docx, "en-US"))).Errors);
        Assert.Equal("TB9204", error.Code);
        Assert.Contains("two words", error.Message, StringComparison.Ordinal);
    }

    // ── embedded texts ───────────────────────────────────────────────────────

    private static readonly TemplateJsonExportOptions _bothLanguages = new TemplateJsonExportOptions
    {
        TextLanguages = [CultureInfo.GetCultureInfo("de"), CultureInfo.GetCultureInfo("en")],
    };

    [Fact]
    public void Export_WithTextLanguages_WritesTextsInFirstUseOrder()
    {
        TemplateJsonDocument exported = TemplateJson.Export(Translated(), Options(TemplateFormat.Docx, "en-US"), _bothLanguages);
        JsonElement texts = Root(exported.Json).GetProperty("texts");

        Assert.Equal(new[] { "de", "en" }, texts.EnumerateObject().Select(p => p.Name));
        Assert.Equal(new[] { "title", "hello", "fmt", "logo", "printed" }, texts.GetProperty("de").EnumerateObject().Select(p => p.Name));
        Assert.Equal("Hallo", texts.GetProperty("de").GetProperty("hello").GetString());
        Assert.Equal("Company logo", texts.GetProperty("en").GetProperty("logo").GetString());
        Assert.Equal(new[] { "templifyDefinition", "identity", "texts" }, Root(exported.Json).EnumerateObject().Select(p => p.Name).Take(3));
    }

    [Theory]
    [MemberData(nameof(FormatsAndLanguages))]
    public void Import_WithEmbeddedTexts_NeedsNoCatalog(TemplateFormat format, string language)
    {
        TemplateJsonDocument exported = TemplateJson.Export(Translated(), Options(TemplateFormat.Docx, "en-US"), _bothLanguages);
        ITemplateDefinition imported = TemplateJson.Import(exported.Json, new TemplateJsonImportOptions { Assets = exported.Assets, AllowRawXml = true });

        Assert.Equal(
            TemplateBuilder.Build(Translated(), Options(format, language)).ContentHash,
            TemplateBuilder.Build(imported, Options(format, language, TextCatalog.Empty)).ContentHash);
    }

    [Fact]
    public void EmbeddedTexts_WinOverTheCatalog_WhichFillsGaps()
    {
        ITemplateDefinition imported = TemplateJson.Import("""
            {
              "templifyDefinition": 1,
              "identity": { "id": "keys", "version": 1 },
              "texts": { "de": { "hello": "Servus" } },
              "content": [ "[[hello]], [[title]]" ]
            }
            """);

        (_, byte[] filled) = BuilderHarness.BuildAndFill(imported, Options(TemplateFormat.Docx, "de-DE"), "{}");
        Assert.Equal(new[] { "Servus, Titel" }, BuilderHarness.ParagraphTexts(TemplateFormat.Docx, filled));
    }

    [Fact]
    public void ReExport_KeepsTheTexts()
    {
        TemplateJsonDocument exported = TemplateJson.Export(Translated(), Options(TemplateFormat.Docx, "en-US"), _bothLanguages);
        ITemplateDefinition imported = TemplateJson.Import(exported.Json, new TemplateJsonImportOptions { Assets = exported.Assets, AllowRawXml = true });

        Assert.Equal(exported.Json, TemplateJson.Export(imported, Options(TemplateFormat.Odt, "de-DE", TextCatalog.Empty)).Json);
    }

    [Theory]
    [InlineData("""{ "de": { "a": 1 } }""", "/texts/de/a")]
    [InlineData("""{ "de": [ "a" ] }""", "/texts/de")]
    [InlineData("""[ "de" ]""", "/texts")]
    public void MalformedTexts_AreTB9200(string texts, string location)
    {
        string json = Definition("""[ "x" ]""").Replace("\"content\"", "\"texts\": " + texts + ", \"content\"", StringComparison.Ordinal);

        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => TemplateJson.Import(json)).Errors);
        Assert.Equal("TB9200", error.Code);
        Assert.Equal(location, error.Location);
    }

    // ── final review fixes ───────────────────────────────────────────────────

    private static ITextCatalog Tricky { get; } = TextCatalog.FromDictionary(new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
        ["a"] = new Dictionary<string, string> { ["de"] = "A", ["en"] = "A" },
        ["amp"] = new Dictionary<string, string> { ["de"] = "Lieferung & Montage <x> &lt;", ["en"] = "Delivery & assembly" },
        ["quote"] = new Dictionary<string, string> { ["de"] = "say \"hi\"", ["en"] = "say \"hi\"" },
        ["pages"] = new Dictionary<string, string> { ["de"] = "Seite {0}", ["en"] = "Page {0}" },
    });

    [Fact]
    public void Export_BracketBeforeAKey_IsTB9204()
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Paragraph("[" + t.Texts["a"] + "]")));

        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => TemplateJson.Export(definition, Options(TemplateFormat.Docx, "de-DE", Tricky))).Errors);
        Assert.Equal("TB9204", error.Code);
    }

    [Theory]
    [MemberData(nameof(FormatsAndLanguages))]
    public void RawXml_TranslationWithXmlCharacters_RoundTrips(TemplateFormat format, string language)
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Raw(new RawBlock
        {
            Docx = _ => new XElement(_w + "p", new XElement(_w + "r", new XElement(_w + "t", t.Texts["amp"]))),
            Odt = _ => new XElement(_text + "p", new XAttribute(_text + "style-name", t.Texts["amp"]), t.Texts["amp"]),
        })));
        TemplateJsonDocument exported = TemplateJson.Export(definition, Options(format, language, Tricky));
        ITemplateDefinition imported = TemplateJson.Import(exported.Json, new TemplateJsonImportOptions { AllowRawXml = true });

        Assert.Equal(TemplateBuilder.Build(definition, Options(format, language, Tricky)).ContentHash, TemplateBuilder.Build(imported, Options(format, language, Tricky)).ContentHash);
    }

    [Fact]
    public void Export_TextChangedByCode_IsTB9204()
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Paragraph(string.Format(CultureInfo.InvariantCulture, t.Texts["pages"], 5))));

        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => TemplateJson.Export(definition, Options(TemplateFormat.Docx, "de-DE", Tricky))).Errors);
        Assert.Equal("TB9204", error.Code);
        Assert.Contains("de-DE", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_TranslationThatChangesAQuotedValue_IsTB9204()
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Paragraph(x => x.Value(t.Data["tags"], ValueFormat.Join(separator: t.Texts["quote"])))));

        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => TemplateJson.Export(definition, Options(TemplateFormat.Docx, "de-DE", Tricky))).Errors);
        Assert.Equal("TB9204", error.Code);
    }

    [Fact]
    public void ReExport_WithoutTextLanguages_KeepsTheSectionAsImported()
    {
        const string json = """
            {
              "templifyDefinition": 1,
              "identity": { "id": "keys", "version": 1 },
              "texts": { "de": { "hello": "Servus", "unused": "U" }, "de-CH": { "hello": "Grüezi" } },
              "content": [ "[[hello]] [[title]]" ]
            }
            """;
        ITemplateDefinition imported = TemplateJson.Import(json);

        JsonElement texts = Root(TemplateJson.Export(imported, Options(TemplateFormat.Docx, "de-DE")).Json).GetProperty("texts");

        Assert.Equal("""{"de":{"hello":"Servus","unused":"U"},"de-CH":{"hello":"Grüezi"}}""", JsonSerializer.Serialize(texts, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }

    private static string Definition(string content) => $$"""
        {
          "templifyDefinition": 1,
          "identity": { "id": "keys", "version": 1 },
          "content": {{content}}
        }
        """;
}
