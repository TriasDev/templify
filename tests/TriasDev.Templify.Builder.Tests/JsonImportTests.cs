// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Builder.Json;
using TriasDev.Templify.Builder.Sample;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>Importing JSON definitions (#274).</summary>
public sealed class JsonImportTests
{
    public static TheoryData<TemplateFormat, string> FormatsAndLanguages => new TheoryData<TemplateFormat, string>
    {
        { TemplateFormat.Docx, "de-DE" }, { TemplateFormat.Odt, "de-DE" }, { TemplateFormat.Docx, "en-GB" }, { TemplateFormat.Odt, "en-GB" },
    };

    [Theory]
    [MemberData(nameof(FormatsAndLanguages))]
    public void Import_RoundTripsTheSample(TemplateFormat format, string language)
    {
        TemplateBuildOptions options = new TemplateBuildOptions { Format = format, Language = CultureInfo.GetCultureInfo(language), Texts = SampleTexts.Catalog };
        OrderConfirmation definition = new OrderConfirmation(BrandTheme.CreateDefault());
        TemplateJsonDocument exported = TemplateJson.Export(definition, options);

        ITemplateDefinition imported = TemplateJson.Import(exported.Json, new TemplateJsonImportOptions { Assets = exported.Assets, AllowRawXml = true });

        Assert.Equal(definition.Identity, imported.Identity);
        Assert.Equal(TemplateBuilder.Build(definition, options).ContentHash, TemplateBuilder.Build(imported, options).ContentHash);
        Assert.Equal(exported.Json, TemplateJson.Export(imported, options).Json);
    }

    [Theory]
    [InlineData(TemplateFormat.Docx)]
    [InlineData(TemplateFormat.Odt)]
    public void Import_AcceptsShorthandsAndAlternativeForms(TemplateFormat format)
    {
        const string json = """
            {
              "templifyDefinition": 1,
              "identity": { "id": "short", "version": 1 },
              "language": "en-US",
              "styles": { "paragraph": { "Body": { "name": "Body Copy", "spaceAfter": "70.866pt", "color": "#1f4e79", "indentStart": "25mm" } } },
              "content": [
                { "paragraph": "Hi" },
                { "paragraph": { "content": "Hi" } },
                { "paragraph": { "style": "Body", "content": [ { "text": "a" }, { "tab": true }, { "value": "name" } ] } }
              ]
            }
            """;

        ITemplateDefinition imported = TemplateJson.Import(json);
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(imported, format, """{ "name": "Ada" }""");

        Assert.Equal(new[] { "Hi", "Hi", "a\tAda" }, BuilderHarness.ParagraphTexts(format, filled));
        string normalised = TemplateJson.Export(imported, BuilderHarness.Options(format)).Json;
        Assert.Contains("\"paragraph\": \"Hi\"", normalised, StringComparison.Ordinal);
        Assert.Contains("\"spaceAfter\": \"25mm\"", normalised, StringComparison.Ordinal);
        Assert.Contains("\"color\": \"#1F4E79\"", normalised, StringComparison.Ordinal);
        Assert.DoesNotContain("70.866pt", normalised, StringComparison.Ordinal);
        Assert.NotNull(template);
    }

    [Theory]
    [InlineData(TemplateFormat.Docx)]
    [InlineData(TemplateFormat.Odt)]
    public void Import_ResolvesLoopItemsLikeTheFluentApi(TemplateFormat format)
    {
        const string json = """
            {
              "templifyDefinition": 1,
              "identity": { "id": "loops", "version": 1 },
              "language": "en-US",
              "content": [
                { "forEach": "order.lines", "as": "line", "body": [
                  { "paragraph": { "content": [ { "value": "@number" }, { "text": " " }, { "value": "line.description" } ] } } ] },
                { "paragraph": { "content": [ { "value": "line.description" } ] } }
              ]
            }
            """;

        ITemplateDefinition imported = TemplateJson.Import(json);
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(
            imported,
            format,
            """{ "order": { "lines": [ { "description": "A" } ] }, "line": { "description": "root" } }""");

        Assert.Equal(new[] { "1 A", "root" }, BuilderHarness.ParagraphTexts(format, filled));
        Assert.Contains(new ContractEntry("order.lines[].description", ContractUsage.Value), template.Contract.Entries);
        Assert.Contains(new ContractEntry("line.description", ContractUsage.Value), template.Contract.Entries);
    }
}
