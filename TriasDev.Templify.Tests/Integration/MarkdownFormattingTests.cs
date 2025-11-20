// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Integration tests for markdown formatting support in placeholder values.
/// Tests that markdown syntax (**bold**, *italic*, ~~strikethrough~~) in variable values
/// renders with proper formatting in the output document.
/// </summary>
public sealed class MarkdownFormattingTests
{
    [Fact]
    public void ProcessTemplate_BoldMarkdown_RendersBoldText()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "My name is **Alice**"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Equal("My name is Alice", text);

        // Verify we have 2 runs: plain text + bold text
        List<Run> runs = verifier.GetRuns(0);
        Assert.Equal(2, runs.Count);

        // First run: "My name is " (plain)
        Assert.Equal("My name is ", runs[0].InnerText);
        RunProperties? props1 = runs[0].RunProperties;
        Assert.True(props1 == null || props1.GetFirstChild<Bold>() == null);

        // Second run: "Alice" (bold)
        Assert.Equal("Alice", runs[1].InnerText);
        RunProperties? props2 = runs[1].RunProperties;
        Assert.NotNull(props2);
        Assert.NotNull(props2.GetFirstChild<Bold>());
    }

    [Fact]
    public void ProcessTemplate_ItalicMarkdown_RendersItalicText()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "This is *important* text"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Equal("This is important text", text);

        // Verify we have 3 runs: plain + italic + plain
        List<Run> runs = verifier.GetRuns(0);
        Assert.Equal(3, runs.Count);

        // First run: "This is " (plain)
        Assert.Equal("This is ", runs[0].InnerText);

        // Second run: "important" (italic)
        Assert.Equal("important", runs[1].InnerText);
        RunProperties? props2 = runs[1].RunProperties;
        Assert.NotNull(props2);
        Assert.NotNull(props2.GetFirstChild<Italic>());

        // Third run: " text" (plain)
        Assert.Equal(" text", runs[2].InnerText);
    }

    [Fact]
    public void ProcessTemplate_StrikethroughMarkdown_RendersStrikethroughText()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "This is ~~deleted~~ text"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Equal("This is deleted text", text);

        // Verify we have 3 runs: plain + strikethrough + plain
        List<Run> runs = verifier.GetRuns(0);
        Assert.Equal(3, runs.Count);

        // Second run: "deleted" (strikethrough)
        Assert.Equal("deleted", runs[1].InnerText);
        RunProperties? props = runs[1].RunProperties;
        Assert.NotNull(props);
        Assert.NotNull(props.GetFirstChild<Strike>());
    }

    [Fact]
    public void ProcessTemplate_BoldItalicMarkdown_RendersBoldAndItalicText()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "This is ***very important*** text"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Equal("This is very important text", text);

        // Verify we have 3 runs
        List<Run> runs = verifier.GetRuns(0);
        Assert.Equal(3, runs.Count);

        // Second run: "very important" (bold + italic)
        Assert.Equal("very important", runs[1].InnerText);
        RunProperties? props = runs[1].RunProperties;
        Assert.NotNull(props);
        Assert.NotNull(props.GetFirstChild<Bold>());
        Assert.NotNull(props.GetFirstChild<Italic>());
    }

    [Fact]
    public void ProcessTemplate_MixedMarkdown_RendersAllFormats()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "Normal **bold** and *italic* and ~~strike~~"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Equal("Normal bold and italic and strike", text);

        // Verify multiple runs with different formatting
        List<Run> runs = verifier.GetRuns(0);
        Assert.True(runs.Count >= 6); // At least: plain, bold, plain, italic, plain, strike

        // Find and verify the bold run
        Run? boldRun = runs.FirstOrDefault(r => r.InnerText == "bold");
        Assert.NotNull(boldRun);
        Assert.NotNull(boldRun.RunProperties?.GetFirstChild<Bold>());

        // Find and verify the italic run
        Run? italicRun = runs.FirstOrDefault(r => r.InnerText == "italic");
        Assert.NotNull(italicRun);
        Assert.NotNull(italicRun.RunProperties?.GetFirstChild<Italic>());

        // Find and verify the strikethrough run
        Run? strikeRun = runs.FirstOrDefault(r => r.InnerText == "strike");
        Assert.NotNull(strikeRun);
        Assert.NotNull(strikeRun.RunProperties?.GetFirstChild<Strike>());
    }

    [Fact]
    public void ProcessTemplate_MarkdownWithTemplateFormatting_MergesFormatting()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        RunProperties redFormatting = DocumentBuilder.CreateFormatting(color: "FF0000"); // Red
        builder.AddParagraph("{{Message}}", redFormatting);

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "Red text with **bold**"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Equal("Red text with bold", text);

        // Verify runs have red color
        List<Run> runs = verifier.GetRuns(0);
        Assert.True(runs.Count >= 2);

        // Find the bold run
        Run? boldRun = runs.FirstOrDefault(r => r.InnerText == "bold");
        Assert.NotNull(boldRun);
        RunProperties? props = boldRun.RunProperties;
        Assert.NotNull(props);

        // Should have both red color (from template) and bold (from markdown)
        Assert.NotNull(props.GetFirstChild<Bold>());
        Color? color = props.GetFirstChild<Color>();
        Assert.NotNull(color);
        Assert.Equal("FF0000", color.Val?.Value);
    }

    [Fact]
    public void ProcessTemplate_MarkdownInLoop_RendersFormattingForEachIteration()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("- {{.}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string>
            {
                "Item **one**",
                "Item *two*",
                "Item ~~three~~"
            }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        List<string> paragraphs = verifier.GetAllParagraphTexts();

        Assert.Equal(3, paragraphs.Count);
        Assert.Equal("- Item one", paragraphs[0]);
        Assert.Equal("- Item two", paragraphs[1]);
        Assert.Equal("- Item three", paragraphs[2]);

        // Verify formatting in each iteration
        // Paragraph 0: should have bold "one"
        List<Run> runs0 = verifier.GetRuns(0);
        Run? boldRun = runs0.FirstOrDefault(r => r.InnerText == "one");
        Assert.NotNull(boldRun);
        Assert.NotNull(boldRun.RunProperties?.GetFirstChild<Bold>());

        // Paragraph 1: should have italic "two"
        List<Run> runs1 = verifier.GetRuns(1);
        Run? italicRun = runs1.FirstOrDefault(r => r.InnerText == "two");
        Assert.NotNull(italicRun);
        Assert.NotNull(italicRun.RunProperties?.GetFirstChild<Italic>());

        // Paragraph 2: should have strikethrough "three"
        List<Run> runs2 = verifier.GetRuns(2);
        Run? strikeRun = runs2.FirstOrDefault(r => r.InnerText == "three");
        Assert.NotNull(strikeRun);
        Assert.NotNull(strikeRun.RunProperties?.GetFirstChild<Strike>());
    }

    [Fact]
    public void ProcessTemplate_MalformedMarkdown_RendersLiterally()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "Hello **world"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        // Malformed markdown should be rendered as-is
        Assert.Equal("Hello **world", text);
    }

    [Fact]
    public void ProcessTemplate_PlainText_WorksWithoutMarkdown()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "Plain text without any markdown"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Equal("Plain text without any markdown", text);

        // Should have single run (no markdown detected)
        List<Run> runs = verifier.GetRuns(0);
        Assert.Single(runs);
    }

    [Fact]
    public void ProcessTemplate_TextBeforeAndAfterPlaceholder_PreservesWithMarkdown()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Prefix {{Message}} suffix");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "with **bold**"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Equal("Prefix with bold suffix", text);

        // Should have runs for: prefix, "with ", bold, " suffix"
        List<Run> runs = verifier.GetRuns(0);
        Assert.True(runs.Count >= 4);

        // Find the bold run
        Run? boldRun = runs.FirstOrDefault(r => r.InnerText == "bold");
        Assert.NotNull(boldRun);
        Assert.NotNull(boldRun.RunProperties?.GetFirstChild<Bold>());
    }

    [Fact]
    public void ProcessTemplate_MultipleMarkdownPlaceholders_RendersAllCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message1}}");
        builder.AddParagraph("{{Message2}}");
        builder.AddParagraph("{{Message3}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message1"] = "First **bold**",
            ["Message2"] = "Second *italic*",
            ["Message3"] = "Third ~~strike~~"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);

        // Verify paragraph 1 has bold
        List<Run> runs1 = verifier.GetRuns(0);
        Run? boldRun = runs1.FirstOrDefault(r => r.InnerText == "bold");
        Assert.NotNull(boldRun);
        Assert.NotNull(boldRun.RunProperties?.GetFirstChild<Bold>());

        // Verify paragraph 2 has italic
        List<Run> runs2 = verifier.GetRuns(1);
        Run? italicRun = runs2.FirstOrDefault(r => r.InnerText == "italic");
        Assert.NotNull(italicRun);
        Assert.NotNull(italicRun.RunProperties?.GetFirstChild<Italic>());

        // Verify paragraph 3 has strikethrough
        List<Run> runs3 = verifier.GetRuns(2);
        Run? strikeRun = runs3.FirstOrDefault(r => r.InnerText == "strike");
        Assert.NotNull(strikeRun);
        Assert.NotNull(strikeRun.RunProperties?.GetFirstChild<Strike>());
    }
}
