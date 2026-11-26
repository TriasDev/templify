// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Json;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>The reader refuses what it used to accept silently (#293, style review A1).</summary>
public sealed class JsonStrictTests
{
    private static string Definition(string content, string extra = "") => $$"""
        {
          "templifyDefinition": 1,
          "identity": { "id": "strict", "version": 1 },{{extra}}
          "content": {{content}}
        }
        """;

    [Theory]
    [InlineData("""[ "{{order.total:N2}}" ]""", "/content/0")]
    [InlineData("""[ "{{order.total:currency:EUR}}" ]""", "/content/0")]
    [InlineData("""[ "{{order.date:date}}" ]""", "/content/0")]
    [InlineData("""[ "{{order.total:number}}" ]""", "/content/0")]
    [InlineData("""[ "{{(order.paid):janein}}" ]""", "/content/0")]
    [InlineData("""[ { "paragraph": [ { "text": "{{order.total:money}}", "style": "Strong" } ] } ]""", "/content/0/paragraph/0")]
    public void UnknownValueFormats_AreErrors(string content, string location)
    {
        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() =>
            TemplateJson.Import(Definition(content, """ "styles": { "character": { "Strong": { "name": "Strong Text", "bold": true } } },"""))).Errors);

        Assert.Equal("TB9200", error.Code);
        Assert.Equal(location, error.Location);
        Assert.Contains("format", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{{order.total:currency}}")]
    [InlineData("{{order.total:number:N2}}")]
    [InlineData("{{order.date:date:d. MMMM yyyy}}")]
    [InlineData("{{order.tags:join}}")]
    [InlineData("{{order.tags:join:\\\" · \\\"}}")]
    [InlineData("{{order.lines:join:name:\\\"; \\\"}}")]
    [InlineData("{{order.paid:yesno}} {{order.paid:checkbox}} {{order.paid:check}} {{order.name:uppercase}} {{order.note:raw}}")]
    [InlineData("{{(order.paid):truefalse}}")]
    public void KnownValueFormats_AreAccepted(string text)
    {
        TemplateJson.Import(Definition("[ \"" + text + "\" ]"));
    }

    [Fact]
    public void HostFormats_CanBeAdded()
    {
        TemplateJson.Import(Definition("""[ "{{order.paid:janein}}" ]"""), new TemplateJsonImportOptions { ValueFormats = ["janein"] });
    }

    [Fact]
    public void PaddingWithFourDifferentValues_IsAnError()
    {
        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => TemplateJson.Import(Definition(
            """[ "x" ]""",
            """ "styles": { "box": { "Panel": { "padding": "1pt 2pt 30pt 40pt" } } },"""))).Errors);

        Assert.Equal("TB9200", error.Code);
        Assert.Equal("/styles/box/Panel/padding", error.Location);
    }

    [Fact]
    public void LoopVariableShadowingItsOwnRoot_IsAnError()
    {
        Assert.Throws<ArgumentException>(() => BuilderHarness.Build(new TestDefinition(t => t.Content(c =>
            c.ForEach(t.Data["order"]["lines"], "order", (item, b) => b.Paragraph(item["name"])))), TemplateFormat.Docx));

        ITemplateDefinition imported = TemplateJson.Import(Definition("""[ { "forEach": "order.lines", "as": "order", "body": [ "{{order.name}}" ] } ]"""));
        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => BuilderHarness.Build(imported, TemplateFormat.Docx)).Errors);
        Assert.Equal("TB9201", error.Code);
        Assert.Equal("/content/0", error.Location);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void DoubleEquals_IsEquals(TemplateFormat format)
    {
        ITemplateDefinition imported = TemplateJson.Import(Definition("""[ { "if": "order.status == \"paid\"", "then": [ "paid" ] } ]"""));

        (_, byte[] filled) = BuilderHarness.BuildAndFill(imported, format, """{ "order": { "status": "paid" } }""");

        Assert.Equal(new[] { "paid" }, BuilderHarness.ParagraphTexts(format, filled));
        Assert.Contains("\"order.status = \\\"paid\\\"\"", TemplateJson.Export(imported, BuilderHarness.Options(format)).Json, StringComparison.Ordinal);
    }

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    [Fact]
    public void SchemaKey_IsIgnored_ComponentsKey_IsReserved()
    {
        TemplateJson.Import(Definition("""[ "x" ]""", """ "$schema": "https://example.invalid/templify-definition.json","""));

        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() =>
            TemplateJson.Import(Definition("""[ "x" ]""", """ "components": {},"""))).Errors);
        Assert.Equal("TB9200", error.Code);
        Assert.Equal("/components", error.Location);
        Assert.Contains("reserved", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportSelfCheck_NamesTheFirstDifference()
    {
        ITextCatalog catalog = TextCatalog.FromDictionary(new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["pages"] = new Dictionary<string, string> { ["en"] = "Page {0}" },
        });
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Paragraph(string.Format(System.Globalization.CultureInfo.InvariantCulture, t.Texts["pages"], 5))));

        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() =>
            TemplateJson.Export(definition, BuilderHarness.Options(TemplateFormat.Docx) with { Texts = catalog })).Errors);

        Assert.Equal("TB9204", error.Code);
        Assert.Contains("Page 5", error.Message, StringComparison.Ordinal);
        Assert.Contains("Page {0}", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TextsKey_MissingALanguageTheOthersHave_IsAnError()
    {
        string json = Definition(
            """[ "[[title]] [[note]]" ]""",
            """ "texts": { "title": { "de": "Titel", "en": "Title" }, "note": { "de": "Hinweis" } },""");

        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => TemplateJson.Import(json)).Errors);

        Assert.Equal("TB9200", error.Code);
        Assert.Equal("/texts/note", error.Location);
        Assert.Contains("'en'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TextsKey_WithoutARegionalVariant_IsCoveredByItsParent_ButNotTheOtherWayRound()
    {
        string covered = Definition("""[ "[[a]] [[b]]" ]""", """ "texts": { "a": { "de": "A", "de-CH": "A!" }, "b": { "de": "B" } },""");
        string notCovered = Definition("""[ "[[a]] [[b]]" ]""", """ "texts": { "a": { "de": "A" }, "b": { "de-CH": "B" } },""");

        Assert.NotNull(TemplateJson.Import(covered));
        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => TemplateJson.Import(notCovered)).Errors);
        Assert.Equal("/texts/b", error.Location);
    }

    [Fact]
    public void Texts_AllInOneLanguage_AreAccepted()
    {
        string json = Definition("""[ "[[title]]" ]""", """ "texts": { "title": { "de": "Titel" } },""");

        Assert.NotNull(TemplateJson.Import(json));
    }
}
