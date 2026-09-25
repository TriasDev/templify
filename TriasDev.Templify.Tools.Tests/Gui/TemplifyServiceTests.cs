// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
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
}
