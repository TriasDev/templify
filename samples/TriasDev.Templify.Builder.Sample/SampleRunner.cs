// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Builder.Json;
using TriasDev.Templify.Builder.Testing;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Sample;

/// <summary>
/// The sample's flow: verify the definition, then build every language and format, fill each template with the
/// sample data and write templates, filled documents and the data contract to a folder.
/// </summary>
public static class SampleRunner
{
    /// <summary>The languages the sample builds.</summary>
    public static readonly CultureInfo[] Languages = [CultureInfo.GetCultureInfo("de-DE"), CultureInfo.GetCultureInfo("en-GB")];

    /// <summary>The components a JSON definition of the sample may refer to by name.</summary>
    public static ComponentCatalog Components { get; } = new ComponentCatalog().Register<AddressBlock>("address");

    /// <summary>Runs the sample.</summary>
    /// <param name="outputDirectory">Where the files go.</param>
    /// <param name="log">Where progress goes.</param>
    /// <returns>The written files.</returns>
    public static IReadOnlyList<string> Run(string outputDirectory, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);
        ArgumentNullException.ThrowIfNull(log);
        Directory.CreateDirectory(outputDirectory);
        BrandTheme theme = BrandTheme.CreateDefault();
        string data = SampleResources.SampleData;

        // 1. What a consumer's CI does: every language and format builds, validates, fills and is deterministic.
        TemplateVerificationReport report = TemplateVerifier.VerifyAll(
            () => new OrderConfirmation(theme),
            new TemplateVerificationOptions { Languages = Languages, Texts = _ => SampleTexts.Catalog, SampleData = [data] });
        log.WriteLine(report);

        // 2. What the application does at runtime: build the template, offer it for download, fill it.
        List<string> files = new List<string>();
        foreach (VerifiedTemplate built in report.Templates)
        {
            GeneratedTemplate template = built.Template;
            string name = $"order-confirmation.{built.Language.Name}";
            files.Add(Write(outputDirectory, $"{name}.template{template.FileExtension}", template.Content.ToArray()));

            TemplateProcessor processor = new TemplateProcessor(new PlaceholderReplacementOptions
            {
                Culture = built.Language,
                MissingVariableBehavior = MissingVariableBehavior.ThrowException,
                // Word builds the table of contents and the page fields when it opens the document.
                UpdateFieldsOnOpen = UpdateFieldsOnOpenMode.Auto,
            });
            using Stream input = template.OpenRead();
            using MemoryStream output = new MemoryStream();
            ProcessingResult result = processor.ProcessTemplate(input, output, data);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(result.ErrorMessage);
            }

