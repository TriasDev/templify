// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// A block marker removes its whole paragraph (or table row). Text next to it is lost; processing reports it as a
/// <see cref="ProcessingWarningType.BlockMarkerWithText"/> warning (the output is unchanged) and validation as a
/// <see cref="ValidationErrorType.BlockMarkerWithText"/> error (#253).
/// </summary>
public sealed class BlockMarkerWithTextTests
{
    /// <summary>The rows of the table in #253, plus an else marker: paragraphs, output today, lost texts.</summary>
    public static TheoryData<string, string[], string[], string[]> LossyTemplates => new()
    {
        { "text before loop start", ["Assets: {{#foreach Items}}", "{{Name}}", "{{/foreach}}"], ["A", "B"], ["Assets:"] },
        { "text after loop end", ["{{#foreach Items}}", "{{Name}}", "{{/foreach}} tail"], ["A", "B"], ["tail"] },
        { "text around true conditional", ["Intro {{#if Flag}}", "shown body", "{{/if}} outro"], ["shown body"], ["Intro", "outro"] },
        { "text around false conditional", ["Intro {{#if Off}}", "hidden body", "{{/if}} outro"], [], ["Intro", "outro"] },
        { "text after else", ["{{#if Off}}", "a", "{{#else}} fallback", "b", "{{/if}}"], ["b"], ["fallback"] },
    };

    public static TheoryData<string, string[]> LosslessTemplates => new()
    {
        { "markers with whitespace", ["  {{#foreach Items}} ", "{{Name}}", "\t{{/foreach}}"] },
        { "inline conditional", ["Assets: {{#if Flag}}yes{{/if}}."] },
        { "block conditional with else", ["{{#if Off}}", "a", "{{#else}}", "b", "{{/if}}"] },
        { "nested blocks", ["{{#foreach Items}}", "{{#if Flag}}", "{{Name}}", "{{/if}}", "{{/foreach}}"] },
        { "invisible format characters", ["\u200B{{#if Flag}}\u200F", "x", "\u00AD{{/if}}\uFEFF"] },
    };

    [Theory]
    [MemberData(nameof(LossyTemplates))]
    public void ProcessTemplate_TextNextToBlockMarker_IsReportedAndOutputUnchanged(
        string name, string[] paragraphs, string[] expectedOutput, string[] lostTexts)
    {
        Assert.NotEmpty(name);
        using TemplateTestRun run = TemplateTestHarness.Process(Build(paragraphs), CreateData());

        Assert.True(run.Result.IsSuccess, run.Result.ErrorMessage);
        Assert.Equal(expectedOutput, run.Verifier.GetAllParagraphTexts().Where(t => t.Length > 0));
        AssertReported(run.Result.Warnings.Where(w => w.Type == ProcessingWarningType.BlockMarkerWithText).Select(w => w.Message), lostTexts);
    }

    [Theory]
    [MemberData(nameof(LossyTemplates))]
    public void ValidateTemplate_TextNextToBlockMarker_IsAnError(
        string name, string[] paragraphs, string[] expectedOutput, string[] lostTexts)
    {
        Assert.NotEmpty(name);
        Assert.NotNull(expectedOutput);
        byte[] template = Build(paragraphs).ToStream().ToArray();

        foreach (ValidationResult result in ValidateWithAndWithoutData(template))
        {
            Assert.False(result.IsValid);
            Assert.All(result.Errors, e => Assert.Equal(ValidationErrorType.BlockMarkerWithText, e.Type));
            AssertReported(result.Errors.Select(e => e.Message), lostTexts);
        }
    }

    [Theory]
    [MemberData(nameof(LosslessTemplates))]
    public void ProcessAndValidate_NoTextNextToBlockMarker_NothingReported(string name, string[] paragraphs)
    {
        Assert.NotEmpty(name);
        byte[] template = Build(paragraphs).ToStream().ToArray();

        using TemplateTestRun run = TemplateTestHarness.Process(new MemoryStream(template), CreateData());

        Assert.True(run.Result.IsSuccess, run.Result.ErrorMessage);
        Assert.DoesNotContain(run.Result.Warnings, w => w.Type == ProcessingWarningType.BlockMarkerWithText);
        foreach (ValidationResult result in ValidateWithAndWithoutData(template))
        {
            Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        }
    }

    [Fact]
    public void TextBeforeLoopStart_MessageNamesMarkerAndLostText()
    {
        DocumentBuilder builder = Build(["Assets: {{#foreach Items}}", "{{Name}}", "{{/foreach}}"]);

        using TemplateTestRun run = TemplateTestHarness.Process(builder, CreateData());

        ProcessingWarning warning = Assert.Single(run.Result.Warnings);
        Assert.Equal(ProcessingWarningType.BlockMarkerWithText, warning.Type);
        Assert.Equal(
            "Text next to the block marker '{{#foreach Items}}' is removed together with its paragraph: 'Assets:'. " +
            "Put block markers in paragraphs of their own.",
            warning.Message);
    }

