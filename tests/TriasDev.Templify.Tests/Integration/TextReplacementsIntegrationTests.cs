// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;
using TriasDev.Templify.Replacements;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Integration tests for text replacement functionality.
/// Tests that HTML entities and custom text patterns in variable values
/// are correctly transformed before being inserted into Word documents.
/// </summary>
public sealed class TextReplacementsIntegrationTests
{
    [Fact]
    public void ProcessTemplate_HtmlLineBreak_RendersAsWordBreak()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "Line 1<br>Line 2"
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            TextReplacements = TextReplacements.HtmlEntities
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Equal("Line 1Line 2", text); // Text without break, concatenated

        // Verify Break element exists
        Paragraph paragraph = verifier.GetParagraph(0);
        List<Break> breaks = paragraph.Descendants<Break>().ToList();
        Assert.Single(breaks);
    }

    [Theory]
    [InlineData("<br>")]
    [InlineData("<br/>")]
    [InlineData("<br />")]
    [InlineData("<BR>")]
    [InlineData("<BR/>")]
    [InlineData("<BR />")]
    public void ProcessTemplate_AllLineBreakVariations_RenderAsWordBreaks(string brTag)
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = $"Before{brTag}After"
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            TextReplacements = TextReplacements.HtmlEntities
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Paragraph paragraph = verifier.GetParagraph(0);
        List<Break> breaks = paragraph.Descendants<Break>().ToList();
        Assert.Single(breaks);

        string text = verifier.GetParagraphText(0);
        Assert.Equal("BeforeAfter", text);
    }

    [Fact]
    public void ProcessTemplate_MultipleHtmlLineBreaks_RendersMultipleBreaks()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "Line 1<br>Line 2<br/>Line 3<br />Line 4"
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            TextReplacements = TextReplacements.HtmlEntities
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Paragraph paragraph = verifier.GetParagraph(0);
        List<Break> breaks = paragraph.Descendants<Break>().ToList();
        Assert.Equal(3, breaks.Count);
    }

    [Fact]
    public void ProcessTemplate_HtmlEntities_ReplacedCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "5 &lt; 10 &amp; 10 &gt; 5"
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            TextReplacements = TextReplacements.HtmlEntities
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Equal("5 < 10 & 10 > 5", text);
    }

    [Fact]
    public void ProcessTemplate_NonBreakingSpace_ReplacedCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "Hello&nbsp;World"
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            TextReplacements = TextReplacements.HtmlEntities
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Equal("Hello\u00A0World", text);
    }

    [Fact]
    public void ProcessTemplate_QuotesAndApostrophes_ReplacedCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "He said &quot;Hello&quot; and &apos;Goodbye&apos;"
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            TextReplacements = TextReplacements.HtmlEntities
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Equal("He said \"Hello\" and 'Goodbye'", text);
    }

    [Fact]
    public void ProcessTemplate_WithoutTextReplacements_HtmlTagsRemainLiteral()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "Line 1<br>Line 2"
        };

        // No TextReplacements option set
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Equal("Line 1<br>Line 2", text); // <br> remains as literal text

        // No Break elements should exist
        Paragraph paragraph = verifier.GetParagraph(0);
        List<Break> breaks = paragraph.Descendants<Break>().ToList();
        Assert.Empty(breaks);
    }

    [Fact]
    public void ProcessTemplate_CustomReplacements_WorkCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "Welcome to COMPANY_NAME! Contact us at SUPPORT_EMAIL."
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            TextReplacements = new Dictionary<string, string>
            {
                ["COMPANY_NAME"] = "Acme Corp",
                ["SUPPORT_EMAIL"] = "support@acme.com"
            }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Equal("Welcome to Acme Corp! Contact us at support@acme.com.", text);
    }

    [Fact]
    public void ProcessTemplate_CombinedPresetAndCustomReplacements_WorkCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "COMPANY_NAME &amp; Partners<br>Contact: SUPPORT_EMAIL"
        };

        // Combine HtmlEntities preset with custom replacements
        var replacements = new Dictionary<string, string>(TextReplacements.HtmlEntities)
        {
            ["COMPANY_NAME"] = "Acme Corp",
            ["SUPPORT_EMAIL"] = "support@acme.com"
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            TextReplacements = replacements
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Equal("Acme Corp & PartnersContact: support@acme.com", text);

        // Verify Break element from <br>
        Paragraph paragraph = verifier.GetParagraph(0);
        List<Break> breaks = paragraph.Descendants<Break>().ToList();
        Assert.Single(breaks);
    }

    [Fact]
    public void ProcessTemplate_HtmlBreaksWithMarkdown_BothWorkTogether()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "**Bold line**<br>*Italic line*"
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            TextReplacements = TextReplacements.HtmlEntities
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Paragraph paragraph = verifier.GetParagraph(0);

        // Verify break exists
        List<Break> breaks = paragraph.Descendants<Break>().ToList();
        Assert.Single(breaks);

        // Verify bold formatting
        List<Run> runs = verifier.GetRuns(0);
        Run? boldRun = runs.FirstOrDefault(r => r.InnerText == "Bold line");
        Assert.NotNull(boldRun);
        Assert.NotNull(boldRun.RunProperties?.GetFirstChild<Bold>());

        // Verify italic formatting
        Run? italicRun = runs.FirstOrDefault(r => r.InnerText == "Italic line");
        Assert.NotNull(italicRun);
        Assert.NotNull(italicRun.RunProperties?.GetFirstChild<Italic>());
    }

    [Fact]
    public void ProcessTemplate_HtmlBreaksInLoop_WorksCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("{{.}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string>
            {
                "Item 1<br>Detail 1",
                "Item 2<br>Detail 2"
            }
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            TextReplacements = TextReplacements.HtmlEntities
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);

        // Each paragraph should have a break
        Paragraph para0 = verifier.GetParagraph(0);
        Assert.Single(para0.Descendants<Break>());

        Paragraph para1 = verifier.GetParagraph(1);
        Assert.Single(para1.Descendants<Break>());
    }

    [Fact]
    public void ProcessTemplate_PreservesFormatting_WithHtmlReplacements()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        RunProperties boldFormatting = DocumentBuilder.CreateFormatting(bold: true);
        builder.AddParagraph("{{Message}}", boldFormatting);

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "Line 1<br>Line 2"
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            TextReplacements = TextReplacements.HtmlEntities
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        List<Run> runs = verifier.GetRuns(0);

        // All text runs should be bold
        Assert.All(
            runs.Where(r => !string.IsNullOrEmpty(r.InnerText)),
            run =>
            {
                RunProperties? props = run.RunProperties;
                Assert.NotNull(props);
                Assert.NotNull(props.GetFirstChild<Bold>());
            });
    }

    [Fact]
    public void ProcessTemplate_MixedHtmlAndNativeNewlines_BothWork()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        // Mix of HTML <br> and native \n newlines
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "HTML break<br>Native newline\nAnother line"
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            TextReplacements = TextReplacements.HtmlEntities
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Paragraph paragraph = verifier.GetParagraph(0);
        List<Break> breaks = paragraph.Descendants<Break>().ToList();
        Assert.Equal(2, breaks.Count); // 1 from <br>, 1 from \n
    }
}
