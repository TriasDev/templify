// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Integration tests for newline character support in placeholder values.
/// Tests that newline characters (\n, \r\n, \r) in variable values
/// render as actual line breaks (Break elements) in the output document.
/// </summary>
public sealed class NewlineHandlingTests
{
    [Fact]
    public void ProcessTemplate_SimpleNewline_RendersLineBreak()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "Line 1\nLine 2"
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
        Assert.Equal("Line 1Line 2", text); // Text without break, just concatenated

        // Verify we have runs with Break element between them
        List<Run> runs = verifier.GetRuns(0);
        Assert.True(runs.Count >= 2); // At least 2 runs

        // Verify there's a Break element in the paragraph
        Paragraph paragraph = verifier.GetParagraph(0);
        List<Break> breaks = paragraph.Descendants<Break>().ToList();
        Assert.Single(breaks);
    }

    [Fact]
    public void ProcessTemplate_MultipleConsecutiveNewlines_RendersMultipleBreaks()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "Line 1\n\n\nLine 2"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Paragraph paragraph = verifier.GetParagraph(0);
        List<Break> breaks = paragraph.Descendants<Break>().ToList();
        Assert.Equal(3, breaks.Count); // 3 breaks for "\n\n\n"
    }

    [Fact]
    public void ProcessTemplate_WindowsNewline_RendersLineBreak()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "Line 1\r\nLine 2"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Paragraph paragraph = verifier.GetParagraph(0);
        List<Break> breaks = paragraph.Descendants<Break>().ToList();
        Assert.Single(breaks);
    }

    [Fact]
    public void ProcessTemplate_CarriageReturn_RendersLineBreak()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "Line 1\rLine 2"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Paragraph paragraph = verifier.GetParagraph(0);
        List<Break> breaks = paragraph.Descendants<Break>().ToList();
        Assert.Single(breaks);
    }

    [Fact]
    public void ProcessTemplate_NewlineWithMarkdown_RendersBothBreaksAndFormatting()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "**Bold line**\n*Italic line*"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
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
    public void ProcessTemplate_NewlineAtStart_RendersBreakAtStart()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "\nLine after break"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
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
        Assert.Equal("Line after break", text);
    }

    [Fact]
    public void ProcessTemplate_NewlineAtEnd_RendersBreakAtEnd()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "Line before break\n"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
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
        Assert.Equal("Line before break", text);
    }

    [Fact]
    public void ProcessTemplate_NewlineWithFormatting_PreservesFormatting()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        RunProperties boldFormatting = DocumentBuilder.CreateFormatting(bold: true);
        builder.AddParagraph("{{Message}}", boldFormatting);

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "Line 1\nLine 2"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        List<Run> runs = verifier.GetRuns(0);

        // All text runs (not break runs) should be bold
        foreach (Run run in runs.Where(r => !string.IsNullOrEmpty(r.InnerText)))
        {
            RunProperties? props = run.RunProperties;
            Assert.NotNull(props);
            Assert.NotNull(props.GetFirstChild<Bold>());
        }
    }

    [Fact]
    public void ProcessTemplate_NewlinesInLoop_RendersBreaksInEachIteration()
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
                "Item 1\nLine 2",
                "Item 2\nLine 2"
            }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
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
    public void ProcessTemplate_NewlineSupportDisabled_LeavesNewlinesAsIs()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "Line 1\nLine 2"
        };

        // Disable newline support
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            EnableNewlineSupport = false
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);

        // No Break elements should be added
        Paragraph paragraph = verifier.GetParagraph(0);
        List<Break> breaks = paragraph.Descendants<Break>().ToList();
        Assert.Empty(breaks);

        // The newline should still be in the text (as a character)
        // Note: It may render as space or be ignored by Word, but Break elements shouldn't be added
    }

    [Fact]
    public void ProcessTemplate_TextBeforeAndAfterPlaceholder_PreservesWithNewlines()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Prefix {{Message}} suffix");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "Line 1\nLine 2"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Contains("Prefix", text);
        Assert.Contains("Line 1", text);
        Assert.Contains("Line 2", text);
        Assert.Contains("suffix", text);

        // Verify break exists
        Paragraph paragraph = verifier.GetParagraph(0);
        List<Break> breaks = paragraph.Descendants<Break>().ToList();
        Assert.Single(breaks);
    }

    [Fact]
    public void ProcessTemplate_MixedNewlineFormats_RendersAllAsBreaks()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        // Mix of Windows (\r\n), Unix (\n), and old Mac (\r) line endings
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "Windows\r\nUnix\nMac\rEnd"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Paragraph paragraph = verifier.GetParagraph(0);
        List<Break> breaks = paragraph.Descendants<Break>().ToList();
        Assert.Equal(3, breaks.Count); // 3 line breaks
    }

    [Fact]
    public void ProcessTemplate_ComplexMarkdownAndNewlines_RendersCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Message"] = "**Bold** on line 1\n*Italic* on line 2\n~~Strikethrough~~ on line 3"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Paragraph paragraph = verifier.GetParagraph(0);

        // Verify breaks
        List<Break> breaks = paragraph.Descendants<Break>().ToList();
        Assert.Equal(2, breaks.Count);

        // Verify formatting
        List<Run> runs = verifier.GetRuns(0);

        Run? boldRun = runs.FirstOrDefault(r => r.InnerText == "Bold");
        Assert.NotNull(boldRun);
        Assert.NotNull(boldRun.RunProperties?.GetFirstChild<Bold>());

        Run? italicRun = runs.FirstOrDefault(r => r.InnerText == "Italic");
        Assert.NotNull(italicRun);
        Assert.NotNull(italicRun.RunProperties?.GetFirstChild<Italic>());

        Run? strikeRun = runs.FirstOrDefault(r => r.InnerText == "Strikethrough");
        Assert.NotNull(strikeRun);
        Assert.NotNull(strikeRun.RunProperties?.GetFirstChild<Strike>());
    }
}