    [Fact]
    public void InlineLoop_ParagraphRemoved_ReportedAsUnsupported()
    {
        const string paragraph = "Assets: {{#foreach Items}}{{Name}}, {{/foreach}}.";
        DocumentBuilder builder = Build(["before", paragraph, "after"]);
        byte[] template = builder.ToStream().ToArray();

        using TemplateTestRun run = TemplateTestHarness.Process(new MemoryStream(template), CreateData());

        // The output is unchanged: the paragraph is removed.
        Assert.Equal(["before", "after"], run.Verifier.GetAllParagraphTexts());
        string expected =
            "The loop '{{#foreach Items}}' opens and closes in the same paragraph, which is not supported: " +
            $"the paragraph is removed with its text '{paragraph}'. " +
            "Put '{{#foreach}}' and '{{/foreach}}' in paragraphs of their own.";
        ProcessingWarning warning = Assert.Single(run.Result.Warnings);
        Assert.Equal(ProcessingWarningType.BlockMarkerWithText, warning.Type);
        Assert.Equal(expected, warning.Message);

        foreach (ValidationResult result in ValidateWithAndWithoutData(template))
        {
            ValidationError error = Assert.Single(result.Errors);
            Assert.Equal(ValidationErrorType.BlockMarkerWithText, error.Type);
            Assert.Equal(expected, error.Message);
        }
    }

    [Fact]
    public void LoopMarkerRowWithTextInOtherCell_IsReported()
    {
        DocumentBuilder builder = new DocumentBuilder().AddTableWithCellParagraphs(3, 2, (row, column) => (row, column) switch
        {
            (0, 0) => ["{{#foreach Items}}"],
            (0, 1) => ["Header"],
            (1, 0) => ["{{Name}}"],
            (2, 0) => ["{{/foreach}}"],
            _ => [""],
        });
        byte[] template = builder.ToStream().ToArray();

        using TemplateTestRun run = TemplateTestHarness.Process(new MemoryStream(template), CreateData());

        string expected =
            "Text next to the block marker '{{#foreach Items}}' is removed together with its table row: 'Header'. " +
            "Put block markers in table rows of their own.";
        Assert.Equal(2, run.Verifier.GetTableRowCount(0));
        Assert.Equal(expected, Assert.Single(run.Result.Warnings).Message);
        foreach (ValidationResult result in ValidateWithAndWithoutData(template))
        {
            Assert.Equal(expected, Assert.Single(result.Errors).Message);
        }
    }

    [Fact]
    public void ParagraphMarkersInTableCell_AreReported()
    {
        DocumentBuilder builder = new DocumentBuilder().AddTableWithCellParagraphs(1, 1,
            (_, _) => ["Assets: {{#foreach Items}}", "{{Name}}", "{{/foreach}}"]);

        AssertReportedOnce(builder.ToStream().ToArray(), "Assets:");
    }

    [Fact]
    public void MarkersInHeader_AreReported()
    {
        DocumentBuilder builder = new DocumentBuilder()
            .AddParagraph("body")
            .AddHeaderWithParagraphs(HeaderFooterValues.Default, "Intro {{#if Flag}}", "x", "{{/if}}");

        AssertReportedOnce(builder.ToStream().ToArray(), "Intro");
    }

    [Fact]
    public void MarkerInNestedLoop_IsReportedOnce()
    {
        DocumentBuilder builder = Build(["{{#foreach Items}}", "Sub: {{#foreach Items}}", "{{Name}}", "{{/foreach}}", "{{/foreach}}"]);

        AssertReportedOnce(builder.ToStream().ToArray(), "Sub:");
    }

