// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Integration tests for named iteration variable syntax ({{#foreach item in Items}}).
/// Tests the ability to explicitly reference loop variables and access parent scope.
/// </summary>
public sealed class NamedIterationVariableIntegrationTests
{
    // Test data classes
    private class Category
    {
        public string Name { get; set; } = "";
        public List<Product> Products { get; set; } = new List<Product>();
    }

    private class Product
    {
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
    }

    private class Order
    {
        public string OrderId { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public List<LineItem> Items { get; set; } = new List<LineItem>();
    }

    private class LineItem
    {
        public string Product { get; set; } = "";
        public int Quantity { get; set; }
    }

    [Fact]
    public void ProcessTemplate_NamedVariable_SimpleLoop()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Products:");
        builder.AddParagraph("{{#foreach product in Products}}");
        builder.AddParagraph("- {{product.Name}}: {{product.Price}} EUR");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Products"] = new List<Product>
            {
                new Product { Name = "Widget", Price = 19.99m },
                new Product { Name = "Gadget", Price = 29.99m }
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
        Assert.Equal("Products:", paragraphs[0]);
        Assert.Contains("Widget", paragraphs[1]);
        Assert.Matches(@"19[.,]99", paragraphs[1]); // Handle locale differences (period or comma)
        Assert.Contains("Gadget", paragraphs[2]);
        Assert.Matches(@"29[.,]99", paragraphs[2]); // Handle locale differences (period or comma)
    }

    [Fact]
    public void ProcessTemplate_NamedVariable_NestedLoops_AccessParent()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach category in Categories}}");
        builder.AddParagraph("Category: {{category.Name}}");
        builder.AddParagraph("{{#foreach product in category.Products}}");
        builder.AddParagraph("  - {{category.Name}}: {{product.Name}} ({{product.Price}} EUR)");
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
                        new Product { Name = "Phone", Price = 599.00m },
                        new Product { Name = "Tablet", Price = 399.00m }
                    }
                },
                new Category
                {
                    Name = "Books",
                    Products = new List<Product>
                    {
                        new Product { Name = "C# Guide", Price = 49.99m }
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

        // Expected: Category header + products for Electronics, Category header + products for Books
        Assert.Contains(paragraphs, p => p.Contains("Category: Electronics"));
        Assert.Contains(paragraphs, p => p.Contains("Electronics: Phone"));
        Assert.Contains(paragraphs, p => p.Contains("Electronics: Tablet"));
        Assert.Contains(paragraphs, p => p.Contains("Category: Books"));
        Assert.Contains(paragraphs, p => p.Contains("Books: C# Guide"));
    }

    [Fact]
    public void ProcessTemplate_NamedVariable_ImplicitSyntaxStillWorks()
    {
        // Arrange: Use named variable but also implicit syntax
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach product in Products}}");
        builder.AddParagraph("Named: {{product.Name}}, Implicit: {{Name}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Products"] = new List<Product>
            {
                new Product { Name = "Widget", Price = 19.99m }
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

        Assert.Single(paragraphs);
        Assert.Contains("Named: Widget", paragraphs[0]);
        Assert.Contains("Implicit: Widget", paragraphs[0]);
    }

    [Fact]
    public void ProcessTemplate_BackwardCompatibility_ImplicitSyntax()
    {
        // Arrange: Old syntax without named variable
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Products}}");
        builder.AddParagraph("{{Name}}: {{Price}} EUR");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Products"] = new List<Product>
            {
                new Product { Name = "Widget", Price = 19.99m },
                new Product { Name = "Gadget", Price = 29.99m }
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
        Assert.Contains("Widget", paragraphs[0]);
        Assert.Contains("Gadget", paragraphs[1]);
    }

    [Fact]
    public void ProcessTemplate_NamedVariable_DirectReference()
    {
        // Arrange: Reference iteration variable directly (for primitive values)
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach name in Names}}");
        builder.AddParagraph("- {{name}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Names"] = new List<string> { "Alice", "Bob", "Charlie" }
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
        Assert.Equal("- Alice", paragraphs[0]);
        Assert.Equal("- Bob", paragraphs[1]);
        Assert.Equal("- Charlie", paragraphs[2]);
    }

    [Fact]
    public void ProcessTemplate_NamedVariable_AccessGlobalFromNestedLoop()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Company: {{CompanyName}}");
        builder.AddParagraph("{{#foreach order in Orders}}");
        builder.AddParagraph("Order {{order.OrderId}} for {{CompanyName}}:");
        builder.AddParagraph("{{#foreach item in order.Items}}");
        builder.AddParagraph("  - {{item.Product}} x{{item.Quantity}}");
        builder.AddParagraph("{{/foreach}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["CompanyName"] = "Acme Corp",
            ["Orders"] = new List<Order>
            {
                new Order
                {
                    OrderId = "ORD-001",
                    Items = new List<LineItem>
                    {
                        new LineItem { Product = "Widget", Quantity = 5 }
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

        Assert.Contains(paragraphs, p => p == "Company: Acme Corp");
        Assert.Contains(paragraphs, p => p.Contains("Order ORD-001 for Acme Corp"));
        Assert.Contains(paragraphs, p => p.Contains("Widget x5"));
    }

    [Fact]
    public void ProcessTemplate_NamedVariable_MixedSyntax_NestedLoops()
    {
        // Arrange: Mix of named and implicit syntax
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach category in Categories}}");
        builder.AddParagraph("{{category.Name}}:");
        builder.AddParagraph("{{#foreach Products}}"); // Implicit syntax for inner loop
        builder.AddParagraph("  - {{Name}} from {{category.Name}}");
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
                        new Product { Name = "Phone", Price = 599.00m }
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

        Assert.Contains(paragraphs, p => p.Contains("Electronics:"));
        Assert.Contains(paragraphs, p => p.Contains("Phone from Electronics"));
    }

    [Fact]
    public void ProcessTemplate_NamedVariable_WithLoopMetadata()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach item in Items}}");
        builder.AddParagraph("{{@index}}: {{item.Name}}{{#if @last}} (last){{/if}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<Product>
            {
                new Product { Name = "First", Price = 10m },
                new Product { Name = "Second", Price = 20m },
                new Product { Name = "Third", Price = 30m }
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
        Assert.Equal("0: First", paragraphs[0]);
        Assert.Equal("1: Second", paragraphs[1]);
        Assert.Equal("2: Third (last)", paragraphs[2]);
    }

    [Fact]
    public void ProcessTemplate_NamedVariable_ReservedKeyword_In_ThrowsException()
    {
        // Arrange: "in" is a reserved keyword and cannot be used as iteration variable name
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach in in Items}}");
        builder.AddParagraph("{{in}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "A", "B" }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => processor.ProcessTemplate(templateStream, outputStream, data));

        Assert.Contains("'in' is a reserved keyword", exception.Message);
    }

    [Fact]
    public void ProcessTemplate_NamedVariable_MetadataPrefix_ThrowsException()
    {
        // Arrange: Variable names starting with @ are reserved for loop metadata
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach @item in Items}}");
        builder.AddParagraph("{{@item}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "A", "B" }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => processor.ProcessTemplate(templateStream, outputStream, data));

        Assert.Contains("reserved for loop metadata", exception.Message);
    }
}
