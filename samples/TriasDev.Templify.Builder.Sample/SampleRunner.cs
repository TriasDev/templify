// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
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

    private static string Write(string directory, string name, byte[] content)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllBytes(path, content);
        return path;
    }
}