    [Fact]
    public void ContentControlAsLoopStartMarker_WithMoreText_IsReported()
    {
        DocumentBuilder builder = new DocumentBuilder()
            .AddElement(new SdtBlock(new SdtProperties(), new SdtContentBlock(P("{{#foreach Items}}"), P("extra"))))
            .AddParagraph("{{Name}}")
            .AddParagraph("{{/foreach}}");

        byte[] template = builder.ToStream().ToArray();
        AssertReportedOnce(template, "extra");
        using TemplateTestRun run = TemplateTestHarness.Process(new MemoryStream(template), CreateData());
        Assert.Contains("together with its content control:", Assert.Single(run.Result.Warnings).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkersInsideContentControl_AreReported()
    {
        DocumentBuilder builder = new DocumentBuilder().AddElement(new SdtBlock(
            new SdtProperties(),
            new SdtContentBlock(P("Assets: {{#foreach Items}}"), P("{{Name}}"), P("{{/foreach}}"))));

        AssertReportedOnce(builder.ToStream().ToArray(), "Assets:");
    }

    [Fact]
    public void MarkersInsideTextBox_AreReported()
    {
        DocumentBuilder builder = new DocumentBuilder().AddElement(new Paragraph(
            new Run(new Text("anchor")),
            new Run(new Picture(new DocumentFormat.OpenXml.Vml.Shape(
                new DocumentFormat.OpenXml.Vml.TextBox(new TextBoxContent(P("Intro {{#if Flag}}"), P("x"), P("{{/if}}"))))
            { Id = "tb1", Style = "width:72pt;height:36pt" }))));

        AssertReportedOnce(builder.ToStream().ToArray(), "Intro");
    }

    [Fact]
    public void MarkersInFootnote_AreReported()
    {
        DocumentBuilder builder = new DocumentBuilder();
        FootnotesPart part = builder.MainPart.AddNewPart<FootnotesPart>();
        part.Footnotes = new Footnotes(
            new Footnote(new Paragraph(new Run(new SeparatorMark()))) { Type = FootnoteEndnoteValues.Separator, Id = -1 },
            new Footnote(P("{{#if Flag}}"), P("x"), P("{{/if}} outro")) { Id = 1 });
        builder.AddElement(new Paragraph(new Run(new Text("body")), new Run(new FootnoteReference { Id = 1 })));

        AssertReportedOnce(builder.ToStream().ToArray(), "outro");
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void ValidateTemplate_DeeplyNestedLoopsInTables_IsNotExponential()
    {
        // loop > table > cell > loop > ... : each level used to double the validator's visits.
        const int Depth = 20;
        OpenXmlElement[] content = [P("Sub: {{#foreach Items}}"), P("{{Name}}"), P("{{/foreach}}")];
        for (int level = 0; level < Depth; level++)
        {
            Table table = new Table(new TableRow(new TableCell(content)));
            content = [P("{{#foreach Items}}"), table, P("{{/foreach}}")];
        }

        DocumentBuilder builder = new DocumentBuilder();
        foreach (OpenXmlElement element in content)
        {
            builder.AddElement(element);
        }

        byte[] template = builder.ToStream().ToArray();
        Stopwatch stopwatch = Stopwatch.StartNew();
        ValidationResult result = TemplateTestHarness.CreateInvariantProcessor().ValidateTemplate(new MemoryStream(template));
        stopwatch.Stop();

        Assert.Equal(ValidationErrorType.BlockMarkerWithText, Assert.Single(result.Errors).Type);
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, $"Validation took {stopwatch.ElapsedMilliseconds} ms");
    }

    private static Paragraph P(string text) => new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    private static void AssertReportedOnce(byte[] template, string lostText)
    {
        using TemplateTestRun run = TemplateTestHarness.Process(new MemoryStream(template), CreateData());

        Assert.True(run.Result.IsSuccess, run.Result.ErrorMessage);
        AssertReported(run.Result.Warnings.Select(w => w.Message), [lostText]);
        foreach (ValidationResult result in ValidateWithAndWithoutData(template))
        {
            AssertReported(result.Errors.Select(e => e.Message), [lostText]);
        }
    }

    private static void AssertReported(IEnumerable<string> messages, string[] lostTexts)
    {
        List<string> list = messages.ToList();
        Assert.Equal(lostTexts.Length, list.Count);
        foreach (string lostText in lostTexts)
        {
            Assert.Contains(list, m => m.Contains($": '{lostText}'.", StringComparison.Ordinal));
        }
    }

    private static IEnumerable<ValidationResult> ValidateWithAndWithoutData(byte[] template)
    {
        DocumentTemplateProcessor processor = TemplateTestHarness.CreateInvariantProcessor();
        yield return processor.ValidateTemplate(new MemoryStream(template));
        yield return processor.ValidateTemplate(new MemoryStream(template), CreateData());
    }

    private static DocumentBuilder Build(string[] paragraphs)
    {
        DocumentBuilder builder = new DocumentBuilder();
        foreach (string paragraph in paragraphs)
        {
            builder.AddParagraph(paragraph);
        }

        return builder;
    }

    private static Dictionary<string, object> CreateData() => new()
    {
        ["Flag"] = true,
        ["Off"] = false,
        ["Items"] = new List<Dictionary<string, object>> { new() { ["Name"] = "A" }, new() { ["Name"] = "B" } },
    };
}
