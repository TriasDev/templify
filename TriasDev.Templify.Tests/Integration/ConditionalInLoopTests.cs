// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Integration tests for conditionals inside loops - Phase 1, Day 5.
/// Tests verify that conditionals can access loop-scoped variables and metadata.
/// </summary>
public sealed class ConditionalInLoopTests
{
    [Fact]
    public void ProcessTemplate_ConditionalInsideLoop_EvaluatesPerIteration()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Orders}}");
        builder.AddParagraph("{{#if Amount > 1000}}");
        builder.AddParagraph("High Value");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("Standard");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Orders"] = new List<Order>
            {
                new Order { Amount = 500 },
                new Order { Amount = 1500 },
                new Order { Amount = 800 }
            }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(3, verifier.GetParagraphCount());
        Assert.Equal("Standard", verifier.GetParagraphText(0));
        Assert.Equal("High Value", verifier.GetParagraphText(1));
        Assert.Equal("Standard", verifier.GetParagraphText(2));
    }

    [Fact]
    public void ProcessTemplate_ConditionalWithFirstMetadata_Works()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("{{#if @first}}");
        builder.AddParagraph("First: {{.}}");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("Item: {{.}}");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "Apple", "Banana", "Cherry" }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(3, verifier.GetParagraphCount());
        Assert.Equal("First: Apple", verifier.GetParagraphText(0));
        Assert.Equal("Item: Banana", verifier.GetParagraphText(1));
        Assert.Equal("Item: Cherry", verifier.GetParagraphText(2));
    }

    [Fact]
    public void ProcessTemplate_ConditionalWithLastMetadata_Works()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("{{#if @last}}");
        builder.AddParagraph("Last: {{.}}");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("Item: {{.}}");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "Red", "Green", "Blue" }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(3, verifier.GetParagraphCount());
        Assert.Equal("Item: Red", verifier.GetParagraphText(0));
        Assert.Equal("Item: Green", verifier.GetParagraphText(1));
        Assert.Equal("Last: Blue", verifier.GetParagraphText(2));
    }

    [Fact]
    public void ProcessTemplate_ConditionalWithIndexMetadata_Works()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("{{#if @index = 1}}");
        builder.AddParagraph("→ {{.}}");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("{{.}}");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "First", "Second", "Third" }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(3, verifier.GetParagraphCount());
        Assert.Equal("First", verifier.GetParagraphText(0));
        Assert.Equal("→ Second", verifier.GetParagraphText(1));
        Assert.Equal("Third", verifier.GetParagraphText(2));
    }

    [Fact]
    public void ProcessTemplate_ConditionalInNestedLoop_Works()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Categories}}");
        builder.AddParagraph("{{Name}}:");
        builder.AddParagraph("{{#foreach Products}}");
        builder.AddParagraph("{{#if Price < 50}}");
        builder.AddParagraph("• {{Name}} (Budget)");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("• {{Name}}");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/foreach}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Categories"] = new List<Category>
            {
                new Category
                {
                    Name = "Electronics",
                    Products = new List<Product>
                    {
                        new Product { Name = "Mouse", Price = 25m },
                        new Product { Name = "Keyboard", Price = 75m },
                        new Product { Name = "Monitor", Price = 300m }
                    }
                },
                new Category
                {
                    Name = "Office",
                    Products = new List<Product>
                    {
                        new Product { Name = "Pen", Price = 2m },
                        new Product { Name = "Notebook", Price = 5m },
                        new Product { Name = "Desk", Price = 150m }
                    }
                }
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

        // Electronics category
        Assert.Contains("Electronics:", paragraphs);
        Assert.Contains("• Mouse (Budget)", paragraphs);
        Assert.Contains("• Keyboard", paragraphs);
        Assert.Contains("• Monitor", paragraphs);

        // Office category
        Assert.Contains("Office:", paragraphs);
        Assert.Contains("• Pen (Budget)", paragraphs);
        Assert.Contains("• Notebook (Budget)", paragraphs);
        Assert.Contains("• Desk", paragraphs);
    }

    [Fact]
    public void ProcessTemplate_ConditionalWithComplexExpression_Works()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Products}}");
        builder.AddParagraph("{{#if IsAvailable and Price < 100}}");
        builder.AddParagraph("{{Name}} - In Stock & Affordable");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("{{Name}}");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Products"] = new List<ProductWithAvailability>
            {
                new ProductWithAvailability { Name = "Laptop", IsAvailable = true, Price = 999m },
                new ProductWithAvailability { Name = "Mouse", IsAvailable = true, Price = 29.99m },
                new ProductWithAvailability { Name = "Keyboard", IsAvailable = false, Price = 79.99m }
            }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(3, verifier.GetParagraphCount());
        Assert.Equal("Laptop", verifier.GetParagraphText(0));
        Assert.Equal("Mouse - In Stock & Affordable", verifier.GetParagraphText(1));
        Assert.Equal("Keyboard", verifier.GetParagraphText(2));
    }

    [Fact]
    public void ProcessTemplate_ConditionalWithEmptyCollection_Works()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Start");
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("{{#if @first}}");
        builder.AddParagraph("First Item");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("Other Item");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/foreach}}");
        builder.AddParagraph("End");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string>()
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(2, verifier.GetParagraphCount());
        Assert.Equal("Start", verifier.GetParagraphText(0));
        Assert.Equal("End", verifier.GetParagraphText(1));
    }

    [Fact]
    public void ProcessTemplate_ConditionalWithSingleItem_Works()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("{{#if @first and @last}}");
        builder.AddParagraph("Only: {{.}}");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("Item: {{.}}");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "Single" }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Only: Single", verifier.GetParagraphText(0));
    }

    // Helper classes for test data
    private class Order
    {
        public decimal Amount { get; set; }
    }

    private class Category
    {
        public string Name { get; set; } = string.Empty;
        public List<Product> Products { get; set; } = new List<Product>();
    }

    private class Product
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    private class ProductWithAvailability
    {
        public string Name { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public decimal Price { get; set; }
    }
}
