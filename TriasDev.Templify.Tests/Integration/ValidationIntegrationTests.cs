// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Integration tests for template validation functionality.
/// These tests create actual Word documents and validate them for errors.
/// </summary>
public sealed class ValidationIntegrationTests
{
    #region Valid Template Tests

    [Fact]
    public void ValidateTemplate_ValidTemplateWithSimplePlaceholders_ReturnsSuccess()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{Name}}!");
        builder.AddParagraph("Welcome to {{CompanyName}}.");

        MemoryStream templateStream = builder.ToStream();

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(2, result.AllPlaceholders.Count);
        Assert.Contains("Name", result.AllPlaceholders);
        Assert.Contains("CompanyName", result.AllPlaceholders);
        Assert.Empty(result.MissingVariables);
    }

    [Fact]
    public void ValidateTemplate_ValidTemplateWithConditionals_ReturnsSuccess()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if IsApproved}}");
        builder.AddParagraph("Status: Approved");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Contains("IsApproved", result.AllPlaceholders);
    }

    [Fact]
    public void ValidateTemplate_ValidTemplateWithLoops_ReturnsSuccess()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Items:");
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("- {{Name}}: {{Price}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Contains("Items", result.AllPlaceholders);
        Assert.Contains("Name", result.AllPlaceholders);
        Assert.Contains("Price", result.AllPlaceholders);
    }

    [Fact]
    public void ValidateTemplate_ValidTemplateWithNestedStructures_ReturnsSuccess()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Orders}}");
        builder.AddParagraph("Order {{OrderId}}:");
        builder.AddParagraph("{{#if IsShipped}}");
        builder.AddParagraph("Shipped on {{ShipDate}}");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Contains("Orders", result.AllPlaceholders);
        Assert.Contains("OrderId", result.AllPlaceholders);
        Assert.Contains("IsShipped", result.AllPlaceholders);
        Assert.Contains("ShipDate", result.AllPlaceholders);
    }

    [Fact]
    public void ValidateTemplate_EmptyTemplate_ReturnsSuccess()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("This template has no placeholders.");

        MemoryStream templateStream = builder.ToStream();

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.AllPlaceholders);
    }

    #endregion

    #region Invalid Template Tests - Conditionals

    [Fact]
    public void ValidateTemplate_UnmatchedConditionalStart_ReturnsError()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if IsApproved}}");
        builder.AddParagraph("This conditional is never closed.");
        builder.AddParagraph("Next paragraph.");

        MemoryStream templateStream = builder.ToStream();

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorType.UnmatchedConditionalStart, result.Errors[0].Type);
        Assert.Contains("has no matching", result.Errors[0].Message);
    }

    [Fact]
    public void ValidateTemplate_NestedUnmatchedConditional_ReturnsError()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if IsActive}}");
        builder.AddParagraph("{{#if IsApproved}}");
        builder.AddParagraph("Content");
        builder.AddParagraph("{{/if}}");
        // Missing closing {{/if}} for IsActive

        MemoryStream templateStream = builder.ToStream();

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Type == ValidationErrorType.UnmatchedConditionalStart);
    }

    #endregion

    #region Invalid Template Tests - Loops

    [Fact]
    public void ValidateTemplate_UnmatchedLoopStart_ReturnsError()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("- {{Name}}");
        // Missing {{/foreach}}

        MemoryStream templateStream = builder.ToStream();

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorType.UnmatchedLoopStart, result.Errors[0].Type);
        Assert.Contains("has no matching", result.Errors[0].Message);
    }

    [Fact]
    public void ValidateTemplate_NestedUnmatchedLoop_ReturnsError()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Orders}}");
        builder.AddParagraph("Order: {{OrderId}}");
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("- {{Name}}");
        builder.AddParagraph("{{/foreach}}");
        // Missing closing {{/foreach}} for Orders

        MemoryStream templateStream = builder.ToStream();

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Type == ValidationErrorType.UnmatchedLoopStart);
    }

    #endregion

    #region Validation With Data Tests

    [Fact]
    public void ValidateTemplate_WithData_AllVariablesProvided_ReturnsSuccess()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{Name}}!");
        builder.AddParagraph("Age: {{Age}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "John Doe",
            ["Age"] = 30
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream, data);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.MissingVariables);
        Assert.Equal(2, result.AllPlaceholders.Count);
    }

    [Fact]
    public void ValidateTemplate_WithData_MissingVariable_ReturnsError()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{Name}}!");
        builder.AddParagraph("Age: {{Age}}");
        builder.AddParagraph("Email: {{Email}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "John Doe",
            ["Age"] = 30
            // Email is missing
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream, data);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.Single(result.MissingVariables);
        Assert.Contains("Email", result.MissingVariables);
        Assert.Contains(result.Errors, e => e.Type == ValidationErrorType.MissingVariable);
        Assert.Contains(result.Errors, e => e.Message.Contains("Email"));
    }

    [Fact]
    public void ValidateTemplate_WithData_MultipleMissingVariables_ReturnsAllErrors()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Name}} - {{Email}} - {{Phone}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "John Doe"
            // Email and Phone are missing
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream, data);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(2, result.MissingVariables.Count);
        Assert.Contains("Email", result.MissingVariables);
        Assert.Contains("Phone", result.MissingVariables);
        Assert.Equal(2, result.Errors.Count(e => e.Type == ValidationErrorType.MissingVariable));
    }

    [Fact]
    public void ValidateTemplate_WithData_NestedProperties_ValidatesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Customer: {{Customer.Name}}");
        builder.AddParagraph("City: {{Customer.Address.City}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Customer"] = new
            {
                Name = "Alice",
                Address = new
                {
                    City = "Munich",
                    ZipCode = "80331"
                }
            }
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream, data);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.MissingVariables);
        Assert.Contains("Customer.Name", result.AllPlaceholders);
        Assert.Contains("Customer.Address.City", result.AllPlaceholders);
    }

    [Fact]
    public void ValidateTemplate_WithData_MissingNestedProperty_ReturnsError()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Customer: {{Customer.Name}}");
        builder.AddParagraph("Phone: {{Customer.Phone}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Customer"] = new
            {
                Name = "Alice"
                // Phone is missing
            }
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream, data);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.MissingVariables);
        Assert.Contains("Customer.Phone", result.MissingVariables);
    }

    #endregion

    #region Special Cases

    [Fact]
    public void ValidateTemplate_WithLoopMetadataPlaceholders_DoesNotReportAsMissing()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("{{@index}}: {{Items[@index]}} (First: {{@first}}, Last: {{@last}})");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new[] { "Item 1", "Item 2" }
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream, data);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.MissingVariables);
        // @index, @first, @last should be in allPlaceholders but not in missingVariables
        Assert.Contains("@index", result.AllPlaceholders);
        Assert.Contains("@first", result.AllPlaceholders);
        Assert.Contains("@last", result.AllPlaceholders);
    }

    [Fact]
    public void ValidateTemplate_WithCurrentItemPlaceholder_DoesNotReportAsMissing()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("- {{.}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new[] { "Apple", "Banana", "Cherry" }
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream, data);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.MissingVariables);
        // The "." placeholder should be in allPlaceholders but not in missingVariables
        Assert.Contains(".", result.AllPlaceholders);
    }

    [Fact]
    public void ValidateTemplate_CombinedSyntaxAndDataErrors_ReturnsAllErrors()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if IsApproved}}");
        builder.AddParagraph("Hello {{Name}}!");
        // Missing {{/if}}
        builder.AddParagraph("Email: {{Email}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["IsApproved"] = true,
            ["Name"] = "John"
            // Email is missing
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream, data);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 2);
        Assert.Contains(result.Errors, e => e.Type == ValidationErrorType.UnmatchedConditionalStart);
        Assert.Contains(result.Errors, e => e.Type == ValidationErrorType.MissingVariable);
    }

    #endregion

    #region Argument Validation Tests

    [Fact]
    public void ValidateTemplate_NullTemplateStream_ThrowsArgumentNullException()
    {
        // Arrange
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions();
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act & Assert
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            processor.ValidateTemplate(null!));

        Assert.Equal("templateStream", exception.ParamName);
    }

    [Fact]
    public void ValidateTemplate_NullData_ThrowsArgumentNullException()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{Name}}!");
        MemoryStream templateStream = builder.ToStream();

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions();
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act & Assert
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            processor.ValidateTemplate(templateStream, null!));

        Assert.Equal("data", exception.ParamName);
    }

    #endregion

    #region Loop-Scoped Variable Validation

    [Fact]
    public void ValidateTemplate_VariablesInsideLoop_DoesNotFlagAsMissing()
    {
        // Arrange - This is the core test case from the customer feedback
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("Name: {{Name}}, Price: {{Price}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new[]
            {
                new Dictionary<string, object> { ["Name"] = "Item 1", ["Price"] = 100 },
                new Dictionary<string, object> { ["Name"] = "Item 2", ["Price"] = 200 }
            }
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream, data);

        // Assert - Name and Price should NOT be flagged as missing
        Assert.True(result.IsValid);
        Assert.Empty(result.MissingVariables);
        Assert.Contains("Items", result.AllPlaceholders);
        Assert.Contains("Name", result.AllPlaceholders);
        Assert.Contains("Price", result.AllPlaceholders);
    }

    [Fact]
    public void ValidateTemplate_MissingVariablesInsideLoop_FlagsCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("Name: {{Name}}, Missing: {{NonExistent}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new[]
            {
                new Dictionary<string, object> { ["Name"] = "Item 1" },
                new Dictionary<string, object> { ["Name"] = "Item 2" }
            }
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream, data);

        // Assert - NonExistent should be flagged as missing, but Name should not
        Assert.False(result.IsValid);
        Assert.Single(result.MissingVariables);
        Assert.Contains("NonExistent", result.MissingVariables);
        Assert.DoesNotContain("Name", result.MissingVariables);
    }

    [Fact]
    public void ValidateTemplate_NestedLoops_ResolvesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Categories}}");
        builder.AddParagraph("Category: {{CategoryName}}");
        builder.AddParagraph("{{#foreach Products}}");
        builder.AddParagraph("- {{ProductName}}: {{ProductPrice}}");
        builder.AddParagraph("{{/foreach}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Categories"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["CategoryName"] = "Electronics",
                    ["Products"] = new[]
                    {
                        new Dictionary<string, object> { ["ProductName"] = "Phone", ["ProductPrice"] = 999 },
                        new Dictionary<string, object> { ["ProductName"] = "Laptop", ["ProductPrice"] = 1999 }
                    }
                }
            }
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream, data);

        // Assert - All variables should be resolved correctly
        Assert.True(result.IsValid);
        Assert.Empty(result.MissingVariables);
    }

    [Fact]
    public void ValidateTemplate_EmptyCollection_AddsWarning()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("Name: {{Name}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = Array.Empty<Dictionary<string, object>>()
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream, data);

        // Assert - Should have a warning about empty collection
        Assert.True(result.IsValid); // Still valid, just a warning
        Assert.Single(result.Warnings);
        Assert.Contains("Items", result.Warnings[0].Message);
        Assert.Equal(ValidationWarningType.EmptyLoopCollection, result.Warnings[0].Type);
    }

    [Fact]
    public void ValidateTemplate_GlobalVariableInsideLoop_ResolvesFromGlobal()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("Item: {{Name}}, Company: {{CompanyName}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["CompanyName"] = "ACME Corp",
            ["Items"] = new[]
            {
                new Dictionary<string, object> { ["Name"] = "Item 1" },
                new Dictionary<string, object> { ["Name"] = "Item 2" }
            }
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream, data);

        // Assert - CompanyName (global) and Name (loop-scoped) should both resolve
        Assert.True(result.IsValid);
        Assert.Empty(result.MissingVariables);
    }

    [Fact]
    public void ValidateTemplate_HeterogeneousCollection_AggregatesAllProperties()
    {
        // Arrange - Different items have different dynamic properties
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("ID: {{id}}, Name: {{name}}, DynProp1: {{dynProperty1}}, DynProp2: {{dynProperty2}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["id"] = "item1",
                    ["name"] = "Item One",
                    ["dynProperty1"] = "Dynamic value for item one."
                },
                new Dictionary<string, object>
                {
                    ["id"] = "item2",
                    ["name"] = "Item Two",
                    ["dynProperty2"] = "Dynamic value for item two."
                }
            }
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream, data);

        // Assert - Both dynProperty1 and dynProperty2 should be recognized
        // because we aggregate properties from ALL items
        Assert.True(result.IsValid);
        Assert.Empty(result.MissingVariables);
    }

    [Fact]
    public void ValidateTemplate_NestedPropertyInsideLoop_ResolvesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Customers}}");
        builder.AddParagraph("Name: {{Name}}, City: {{Address.City}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Customers"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["Name"] = "John",
                    ["Address"] = new Dictionary<string, object> { ["City"] = "New York" }
                }
            }
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream, data);

        // Assert - Address.City should resolve (Address is a property of loop item)
        Assert.True(result.IsValid);
        Assert.Empty(result.MissingVariables);
    }

    [Fact]
    public void ValidateTemplate_LoopVariableShadowsGlobal_ResolvesCorrectly()
    {
        // Arrange - Both global and loop item have "Name" property
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Global: {{Name}}");
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("Item: {{Name}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "Global Name",
            ["Items"] = new[]
            {
                new Dictionary<string, object> { ["Name"] = "Item 1" },
                new Dictionary<string, object> { ["Name"] = "Item 2" }
            }
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream, data);

        // Assert - Name should resolve in both contexts (global and loop-scoped)
        Assert.True(result.IsValid);
        Assert.Empty(result.MissingVariables);
    }

    #endregion
}
