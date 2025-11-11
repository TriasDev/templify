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
            ["Title"] = "ViasPro Documentation"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Contains("ViasPro Documentation", text);

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
}
