// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Gui.Models;
using TriasDev.Templify.Gui.Services;

namespace TriasDev.Templify.Tools.Tests.Gui;

public sealed class TemplifyServiceTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly TemplifyService _service = new();

    public void Dispose() => _temp.Dispose();

    private string CreateTemplate(string text)
    {
        string path = _temp.File("template.docx");
        using WordprocessingDocument document = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        MainDocumentPart mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document(new Body(new Paragraph(new Run(new Text(text)))));
        return path;
    }

    private static string ReadBodyText(string path)
    {
        using WordprocessingDocument document = WordprocessingDocument.Open(path, false);
        return document.MainDocumentPart!.Document!.Body!.InnerText;
    }

    [Fact]
    public async Task ProcessTemplate_Success_WritesOutputAndNoTempFiles()
    {
        string template = CreateTemplate("Hello {{Name}}!");
        string json = _temp.File("data.json");
        File.WriteAllText(json, """{ "Name": "World" }""");
        string output = _temp.File("out.docx");

        UiProcessingResult result = await _service.ProcessTemplateAsync(template, json, output);

        Assert.True(result.Success, result.Processing.ErrorMessage);
        Assert.Equal("Hello World!", ReadBodyText(output));
        Assert.Equal(3, Directory.GetFiles(_temp.Path).Length); // template, json, output
    }

    [Fact]
    public async Task ProcessTemplate_InvalidJson_LeavesNoOutputFile()
    {
        string template = CreateTemplate("Hello {{Name}}!");
        string json = _temp.File("data.json");
        File.WriteAllText(json, "{ not valid json");
        string output = _temp.File("out.docx");

        UiProcessingResult result = await _service.ProcessTemplateAsync(template, json, output);

        Assert.False(result.Success);
        Assert.False(File.Exists(output));
        Assert.Equal(2, Directory.GetFiles(_temp.Path).Length); // no temp leftovers
    }

    [Fact]
    public async Task ProcessTemplate_InvalidTemplate_KeepsExistingOutputUntouched()
    {
        string template = _temp.File("template.docx");
        File.WriteAllText(template, "this is not a docx");
        string json = _temp.File("data.json");
        File.WriteAllText(json, "{}");
        string output = _temp.File("out.docx");
        File.WriteAllText(output, "previous output");

        UiProcessingResult result = await _service.ProcessTemplateAsync(template, json, output);

        Assert.False(result.Success);
        Assert.Equal("previous output", File.ReadAllText(output));
        Assert.Equal(3, Directory.GetFiles(_temp.Path).Length);
    }

    [Fact]
    public async Task ProcessTemplate_OutputEqualsTemplate_FailsWithoutTouchingTemplate()
    {
        string template = CreateTemplate("Hello {{Name}}!");
        string json = _temp.File("data.json");
        File.WriteAllText(json, """{ "Name": "World" }""");

        UiProcessingResult result = await _service.ProcessTemplateAsync(template, json, template);

        Assert.False(result.Success);
        Assert.Contains("different from the template", result.Processing.ErrorMessage);
        Assert.Equal("Hello {{Name}}!", ReadBodyText(template));
    }

    [Theory]
    [InlineData("a/b.docx", "a/b.docx", true)]
    [InlineData("a/b.docx", "a/./b.docx", true)]
    [InlineData("a/B.docx", "a/b.docx", true)]
    [InlineData("a/b.docx", "a/c.docx", false)]
    [InlineData(null, "a/c.docx", false)]
    [InlineData("", "", false)]
    public void PathsAreEqual_ComparesNormalizedPaths(string? first, string? second, bool expected)
    {
        Assert.Equal(expected, TemplifyService.PathsAreEqual(first, second));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessTemplate_OpenDocumentTemplate_WritesOdt(bool asTemplate)
    {
        string template = OdtTestFile.Create(_temp.File(asTemplate ? "letter.ott" : "letter.odt"), asTemplate, "Hello {{Name}}!");
        string json = _temp.File("data.json");
        File.WriteAllText(json, """{ "Name": "World" }""");
        string output = _temp.File("out.odt");

        UiProcessingResult result = await _service.ProcessTemplateAsync(template, json, output);

        Assert.True(result.Success, result.Processing.ErrorMessage);
        Assert.True(result.Validation!.IsValid);
        Assert.Equal(new[] { "Hello World!" }, OdtTestFile.ReadParagraphs(output));
        Assert.Equal(OdtTestFile.TextMediaType, OdtTestFile.ReadMimetype(output));
        Assert.Equal(3, Directory.GetFiles(_temp.Path).Length); // template, json, output
    }

    [Fact]
    public async Task ValidateTemplate_OpenDocumentTemplate_ReportsErrorsAndMissingVariables()
    {
        string invalid = OdtTestFile.Create(_temp.File("invalid.odt"), asTemplate: false, "{{#if Flag}}", "x");
        string valid = OdtTestFile.Create(_temp.File("valid.odt"), asTemplate: false, "{{Name}} {{Other}}");
        string json = _temp.File("data.json");
        File.WriteAllText(json, """{ "Name": "World" }""");

        ValidationResult invalidResult = await _service.ValidateTemplateAsync(invalid);
        ValidationResult validResult = await _service.ValidateTemplateAsync(valid, json);

        Assert.False(invalidResult.IsValid);
        Assert.Contains(invalidResult.Errors, e => e.Message.Contains("{{#if Flag}}", StringComparison.Ordinal));
        Assert.Equal(new[] { "Other" }, validResult.MissingVariables);
    }

    [Fact]
    public async Task ProcessTemplate_UnsupportedFormat_FailsWithClearMessage()
    {
        string template = _temp.File("template.odt");
        File.WriteAllText(template, "not a package");
        string json = _temp.File("data.json");
        File.WriteAllText(json, "{}");
        string output = _temp.File("out.odt");

        UiProcessingResult result = await _service.ProcessTemplateAsync(template, json, output);

        Assert.False(result.Success);
        Assert.StartsWith("Unsupported template format", result.Processing.ErrorMessage);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void GetOutputExtension_UsesContentThenFileName()
    {
        string odtNamedDocx = OdtTestFile.Create(_temp.File("misnamed.docx"), asTemplate: false, "x");
        string ott = OdtTestFile.Create(_temp.File("letter.ott"), asTemplate: true, "x");
        string docx = CreateTemplate("x");

        Assert.Equal(".odt", TemplifyService.GetOutputExtension(odtNamedDocx));   // content wins
        Assert.Equal(".odt", TemplifyService.GetOutputExtension(ott));
        Assert.Equal(".docx", TemplifyService.GetOutputExtension(docx));
        Assert.Equal(".odt", TemplifyService.GetOutputExtension(_temp.File("missing.ott")));   // file name fallback
        Assert.Equal(".odt", TemplifyService.GetOutputExtension(_temp.File("missing.ODT")));
        Assert.Equal(".docx", TemplifyService.GetOutputExtension(_temp.File("missing.docx")));
        Assert.Equal(".docx", TemplifyService.GetOutputExtension(null));
    }

    [Fact]
    public void FileDialog_TemplateFilterAcceptsWordAndOpenDocument()
    {
        IReadOnlyList<string> patterns = FileDialogService.TemplateFileTypes[0].Patterns!;

        Assert.Equal(new[] { "*.docx", "*.odt", "*.ott" }, patterns);
        Assert.Equal(("odt", "*.odt"), (FileDialogService.GetSaveFileTypes("letter-output.odt").DefaultExtension,
            FileDialogService.GetSaveFileTypes("letter-output.odt").Choices.Single().Patterns!.Single()));
        Assert.Equal(("docx", "*.docx"), (FileDialogService.GetSaveFileTypes("warnings.docx").DefaultExtension,
            FileDialogService.GetSaveFileTypes("warnings.docx").Choices.Single().Patterns!.Single()));
    }
}
