// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Builder.Json;
using TriasDev.Templify.Builder.Sample;
using TriasDev.Templify.Builder.Testing;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>Runs the sample (samples/TriasDev.Templify.Builder.Sample), so CI keeps it working (#287).</summary>
public sealed class SampleTests
{
    [Fact]
    public void Sample_VerifiesBuildsAndFillsEveryLanguageAndFormat()
    {
        string directory = Path.Combine(Path.GetTempPath(), "templify-sample-" + Guid.NewGuid().ToString("N"));
        try
        {
            StringWriter log = new StringWriter();

            IReadOnlyList<string> files = SampleRunner.Run(directory, log);

            Assert.Equal(
                new[]
                {
                    "order-confirmation.de-DE.template.docx", "order-confirmation.de-DE.filled.docx",
                    "order-confirmation.de-DE.template.odt", "order-confirmation.de-DE.filled.odt",
                    "order-confirmation.en-GB.template.docx", "order-confirmation.en-GB.filled.docx",
                    "order-confirmation.en-GB.template.odt", "order-confirmation.en-GB.filled.odt",
                    "data-contract.json",
                },
                files.Select(Path.GetFileName));
            Assert.All(files, f => Assert.True(new FileInfo(f).Length > 0, f));
            Assert.Contains("Template verification passed (4 templates).", log.ToString(), StringComparison.Ordinal);
            Assert.Contains("check of the unchanged template: Ok", log.ToString(), StringComparison.Ordinal);

            byte[] filled = File.ReadAllBytes(Path.Combine(directory, "order-confirmation.en-GB.filled.odt"));
            string snapshot = TemplateSnapshot.Create(TemplateFormat.Odt, filled);
            Assert.Contains("DocTitle | Order confirmation", snapshot, StringComparison.Ordinal);
            Assert.Contains("TableText | Laptop 14\" — in stock", snapshot, StringComparison.Ordinal);
            Assert.Contains("ListItem [list 1] | Credit limit: ✓", snapshot, StringComparison.Ordinal);
            Assert.DoesNotContain("{{", snapshot, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Phase3_JsonRoundTripIsByteIdentical()
    {
        string directory = Path.Combine(Path.GetTempPath(), "templify-phase3-" + Guid.NewGuid().ToString("N"));
        try
        {
            StringWriter log = new StringWriter();

            IReadOnlyList<string> files = SampleRunner.RunPhase3(directory, log);

            string json = Assert.Single(files, f => Path.GetExtension(f) == ".json");
            Assert.Equal("order-confirmation.json", Path.GetFileName(json));
            Assert.Contains("\"texts\"", File.ReadAllText(json), StringComparison.Ordinal);
            foreach (string language in new[] { "de-DE", "en-GB" })
            {
                foreach (string extension in new[] { "docx", "odt" })
                {
                    Assert.Contains(files, f => Path.GetFileName(f) == $"order-confirmation.{language}.from-json.{extension}");
                    Assert.Contains(files, f => Path.GetFileName(f) == $"order-confirmation.{language}.from-json.filled.{extension}");
                }
            }

            Assert.Contains(files, f => Path.GetFileName(Path.GetDirectoryName(f)) == "assets" && f.EndsWith(".png", StringComparison.Ordinal));
            Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(log.ToString(), "same bytes as C#").Count);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(TemplateFormat.Docx, "de-DE")]
    [InlineData(TemplateFormat.Odt, "de-DE")]
    [InlineData(TemplateFormat.Docx, "en-GB")]
    [InlineData(TemplateFormat.Odt, "en-GB")]
    public void Typed_SampleProducesTheSameBytes(TemplateFormat format, string language)
    {
        TemplateBuildOptions options = new TemplateBuildOptions { Format = format, Language = CultureInfo.GetCultureInfo(language), Texts = SampleTexts.Catalog };
        BrandTheme theme = BrandTheme.CreateDefault();

        Assert.Equal(TemplateBuilder.Build(new OrderConfirmation(theme), options).ContentHash, TemplateBuilder.Build(new TypedOrderConfirmation(theme), options).ContentHash);
    }

    [Fact]
    public void Typed_SampleVerifies()
    {
        BrandTheme theme = BrandTheme.CreateDefault();

        TemplateVerifier.VerifyAll(
            () => new TypedOrderConfirmation(theme),
            new TemplateVerificationOptions { Languages = SampleRunner.Languages, Texts = _ => SampleTexts.Catalog, SampleData = [SampleResources.SampleData] });
    }

    [Fact]
    public void Phase3_ImportCheckedAgainstTheDataCatalogue()
    {
        TemplateBuildOptions options = new TemplateBuildOptions { Format = TemplateFormat.Docx, Language = CultureInfo.GetCultureInfo("en-GB"), Texts = SampleTexts.Catalog };
        TemplateJsonDocument exported = TemplateJson.Export(new OrderConfirmation(BrandTheme.CreateDefault()), options, SampleRunner.Components);

        ITemplateDefinition imported = TemplateJson.Import(exported.Json, new TemplateJsonImportOptions
        {
            Assets = exported.Assets,
            Components = SampleRunner.Components,
            Data = DataCatalog.From<OrderData>(),
            Shared = [BrandTheme.CreateDefault()],
            AllowRawXml = true,
        });

        Assert.Contains("\"component\": \"address\"", exported.Json, StringComparison.Ordinal);
        Assert.Contains("{\"component\": \"terms\"}", exported.Json, StringComparison.Ordinal);
        Assert.Equal(TemplateBuilder.Build(new OrderConfirmation(BrandTheme.CreateDefault()), options).ContentHash, TemplateBuilder.Build(imported, options).ContentHash);
    }
}
