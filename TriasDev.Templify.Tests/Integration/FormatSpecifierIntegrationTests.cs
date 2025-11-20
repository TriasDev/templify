// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Formatting;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Integration tests for format specifier feature using real Word documents.
/// </summary>
public class FormatSpecifierIntegrationTests
{
    [Fact]
    public void ProcessTemplate_WithCheckboxFormat_ReplacesWithCheckboxSymbol()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Status: {{IsActive:checkbox}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object> { ["IsActive"] = true };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Status: ☑", result);
    }

    [Fact]
    public void ProcessTemplate_WithYesNoFormat_ReplacesWithYesNo()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Confirmed: {{IsConfirmed:yesno}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object> { ["IsConfirmed"] = true };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Confirmed: Yes", result);
    }

    [Fact]
    public void ProcessTemplate_WithCheckmarkFormat_ReplacesWithCheckmark()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Valid: {{IsValid:checkmark}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object> { ["IsValid"] = false };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Valid: ✗", result);
    }

    [Fact]
    public void ProcessTemplate_WithMultipleFormats_ReplacesAll()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Active: {{IsActive:checkbox}}, Enabled: {{IsEnabled:yesno}}, Valid: {{IsValid:checkmark}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object>
        {
            ["IsActive"] = true,
            ["IsEnabled"] = true,
            ["IsValid"] = false
        };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Active: ☑", result);
        Assert.Contains("Enabled: Yes", result);
        Assert.Contains("Valid: ✗", result);
    }

    [Fact]
    public void ProcessTemplate_WithTrueFalseFormat_ReplacesTrueFalse()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Result: {{Success:truefalse}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object> { ["Success"] = true };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Result: True", result);
    }

    [Fact]
    public void ProcessTemplate_WithOnOffFormat_ReplacesOnOff()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Power: {{PowerStatus:onoff}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object> { ["PowerStatus"] = false };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Power: Off", result);
    }

    [Fact]
    public void ProcessTemplate_WithEnabledFormat_ReplacesEnabledDisabled()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Feature: {{FeatureFlag:enabled}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object> { ["FeatureFlag"] = true };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Feature: Enabled", result);
    }

    [Fact]
    public void ProcessTemplate_WithActiveFormat_ReplacesActiveInactive()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("User: {{UserStatus:active}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object> { ["UserStatus"] = false };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("User: Inactive", result);
    }

    [Fact]
    public void ProcessTemplate_WithLocalizedFormat_UsesCorrectCulture()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Bestätigt: {{IsConfirmed:yesno}}");
        using MemoryStream templateStream = builder.ToStream();

