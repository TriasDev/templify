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

    /// <summary>
    /// Regression test for bug where conditionals inside loops were evaluated at global scope
    /// instead of loop item scope. This caused conditionals to fail when the path exists on
    /// loop items but not on the global context.
    /// </summary>
    /// <remarks>
    /// Based on customer issue where:
    /// - Global context has interview.availableItemsByKey.items but WITHOUT certain keys
    /// - Loop items (itAssets) each have their own interview with the keys
    /// - Conditional {{#if interview.availableItemsByKey.items.someKey...}} inside loop
    ///   should evaluate against loop item, not global context
    /// </remarks>
    [Fact]
    public void ProcessTemplate_ConditionalInLoop_UsesLoopItemContext_NotGlobalContext()
    {
        // Arrange: Global interview does NOT have the nested path, but loop items DO
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach process.itAssets.items}}");
        builder.AddParagraph("{{#if interview.settings.isEnabled}}");
        builder.AddParagraph("YES");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("NO");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        // Global interview does NOT have settings.isEnabled
        // But each loop item has its own interview WITH settings.isEnabled = true
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            // Global interview - missing the nested path
            ["interview"] = new Dictionary<string, object>
            {
                ["name"] = "Global Interview"
                // Note: NO "settings" here - this is the key difference
            },
            ["process"] = new Dictionary<string, object>
            {
                ["itAssets"] = new Dictionary<string, object>
                {
                    ["items"] = new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            ["name"] = "Asset 1",
                            // Loop item HAS the nested path
                            ["interview"] = new Dictionary<string, object>
                            {
                                ["settings"] = new Dictionary<string, object>
                                {
                                    ["isEnabled"] = true
                                }
                            }
                        },
                        new Dictionary<string, object>
                        {
                            ["name"] = "Asset 2",
                            ["interview"] = new Dictionary<string, object>
                            {
                                ["settings"] = new Dictionary<string, object>
                                {
                                    ["isEnabled"] = true
                                }
                            }
                        }
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

        // Both items should show "YES" because their interview.settings.isEnabled is true
        // Bug behavior: Would show "NO" because global interview doesn't have the path
        Assert.Equal(2, paragraphs.Count);
        Assert.Equal("YES", paragraphs[0]);
        Assert.Equal("YES", paragraphs[1]);
    }

    /// <summary>
    /// Test inline conditional inside loop also uses loop item context.
    /// </summary>
    [Fact]
    public void ProcessTemplate_InlineConditionalInLoop_UsesLoopItemContext()
    {
        // Arrange: Same scenario but with inline conditional (same paragraph)
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach items}}");
        builder.AddParagraph("{{#if data.active}}Active{{else}}Inactive{{/if}} - {{name}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        // Global data does NOT have data.active, but loop items DO
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["data"] = new Dictionary<string, object>
            {
                ["name"] = "Global"
                // NO "active" field
            },
            ["items"] = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["name"] = "Item1",
                    ["data"] = new Dictionary<string, object>
                    {
                        ["active"] = true
                    }
                },
                new Dictionary<string, object>
                {
                    ["name"] = "Item2",
                    ["data"] = new Dictionary<string, object>
                    {
                        ["active"] = false
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

        Assert.Equal(2, paragraphs.Count);
        Assert.Equal("Active - Item1", paragraphs[0]);
        Assert.Equal("Inactive - Item2", paragraphs[1]);
    }

    /// <summary>
    /// Test conditional in nested loop accessing outer loop's data.
    /// </summary>
    [Fact]
    public void ProcessTemplate_ConditionalInNestedLoop_AccessesOuterLoopData()
    {
        // Arrange: Conditional inside inner loop references outer loop's property
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach categories}}");
        builder.AddParagraph("Category: {{name}}");
        builder.AddParagraph("{{#foreach items}}");
        // Conditional checks OUTER loop's "isPremium" property, not inner item's
        builder.AddParagraph("{{#if isPremium}}★ {{title}}{{else}}{{title}}{{/if}}");
        builder.AddParagraph("{{/foreach}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["categories"] = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["name"] = "Premium Category",
                    ["isPremium"] = true,
                    ["items"] = new List<object>
                    {
                        new Dictionary<string, object> { ["title"] = "Item A" },
                        new Dictionary<string, object> { ["title"] = "Item B" }
                    }
                },
                new Dictionary<string, object>
                {
                    ["name"] = "Standard Category",
                    ["isPremium"] = false,
                    ["items"] = new List<object>
                    {
                        new Dictionary<string, object> { ["title"] = "Item C" }
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

        // Premium category items get stars
        Assert.Contains("Category: Premium Category", paragraphs);
        Assert.Contains("★ Item A", paragraphs);
        Assert.Contains("★ Item B", paragraphs);

        // Standard category items don't get stars
        Assert.Contains("Category: Standard Category", paragraphs);
        Assert.Contains("Item C", paragraphs);
        Assert.DoesNotContain("★ Item C", paragraphs);
    }

    /// <summary>
    /// Test conditional in nested loop accessing global data.
    /// </summary>
    [Fact]
    public void ProcessTemplate_ConditionalInNestedLoop_AccessesGlobalData()
    {
        // Arrange: Conditional inside nested loop references GLOBAL property
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach orders}}");
        builder.AddParagraph("Order: {{id}}");
        builder.AddParagraph("{{#foreach items}}");
        // Conditional checks GLOBAL "showPrices" setting
        builder.AddParagraph("{{#if showPrices}}{{name}}: ${{price}}{{else}}{{name}}{{/if}}");
        builder.AddParagraph("{{/foreach}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["showPrices"] = true, // Global setting
            ["orders"] = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["id"] = "ORD-001",
                    ["items"] = new List<object>
                    {
                        new Dictionary<string, object> { ["name"] = "Widget", ["price"] = 10 },
                        new Dictionary<string, object> { ["name"] = "Gadget", ["price"] = 20 }
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

        Assert.Contains("Order: ORD-001", paragraphs);
        Assert.Contains("Widget: $10", paragraphs);
        Assert.Contains("Gadget: $20", paragraphs);
    }

    /// <summary>
    /// Test deeply nested path resolution in loop context (like customer's interview structure).
    /// </summary>
    [Fact]
    public void ProcessTemplate_DeeplyNestedPathInLoopConditional_Works()
    {
        // Arrange: Mimics customer's interview.availableItemsByKey.items.someKey.answers... structure
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach assets}}");
        builder.AddParagraph("{{#if interview.availableItemsByKey.items.question1.answers.yes.selected}}Ja{{else}}Nein{{/if}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            // Global interview has different structure (no question1)
            ["interview"] = new Dictionary<string, object>
            {
                ["availableItemsByKey"] = new Dictionary<string, object>
                {
                    ["items"] = new Dictionary<string, object>
                    {
                        ["otherQuestion"] = new Dictionary<string, object>
                        {
                            ["answers"] = new Dictionary<string, object>()
                        }
                    }
                }
            },
            ["assets"] = new List<object>
            {
                // Loop item 1: question1 selected = true
                new Dictionary<string, object>
                {
                    ["interview"] = new Dictionary<string, object>
                    {
                        ["availableItemsByKey"] = new Dictionary<string, object>
                        {
                            ["items"] = new Dictionary<string, object>
                            {
                                ["question1"] = new Dictionary<string, object>
                                {
                                    ["answers"] = new Dictionary<string, object>
                                    {
                                        ["yes"] = new Dictionary<string, object>
                                        {
                                            ["selected"] = true
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                // Loop item 2: question1 selected = false
                new Dictionary<string, object>
                {
                    ["interview"] = new Dictionary<string, object>
                    {
                        ["availableItemsByKey"] = new Dictionary<string, object>
                        {
                            ["items"] = new Dictionary<string, object>
                            {
                                ["question1"] = new Dictionary<string, object>
                                {
                                    ["answers"] = new Dictionary<string, object>
                                    {
                                        ["yes"] = new Dictionary<string, object>
                                        {
                                            ["selected"] = false
                                        }
                                    }
                                }
                            }
                        }
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

        Assert.Equal(2, paragraphs.Count);
        Assert.Equal("Ja", paragraphs[0]);   // First item has selected=true
        Assert.Equal("Nein", paragraphs[1]); // Second item has selected=false
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
