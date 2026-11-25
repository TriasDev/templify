// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;
using TriasDev.Templify.Tests.Integration;

namespace TriasDev.Templify.Tests.Odt;

/// <summary>
/// OpenDocument counterpart of <see cref="BlockMarkerWithTextTests"/> (#253): text next to a block marker is removed
/// with the marker's paragraph, row or list item; processing warns (output unchanged), validation fails.
/// </summary>
public sealed class OdtBlockMarkerWithTextTests
{
    public static TheoryData<string, string[], string[], string[]> LossyTemplates => BlockMarkerWithTextTests.LossyTemplates;

    public static TheoryData<string, string[]> LosslessTemplates => BlockMarkerWithTextTests.LosslessTemplates;

    [Theory]
    [MemberData(nameof(LossyTemplates))]
    public void ProcessTemplate_TextNextToBlockMarker_IsReportedAndOutputUnchanged(
        string name, string[] paragraphs, string[] expectedOutput, string[] lostTexts)
    {
        Assert.NotEmpty(name);
        (ProcessingResult result, OdtDocumentVerifier output) = OdtTestHelper.Process(Build(paragraphs), CreateData());

        Assert.Equal(expectedOutput, output.GetParagraphTexts().Where(t => t.Length > 0));
        AssertReported(result.Warnings.Where(w => w.Type == ProcessingWarningType.BlockMarkerWithText).Select(w => w.Message), lostTexts);
    }

    [Theory]
    [MemberData(nameof(LossyTemplates))]
    public void ValidateTemplate_TextNextToBlockMarker_IsAnError(
        string name, string[] paragraphs, string[] expectedOutput, string[] lostTexts)
    {
        Assert.NotEmpty(name);
        Assert.NotNull(expectedOutput);

        foreach (ValidationResult result in ValidateWithAndWithoutData(Build(paragraphs).ToBytes()))
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
        OdtDocumentBuilder template = Build(paragraphs);

        (ProcessingResult result, _) = OdtTestHelper.Process(template, CreateData());

        Assert.DoesNotContain(result.Warnings, w => w.Type == ProcessingWarningType.BlockMarkerWithText);
        foreach (ValidationResult validation in ValidateWithAndWithoutData(template.ToBytes()))
        {
            Assert.True(validation.IsValid, string.Join("; ", validation.Errors.Select(e => e.Message)));
        }
    }

    [Fact]
    public void InlineLoop_ParagraphRemoved_ReportedAsUnsupported()
    {
        const string paragraph = "Assets: {{#foreach Items}}{{Name}}, {{/foreach}}.";
        OdtDocumentBuilder template = Build(["before", paragraph, "after"]);

        (ProcessingResult result, OdtDocumentVerifier output) = OdtTestHelper.Process(template, CreateData());

        Assert.Equal(["before", "after"], output.GetParagraphTexts());
        string expected =
            "The loop '{{#foreach Items}}' opens and closes in the same paragraph, which is not supported: " +
            $"the paragraph is removed with its text '{paragraph}'. " +
            "Put '{{#foreach}}' and '{{/foreach}}' in paragraphs of their own.";
        Assert.Equal(expected, Assert.Single(result.Warnings).Message);
        foreach (ValidationResult validation in ValidateWithAndWithoutData(template.ToBytes()))
        {
            Assert.Equal(expected, Assert.Single(validation.Errors).Message);
        }
    }

    [Fact]
    public void LoopMarkerRowWithTextInOtherCell_IsReported()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddTable(
            ["{{#foreach Items}}", "Header"],
            ["{{Name}}", "x"],
            ["{{/foreach}}", ""]);

