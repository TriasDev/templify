// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.PropertyPaths;
using TriasDev.Templify.Utilities;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Integration tests for formatting preservation (bold, italic, fonts, colors, etc.).
/// These tests create actual Word documents with formatting, process them, and verify formatting is preserved.
/// </summary>
public sealed class FormattingPreservationTests
{
    [Fact]
    public void ProcessTemplate_BoldText_PreservesFormatting()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        RunProperties boldFormatting = DocumentBuilder.CreateFormatting(bold: true);
        builder.AddParagraph("Customer: {{CustomerName}}", boldFormatting);

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["CustomerName"] = "TriasDev GmbH & Co. KG"
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
        Assert.Contains("TriasDev GmbH & Co. KG", text);

        RunProperties? runProps = verifier.GetRunProperties(0, 0);
        DocumentVerifier.VerifyFormatting(runProps, expectedBold: true);
    }

    [Fact]
    public void ProcessTemplate_ItalicText_PreservesFormatting()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        RunProperties italicFormatting = DocumentBuilder.CreateFormatting(italic: true);
        builder.AddParagraph("Note: {{NoteText}}", italicFormatting);

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["NoteText"] = "This is an important note"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        RunProperties? runProps = verifier.GetRunProperties(0, 0);
        DocumentVerifier.VerifyFormatting(runProps, expectedItalic: true);
    }

    [Fact]
    public void ProcessTemplate_ColoredText_PreservesColor()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        RunProperties colorFormatting = DocumentBuilder.CreateFormatting(color: "FF0000"); // Red
        builder.AddParagraph("Alert: {{AlertMessage}}", colorFormatting);

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["AlertMessage"] = "Critical system warning"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Contains("Critical system warning", text);

        RunProperties? runProps = verifier.GetRunProperties(0, 0);
        DocumentVerifier.VerifyFormatting(runProps, expectedColor: "FF0000");
    }

    [Fact]
    public void ProcessTemplate_FontFamilyAndSize_PreservesFont()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        RunProperties fontFormatting = DocumentBuilder.CreateFormatting(
            fontFamily: "Courier New",
            fontSize: "28"); // Font size in half-points (28 = 14pt)
        builder.AddParagraph("Code: {{CodeSnippet}}", fontFormatting);

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["CodeSnippet"] = "var x = 42;"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Contains("var x = 42;", text);

        RunProperties? runProps = verifier.GetRunProperties(0, 0);
        DocumentVerifier.VerifyFormatting(runProps,
            expectedFontFamily: "Courier New",
            expectedFontSize: "28");
    }

    [Fact]
    public void ProcessTemplate_MultipleFormattingAttributes_PreservesAll()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        RunProperties complexFormatting = DocumentBuilder.CreateFormatting(
            bold: true,
            italic: true,
            color: "0000FF", // Blue
            fontFamily: "Arial",
            fontSize: "24"); // 12pt
        builder.AddParagraph("Title: {{Title}}", complexFormatting);

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Title"] = "Software Documentation"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Contains("Software Documentation", text);

        RunProperties? runProps = verifier.GetRunProperties(0, 0);
        DocumentVerifier.VerifyFormatting(runProps,
            expectedBold: true,
            expectedItalic: true,
            expectedColor: "0000FF",
            expectedFontFamily: "Arial",
            expectedFontSize: "24");
    }

    [Fact]
    public void ProcessTemplate_FormattingInLoop_PreservesForEachIteration()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Items:");
        builder.AddParagraph("{{#foreach Items}}");

        RunProperties boldFormatting = DocumentBuilder.CreateFormatting(bold: true);
        builder.AddParagraph("- {{.}}", boldFormatting);

        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "Item One", "Item Two", "Item Three" }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        List<string> paragraphs = verifier.GetAllParagraphTexts();

        Assert.Equal(4, paragraphs.Count); // "Items:" + 3 items
        Assert.Equal("- Item One", paragraphs[1]);
        Assert.Equal("- Item Two", paragraphs[2]);
        Assert.Equal("- Item Three", paragraphs[3]);

        // Verify all loop items preserved bold formatting
        RunProperties? props1 = verifier.GetRunProperties(1, 0);
        DocumentVerifier.VerifyFormatting(props1, expectedBold: true);

        RunProperties? props2 = verifier.GetRunProperties(2, 0);
        DocumentVerifier.VerifyFormatting(props2, expectedBold: true);

        RunProperties? props3 = verifier.GetRunProperties(3, 0);
        DocumentVerifier.VerifyFormatting(props3, expectedBold: true);
    }

    [Fact]
    public void ProcessTemplate_MixedFormatting_UsesFirstRunFormatting()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();

        RunProperties boldFormatting = DocumentBuilder.CreateFormatting(bold: true);
        RunProperties italicFormatting = DocumentBuilder.CreateFormatting(italic: true);

        // Paragraph with mixed formatting: bold placeholder, then italic text
        builder.AddParagraphWithRuns(
            ("Hello {{Name}}", boldFormatting),
            (" and welcome!", italicFormatting)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "John Doe"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Contains("John Doe", text);

        // The replacement should inherit bold formatting from the first run
        RunProperties? runProps = verifier.GetRunProperties(0, 0);
        DocumentVerifier.VerifyFormatting(runProps, expectedBold: true);
    }

    [Fact]
    public void ProcessTemplate_NoFormatting_WorksWithoutRunProperties()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Plain text: {{Value}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Value"] = "No formatting"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Equal("Plain text: No formatting", text);

        // No special formatting should be present
        RunProperties? runProps = verifier.GetRunProperties(0, 0);
        // RunProperties may be null or empty - both are acceptable for plain text
    }

    [Fact]
    public void ProcessTemplate_DifferentFormattingPerParagraph_PreservesEach()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();

        RunProperties boldFormatting = DocumentBuilder.CreateFormatting(bold: true);
        RunProperties italicFormatting = DocumentBuilder.CreateFormatting(italic: true);
        RunProperties colorFormatting = DocumentBuilder.CreateFormatting(color: "FF0000");

        builder.AddParagraph("Bold: {{Value1}}", boldFormatting);
        builder.AddParagraph("Italic: {{Value2}}", italicFormatting);
        builder.AddParagraph("Red: {{Value3}}", colorFormatting);

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Value1"] = "Bold Text",
            ["Value2"] = "Italic Text",
            ["Value3"] = "Red Text"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);

        // Verify paragraph 1 is bold
        RunProperties? props1 = verifier.GetRunProperties(0, 0);
        DocumentVerifier.VerifyFormatting(props1, expectedBold: true);

        // Verify paragraph 2 is italic
        RunProperties? props2 = verifier.GetRunProperties(1, 0);
        DocumentVerifier.VerifyFormatting(props2, expectedItalic: true);

        // Verify paragraph 3 is red
        RunProperties? props3 = verifier.GetRunProperties(2, 0);
        DocumentVerifier.VerifyFormatting(props3, expectedColor: "FF0000");
    }

    [Fact]
    public void ProcessTemplate_HighlightedText_PreservesHighlight()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        RunProperties yellowHighlight = DocumentBuilder.CreateFormatting(highlight: HighlightColorValues.Yellow);
        builder.AddParagraph("C{{Confidentiality}}", yellowHighlight);

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Confidentiality"] = "1"
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
        Assert.Equal("C1", text);

        RunProperties? runProps = verifier.GetRunProperties(0, 0);
        DocumentVerifier.VerifyFormatting(runProps, expectedHighlight: HighlightColorValues.Yellow);
    }

    [Fact]
    public void ProcessTemplate_ShadedText_PreservesShadingAndColor()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        RunProperties blackShading = DocumentBuilder.CreateFormatting(color: "FFFFFF", shadingFill: "000000");
        builder.AddParagraph("I{{Integrity}}", blackShading);

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Integrity"] = "4"
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
        Assert.Equal("I4", text);

        RunProperties? runProps = verifier.GetRunProperties(0, 0);
        DocumentVerifier.VerifyFormatting(runProps, expectedColor: "FFFFFF", expectedShadingFill: "000000");
    }

    [Fact]
    public void ProcessTemplate_CyanHighlight_PreservesHighlight()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        RunProperties cyanHighlight = DocumentBuilder.CreateFormatting(highlight: HighlightColorValues.Cyan);
        builder.AddParagraph("A{{Availability}}", cyanHighlight);

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Availability"] = "0"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Equal("A0", text);

        RunProperties? runProps = verifier.GetRunProperties(0, 0);
        DocumentVerifier.VerifyFormatting(runProps, expectedHighlight: HighlightColorValues.Cyan);
    }

    [Fact]
    public void ProcessTemplate_CIABlocksWithTabs_PreservesEachBlockStyle()
    {
        // Arrange - Create template with styled blocks separated by tabs:
        // C{{Confidentiality}} (yellow) TAB I{{Integrity}} (black bg, white text) TAB A{{Availability}} (cyan)
        DocumentBuilder builder = new DocumentBuilder();

        RunProperties yellowHighlight = DocumentBuilder.CreateFormatting(highlight: HighlightColorValues.Yellow);
        RunProperties blackShading = DocumentBuilder.CreateFormatting(color: "FFFFFF", shadingFill: "000000");
        RunProperties cyanHighlight = DocumentBuilder.CreateFormatting(highlight: HighlightColorValues.Cyan);

        builder.AddParagraphWithRuns(
            ("C{{Confidentiality}}", yellowHighlight),
            ("\t", null),
            ("I{{Integrity}}", blackShading),
            ("\t", null),
            ("A{{Availability}}", cyanHighlight)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Confidentiality"] = "1",
            ["Integrity"] = "4",
            ["Availability"] = "0"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Equal("C1\tI4\tA0", text);

        // Verify each block preserved its style
        List<Run> runs = verifier.GetRuns(0);
        Assert.True(runs.Count >= 3, $"Expected at least 3 runs, got {runs.Count}");

        // Run 0: C1 with yellow highlight
        DocumentVerifier.VerifyFormatting(runs[0].RunProperties, expectedHighlight: HighlightColorValues.Yellow);

        // Run 2: I4 with black shading and white text
        DocumentVerifier.VerifyFormatting(runs[2].RunProperties, expectedColor: "FFFFFF", expectedShadingFill: "000000");

        // Run 4: A0 with cyan highlight
        DocumentVerifier.VerifyFormatting(runs[4].RunProperties, expectedHighlight: HighlightColorValues.Cyan);
    }

    #region Per-Run Replacement Edge Cases

    /// <summary>
    /// Tests that a placeholder at the exact start of a run is handled correctly.
    /// Edge case: placeholder starts at index 0 within its run.
    /// </summary>
    [Fact]
    public void ProcessTemplate_PlaceholderAtRunStart_PreservesFormatting()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        RunProperties redFormatting = DocumentBuilder.CreateFormatting(color: "FF0000");
        builder.AddParagraph("{{Value}} is the answer", redFormatting);

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Value"] = "42"
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
        Assert.Equal("42 is the answer", text);

        RunProperties? runProps = verifier.GetRunProperties(0, 0);
        DocumentVerifier.VerifyFormatting(runProps, expectedColor: "FF0000");
    }

    /// <summary>
    /// Tests that a placeholder at the exact end of a run is handled correctly.
    /// Edge case: placeholder ends at the last character of its run.
    /// </summary>
    [Fact]
    public void ProcessTemplate_PlaceholderAtRunEnd_PreservesFormatting()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        RunProperties blueFormatting = DocumentBuilder.CreateFormatting(color: "0000FF");
        builder.AddParagraph("The answer is {{Value}}", blueFormatting);

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Value"] = "42"
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
        Assert.Equal("The answer is 42", text);

        RunProperties? runProps = verifier.GetRunProperties(0, 0);
        DocumentVerifier.VerifyFormatting(runProps, expectedColor: "0000FF");
    }

    /// <summary>
    /// Tests that a placeholder spanning the entire run content is handled correctly.
    /// Edge case: the run contains ONLY the placeholder text.
    /// </summary>
    [Fact]
    public void ProcessTemplate_PlaceholderIsEntireRun_PreservesFormatting()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        RunProperties greenHighlight = DocumentBuilder.CreateFormatting(highlight: HighlightColorValues.Green);
        builder.AddParagraph("{{Value}}", greenHighlight);

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Value"] = "Complete replacement"
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
        Assert.Equal("Complete replacement", text);

        RunProperties? runProps = verifier.GetRunProperties(0, 0);
        DocumentVerifier.VerifyFormatting(runProps, expectedHighlight: HighlightColorValues.Green);
    }

    /// <summary>
    /// Tests that a placeholder spanning multiple runs uses merge behavior (first run formatting).
    /// Edge case: placeholder split across runs should fall back to multi-run processing.
    /// </summary>
    [Fact]
    public void ProcessTemplate_PlaceholderSpansMultipleRuns_UsesMergeFormatting()
    {
        // Arrange - Create a paragraph where the placeholder is split across runs
        // This simulates Word's behavior of splitting text into multiple runs
        DocumentBuilder builder = new DocumentBuilder();
        RunProperties boldFormatting = DocumentBuilder.CreateFormatting(bold: true);
        RunProperties italicFormatting = DocumentBuilder.CreateFormatting(italic: true);

        // Split "{{Name}}" into "{{Na" and "me}}"
        builder.AddParagraphWithRuns(
            ("Hello {{Na", boldFormatting),
            ("me}}!", italicFormatting)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "World"
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
        Assert.Equal("Hello World!", text);

        // When spanning multiple runs, formatting comes from first run (bold)
        RunProperties? runProps = verifier.GetRunProperties(0, 0);
        DocumentVerifier.VerifyFormatting(runProps, expectedBold: true);
    }

    /// <summary>
    /// Tests multiple placeholders in the same run, each preserving the run's formatting.
    /// Edge case: ensures per-run replacement works with multiple placeholders in sequence.
    /// </summary>
    [Fact]
    public void ProcessTemplate_MultiplePlaceholdersInSameRun_PreservesFormatting()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        RunProperties magentaHighlight = DocumentBuilder.CreateFormatting(highlight: HighlightColorValues.Magenta);
        builder.AddParagraph("{{First}} and {{Second}}", magentaHighlight);

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["First"] = "Alpha",
            ["Second"] = "Beta"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Equal("Alpha and Beta", text);

        // All content should preserve magenta highlight
        RunProperties? runProps = verifier.GetRunProperties(0, 0);
        DocumentVerifier.VerifyFormatting(runProps, expectedHighlight: HighlightColorValues.Magenta);
    }

    /// <summary>
    /// Tests that empty replacement value preserves formatting.
    /// Edge case: replacement with empty string should still maintain run formatting.
    /// </summary>
    [Fact]
    public void ProcessTemplate_EmptyReplacementValue_PreservesFormatting()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        RunProperties yellowHighlight = DocumentBuilder.CreateFormatting(highlight: HighlightColorValues.Yellow);
        builder.AddParagraph("Before{{Optional}}After", yellowHighlight);

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Optional"] = ""
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
        Assert.Equal("BeforeAfter", text);

        RunProperties? runProps = verifier.GetRunProperties(0, 0);
        DocumentVerifier.VerifyFormatting(runProps, expectedHighlight: HighlightColorValues.Yellow);
    }

    #endregion

    #region Multi-placeholder shading preservation

    [Fact]
    public void ProcessTemplate_MultiplePlaceholders_DifferentShadings_PreservesEachShading()
    {
        // Arrange - Create a paragraph with multiple placeholders, each with different shading
        // This simulates real-world templates like: C{{Confidentiality}}I{{Integrity}}A{{Availability}}
        // where each segment has different background colors
        DocumentBuilder builder = new DocumentBuilder();

        // Orange shading for "C" prefix and confidentiality placeholder
        RunProperties orangeShading = DocumentBuilder.CreateFormatting(color: "FFFFFF", shadingFill: "BE8E18");
        // Dark shading for "I" prefix and integrity placeholder
        RunProperties darkShading = DocumentBuilder.CreateFormatting(color: "FFFFFF", shadingFill: "232323");
        // Blue shading for "A" prefix and availability placeholder
        RunProperties blueShading = DocumentBuilder.CreateFormatting(color: "FFFFFF", shadingFill: "175788");

        builder.AddParagraphWithRuns(
            ("C{{Confidentiality}}", orangeShading),
            ("I{{Integrity}}", darkShading),
            ("A{{Availability}}", blueShading)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Confidentiality"] = "3",
            ["Integrity"] = "1",
            ["Availability"] = "2"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);

        // Verify text is correct
        string text = verifier.GetParagraphText(0);
        Assert.Equal("C3I1A2", text);

        // Verify each segment has its correct shading preserved
        // Run 0: "C3" with orange shading
        RunProperties? run0Props = verifier.GetRunProperties(0, 0);
        Assert.NotNull(run0Props);
        DocumentVerifier.VerifyFormatting(run0Props, expectedColor: "FFFFFF", expectedShadingFill: "BE8E18");

        // Run 1: "I1" with dark shading
        RunProperties? run1Props = verifier.GetRunProperties(0, 1);
        Assert.NotNull(run1Props);
        DocumentVerifier.VerifyFormatting(run1Props, expectedColor: "FFFFFF", expectedShadingFill: "232323");

        // Run 2: "A2" with blue shading
        RunProperties? run2Props = verifier.GetRunProperties(0, 2);
        Assert.NotNull(run2Props);
        DocumentVerifier.VerifyFormatting(run2Props, expectedColor: "FFFFFF", expectedShadingFill: "175788");
    }

    [Fact]
    public void ProcessTemplate_MultiplePlaceholders_SplitAcrossRuns_PreservesEachShading()
    {
        // Arrange - More realistic case: placeholder split across multiple runs (like Word often does)
        // "C" | "{{" | "Confidentiality" | "}}" all with same shading
        DocumentBuilder builder = new DocumentBuilder();

        RunProperties orangeShading = DocumentBuilder.CreateFormatting(color: "FFFFFF", shadingFill: "BE8E18");
        RunProperties darkShading = DocumentBuilder.CreateFormatting(color: "FFFFFF", shadingFill: "232323");

        builder.AddParagraphWithRuns(
            ("C", orangeShading),
            ("{{", orangeShading),
            ("Confidentiality", orangeShading),
            ("}}", orangeShading),
            ("I", darkShading),
            ("{{", darkShading),
            ("Integrity", darkShading),
            ("}}", darkShading)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Confidentiality"] = "3",
            ["Integrity"] = "1"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);

        // Verify text is correct
        string text = verifier.GetParagraphText(0);
        Assert.Equal("C3I1", text);

        // Get all runs - should have "C3" with orange and "I1" with dark
        var runs = verifier.GetAllRunsInParagraph(0);
        Assert.True(runs.Count >= 2, $"Expected at least 2 runs but got {runs.Count}");

        // Find the run containing "C" or "3" - should have orange shading
        bool foundOrangeShadedRun = runs.Any(r =>
        {
            string runText = r.InnerText;
            var shading = r.RunProperties?.Shading;
            return (runText.Contains("C") || runText.Contains("3")) &&
                   shading?.Fill?.Value == "BE8E18";
        });
        Assert.True(foundOrangeShadedRun, "Expected orange-shaded run containing C or 3");

        // Find the run containing "I" or "1" - should have dark shading
        bool foundDarkShadedRun = runs.Any(r =>
        {
            string runText = r.InnerText;
            var shading = r.RunProperties?.Shading;
            return (runText.Contains("I") || runText.Contains("1")) &&
                   shading?.Fill?.Value == "232323";
        });
        Assert.True(foundDarkShadedRun, "Expected dark-shaded run containing I or 1");
    }

    #endregion
}
