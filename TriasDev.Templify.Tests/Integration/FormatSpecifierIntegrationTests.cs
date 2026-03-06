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

    #region Number Format Integration Tests

    [Fact]
    public void ProcessTemplate_WithCurrencyFormat_ReplacesWithCurrencyString()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Total: {{Amount:currency}}");
        using MemoryStream templateStream = builder.ToStream();

        var options = new PlaceholderReplacementOptions { Culture = new CultureInfo("en-US") };
        var processor = new DocumentTemplateProcessor(options);
        var data = new Dictionary<string, object> { ["Amount"] = 1234.56m };

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Total: $1,234.56", result);
    }

    [Fact]
    public void ProcessTemplate_WithNumberFormatN2_ReplacesWithFormattedNumber()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Value: {{Value:number:N2}}");
        using MemoryStream templateStream = builder.ToStream();

        var options = new PlaceholderReplacementOptions { Culture = new CultureInfo("en-US") };
        var processor = new DocumentTemplateProcessor(options);
        var data = new Dictionary<string, object> { ["Value"] = 1234.5678m };

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Value: 1,234.57", result);
    }

    [Fact]
    public void ProcessTemplate_WithCurrencyFormatAndGermanCulture_ReplacesWithEuroFormatting()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Betrag: {{Amount:currency}}");
        using MemoryStream templateStream = builder.ToStream();

        var germanCulture = new CultureInfo("de-DE");
        var options = new PlaceholderReplacementOptions { Culture = germanCulture };
        var processor = new DocumentTemplateProcessor(options);
        var data = new Dictionary<string, object> { ["Amount"] = 1234.56m };

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("1.234,56", result);
    }

    [Fact]
    public void ProcessTemplate_WithNumberFormatInLoop_ReplacesAllItems()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("- {{Name}}: {{Price:currency}}");
        builder.AddParagraph("{{/foreach}}");
        using MemoryStream templateStream = builder.ToStream();

        var options = new PlaceholderReplacementOptions { Culture = new CultureInfo("en-US") };
        var processor = new DocumentTemplateProcessor(options);
        var data = new Dictionary<string, object>
        {
            ["Items"] = new[]
            {
                new { Name = "Widget", Price = 9.99m },
                new { Name = "Gadget", Price = 19.99m }
            }
        };

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Widget: $9.99", result);
        Assert.Contains("Gadget: $19.99", result);
    }

    [Fact]
    public void ProcessTemplate_WithMixedBooleanAndNumberFormats_ReplacesAll()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Active: {{IsActive:checkbox}}, Total: {{Amount:currency}}");
        using MemoryStream templateStream = builder.ToStream();

        var options = new PlaceholderReplacementOptions
        {
            Culture = new CultureInfo("en-US"),
            BooleanFormatterRegistry = new BooleanFormatterRegistry(CultureInfo.InvariantCulture)
        };
        var processor = new DocumentTemplateProcessor(options);
        var data = new Dictionary<string, object>
        {
            ["IsActive"] = true,
            ["Amount"] = 42.50m
        };

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Active: ☑", result);
        Assert.Contains("Total: $42.50", result);
    }

    [Fact]
    public void ProcessTemplate_WithNestedPropertyAndCurrencyFormat_ReplacesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Order total: {{Order.Total:currency}}");
        using MemoryStream templateStream = builder.ToStream();

        var options = new PlaceholderReplacementOptions { Culture = new CultureInfo("en-US") };
        var processor = new DocumentTemplateProcessor(options);
        var data = new Dictionary<string, object>
        {
            ["Order"] = new { Total = 99.95m }
        };

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Order total: $99.95", result);
    }

    #endregion

    #region String Format Integration Tests

    [Fact]
    public void ProcessTemplate_WithUppercaseFormat_ReplacesWithUppercase()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Name: {{Name:uppercase}}");
        using MemoryStream templateStream = builder.ToStream();

        var processor = CreateInvariantProcessor();
        var data = new Dictionary<string, object> { ["Name"] = "alice johnson" };

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Name: ALICE JOHNSON", result);
    }

    [Fact]
    public void ProcessTemplate_WithLowercaseFormat_ReplacesWithLowercase()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Code: {{Code:lowercase}}");
        using MemoryStream templateStream = builder.ToStream();

        var processor = CreateInvariantProcessor();
        var data = new Dictionary<string, object> { ["Code"] = "ABC-123-XYZ" };

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Code: abc-123-xyz", result);
    }

    [Fact]
    public void ProcessTemplate_WithStringFormatInLoop_ReplacesAllItems()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("- {{Name:uppercase}}");
        builder.AddParagraph("{{/foreach}}");
        using MemoryStream templateStream = builder.ToStream();

        var processor = CreateInvariantProcessor();
        var data = new Dictionary<string, object>
        {
            ["Items"] = new[]
            {
                new { Name = "widget" },
                new { Name = "gadget" }
            }
        };

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("WIDGET", result);
        Assert.Contains("GADGET", result);
    }

    #endregion

    #region Date Format Integration Tests

    [Fact]
    public void ProcessTemplate_WithDateFormat_ReplacesWithFormattedDate()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Date: {{OrderDate:date:yyyy-MM-dd}}");
        using MemoryStream templateStream = builder.ToStream();

        var options = new PlaceholderReplacementOptions { Culture = new CultureInfo("en-US") };
        var processor = new DocumentTemplateProcessor(options);
        var data = new Dictionary<string, object> { ["OrderDate"] = new DateTime(2024, 1, 15) };

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Date: 2024-01-15", result);
    }

    [Fact]
    public void ProcessTemplate_WithLongDateFormat_ReplacesWithFormattedDate()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Date: {{OrderDate:date:MMMM d, yyyy}}");
        using MemoryStream templateStream = builder.ToStream();

        var options = new PlaceholderReplacementOptions { Culture = new CultureInfo("en-US") };
        var processor = new DocumentTemplateProcessor(options);
        var data = new Dictionary<string, object> { ["OrderDate"] = new DateTime(2024, 1, 15) };

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Date: January 15, 2024", result);
    }

    [Fact]
    public void ProcessTemplate_WithDateFormatAndGermanCulture_ReplacesWithLocalizedDate()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Datum: {{OrderDate:date:dd. MMMM yyyy}}");
        using MemoryStream templateStream = builder.ToStream();

        var germanCulture = new CultureInfo("de-DE");
        var options = new PlaceholderReplacementOptions { Culture = germanCulture };
        var processor = new DocumentTemplateProcessor(options);
        var data = new Dictionary<string, object> { ["OrderDate"] = new DateTime(2024, 1, 15) };

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Datum: 15. Januar 2024", result);
    }

    [Fact]
    public void ProcessTemplate_WithStringDateAndDateFormat_ParsesAndFormats()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Date: {{OrderDate:date:dd.MM.yyyy}}");
        using MemoryStream templateStream = builder.ToStream();

        var processor = CreateInvariantProcessor();
        var data = new Dictionary<string, object> { ["OrderDate"] = "01/15/2024" };

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Date: 15.01.2024", result);
    }

    [Fact]
    public void ProcessTemplate_WithAllFormatTypes_ReplacesAll()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Name: {{Name:uppercase}}, Active: {{IsActive:checkbox}}, Total: {{Amount:currency}}, Date: {{OrderDate:date:yyyy-MM-dd}}");
        using MemoryStream templateStream = builder.ToStream();

        var options = new PlaceholderReplacementOptions
        {
            Culture = new CultureInfo("en-US"),
            BooleanFormatterRegistry = new BooleanFormatterRegistry(CultureInfo.InvariantCulture)
        };
        var processor = new DocumentTemplateProcessor(options);
        var data = new Dictionary<string, object>
        {
            ["Name"] = "alice",
            ["IsActive"] = true,
            ["Amount"] = 42.50m,
            ["OrderDate"] = new DateTime(2024, 1, 15)
        };

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Name: ALICE", result);
        Assert.Contains("Active: ☑", result);
        Assert.Contains("Total: $42.50", result);
        Assert.Contains("Date: 2024-01-15", result);
    }

    #endregion

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
