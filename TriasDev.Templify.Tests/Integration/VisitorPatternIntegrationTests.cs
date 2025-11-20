// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Integration tests that verify the visitor pattern implementation.
/// These tests were originally used to compare legacy and visitor paths during Phase 2.
/// Now they serve as regression tests for the visitor pattern implementation.
/// </summary>
/// <remarks>
/// Phase 2 Week 3 Days 8-9: Legacy path removed, tests updated to verify visitor pattern only.
/// All tests now use the unified visitor-based processing.
/// </remarks>
public sealed class VisitorPatternIntegrationTests
{
    [Fact]
    public void ProcessTemplate_SimpleReplacements_ProducesCorrectResult()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{Name}}!");
        builder.AddParagraph("Age: {{Age}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "World",
            ["Age"] = 42
        };

        using MemoryStream templateStream = builder.ToStream();

        // Act
        string result = ProcessTemplate(templateStream, data);

        // Assert
        Assert.Contains("Hello World!", result);
        Assert.Contains("Age: 42", result);
    }

    [Fact]
    public void ProcessTemplate_SimpleConditional_ProducesCorrectResult()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if IsActive}}");
        builder.AddParagraph("Status: Active");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("Status: Inactive");
        builder.AddParagraph("{{/if}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["IsActive"] = true
        };

        using MemoryStream templateStream = builder.ToStream();

        // Act
        string result = ProcessTemplate(templateStream, data);

        // Assert
        Assert.Contains("Status: Active", result);
        Assert.DoesNotContain("Status: Inactive", result);
    }

    [Fact]
    public void ProcessTemplate_SimpleLoop_ProducesCorrectResult()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("Item: {{.}}");
        builder.AddParagraph("{{/foreach}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "A", "B", "C" }
        };

        using MemoryStream templateStream = builder.ToStream();

        // Act
        string result = ProcessTemplate(templateStream, data);

        // Assert
        Assert.Contains("Item: A", result);
        Assert.Contains("Item: B", result);
        Assert.Contains("Item: C", result);
    }

    [Fact]
    public void ProcessTemplate_ConditionalInsideLoop_ProducesCorrectResult()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("{{#if @first}}");
        builder.AddParagraph("First: {{.}}");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("Other: {{.}}");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/foreach}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "Alpha", "Beta", "Gamma" }
        };

        using MemoryStream templateStream = builder.ToStream();

        // Act
        string result = ProcessTemplate(templateStream, data);

        // Assert
        Assert.Contains("First: Alpha", result);
        Assert.Contains("Other: Beta", result);
        Assert.Contains("Other: Gamma", result);
    }

    [Fact]
    public void ProcessTemplate_NestedConditionals_ProducesCorrectResult()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Outer}}");
        builder.AddParagraph("Outer is true");
        builder.AddParagraph("{{#if Inner}}");
        builder.AddParagraph("Inner is also true");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/if}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Outer"] = true,
            ["Inner"] = true
        };

        using MemoryStream templateStream = builder.ToStream();

        // Act
        string result = ProcessTemplate(templateStream, data);

        // Assert
        Assert.Contains("Outer is true", result);
        Assert.Contains("Inner is also true", result);
    }

    [Fact]
    public void ProcessTemplate_LoopMetadata_ProducesCorrectResult()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Numbers}}");
        builder.AddParagraph("Index: {{@index}}, Value: {{.}}");
        builder.AddParagraph("{{/foreach}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Numbers"] = new List<int> { 10, 20, 30 }
        };

        using MemoryStream templateStream = builder.ToStream();

        // Act
        string result = ProcessTemplate(templateStream, data);

        // Assert
        Assert.Contains("Index: 0, Value: 10", result);
        Assert.Contains("Index: 1, Value: 20", result);
        Assert.Contains("Index: 2, Value: 30", result);
    }

    [Fact]
    public void ProcessTemplate_EmptyLoop_ProducesCorrectResult()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Before");
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("Item: {{.}}");
        builder.AddParagraph("{{/foreach}}");
        builder.AddParagraph("After");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string>()
        };

        using MemoryStream templateStream = builder.ToStream();

        // Act
        string result = ProcessTemplate(templateStream, data);

        // Assert
        Assert.Contains("Before", result);
        Assert.Contains("After", result);
        Assert.DoesNotContain("Item:", result);
    }

    [Fact]
    public void ProcessTemplate_MissingVariable_ProducesCorrectResult()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{MissingVar}}!");

        Dictionary<string, object> data = new Dictionary<string, object>();

        using MemoryStream templateStream = builder.ToStream();

        // Act - Using LeaveUnchanged behavior (default)
        string result = ProcessTemplate(templateStream, data);

        // Assert - Should leave placeholder unchanged
        Assert.Contains("{{MissingVar}}", result);
    }

    [Fact]
    public void ProcessTemplate_ComplexDocument_ProducesCorrectResult()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Report for {{CompanyName}}");
        builder.AddParagraph("{{#if HasData}}");
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("{{#if @first}}");
        builder.AddParagraph("First Item: {{Name}}");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("Item: {{Name}}");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/foreach}}");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("No data available");
        builder.AddParagraph("{{/if}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["CompanyName"] = "Acme Corp",
            ["HasData"] = true,
            ["Items"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { ["Name"] = "Product A" },
                new Dictionary<string, object> { ["Name"] = "Product B" }
            }
        };

        using MemoryStream templateStream = builder.ToStream();

        // Act
        string result = ProcessTemplate(templateStream, data);

        // Assert
        Assert.Contains("Report for Acme Corp", result);
        Assert.Contains("First Item: Product A", result);
        Assert.Contains("Item: Product B", result);
    }

    /// <summary>
    /// Helper method to process a template and extract the resulting text.
    /// </summary>
    private static string ProcessTemplate(MemoryStream templateStream, Dictionary<string, object> data)
    {
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();

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
}
