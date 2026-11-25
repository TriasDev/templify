// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>
/// Builds definitions and checks the invariants every builder test holds (design spec, section 5).
/// </summary>
internal static class BuilderHarness
{
    public static readonly TemplateFormat[] Formats = { TemplateFormat.Docx, TemplateFormat.Odt };

    public static TemplateBuildOptions Options(TemplateFormat format, string language = "en-US", ITextCatalog? texts = null) =>
        new TemplateBuildOptions { Format = format, Language = CultureInfo.GetCultureInfo(language), Texts = texts ?? TextCatalog.Empty };

    public static GeneratedTemplate Build(ITemplateDefinition definition, TemplateFormat format) =>
        TemplateBuilder.Build(definition, Options(format));

    /// <summary>
    /// Builds the definition, checks the invariants and fills it with <paramref name="json"/>.
    /// </summary>
    /// <returns>The built template and the filled document.</returns>
    public static (GeneratedTemplate Template, byte[] Filled) BuildAndFill(ITemplateDefinition definition, TemplateFormat format, string json) =>
        BuildAndFill(definition, Options(format), (processor, input, output) => processor.ProcessTemplate(input, output, json));

    /// <summary>As <see cref="BuildAndFill(ITemplateDefinition, TemplateFormat, string)"/>, with explicit build options (language, texts).</summary>
    public static (GeneratedTemplate Template, byte[] Filled) BuildAndFill(ITemplateDefinition definition, TemplateBuildOptions options, string json) =>
        BuildAndFill(definition, options, (processor, input, output) => processor.ProcessTemplate(input, output, json));

    /// <summary>As <see cref="BuildAndFill(ITemplateDefinition, TemplateFormat, string)"/>, with data as a dictionary.</summary>
    public static (GeneratedTemplate Template, byte[] Filled) BuildAndFill(ITemplateDefinition definition, TemplateFormat format, Dictionary<string, object> data) =>
        BuildAndFill(definition, Options(format), (processor, input, output) => processor.ProcessTemplate(input, output, data));

    /// <summary>As <see cref="BuildAndFill(ITemplateDefinition, TemplateFormat, string)"/>, with read-only data (a contract's witness).</summary>
    public static (GeneratedTemplate Template, byte[] Filled) BuildAndFill(ITemplateDefinition definition, TemplateFormat format, IReadOnlyDictionary<string, object?> data) =>
        BuildAndFill(definition, Options(format), (processor, input, output) => processor.ProcessTemplate(input, output, data));

    /// <summary>The options every fill uses: missing variables throw, invariant culture.</summary>
    public static PlaceholderReplacementOptions FillOptions => new PlaceholderReplacementOptions
    {
        MissingVariableBehavior = MissingVariableBehavior.ThrowException,
        Culture = CultureInfo.InvariantCulture,
    };

    private static (GeneratedTemplate Template, byte[] Filled) BuildAndFill(
        ITemplateDefinition definition,
        TemplateBuildOptions options,
        Func<TemplateProcessor, Stream, Stream, ProcessingResult> fill)
    {
        TemplateFormat format = options.Format;
        GeneratedTemplate template = TemplateBuilder.Build(definition, options);
        AssertInvariants(definition, template, options);

        TemplateProcessor processor = new TemplateProcessor(FillOptions);
        using Stream input = template.OpenRead();
        using MemoryStream output = new MemoryStream();
        ProcessingResult result = fill(processor, input, output);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.False(result.HasWarnings, string.Join("; ", result.Warnings.Select(w => w.Message)));

        byte[] filled = output.ToArray();
        AssertPackage(format, filled);
        return (template, filled);
    }

