// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.PropertyPaths;
using TriasDev.Templify.Utilities;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Integration tests for nested structure support (dot notation, array indexing, dictionaries).
/// These tests create actual Word documents, process them, and verify the output.
/// </summary>
public sealed class NestedStructureTests
{
    // Test data classes
    private class Customer
    {
        public string Name { get; set; } = "";
        public Address Address { get; set; } = new Address();
    }

    private class Address
    {
        public string Street { get; set; } = "";
        public string City { get; set; } = "";
        public string PostalCode { get; set; } = "";
    }

    private class Order
    {
        public string Id { get; set; } = "";
        public decimal Amount { get; set; }
        public Customer Customer { get; set; } = new Customer();
    }

    [Fact]
    public void ProcessTemplate_DotNotation_AccessesNestedProperties()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Customer: {{Customer.Name}}");
        builder.AddParagraph("City: {{Customer.Address.City}}");
        builder.AddParagraph("Street: {{Customer.Address.Street}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Customer"] = new Customer
            {
                Name = "TriasDev GmbH & Co. KG",
                Address = new Address
                {
                    Street = "Tech Street 123",
                    City = "Munich",
                    PostalCode = "80331"
                }
            }
        };

        // Act
        using TemplateTestRun run = TemplateTestHarness.Process(builder, data);
        ProcessingResult result = run.Result;

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.ReplacementCount);
        Assert.Empty(result.MissingVariables);

        DocumentVerifier verifier = run.Verifier;
        Assert.Equal(3, verifier.GetParagraphCount());
        Assert.Equal("Customer: TriasDev GmbH & Co. KG", verifier.GetParagraphText(0));
        Assert.Equal("City: Munich", verifier.GetParagraphText(1));
        Assert.Equal("Street: Tech Street 123", verifier.GetParagraphText(2));
    }

    [Fact]
    public void ProcessTemplate_ArrayIndexing_AccessesListElements()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("First: {{Items[0]}}");
        builder.AddParagraph("Second: {{Items[1]}}");
        builder.AddParagraph("Third: {{Items[2]}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "License", "Support", "Training" }
        };

        // Act
        using TemplateTestRun run = TemplateTestHarness.Process(builder, data);
        ProcessingResult result = run.Result;

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.ReplacementCount);

        DocumentVerifier verifier = run.Verifier;
        Assert.Equal("First: License", verifier.GetParagraphText(0));
        Assert.Equal("Second: Support", verifier.GetParagraphText(1));
        Assert.Equal("Third: Training", verifier.GetParagraphText(2));
    }

    [Fact]
    public void ProcessTemplate_ArrayWithObjects_AccessesNestedProperties()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Order 1: {{Orders[0].Id}} - {{Orders[0].Amount}}");
        builder.AddParagraph("Order 2: {{Orders[1].Id}} - {{Orders[1].Amount}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Orders"] = new List<Order>
            {
                new Order { Id = "ORD-001", Amount = 999.00m },
                new Order { Id = "ORD-002", Amount = 1500.00m }
            }
        };

        // Act
        using TemplateTestRun run = TemplateTestHarness.Process(builder, data);
        ProcessingResult result = run.Result;

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.ReplacementCount);

        DocumentVerifier verifier = run.Verifier;
        string text1 = verifier.GetParagraphText(0);
        Assert.Contains("ORD-001", text1);
        Assert.Equal("Order 1: ORD-001 - 999.00", text1);

        string text2 = verifier.GetParagraphText(1);
        Assert.Contains("ORD-002", text2);
        Assert.Equal("Order 2: ORD-002 - 1500.00", text2);
    }

    [Fact]
    public void ProcessTemplate_DictionaryAccess_BothNotationStyles()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Theme (bracket): {{Settings[Theme]}}");
        builder.AddParagraph("Language (dot): {{Settings.Language}}");
        builder.AddParagraph("Currency (bracket): {{Settings[Currency]}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Settings"] = new Dictionary<string, string>
            {
                ["Theme"] = "Dark",
                ["Language"] = "German",
                ["Currency"] = "EUR"
            }
        };

        // Act
        using TemplateTestRun run = TemplateTestHarness.Process(builder, data);
        ProcessingResult result = run.Result;

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.ReplacementCount);

        DocumentVerifier verifier = run.Verifier;
        Assert.Equal("Theme (bracket): Dark", verifier.GetParagraphText(0));
        Assert.Equal("Language (dot): German", verifier.GetParagraphText(1));
        Assert.Equal("Currency (bracket): EUR", verifier.GetParagraphText(2));
    }

    [Fact]
    public void ProcessTemplate_MixedNotation_ComplexNestedPath()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Order ID: {{Orders[0].Id}}");
        builder.AddParagraph("Customer: {{Orders[0].Customer.Name}}");
        builder.AddParagraph("City: {{Orders[0].Customer.Address.City}}");
        builder.AddParagraph("Postal: {{Orders[0].Customer.Address.PostalCode}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Orders"] = new List<Order>
            {
                new Order
                {
                    Id = "ORD-2025-001",
                    Amount = 2499.99m,
                    Customer = new Customer
                    {
                        Name = "TriasDev GmbH & Co. KG",
                        Address = new Address
                        {
                            Street = "Innovation Boulevard 42",
                            City = "Munich",
                            PostalCode = "80331"
                        }
                    }
                }
            }
        };

        // Act
        using TemplateTestRun run = TemplateTestHarness.Process(builder, data);
        ProcessingResult result = run.Result;

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.ReplacementCount);

        DocumentVerifier verifier = run.Verifier;
        Assert.Equal("Order ID: ORD-2025-001", verifier.GetParagraphText(0));
        Assert.Equal("Customer: TriasDev GmbH & Co. KG", verifier.GetParagraphText(1));
        Assert.Equal("City: Munich", verifier.GetParagraphText(2));
        Assert.Equal("Postal: 80331", verifier.GetParagraphText(3));
    }

    [Fact]
    public void ProcessTemplate_DirectKeyTakesPrecedence_OverNestedPath()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Value: {{Customer.Name}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Customer.Name"] = "Direct Value",  // Direct key takes precedence
            ["Customer"] = new Customer { Name = "Nested Value" }
        };

        // Act
        using TemplateTestRun run = TemplateTestHarness.Process(builder, data);
        ProcessingResult result = run.Result;

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.ReplacementCount);

        DocumentVerifier verifier = run.Verifier;
        Assert.Equal("Value: Direct Value", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_ArrayOutOfBounds_TreatsAsMissing()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("First: {{Items[0]}}, Fifth: {{Items[4]}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "Item1", "Item2", "Item3" }
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.LeaveUnchanged
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.ReplacementCount); // Only Items[0] replaced
        Assert.Single(result.MissingVariables);
        Assert.Contains("Items[4]", result.MissingVariables);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Contains("Item1", text);
        Assert.Contains("{{Items[4]}}", text);
    }
}
