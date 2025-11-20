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
/// Integration tests for boolean expression support in templates.
/// </summary>
public class ExpressionIntegrationTests
{
    [Fact]
    public void ProcessTemplate_WithSimpleAndExpression_EvaluatesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Result: {{(var1 and var2)}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object>
        {
            ["var1"] = true,
            ["var2"] = true
        };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Result: True", result);
    }

    [Fact]
    public void ProcessTemplate_WithSimpleOrExpression_EvaluatesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Result: {{(var1 or var2)}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object>
        {
            ["var1"] = false,
            ["var2"] = true
        };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Result: True", result);
    }

    [Fact]
    public void ProcessTemplate_WithNotExpression_EvaluatesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Result: {{(not IsActive)}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object> { ["IsActive"] = false };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Result: True", result);
    }

    [Fact]
    public void ProcessTemplate_WithComparisonGreaterThan_EvaluatesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Result: {{(Count > 5)}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object> { ["Count"] = 10 };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Result: True", result);
    }

    [Fact]
    public void ProcessTemplate_WithComparisonEquals_EvaluatesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Result: {{(Status == \"active\")}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object> { ["Status"] = "active" };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Result: True", result);
    }

    [Fact]
    public void ProcessTemplate_WithNestedExpression_EvaluatesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Result: {{((var1 or var2) and var3)}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object>
        {
            ["var1"] = true,
            ["var2"] = false,
            ["var3"] = true
        };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Result: True", result);
    }

    [Fact]
    public void ProcessTemplate_WithExpressionAndCheckboxFormat_CombinesFeatures()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Active: {{(var1 and var2):checkbox}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object>
        {
            ["var1"] = true,
            ["var2"] = true
        };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Active: ☑", result);
    }

    [Fact]
    public void ProcessTemplate_WithExpressionAndYesNoFormat_CombinesFeatures()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Enabled: {{(Count > 0):yesno}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object> { ["Count"] = 5 };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Enabled: Yes", result);
    }

    [Fact]
    public void ProcessTemplate_WithExpressionAndCheckmarkFormat_CombinesFeatures()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Valid: {{(not IsInvalid):checkmark}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object> { ["IsInvalid"] = false };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Valid: ✓", result);
    }

    [Fact]
    public void ProcessTemplate_WithMultipleExpressions_EvaluatesAll()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("And: {{(var1 and var2):checkbox}}, Or: {{(var3 or var4):yesno}}, Not: {{(not var5):checkmark}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object>
        {
            ["var1"] = true,
            ["var2"] = true,
            ["var3"] = false,
            ["var4"] = true,
            ["var5"] = false
        };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("And: ☑", result);
        Assert.Contains("Or: Yes", result);
        Assert.Contains("Not: ✓", result);
    }

    [Fact]
    public void ProcessTemplate_WithExpressionInLoop_EvaluatesForEachItem()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("- {{Name}}: {{(Count > 5):yesno}}");
        builder.AddParagraph("{{/foreach}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object>
        {
            ["Items"] = new[]
            {
                new { Name = "Item 1", Count = 10 },
                new { Name = "Item 2", Count = 3 },
                new { Name = "Item 3", Count = 7 }
            }
        };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Item 1: Yes", result);
        Assert.Contains("Item 2: No", result);
        Assert.Contains("Item 3: Yes", result);
    }

    [Fact]
    public void ProcessTemplate_WithExpressionInConditional_Works()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if ShowDetails}}");
        builder.AddParagraph("Active: {{(IsActive and IsEnabled):checkbox}}");
        builder.AddParagraph("{{/if}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object>
        {
            ["ShowDetails"] = true,
            ["IsActive"] = true,
            ["IsEnabled"] = true
        };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Active: ☑", result);
    }

    [Fact]
    public void ProcessTemplate_WithComplexExpression_EvaluatesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Qualified: {{((Age >= 18) and (HasLicense or HasPermit)):yesno}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object>
        {
            ["Age"] = 20,
            ["HasLicense"] = false,
            ["HasPermit"] = true
        };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Qualified: Yes", result);
    }

    [Theory]
    [InlineData(true, true, "☑")]
    [InlineData(true, false, "☐")]
    [InlineData(false, true, "☐")]
    [InlineData(false, false, "☐")]
    public void ProcessTemplate_WithAndExpressionAndFormat_EvaluatesCorrectly(
        bool var1, bool var2, string expectedSymbol)
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{(var1 and var2):checkbox}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object> { ["var1"] = var1, ["var2"] = var2 };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains(expectedSymbol, result);
    }

    [Theory]
    [InlineData(10, 5, "Yes")]
    [InlineData(5, 5, "No")]
    [InlineData(3, 5, "No")]
    public void ProcessTemplate_WithComparisonExpressionAndFormat_EvaluatesCorrectly(
        int count, int threshold, string expectedText)
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph($"{{{{(Count > {threshold}):yesno}}}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object> { ["Count"] = count };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains(expectedText, result);
    }

    [Fact]
    public void ProcessTemplate_WithExpressionUsingNestedProperty_Works()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Active: {{(User.IsActive):checkbox}}");
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
    public void ProcessTemplate_WithMixedExpressionsAndVariables_ReplacesAll()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Name: {{Name}}, Active: {{(IsActive and IsVerified):checkbox}}, Count: {{Count}}");
        using MemoryStream templateStream = builder.ToStream();

        var data = new Dictionary<string, object>
        {
            ["Name"] = "Test User",
            ["IsActive"] = true,
            ["IsVerified"] = true,
            ["Count"] = 42
        };
        var processor = CreateInvariantProcessor();

        // Act
        string result = ProcessTemplate(templateStream, data, processor);

        // Assert
        Assert.Contains("Name: Test User", result);
        Assert.Contains("Active: ☑", result);
        Assert.Contains("Count: 42", result);
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
