// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO.Packaging;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Argument guards, invalid input documents and the UpdateFieldsOnOpen handling of <see cref="DocumentTemplateProcessor"/>.
/// </summary>
public sealed class DocumentTemplateProcessorEdgeCaseTests
{
    private static readonly Dictionary<string, object> _data = new Dictionary<string, object> { ["Name"] = "Alice" };

    // ---------- Argument guards ----------

    [Fact]
    public void ProcessTemplate_NullTemplateStream_ThrowsArgumentNullException()
    {
        DocumentTemplateProcessor processor = TemplateTestHarness.CreateInvariantProcessor();

        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => processor.ProcessTemplate(null!, new MemoryStream(), _data));
        Assert.Equal("templateStream", ex.ParamName);
    }

    [Fact]
    public void ProcessTemplate_NullOutputStream_ThrowsArgumentNullException()
    {
        DocumentTemplateProcessor processor = TemplateTestHarness.CreateInvariantProcessor();
        using MemoryStream template = new DocumentBuilder().AddParagraph("x").ToStream();

        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => processor.ProcessTemplate(template, null!, _data));
        Assert.Equal("outputStream", ex.ParamName);
    }

    [Fact]
    public void ProcessTemplate_NullData_ThrowsArgumentNullException()
    {
        DocumentTemplateProcessor processor = TemplateTestHarness.CreateInvariantProcessor();
        using MemoryStream template = new DocumentBuilder().AddParagraph("x").ToStream();

        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => processor.ProcessTemplate(template, new MemoryStream(), (Dictionary<string, object>)null!));
        Assert.Equal("data", ex.ParamName);
    }

    [Fact]
    public void ProcessTemplate_JsonOverloadNullTemplateStream_ThrowsArgumentNullException()
    {
        DocumentTemplateProcessor processor = TemplateTestHarness.CreateInvariantProcessor();

        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => processor.ProcessTemplate(null!, new MemoryStream(), "{\"Name\":\"Alice\"}"));
        Assert.Equal("templateStream", ex.ParamName);
    }

    // ---------- Invalid input documents ----------

    [Fact]
    public void ProcessTemplate_NonDocxBytes_ReturnsFailure()
    {
        DocumentTemplateProcessor processor = TemplateTestHarness.CreateInvariantProcessor();
        using MemoryStream template = new MemoryStream(Encoding.UTF8.GetBytes("this is not a zip package"));
        using MemoryStream output = new MemoryStream();

        ProcessingResult result = processor.ProcessTemplate(template, output, _data);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("Processing failed:", result.ErrorMessage);
    }

    [Fact]
    public void ProcessTemplate_EmptyStream_ReturnsMissingMainDocumentPartFailure()
    {
        // An empty, editable stream opens as an empty package, so the main document part check applies
        DocumentTemplateProcessor processor = TemplateTestHarness.CreateInvariantProcessor();
        using MemoryStream template = new MemoryStream();
        using MemoryStream output = new MemoryStream();

        ProcessingResult result = processor.ProcessTemplate(template, output, _data);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid document: MainDocumentPart is missing.", result.ErrorMessage);
    }

    [Fact]
    public void ProcessTemplate_TruncatedDocx_ReturnsFailure()
    {
        DocumentTemplateProcessor processor = TemplateTestHarness.CreateInvariantProcessor();
        byte[] docx = new DocumentBuilder().AddParagraph("Hello {{Name}}").ToStream().ToArray();
        using MemoryStream template = new MemoryStream(docx, 0, docx.Length / 2);
        using MemoryStream output = new MemoryStream();

        ProcessingResult result = processor.ProcessTemplate(template, output, _data);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("Processing failed:", result.ErrorMessage);
    }

    [Fact]
    public void ProcessTemplate_ZipWithoutMainDocumentPart_ReturnsFailure()
    {
        DocumentTemplateProcessor processor = TemplateTestHarness.CreateInvariantProcessor();
        using MemoryStream template = new MemoryStream();
        using (Package package = Package.Open(template, FileMode.Create, FileAccess.ReadWrite))
        {
            PackagePart part = package.CreatePart(new Uri("/docProps/custom.xml", UriKind.Relative), "application/xml");
            using Stream partStream = part.GetStream();
            partStream.Write(Encoding.UTF8.GetBytes("<root/>"));
        }

        template.Position = 0;
        using MemoryStream output = new MemoryStream();

        ProcessingResult result = processor.ProcessTemplate(template, output, _data);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid document: MainDocumentPart is missing.", result.ErrorMessage);
    }

    // ---------- UpdateFieldsOnOpen: Auto mode field detection ----------

    [Fact]
    public void ProcessTemplate_AutoMode_FieldOnlyInHeader_SetsUpdateFieldsOnOpen()
    {
        DocumentBuilder builder = new DocumentBuilder().AddParagraph("Hello {{Name}}").AddHeader("Header");
        AppendComplexField(builder.MainPart.HeaderParts.Single().Header!, " PAGE ");

        using TemplateTestRun run = TemplateTestHarness.Process(builder, _data, AutoOptions());

        Assert.True(run.Verifier.HasUpdateFieldsOnOpen());
    }

    [Fact]
    public void ProcessTemplate_AutoMode_FieldOnlyInFooter_SetsUpdateFieldsOnOpen()
    {
        DocumentBuilder builder = new DocumentBuilder().AddParagraph("Hello {{Name}}").AddFooter("Footer");
        AppendComplexField(builder.MainPart.FooterParts.Single().Footer!, "NUMPAGES \\* MERGEFORMAT");

        using TemplateTestRun run = TemplateTestHarness.Process(builder, _data, AutoOptions());

        Assert.True(run.Verifier.HasUpdateFieldsOnOpen());
    }

    [Fact(Skip = "Bug: #196")]
    public void ProcessTemplate_AutoMode_SimpleFieldInFooter_SetsUpdateFieldsOnOpen()
    {
        // <w:fldSimple w:instr="PAGE"/> is how many generators (and Word for some fields) write page numbers
        DocumentBuilder builder = new DocumentBuilder().AddParagraph("Hello {{Name}}").AddFooter("Footer");
        builder.MainPart.FooterParts.Single().Footer!.Append(
            new Paragraph(new SimpleField(new Run(new Text("1"))) { Instruction = " PAGE " }));

        using TemplateTestRun run = TemplateTestHarness.Process(builder, _data, AutoOptions());

        Assert.True(run.Verifier.HasUpdateFieldsOnOpen());
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(" MERGEFIELD Name ")]
    [InlineData("PAGEX")]
    public void ProcessTemplate_AutoMode_OnlyBlankOrStaticFields_DoesNotSetUpdateFieldsOnOpen(string fieldCode)
    {
        DocumentBuilder builder = new DocumentBuilder().AddParagraph("Hello {{Name}}").AddFooter("Footer");
        AppendComplexField(builder.MainPart.FooterParts.Single().Footer!, fieldCode);

        using TemplateTestRun run = TemplateTestHarness.Process(builder, _data, AutoOptions());

        Assert.False(run.Verifier.HasUpdateFieldsOnOpen());
    }

    // ---------- UpdateFieldsOnOpen: settings part handling ----------

    [Fact]
    public void ProcessTemplate_AlwaysMode_ExistingUpdateFieldsFalse_IsSetToTrueWithoutDuplicate()
    {
        DocumentBuilder builder = new DocumentBuilder().AddParagraph("Hello {{Name}}");
        DocumentSettingsPart settingsPart = builder.MainPart.AddNewPart<DocumentSettingsPart>();
        settingsPart.Settings = new Settings(new UpdateFieldsOnOpen { Val = false });
        settingsPart.Settings.Save();

        using TemplateTestRun run = TemplateTestHarness.Process(builder, _data, AlwaysOptions());

        Settings settings = run.Verifier.Document.MainDocumentPart!.DocumentSettingsPart!.Settings!;
        UpdateFieldsOnOpen element = Assert.Single(settings.Elements<UpdateFieldsOnOpen>());
        Assert.True(element.Val?.Value);
    }

    [Fact]
    public void ProcessTemplate_AlwaysMode_WithoutSettingsPart_CreatesIt()
    {
        DocumentBuilder builder = new DocumentBuilder().AddParagraph("Hello {{Name}}");
        Assert.Null(builder.MainPart.DocumentSettingsPart);

        using TemplateTestRun run = TemplateTestHarness.Process(builder, _data, AlwaysOptions());

        Assert.True(run.Verifier.HasUpdateFieldsOnOpen());
        Assert.Empty(run.Verifier.GetValidationErrors());
    }

    [Fact]
    public void ProcessTemplate_AlwaysMode_ExistingSettings_KeepsOtherSettings()
    {
        DocumentBuilder builder = new DocumentBuilder().AddParagraph("Hello {{Name}}");
        DocumentSettingsPart settingsPart = builder.MainPart.AddNewPart<DocumentSettingsPart>();
        settingsPart.Settings = new Settings(new DefaultTabStop { Val = 720 });
        settingsPart.Settings.Save();

        using TemplateTestRun run = TemplateTestHarness.Process(builder, _data, AlwaysOptions());

        Settings settings = run.Verifier.Document.MainDocumentPart!.DocumentSettingsPart!.Settings!;
        Assert.True(run.Verifier.HasUpdateFieldsOnOpen());
        Assert.Equal((short)720, settings.GetFirstChild<DefaultTabStop>()?.Val?.Value);
    }

    private static PlaceholderReplacementOptions AutoOptions() => new PlaceholderReplacementOptions
    {
        Culture = System.Globalization.CultureInfo.InvariantCulture,
        UpdateFieldsOnOpen = UpdateFieldsOnOpenMode.Auto
    };

    private static PlaceholderReplacementOptions AlwaysOptions() => new PlaceholderReplacementOptions
    {
        Culture = System.Globalization.CultureInfo.InvariantCulture,
        UpdateFieldsOnOpen = UpdateFieldsOnOpenMode.Always
    };

    /// <summary>
    /// Appends a paragraph with a complex field (<c>fldChar begin / instrText / separate / result / end</c>).
    /// </summary>
    private static void AppendComplexField(OpenXmlCompositeElement container, string instruction)
    {
        container.Append(new Paragraph(
            new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
            new Run(new FieldCode(instruction) { Space = SpaceProcessingModeValues.Preserve }),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
            new Run(new Text("1")),
            new Run(new FieldChar { FieldCharType = FieldCharValues.End })));
    }
}
