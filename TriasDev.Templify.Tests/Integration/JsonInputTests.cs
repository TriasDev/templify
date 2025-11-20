using System.Globalization;
using System.Text.Json;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Integration tests for JSON string input functionality.
/// These tests verify that the JSON overload of ProcessTemplate works correctly end-to-end.
/// </summary>
public sealed class JsonInputTests
{
    [Fact]
    public void ProcessTemplate_WithJsonSimplePlaceholder_ReplacesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{Name}}!");

        MemoryStream templateStream = builder.ToStream();

        string jsonData = """
            {
                "Name": "John Doe"
            }
            """;

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, jsonData);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.ReplacementCount);
        Assert.Empty(result.MissingVariables);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Hello John Doe!", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_WithJsonMultiplePlaceholders_ReplacesAll()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Company: {{CompanyName}}, Contact: {{ContactPerson}}");

        MemoryStream templateStream = builder.ToStream();

        string jsonData = """
            {
                "CompanyName": "TriasDev GmbH & Co. KG",
                "ContactPerson": "Max Mustermann"
            }
            """;

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, jsonData);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.ReplacementCount);
        Assert.Empty(result.MissingVariables);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Contains("TriasDev GmbH & Co. KG", text);
        Assert.Contains("Max Mustermann", text);
    }

    [Fact]
    public void ProcessTemplate_WithJsonNestedObject_ReplacesNestedValues()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Customer: {{Customer.Name}}");
        builder.AddParagraph("City: {{Customer.Address.City}}");

        MemoryStream templateStream = builder.ToStream();

        string jsonData = """
            {
                "Customer": {
                    "Name": "Alice",
                    "Address": {
                        "City": "Munich",
                        "ZipCode": "80331"
                    }
                }
            }
            """;

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, jsonData);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.ReplacementCount);
        Assert.Empty(result.MissingVariables);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal("Customer: Alice", verifier.GetParagraphText(0));
        Assert.Equal("City: Munich", verifier.GetParagraphText(1));
    }

    [Fact]
    public void ProcessTemplate_WithJsonArray_WorksWithLoops()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Items:");
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("- {{.}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        string jsonData = """
            {
                "Items": ["Apple", "Banana", "Cherry"]
            }
            """;

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, jsonData);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string allText = string.Join(" ", Enumerable.Range(0, verifier.GetParagraphCount())
            .Select(i => verifier.GetParagraphText(i)));
        Assert.Contains("Apple", allText);
        Assert.Contains("Banana", allText);
        Assert.Contains("Cherry", allText);
    }

    [Fact]
    public void ProcessTemplate_WithJsonArrayOfObjects_WorksWithLoopsAndNestedProperties()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Line Items:");
        builder.AddParagraph("{{#foreach LineItems}}");
        builder.AddParagraph("Product: {{Product}}, Qty: {{Quantity}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        string jsonData = """
            {
                "LineItems": [
                    { "Product": "Widget", "Quantity": 2 },
                    { "Product": "Gadget", "Quantity": 5 }
                ]
            }
            """;

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, jsonData);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string allText = string.Join(" ", Enumerable.Range(0, verifier.GetParagraphCount())
            .Select(i => verifier.GetParagraphText(i)));
        Assert.Contains("Widget", allText);
        Assert.Contains("2", allText);
        Assert.Contains("Gadget", allText);
        Assert.Contains("5", allText);
    }

    [Fact]
    public void ProcessTemplate_WithJsonBooleans_WorksWithConditionals()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if IsApproved}}");
        builder.AddParagraph("Status: Approved");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{#if IsRejected}}");
        builder.AddParagraph("Status: Rejected");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        string jsonData = """
            {
                "IsApproved": true,
                "IsRejected": false
            }
            """;

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, jsonData);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Contains("Approved", text);

        // The rejected paragraph should be empty or removed
        if (verifier.GetParagraphCount() > 1)
        {
            string secondText = verifier.GetParagraphText(1);
            Assert.DoesNotContain("Rejected", secondText);
        }
    }

    [Fact]
    public void ProcessTemplate_WithJsonNumericTypes_ReplacesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Price: {{Price}}");
        builder.AddParagraph("Quantity: {{Quantity}}");
        builder.AddParagraph("Total: {{Total}}");

        MemoryStream templateStream = builder.ToStream();

        string jsonData = """
            {
                "Price": 19.99,
                "Quantity": 3,
                "Total": 59.97
            }
            """;

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, jsonData);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.ReplacementCount);
        Assert.Empty(result.MissingVariables);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string allText = string.Join(" ", Enumerable.Range(0, verifier.GetParagraphCount())
            .Select(i => verifier.GetParagraphText(i)));
        Assert.Contains("19", allText);
        Assert.Contains("3", allText);
        Assert.Contains("59", allText);
    }

    [Fact]
    public void ProcessTemplate_WithJsonComplexStructure_HandlesAllFeatures()
    {
        // Arrange - Test nested objects with multiple levels
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Company: {{Company.Name}}");
        builder.AddParagraph("Active: {{Company.IsActive}}");
        builder.AddParagraph("Department Count: {{Company.DepartmentCount}}");

        MemoryStream templateStream = builder.ToStream();

        string jsonData = """
            {
                "Company": {
                    "Name": "TriasDev GmbH & Co. KG",
                    "IsActive": true,
                    "DepartmentCount": 5
                }
            }
            """;

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, jsonData);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(3, verifier.GetParagraphCount());
        Assert.Contains("TriasDev GmbH & Co. KG", verifier.GetParagraphText(0));
        Assert.Contains("True", verifier.GetParagraphText(1));
        Assert.Contains("5", verifier.GetParagraphText(2));
    }

    [Fact]
    public void ProcessTemplate_WithInvalidJson_ThrowsJsonException()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{Name}}!");

        MemoryStream templateStream = builder.ToStream();

        string invalidJsonData = "{invalid json}";

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act & Assert
        Assert.Throws<JsonException>(() =>
            processor.ProcessTemplate(templateStream, outputStream, invalidJsonData));
    }

    [Fact]
    public void ProcessTemplate_WithJsonArray_ThrowsJsonException()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{Name}}!");

        MemoryStream templateStream = builder.ToStream();

        string jsonArray = """
            ["item1", "item2", "item3"]
            """;

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act & Assert
        JsonException exception = Assert.Throws<JsonException>(() =>
            processor.ProcessTemplate(templateStream, outputStream, jsonArray));

        Assert.Contains("JSON root must be an object", exception.Message);
    }

    [Fact]
    public void ProcessTemplate_WithNullJsonString_ThrowsArgumentNullException()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{Name}}!");

        MemoryStream templateStream = builder.ToStream();

        string? nullJsonData = null;

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act & Assert
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            processor.ProcessTemplate(templateStream, outputStream, nullJsonData!));

        Assert.Equal("jsonData", exception.ParamName);
    }

    [Fact]
    public void ProcessTemplate_WithEmptyJsonString_ThrowsArgumentException()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{Name}}!");

        MemoryStream templateStream = builder.ToStream();

        string emptyJsonData = "";

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            processor.ProcessTemplate(templateStream, outputStream, emptyJsonData));
    }

    [Fact]
    public void ProcessTemplate_WithJsonMissingVariable_BehavesAccordingToOptions()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{Name}}! Your age is {{Age}}.");

        MemoryStream templateStream = builder.ToStream();

        string jsonData = """
            {
                "Name": "John Doe"
            }
            """;

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture,
            MissingVariableBehavior = MissingVariableBehavior.LeaveUnchanged
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, jsonData);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.ReplacementCount);
        Assert.Single(result.MissingVariables);
        Assert.Contains("Age", result.MissingVariables);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string text = verifier.GetParagraphText(0);
        Assert.Contains("John Doe", text);
        Assert.Contains("{{Age}}", text); // Should remain unchanged
    }
}