        var germanCulture = new CultureInfo("de-DE");
        var options = new PlaceholderReplacementOptions
        {
            Culture = germanCulture,
            BooleanFormatterRegistry = new BooleanFormatterRegistry(germanCulture)
        };
        var processor = new DocumentTemplateProcessor(options);
        var data = new Dictionary<string, object> { ["IsConfirmed"] = true };

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Bestätigt: Ja", result);
    }

    [Fact]
    public void ProcessTemplate_WithCustomFormatter_UsesCustomFormat()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Status: {{IsLiked:thumbs}}");
        using MemoryStream templateStream = builder.ToStream();

        var registry = new BooleanFormatterRegistry();
        registry.Register("thumbs", new BooleanFormatter("👍", "👎"));
        var options = new PlaceholderReplacementOptions
        {
            BooleanFormatterRegistry = registry
        };
        var processor = new DocumentTemplateProcessor(options);
        var data = new Dictionary<string, object> { ["IsLiked"] = true };

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Status: 👍", result);
    }

    [Fact]
    public void ProcessTemplate_WithNestedPropertyAndFormat_ReplacesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Active: {{User.IsActive:checkbox}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object>
        {
            ["User"] = new { IsActive = true }
        };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Active: ☑", result);
    }

    [Fact]
    public void ProcessTemplate_WithCollectionItemAndFormat_ReplacesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("First item active: {{Items[0].IsActive:yesno}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object>
        {
            ["Items"] = new[] { new { IsActive = true }, new { IsActive = false } }
        };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("First item active: Yes", result);
    }

    [Fact]
    public void ProcessTemplate_WithLoopAndFormat_ReplacesAllItems()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("- {{Name}}: {{IsActive:checkbox}}");
        builder.AddParagraph("{{/foreach}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object>
        {
            ["Items"] = new[]
            {
                new { Name = "Item 1", IsActive = true },
                new { Name = "Item 2", IsActive = false },
                new { Name = "Item 3", IsActive = true }
            }
        };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Item 1: ☑", result);
        Assert.Contains("Item 2: ☐", result);
        Assert.Contains("Item 3: ☑", result);
    }

    [Fact]
    public void ProcessTemplate_WithConditionalAndFormat_ReplacesInBranch()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if ShowStatus}}");
        builder.AddParagraph("Status: {{IsActive:checkbox}}");
        builder.AddParagraph("{{/if}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object>
        {
            ["ShowStatus"] = true,
            ["IsActive"] = true
        };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Status: ☑", result);
    }

    [Fact]
    public void ProcessTemplate_WithFormatOnNonBoolean_IgnoresFormat()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Name: {{Name:checkbox}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object> { ["Name"] = "John Doe" };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Name: John Doe", result);
    }

    [Fact]
    public void ProcessTemplate_WithUnknownFormat_FallsBackToDefault()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Status: {{IsActive:unknownformat}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object> { ["IsActive"] = true };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Status: True", result);
    }

    [Fact]
    public void ProcessTemplate_WithMixedFormattedAndUnformatted_ReplacesAll()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Name: {{Name}}, Active: {{IsActive:checkbox}}, Count: {{Count}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object>
        {
            ["Name"] = "Test",
            ["IsActive"] = true,
            ["Count"] = 42
        };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Name: Test", result);
        Assert.Contains("Active: ☑", result);
        Assert.Contains("Count: 42", result);
    }

    [Theory]
    [InlineData("de-DE", "yesno", true, "Ja")]
    [InlineData("fr-FR", "yesno", true, "Oui")]
    [InlineData("es-ES", "yesno", true, "Sí")]
    [InlineData("it-IT", "yesno", true, "Sì")]
    [InlineData("pt-PT", "yesno", true, "Sim")]
    public void ProcessTemplate_WithVariousCultures_ReturnsLocalizedValue(
        string cultureName, string format, bool value, string expectedText)
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph($"{{{{Value:{format}}}}}");
        using MemoryStream templateStream = builder.ToStream();

        var culture = new CultureInfo(cultureName);
        var options = new PlaceholderReplacementOptions
        {
            Culture = culture,
            BooleanFormatterRegistry = new BooleanFormatterRegistry(culture)
        };
        var processor = new DocumentTemplateProcessor(options);
        var data = new Dictionary<string, object> { ["Value"] = value };

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains(expectedText, result);
    }

    [Fact]
    public void ProcessTemplate_WithCaseInsensitiveFormat_Works()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{IsActive:CHECKBOX}} {{IsEnabled:CheckBox}} {{IsValid:checkbox}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object>
        {
            ["IsActive"] = true,
            ["IsEnabled"] = true,
            ["IsValid"] = true
        };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        // All three should use the checkbox formatter (case-insensitive)
        int checkboxCount = result.Split("☑").Length - 1;
        Assert.Equal(3, checkboxCount);
    }

    #region Helper Methods

    /// <summary>
    /// Creates a DocumentTemplateProcessor with InvariantCulture for predictable test results.
    /// </summary>
    private static DocumentTemplateProcessor CreateInvariantProcessor()
    {
        var options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture,
            BooleanFormatterRegistry = new BooleanFormatterRegistry(CultureInfo.InvariantCulture)
        };
        return new DocumentTemplateProcessor(options);
    }

    /// <summary>
    /// Helper method to process a template and extract the resulting text.
    /// </summary>
    private static string ProcessTemplate(MemoryStream templateStream, Dictionary<string, object> data, DocumentTemplateProcessor processor)
    {
        templateStream.Position = 0;
        using MemoryStream outputStream = new MemoryStream();

        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        Assert.True(result.IsSuccess, $"Processing failed: {result.ErrorMessage}");

        // Read result document and extract text
        outputStream.Position = 0;
        using WordprocessingDocument document = WordprocessingDocument.Open(outputStream, false);

        if (document.MainDocumentPart?.Document?.Body == null)
        {
            return string.Empty;
        }

        // Get all paragraph text in order
        IEnumerable<Paragraph> paragraphs = document.MainDocumentPart.Document.Body.Descendants<Paragraph>();
        return string.Join("\n", paragraphs.Select(p => p.InnerText));
    }

    #endregion
}