    /// <summary>
    /// The invariants of a built template, checked by the test kit (<see cref="Testing.TemplateVerifier.CheckTemplate"/>):
    /// syntax, a complete contract (the witness fills it without missing variables or warnings), the package; plus
    /// byte determinism.
    /// </summary>
    public static void AssertInvariants(ITemplateDefinition definition, GeneratedTemplate template, TemplateBuildOptions? options = null)
    {
        IReadOnlyList<string> problems = Testing.TemplateVerifier.CheckTemplate(template);
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));

        GeneratedTemplate again = TemplateBuilder.Build(definition, options ?? Options(template.Format));
        Assert.Equal(template.ContentHash, again.ContentHash);
    }

    /// <summary>DOCX: OOXML schema validation. ODT: package structure (mimetype first, stored, no extra field; manifest).</summary>
    public static void AssertPackage(TemplateFormat format, byte[] package)
    {
        IReadOnlyList<string> problems = Testing.TemplateVerifier.CheckPackage(format, package);
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// Edits one entry of a package as text, the way a hand edit changes a document; the entry order is kept and the
    /// ODF <c>mimetype</c> stays stored.
    /// </summary>
    public static byte[] EditEntry(byte[] package, string entryName, Func<string, string> edit)
    {
        List<(string Name, byte[] Data)> entries = new List<(string, byte[])>();
        using (MemoryStream input = new MemoryStream(package))
        using (System.IO.Compression.ZipArchive archive = new System.IO.Compression.ZipArchive(input, System.IO.Compression.ZipArchiveMode.Read))
        {
            foreach (System.IO.Compression.ZipArchiveEntry entry in archive.Entries)
            {
                using Stream entryStream = entry.Open();
                using MemoryStream data = new MemoryStream();
                entryStream.CopyTo(data);
                byte[] content = data.ToArray();
                if (entry.FullName == entryName)
                {
                    content = System.Text.Encoding.UTF8.GetBytes(edit(System.Text.Encoding.UTF8.GetString(content)));
                }

                entries.Add((entry.FullName, content));
            }
        }

        using MemoryStream output = new MemoryStream();
        using (System.IO.Compression.ZipArchive archive = new System.IO.Compression.ZipArchive(output, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, byte[] data) in entries)
            {
                System.IO.Compression.ZipArchiveEntry entry = archive.CreateEntry(
                    name,
                    name == "mimetype" ? System.IO.Compression.CompressionLevel.NoCompression : System.IO.Compression.CompressionLevel.Optimal);
                using Stream entryStream = entry.Open();
                entryStream.Write(data);
            }
        }

        return output.ToArray();
    }

    /// <summary>The main XML part of a template: <c>word/document.xml</c> or <c>content.xml</c>.</summary>
    public static string MainPart(TemplateFormat format) => format == TemplateFormat.Docx ? "word/document.xml" : "content.xml";

    /// <summary>Reads one entry of a package as XML.</summary>
    public static System.Xml.Linq.XDocument ReadXml(byte[] package, string entryName)
    {
        using MemoryStream stream = new MemoryStream(package);
        using System.IO.Compression.ZipArchive archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
        System.IO.Compression.ZipArchiveEntry entry = archive.GetEntry(entryName)
            ?? throw new InvalidOperationException($"The package has no entry '{entryName}'.");
        using Stream entryStream = entry.Open();
        return System.Xml.Linq.XDocument.Load(entryStream);
    }

    /// <summary>The rendered text of the body's paragraphs.</summary>
    public static List<string> ParagraphTexts(TemplateFormat format, byte[] package)
    {
        if (format == TemplateFormat.Odt)
        {
            return new OdtDocumentVerifier(package).GetParagraphTexts();
        }

        using MemoryStream stream = new MemoryStream(package);
        using WordprocessingDocument document = WordprocessingDocument.Open(stream, false);
        return document.MainDocumentPart!.Document!.Body!
            .Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
            .Select(DocxText)
            .ToList();
    }

    private static string DocxText(DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph)
    {
        System.Text.StringBuilder text = new System.Text.StringBuilder();
        foreach (DocumentFormat.OpenXml.OpenXmlElement element in paragraph.Descendants())
        {
            switch (element)
            {
                case DocumentFormat.OpenXml.Wordprocessing.Text t:
                    text.Append(t.Text);
                    break;
                case DocumentFormat.OpenXml.Wordprocessing.TabChar:
                    text.Append('\t');
                    break;
                case DocumentFormat.OpenXml.Wordprocessing.Break br
                    when br.Type is null || br.Type.Value == DocumentFormat.OpenXml.Wordprocessing.BreakValues.TextWrapping:
                    text.Append('\n');
                    break;
            }
        }

        return text.ToString();
    }
}

/// <summary>A definition written as a lambda, for tests.</summary>
internal sealed class TestDefinition(Action<TemplateComposer> compose, TemplateIdentity? identity = null) : ITemplateDefinition
{
    public TemplateIdentity Identity { get; } = identity ?? new TemplateIdentity("test", 1);

    public void Compose(TemplateComposer template) => compose(template);
}