            files.Add(Write(outputDirectory, $"{name}.filled{template.FileExtension}", output.ToArray()));
            log.WriteLine(
                $"{name}{template.FileExtension}: {template.Content.Length / 1024.0:F1} KB, contract {template.Contract.Hash[..12]}, " +
                $"{result.ReplacementCount} replacements, build warnings: {string.Join(", ", template.Warnings.Select(w => w.Code))}");
        }

        // 3. The data contract (the JSON shape the template reads) and the upload check of an unchanged template.
        GeneratedTemplate first = report.Templates[0].Template;
        files.Add(Write(outputDirectory, "data-contract.json", System.Text.Encoding.UTF8.GetBytes(first.Contract.ToJson())));
        using (Stream upload = first.OpenRead())
        {
            UploadCheckResult check = TemplateInspector.CheckUpload(upload, first.Contract, new OrderConfirmation(theme).Identity);
            log.WriteLine($"upload check of the unchanged template: usable={check.IsUsable}, origin={check.Origin}");
        }

        return files;
    }

    /// <summary>
    /// Phase 3 (#274): exports the definition to JSON per language, imports it again (checked against the data
    /// catalogue of <see cref="OrderData"/>), builds and fills DOCX and ODT from the import and checks that the
    /// templates are the bytes the C# definition builds.
    /// </summary>
    /// <param name="outputDirectory">Where the files go.</param>
    /// <param name="log">Where progress and sizes go.</param>
    /// <returns>The written files.</returns>
    public static IReadOnlyList<string> RunPhase3(string outputDirectory, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);
        ArgumentNullException.ThrowIfNull(log);
        Directory.CreateDirectory(Path.Combine(outputDirectory, "assets"));
        BrandTheme theme = BrandTheme.CreateDefault();
        string data = SampleResources.SampleData;
        List<string> files = new List<string>();

        // One JSON for every language: texts are [[key]] references, translated from the JSON's own "texts" section.
        TemplateJsonDocument exported = TemplateJson.Export(
            new OrderConfirmation(theme),
            new TemplateBuildOptions { Format = TemplateFormat.Docx, Language = Languages[0], Texts = SampleTexts.Catalog },
            new TemplateJsonExportOptions { Components = Components, TextLanguages = [CultureInfo.GetCultureInfo("de"), CultureInfo.GetCultureInfo("en")] });
        files.Add(Write(outputDirectory, "order-confirmation.json", System.Text.Encoding.UTF8.GetBytes(exported.Json)));
        foreach ((string key, byte[] asset) in exported.Assets)
        {
            files.Add(Write(Path.Combine(outputDirectory, "assets"), key.Replace("sha256:", string.Empty, StringComparison.Ordinal)[..16] + ".png", asset));
        }

        int lines = exported.Json.Count(c => c == '\n');
        log.WriteLine($"order-confirmation.json: {exported.Json.Length} characters, {lines} lines, about {exported.Json.Length / 4} tokens");
        ITemplateDefinition imported = TemplateJson.Import(exported.Json, new TemplateJsonImportOptions
        {
            Assets = exported.Assets,
            Components = Components,
            Data = DataCatalog.From<OrderData>(),
            AllowRawXml = true,
        });

        foreach (CultureInfo language in Languages)
        {
            // The C# definition translates from the catalog; the imported JSON needs none.
            TemplateBuildOptions options = new TemplateBuildOptions { Format = TemplateFormat.Docx, Language = language, Texts = SampleTexts.Catalog };
            string name = $"order-confirmation.{language.Name}";
            foreach (TemplateFormat format in new[] { TemplateFormat.Docx, TemplateFormat.Odt })
            {
                TemplateBuildOptions formatOptions = options with { Format = format };
                GeneratedTemplate fromJson = TemplateBuilder.Build(imported, formatOptions with { Texts = TextCatalog.Empty });
                GeneratedTemplate fromCSharp = TemplateBuilder.Build(new OrderConfirmation(theme), formatOptions);
                if (fromJson.ContentHash != fromCSharp.ContentHash)
                {
                    throw new InvalidOperationException($"{name}{fromJson.FileExtension}: the template built from JSON differs from the C# one.");
                }

                files.Add(Write(outputDirectory, $"{name}.from-json{fromJson.FileExtension}", fromJson.Content.ToArray()));
                using Stream input = fromJson.OpenRead();
                using MemoryStream output = new MemoryStream();
                ProcessingResult result = new TemplateProcessor(new PlaceholderReplacementOptions { Culture = language, MissingVariableBehavior = MissingVariableBehavior.ThrowException })
                    .ProcessTemplate(input, output, data);
                if (!result.IsSuccess)
                {
                    throw new InvalidOperationException(result.ErrorMessage);
                }

                files.Add(Write(outputDirectory, $"{name}.from-json.filled{fromJson.FileExtension}", output.ToArray()));
                log.WriteLine($"{name}.from-json{fromJson.FileExtension}: same bytes as C# ({fromJson.ContentHash[..12]})");
            }
        }

        return files;
    }

    private static string Write(string directory, string name, byte[] content)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllBytes(path, content);
        return path;
    }
}
