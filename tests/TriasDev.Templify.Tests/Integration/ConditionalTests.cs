// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Core;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.PropertyPaths;
using TriasDev.Templify.Utilities;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Integration tests for conditional block functionality.
/// These tests create actual Word documents with conditionals, process them, and verify the output.
/// </summary>
public sealed class ConditionalTests
{
    [Fact]
    public void ProcessTemplate_SimpleIfWithoutElse_ConditionTrue_KeepsContent()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if IsActive}}");
        builder.AddParagraph("This content is shown");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["IsActive"] = true
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("This content is shown", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_SimpleIfWithoutElse_ConditionFalse_RemovesContent()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Before");
        builder.AddParagraph("{{#if IsActive}}");
        builder.AddParagraph("This content is hidden");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("After");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["IsActive"] = false
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(2, verifier.GetParagraphCount());
        Assert.Equal("Before", verifier.GetParagraphText(0));
        Assert.Equal("After", verifier.GetParagraphText(1));
    }

    [Fact]
    public void ProcessTemplate_IfWithElse_ConditionTrue_ShowsIfBranch()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if IsActive}}");
        builder.AddParagraph("Status: Active");
        builder.AddParagraph("{{#else}}");
        builder.AddParagraph("Status: Inactive");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["IsActive"] = true
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Status: Active", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_IfWithElse_ConditionFalse_ShowsElseBranch()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if IsActive}}");
        builder.AddParagraph("Status: Active");
        builder.AddParagraph("{{#else}}");
        builder.AddParagraph("Status: Inactive");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["IsActive"] = false
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Status: Inactive", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_EqualsOperator_WithMatchingValue_ShowsContent()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Status = Active}}");
        builder.AddParagraph("The status is active");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Status"] = "Active"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("The status is active", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_EqualsOperator_WithQuotedString_ShowsContent()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Status = \"In Progress\"}}");
        builder.AddParagraph("Work in progress");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Status"] = "In Progress"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Work in progress", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_GreaterThanOperator_WithValidCondition_ShowsContent()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Count > 5}}");
        builder.AddParagraph("Count is greater than 5");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Count"] = 10
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Count is greater than 5", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_AndOperator_BothConditionsTrue_ShowsContent()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if IsActive and Count > 0}}");
        builder.AddParagraph("Active and has items");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["IsActive"] = true,
            ["Count"] = 5
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Active and has items", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_OrOperator_OneConditionTrue_ShowsContent()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Status = Active or Status = Pending}}");
        builder.AddParagraph("Status is Active or Pending");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Status"] = "Pending"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Status is Active or Pending", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_ComplexExpression_RangeCheck_ShowsContent()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Price > 100 and Price < 200}}");
        builder.AddParagraph("Price is in mid range");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Price"] = 150m
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Price is in mid range", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_NestedProperties_WithEqualsOperator_ShowsContent()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Customer.Address.Country = Germany}}");
        builder.AddParagraph("German customer");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Customer"] = new
            {
                Address = new
                {
                    Country = "Germany"
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
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("German customer", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_ConditionalWithPlaceholders_ReplacesPlaceholdersInTrueBranch()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if IsActive}}");
        builder.AddParagraph("Name: {{Name}}");
        builder.AddParagraph("{{#else}}");
        builder.AddParagraph("Inactive user");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["IsActive"] = true,
            ["Name"] = "John Doe"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Name: John Doe", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_NestedConditionals_BothTrue_ShowsInnerContent()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if OuterCondition}}");
        builder.AddParagraph("Outer content");
        builder.AddParagraph("{{#if InnerCondition}}");
        builder.AddParagraph("Inner content");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["OuterCondition"] = true,
            ["InnerCondition"] = true
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(2, verifier.GetParagraphCount());
        Assert.Equal("Outer content", verifier.GetParagraphText(0));
        Assert.Equal("Inner content", verifier.GetParagraphText(1));
    }

    [Fact]
    public void ProcessTemplate_NestedConditionals_OuterTrueInnerFalse_ShowsOnlyOuter()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if OuterCondition}}");
        builder.AddParagraph("Outer content");
        builder.AddParagraph("{{#if InnerCondition}}");
        builder.AddParagraph("Inner content");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("More outer content");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["OuterCondition"] = true,
            ["InnerCondition"] = false
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(2, verifier.GetParagraphCount());
        Assert.Equal("Outer content", verifier.GetParagraphText(0));
        Assert.Equal("More outer content", verifier.GetParagraphText(1));
    }

    [Fact]
    public void ProcessTemplate_NestedConditionals_OuterFalse_RemovesEverything()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Before");
        builder.AddParagraph("{{#if OuterCondition}}");
        builder.AddParagraph("Outer content");
        builder.AddParagraph("{{#if InnerCondition}}");
        builder.AddParagraph("Inner content");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("After");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["OuterCondition"] = false,
            ["InnerCondition"] = true
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(2, verifier.GetParagraphCount());
        Assert.Equal("Before", verifier.GetParagraphText(0));
        Assert.Equal("After", verifier.GetParagraphText(1));
    }

    [Fact]
    public void ProcessTemplate_NestedConditionalsWithElse_ComplexScenario()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if IsVIP}}");
        builder.AddParagraph("VIP Customer");
        builder.AddParagraph("{{#if HasDiscount}}");
        builder.AddParagraph("Discount applied");
        builder.AddParagraph("{{#else}}");
        builder.AddParagraph("No discount");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{#else}}");
        builder.AddParagraph("Regular Customer");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["IsVIP"] = true,
            ["HasDiscount"] = false
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(2, verifier.GetParagraphCount());
        Assert.Equal("VIP Customer", verifier.GetParagraphText(0));
        Assert.Equal("No discount", verifier.GetParagraphText(1));
    }

    [Fact]
    public void ProcessTemplate_ThreeLevelNesting_ProcessesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Level1}}");
        builder.AddParagraph("Level 1");
        builder.AddParagraph("{{#if Level2}}");
        builder.AddParagraph("Level 2");
        builder.AddParagraph("{{#if Level3}}");
        builder.AddParagraph("Level 3");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Level1"] = true,
            ["Level2"] = true,
            ["Level3"] = true
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(3, verifier.GetParagraphCount());
        Assert.Equal("Level 1", verifier.GetParagraphText(0));
        Assert.Equal("Level 2", verifier.GetParagraphText(1));
        Assert.Equal("Level 3", verifier.GetParagraphText(2));
    }

    [Fact]
    public void ProcessTemplate_NestedConditionalsInElseBranch_ProcessesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if IsActive}}");
        builder.AddParagraph("Active");
        builder.AddParagraph("{{#else}}");
        builder.AddParagraph("{{#if IsArchived}}");
        builder.AddParagraph("Archived");
        builder.AddParagraph("{{#else}}");
        builder.AddParagraph("Inactive");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["IsActive"] = false,
            ["IsArchived"] = true
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Archived", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_NestedConditionalsWithOperators_ProcessesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Price > 100}}");
        builder.AddParagraph("Expensive item");
        builder.AddParagraph("{{#if Price > 1000}}");
        builder.AddParagraph("Very expensive");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Price"] = 1500m
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(2, verifier.GetParagraphCount());
        Assert.Equal("Expensive item", verifier.GetParagraphText(0));
        Assert.Equal("Very expensive", verifier.GetParagraphText(1));
    }

    [Fact]
    public void ProcessTemplate_ConditionalWithLoop_ProcessesBothCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if HasItems}}");
        builder.AddParagraph("Items:");
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("- {{.}}");
        builder.AddParagraph("{{/foreach}}");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["HasItems"] = true,
            ["Items"] = new List<string> { "Item 1", "Item 2" }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(3, verifier.GetParagraphCount());
        Assert.Equal("Items:", verifier.GetParagraphText(0));
        Assert.Equal("- Item 1", verifier.GetParagraphText(1));
        Assert.Equal("- Item 2", verifier.GetParagraphText(2));
    }
}
