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

    private static string Definition(string content) => $$"""
        {
          "templifyDefinition": 1,
          "identity": { "id": "keys", "version": 1 },
          "content": {{content}}
        }
        """;
}
