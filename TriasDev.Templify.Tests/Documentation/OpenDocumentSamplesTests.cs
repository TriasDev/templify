// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Documentation;

/// <summary>
/// Mirrors the code samples and documented behavior of docs/for-developers/opendocument.md,
/// docs/for-template-authors/libreoffice.md and the OpenDocument sections of README.md and
/// TriasDev.Templify/README.md. When one of these tests fails, update the documentation together with the code.
/// </summary>
public sealed class OpenDocumentSamplesTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("templify-odt-docs-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static PlaceholderReplacementOptions Options() =>
        new PlaceholderReplacementOptions { Culture = CultureInfo.GetCultureInfo("en-US") };

    private static Dictionary<string, object> SampleData() => new Dictionary<string, object>
    {
        ["Name"] = "John Doe",
        ["Items"] = new List<object>
        {
            new { Product = "Service A", Price = 100m },
            new { Product = "Service B", Price = 200m },
        },
    };

    private static OdtDocumentBuilder SampleTemplate() => new OdtDocumentBuilder()
        .AddParagraph("Hello {{Name}}!")
        .AddParagraph("{{#foreach Items}}")
        .AddParagraph("{{Product}}: {{Price:currency}}")
        .AddParagraph("{{/foreach}}");

    private string Write(string fileName, byte[] content)
    {
        string path = Path.Combine(_directory, fileName);
        File.WriteAllBytes(path, content);
        return path;
    }

    private string PathOf(string fileName) => Path.Combine(_directory, fileName);

    // opendocument.md "OdtTemplateProcessor": file to file
    [Fact]
    public void OdtTemplateProcessor_FileToFile()
    {
        string templatePath = Write("template.odt", SampleTemplate().ToBytes());
        string outputPath = PathOf("output.odt");

        OdtTemplateProcessor processor = new OdtTemplateProcessor(Options());
        ProcessingResult result = processor.ProcessTemplateFile(templatePath, outputPath, SampleData());

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(
            new[] { "Hello John Doe!", "Service A: $100.00", "Service B: $200.00" },
            new OdtDocumentVerifier(File.ReadAllBytes(outputPath)).GetParagraphTexts());
    }

    // opendocument.md: "An .ott template produces an .odt document"
    [Fact]
    public void OdtTemplateProcessor_OttProducesOdt()
    {
        byte[] template = SampleTemplate().AsTemplate().ToBytes();

        ProcessingResult result = new OdtTemplateProcessor(Options()).ProcessTemplate(template, SampleData(), out byte[] output);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        OdtDocumentVerifier verifier = new OdtDocumentVerifier(output);
        Assert.Equal("application/vnd.oasis.opendocument.text", verifier.Mimetype);
        Assert.Equal("application/vnd.oasis.opendocument.text", verifier.ManifestRootMediaType);
    }

    // opendocument.md "Streams and Byte Arrays": bytes, and a write-only output stream (File.OpenWrite)
    [Fact]
    public void OdtTemplateProcessor_BytesAndWriteOnlyOutputStream()
    {
        OdtTemplateProcessor processor = new OdtTemplateProcessor(Options());
        string templatePath = Write("template.ott", SampleTemplate().AsTemplate().ToBytes());

        byte[] template = File.ReadAllBytes(templatePath);
        ProcessingResult bytesResult = processor.ProcessTemplate(template, SampleData(), out byte[] output);

        ProcessingResult streamResult;
        using (FileStream templateStream = File.OpenRead(templatePath))
        using (FileStream outputStream = File.OpenWrite(PathOf("output.odt")))
        {
            streamResult = processor.ProcessTemplate(templateStream, outputStream, SampleData());
        }

        Assert.True(bytesResult.IsSuccess, bytesResult.ErrorMessage);
        Assert.True(streamResult.IsSuccess, streamResult.ErrorMessage);
        Assert.Equal("Hello John Doe!", new OdtDocumentVerifier(output).GetParagraphTexts()[0]);
        Assert.Equal("Hello John Doe!", new OdtDocumentVerifier(File.ReadAllBytes(PathOf("output.odt"))).GetParagraphTexts()[0]);
    }

    // opendocument.md: "On failure nothing is written" and a non-ODT template is a failed result
    [Fact]
    public void OdtTemplateProcessor_WordTemplate_IsAFailedResultAndNothingIsWritten()
    {
        DocumentBuilder word = new DocumentBuilder();
        word.AddParagraph("Hello {{Name}}!");
        using MemoryStream template = word.ToStream();
        using MemoryStream output = new MemoryStream();

        ProcessingResult result = new OdtTemplateProcessor().ProcessTemplate(template, output, SampleData());

        Assert.False(result.IsSuccess);
        Assert.Contains("not an OpenDocument Text", result.ErrorMessage);
        Assert.Equal(0, output.Length);
    }

    // opendocument.md "Options": DocumentProperties go to meta.xml
    [Fact]
    public void OdtTemplateProcessor_DocumentProperties_AreWrittenToMeta()
    {
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            DocumentProperties = new DocumentProperties { Author = "Alice", Title = "Offer", Category = "Sales" },
        };

        ProcessingResult result = new OdtTemplateProcessor(options).ProcessTemplate(
            new OdtDocumentBuilder().AddParagraph("x").ToBytes(), SampleData(), out byte[] output);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        string meta = new OdtDocumentVerifier(output).GetEntryString("meta.xml");
        Assert.Contains("initial-creator>Alice<", meta);
        Assert.Contains("title>Offer<", meta);
        Assert.Contains("meta:name=\"Category\"", meta);
    }

    // opendocument.md "Validation"
    [Fact]
    public void OdtTemplateProcessor_ValidateTemplate_ReportsMissingVariables()
    {
        string templatePath = Write("template.odt", new OdtDocumentBuilder().AddParagraph("{{Name}} {{Other}}").ToBytes());

        using FileStream stream = File.OpenRead(templatePath);
        ValidationResult validation = new OdtTemplateProcessor().ValidateTemplate(stream, SampleData());

        Assert.Equal(new[] { "Other" }, validation.MissingVariables);
    }

    // opendocument.md / README.md "TemplateProcessor": one call for .docx, .odt and .ott
    [Fact]
    public void TemplateProcessor_ProcessTemplateFile_HandlesWordAndOpenDocument()
    {
        DocumentBuilder word = new DocumentBuilder();
        word.AddParagraph("Hello {{Name}}!");
        string docxTemplate = Write("template.docx", word.ToStream().ToArray());
        string odtTemplate = Write("template.odt", SampleTemplate().ToBytes());
        string ottTemplate = Write("template.ott", SampleTemplate().AsTemplate().ToBytes());

        TemplateProcessor processor = new TemplateProcessor(Options());

        Assert.True(processor.ProcessTemplateFile(docxTemplate, PathOf("output.docx"), SampleData()).IsSuccess);
        Assert.True(processor.ProcessTemplateFile(odtTemplate, PathOf("output.odt"), SampleData()).IsSuccess);
        Assert.True(processor.ProcessTemplateFile(ottTemplate, PathOf("output2.odt"), SampleData()).IsSuccess);

        using (FileStream docx = File.OpenRead(PathOf("output.docx")))
        {
            Assert.Equal(TemplateFormat.Docx, TemplateProcessor.DetectFormat(docx));
        }

        Assert.Equal("Hello John Doe!", new OdtDocumentVerifier(File.ReadAllBytes(PathOf("output.odt"))).GetParagraphTexts()[0]);
        Assert.Equal("application/vnd.oasis.opendocument.text", new OdtDocumentVerifier(File.ReadAllBytes(PathOf("output2.odt"))).Mimetype);
    }

    // opendocument.md: DetectFormat to choose the output extension; the stream position is restored
    [Theory]
    [InlineData(false, ".odt")]
    [InlineData(true, ".odt")]
    public void TemplateProcessor_DetectFormat_ChoosesExtension(bool asTemplate, string expected)
    {
        OdtDocumentBuilder builder = SampleTemplate();
        string templatePath = Write("template.bin", (asTemplate ? builder.AsTemplate() : builder).ToBytes());

        using FileStream template = File.OpenRead(templatePath);
        TemplateFormat format = TemplateProcessor.DetectFormat(template);
        string extension = format switch
        {
            TemplateFormat.Docx => ".docx",
            TemplateFormat.Odt => ".odt",
            _ => throw new NotSupportedException("Not a Word or OpenDocument Text template."),
        };

        Assert.Equal(expected, extension);
        Assert.Equal(0, template.Position);
    }

    // opendocument.md: other formats are a failed result starting with "Unsupported template format"
    [Fact]
    public void TemplateProcessor_UnsupportedFormat_IsAFailedResult()
    {
        string templatePath = Write("template.fodt", System.Text.Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><office:document/>"));
        string outputPath = PathOf("output.odt");

        ProcessingResult result = new TemplateProcessor().ProcessTemplateFile(templatePath, outputPath, SampleData());

        Assert.False(result.IsSuccess);
        Assert.StartsWith("Unsupported template format", result.ErrorMessage);
        Assert.False(File.Exists(outputPath));
    }

    // libreoffice.md "Tips": typographic quotes from AutoCorrect work in conditions
    [Fact]
    public void LibreOfficeGuide_TypographicQuotesInConditions_Work()
    {
        byte[] template = new OdtDocumentBuilder()
            .AddParagraph("{{#if Status = “Active”}}")
            .AddParagraph("Active!")
            .AddParagraph("{{/if}}")
            .ToBytes();

        ProcessingResult result = new OdtTemplateProcessor().ProcessTemplate(
            template, new Dictionary<string, object> { ["Status"] = "Active" }, out byte[] output);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new[] { "Active!" }, new OdtDocumentVerifier(output).GetParagraphTexts());
    }

    // libreoffice.md "Lists": markers in their own list items repeat the items between them
    [Fact]
    public void LibreOfficeGuide_LoopOverListItems()
    {
        byte[] template = new OdtDocumentBuilder()
            .AddXml("<text:list><text:list-item><text:p>{{#foreach Items}}</text:p></text:list-item>"
                + "<text:list-item><text:p>{{Product}}</text:p></text:list-item>"
                + "<text:list-item><text:p>{{/foreach}}</text:p></text:list-item></text:list>")
            .ToBytes();

        ProcessingResult result = new OdtTemplateProcessor().ProcessTemplate(template, SampleData(), out byte[] output);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(new[] { "Service A", "Service B" }, new OdtDocumentVerifier(output).GetParagraphTexts());
    }

    // libreoffice.md "Lists": a marker inside a longer list needs its end marker in the same list
    [Fact]
    public void LibreOfficeGuide_UnmatchedMarkerInsideLongerList_Fails()
    {
        byte[] template = new OdtDocumentBuilder()
            .AddXml("<text:list><text:list-item><text:p>{{#if Name}}</text:p></text:list-item>"
                + "<text:list-item><text:p>Item</text:p></text:list-item></text:list>")
            .AddParagraph("{{/if}}")
            .ToBytes();

        ProcessingResult result = new OdtTemplateProcessor().ProcessTemplate(template, SampleData(), out _);

        Assert.False(result.IsSuccess);
        Assert.Contains("no matching", result.ErrorMessage);
    }
}