        AssertReportedOnce(
            template,
            "Text next to the block marker '{{#foreach Items}}' is removed together with its table row: 'Header'. " +
            "Put block markers in table rows of their own.");
    }

    [Fact]
    public void LoopMarkerListItemWithMoreText_IsReported()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<text:list>" +
            "<text:list-item><text:p>{{#foreach Items}}</text:p><text:p>extra</text:p></text:list-item>" +
            "<text:list-item><text:p>{{Name}}</text:p></text:list-item>" +
            "<text:list-item><text:p>{{/foreach}}</text:p></text:list-item>" +
            "</text:list>");

        AssertReportedOnce(
            template,
            "Text next to the block marker '{{#foreach Items}}' is removed together with its list item: 'extra'. " +
            "Put block markers in list items of their own.");
    }

    [Fact]
    public void ParagraphMarkersInTableCell_AreReported()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<table:table table:name=\"T\"><table:table-column/><table:table-row><table:table-cell>" +
            "<text:p>Assets: {{#foreach Items}}</text:p><text:p>{{Name}}</text:p><text:p>{{/foreach}}</text:p>" +
            "</table:table-cell></table:table-row></table:table>");

        AssertReportedOnce(template, LostTextMessage("{{#foreach Items}}", "Assets:"));
    }

    [Fact]
    public void MarkersInHeader_AreReported()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("body")
            .AddHeaderParagraph("Intro {{#if Flag}}")
            .AddHeaderParagraph("x")
            .AddHeaderParagraph("{{/if}}");

        AssertReportedOnce(template, LostTextMessage("{{#if Flag}}", "Intro"));
    }

    [Fact]
    public void MarkerInNestedLoop_IsReportedOnce()
    {
        OdtDocumentBuilder template = Build(["{{#foreach Items}}", "Sub: {{#foreach Items}}", "{{Name}}", "{{/foreach}}", "{{/foreach}}"]);

        AssertReportedOnce(template, LostTextMessage("{{#foreach Items}}", "Sub:"));
    }

    [Fact]
    public void MarkersInFrameOfInlineConditionalParagraph_AreReported()
    {
        // The paragraph of an inline conditional stays; its frames are processed like other containers.
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<text:p>Status {{#if Flag}}yes{{/if}}<draw:frame draw:name=\"Box\" text:anchor-type=\"as-char\"><draw:text-box>" +
            "<text:p>Intro {{#if Flag}}</text:p><text:p>x</text:p><text:p>{{/if}}</text:p>" +
            "</draw:text-box></draw:frame></text:p>");

        AssertReportedOnce(template, LostTextMessage("{{#if Flag}}", "Intro"));
    }

    [Fact]
    public void MarkersInSection_AreReported()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<text:section text:name=\"S\"><text:p>{{#foreach Items}}</text:p><text:p>{{Name}}</text:p><text:p>{{/foreach}} tail</text:p></text:section>");

        AssertReportedOnce(template, LostTextMessage("{{/foreach}}", "tail"));
    }

    [Fact]
    public void MarkerHeading_IsReported()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddHeading("Assets {{#if Flag}}")
            .AddParagraph("x")
            .AddParagraph("{{/if}}");

        AssertReportedOnce(template, LostTextMessage("{{#if Flag}}", "Assets"));
    }

    private static string LostTextMessage(string marker, string lostText) =>
        $"Text next to the block marker '{marker}' is removed together with its paragraph: '{lostText}'. " +
        "Put block markers in paragraphs of their own.";

    private static void AssertReportedOnce(OdtDocumentBuilder template, string expectedMessage)
    {
        (ProcessingResult result, _) = OdtTestHelper.Process(template, CreateData());

        Assert.Equal(expectedMessage, Assert.Single(result.Warnings).Message);
        foreach (ValidationResult validation in ValidateWithAndWithoutData(template.ToBytes()))
        {
            ValidationError error = Assert.Single(validation.Errors);
            Assert.Equal(ValidationErrorType.BlockMarkerWithText, error.Type);
            Assert.Equal(expectedMessage, error.Message);
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
        OdtTemplateProcessor processor = new OdtTemplateProcessor(OdtTestHelper.InvariantOptions());
        yield return processor.ValidateTemplate(new MemoryStream(template));
        yield return processor.ValidateTemplate(new MemoryStream(template), CreateData());
    }

    private static OdtDocumentBuilder Build(string[] paragraphs)
    {
        OdtDocumentBuilder builder = new OdtDocumentBuilder();
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
