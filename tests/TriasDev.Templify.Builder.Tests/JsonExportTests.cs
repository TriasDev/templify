// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json;
using TriasDev.Templify.Builder.Json;
using TriasDev.Templify.Builder.Sample;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>Exporting definitions to JSON (#274).</summary>
public sealed class JsonExportTests
{
    private static TemplateBuildOptions Options(TemplateFormat format, string language = "en-GB") => new TemplateBuildOptions
    {
        Format = format,
        Language = CultureInfo.GetCultureInfo(language),
        Texts = SampleTexts.Catalog,
    };

    [Fact]
    public void Export_WalkingSkeleton_IsThisJson()
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Paragraph(x =>
        {
            x.Text("Hello ");
            x.Value(t.Data["name"]);
        })));

        TemplateJsonDocument document = TemplateJson.Export(definition, BuilderHarness.Options(TemplateFormat.Docx));

        Assert.Equal(
            "{\"templifyDefinition\": 1, \"identity\": {\"id\": \"test\", \"version\": 1}, \"content\": [\"Hello {{name}}\"]}\n",
            document.Json);
        Assert.Empty(document.Assets);
    }

    [Fact]
    public void Export_WithoutIndentation_IsOneLine_AndImportsToTheSameTemplate()
    {
        BrandTheme theme = BrandTheme.CreateDefault();
        TemplateBuildOptions options = Options(TemplateFormat.Docx);
        TemplateJsonDocument indented = TemplateJson.Export(new OrderConfirmation(theme), options, SampleRunner.Components);

        TemplateJsonDocument compact = TemplateJson.Export(new OrderConfirmation(theme), options, new TemplateJsonExportOptions { Indented = false, Components = SampleRunner.Components });

        Assert.DoesNotContain('\n', compact.Json);
        Assert.Equal(JsonSerializer.Serialize(JsonDocument.Parse(compact.Json).RootElement, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }), compact.Json);
        Assert.Equal(JsonLayout.Format(compact.Json), indented.Json);
        ITemplateDefinition imported = TemplateJson.Import(compact.Json, new TemplateJsonImportOptions { Assets = compact.Assets, AllowRawXml = true, Components = SampleRunner.Components, Shared = [theme] });
        Assert.Equal(TemplateBuilder.Build(new OrderConfirmation(theme), options).ContentHash, TemplateBuilder.Build(imported, options).ContentHash);
        Assert.Equal(indented.Json, TemplateJson.Export(imported, options, SampleRunner.Components).Json);
    }

    [Fact]
    public void Export_IsDeterministic_AndDoesNotDependOnTheFormat()
    {
        BrandTheme theme = BrandTheme.CreateDefault();
        string docx = TemplateJson.Export(new OrderConfirmation(theme), Options(TemplateFormat.Docx), SampleRunner.Components).Json;

        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(docx, TemplateJson.Export(new OrderConfirmation(theme), Options(TemplateFormat.Docx), SampleRunner.Components).Json);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            Assert.Equal(docx, TemplateJson.Export(new OrderConfirmation(theme), Options(TemplateFormat.Odt), SampleRunner.Components).Json);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Export_CoversEveryBlock()
    {
        BrandTheme theme = BrandTheme.CreateDefault();

        TemplateJsonDocument document = TemplateJson.Export(new OrderConfirmation(theme), Options(TemplateFormat.Docx), SampleRunner.Components);

        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        using (JsonDocument json = JsonDocument.Parse(document.Json))
        {
            Collect(json.RootElement, keys);
        }

        foreach (string key in new[]
        {
            "styles", "page", "properties", "paragraph", "if", "elseIf", "else", "table", "forEachRow", "rowsIf", "box", "list",
            "forEach", "image", "toc", "raw", "tabRight", "pageNumber", "pageCount", "section", "component",
        })
        {
            Assert.True(keys.Contains(key), $"The export has no '{key}'.");
        }

        Assert.Contains("{{(order.isExpress and order.total > 1000):yesno}}", document.Json, StringComparison.Ordinal);
        Assert.Contains("{{line.amount:currency}}", document.Json, StringComparison.Ordinal);
        string logo = Assert.Single(document.Assets.Keys);
        Assert.StartsWith("sha256:", logo, StringComparison.Ordinal);
        Assert.Contains("\"" + logo + "\"", document.Json, StringComparison.Ordinal);
        Assert.Contains("[[title]]", TemplateJson.Export(new OrderConfirmation(theme), Options(TemplateFormat.Docx, "de-DE"), SampleRunner.Components).Json, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_FailingDefinition_Throws()
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Paragraph(new ParagraphStyleKey("Missing", "Missing"), "x")));

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(() => TemplateJson.Export(definition, BuilderHarness.Options(TemplateFormat.Docx)));

        Assert.Contains(exception.Errors, e => e.Code == "TB2010");
    }

    private static void Collect(JsonElement element, HashSet<string> keys)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    keys.Add(property.Name);
                    Collect(property.Value, keys);
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    Collect(item, keys);
                }

                break;
        }
    }
}
