// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests;

/// <summary>
/// Tests for TextTemplateProcessor - processing text templates with placeholders, conditionals, and loops.
/// </summary>
public sealed class TextTemplateProcessorTests
{
    #region Basic Placeholder Tests

    [Fact]
    public void ProcessTemplate_SimplePlaceholder_ReplacesValue()
    {
        // Arrange
        var processor = new TextTemplateProcessor();
        string template = "Hello {{Name}}!";
        var data = new Dictionary<string, object>
        {
            ["Name"] = "Alice"
        };

        // Act
        TextProcessingResult result = processor.ProcessTemplate(template, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Hello Alice!", result.ProcessedText);
        Assert.Equal(1, result.ReplacementCount);
    }

    [Fact]
    public void ProcessTemplate_MultiplePlaceholders_ReplacesAll()
    {
        // Arrange
        var processor = new TextTemplateProcessor();
        string template = "Hello {{FirstName}} {{LastName}}!";
        var data = new Dictionary<string, object>
        {
            ["FirstName"] = "Alice",
            ["LastName"] = "Smith"
        };

        // Act
        TextProcessingResult result = processor.ProcessTemplate(template, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Hello Alice Smith!", result.ProcessedText);
        Assert.Equal(2, result.ReplacementCount);
    }

    [Fact]
    public void ProcessTemplate_NestedProperty_ReplacesValue()
    {
        // Arrange
        var processor = new TextTemplateProcessor();
        string template = "City: {{Customer.Address.City}}";
        var data = new Dictionary<string, object>
        {
            ["Customer"] = new
            {
                Address = new
                {
                    City = "New York"
                }
            }
        };

        // Act
        TextProcessingResult result = processor.ProcessTemplate(template, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("City: New York", result.ProcessedText);
        Assert.Equal(1, result.ReplacementCount);
    }

    [Fact]
    public void ProcessTemplate_MissingVariable_LeaveUnchanged()
    {
        // Arrange
        var options = new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.LeaveUnchanged
        };
        var processor = new TextTemplateProcessor(options);
        string template = "Hello {{Name}}!";
        var data = new Dictionary<string, object>();

        // Act
        TextProcessingResult result = processor.ProcessTemplate(template, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Hello {{Name}}!", result.ProcessedText);
        Assert.Equal(0, result.ReplacementCount);
        Assert.Single(result.MissingVariables);
        Assert.Equal("Name", result.MissingVariables[0]);
    }

    [Fact]
    public void ProcessTemplate_MissingVariable_ReplaceWithEmpty()
    {
        // Arrange
        var options = new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.ReplaceWithEmpty
        };
        var processor = new TextTemplateProcessor(options);
        string template = "Hello {{Name}}!";
        var data = new Dictionary<string, object>();

        // Act
        TextProcessingResult result = processor.ProcessTemplate(template, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Hello !", result.ProcessedText);
        Assert.Equal(1, result.ReplacementCount);
        Assert.Single(result.MissingVariables);
    }

    [Fact]
    public void ProcessTemplate_MissingVariable_ThrowException()
    {
        // Arrange
        var options = new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.ThrowException
        };
        var processor = new TextTemplateProcessor(options);
        string template = "Hello {{Name}}!";
        var data = new Dictionary<string, object>();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            processor.ProcessTemplate(template, data));
    }

    #endregion

    #region Conditional Tests

    [Fact]
    public void ProcessTemplate_ConditionalTrue_IncludesIfBranch()
    {
        // Arrange
        var processor = new TextTemplateProcessor();
        string template = @"{{#if IsActive}}
Active user
{{/if}}";
        var data = new Dictionary<string, object>
        {
            ["IsActive"] = true
        };

        // Act
        TextProcessingResult result = processor.ProcessTemplate(template, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("Active user", result.ProcessedText);
        Assert.DoesNotContain("{{#if", result.ProcessedText);
        Assert.DoesNotContain("{{/if}}", result.ProcessedText);
    }

    [Fact]
    public void ProcessTemplate_ConditionalFalse_ExcludesIfBranch()
    {
        // Arrange
        var processor = new TextTemplateProcessor();
        string template = @"{{#if IsActive}}
Active user
{{/if}}";
        var data = new Dictionary<string, object>
        {
            ["IsActive"] = false
        };

        // Act
        TextProcessingResult result = processor.ProcessTemplate(template, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("Active user", result.ProcessedText);
        Assert.DoesNotContain("{{#if", result.ProcessedText);
    }

    [Fact]
    public void ProcessTemplate_ConditionalWithElse_ChoosesCorrectBranch()
    {
        // Arrange
        var processor = new TextTemplateProcessor();
        string template = @"{{#if IsVip}}
VIP customer
{{#else}}
Regular customer
{{/if}}";
        var data = new Dictionary<string, object>
        {
            ["IsVip"] = false
        };

        // Act
        TextProcessingResult result = processor.ProcessTemplate(template, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("Regular customer", result.ProcessedText);
        Assert.DoesNotContain("VIP customer", result.ProcessedText);
    }

    [Fact]
    public void ProcessTemplate_ConditionalWithComparison_Evaluates()
    {
        // Arrange
        var processor = new TextTemplateProcessor();
        string template = @"{{#if Status = Active}}
Status is active
{{/if}}";
        var data = new Dictionary<string, object>
        {
            ["Status"] = "Active"
        };

        // Act
        TextProcessingResult result = processor.ProcessTemplate(template, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("Status is active", result.ProcessedText);
    }

    #endregion

    #region Loop Tests

    [Fact]
    public void ProcessTemplate_SimpleLoop_ExpandsCollection()
    {
        // Arrange
        var processor = new TextTemplateProcessor();
        string template = @"Items:
{{#foreach Items}}
- {{Name}}
{{/foreach}}";
        var data = new Dictionary<string, object>
        {
            ["Items"] = new[]
            {
                new { Name = "Item 1" },
                new { Name = "Item 2" },
                new { Name = "Item 3" }
            }
        };

        // Act
        TextProcessingResult result = processor.ProcessTemplate(template, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("- Item 1", result.ProcessedText);
        Assert.Contains("- Item 2", result.ProcessedText);
        Assert.Contains("- Item 3", result.ProcessedText);
    }

    [Fact]
    public void ProcessTemplate_LoopWithIndex_ShowsIndex()
    {
        // Arrange
        var processor = new TextTemplateProcessor();
        string template = @"{{#foreach Items}}
{{@index}}. {{Name}}
{{/foreach}}";
        var data = new Dictionary<string, object>
        {
            ["Items"] = new[]
            {
                new { Name = "First" },
                new { Name = "Second" }
            }
        };

        // Act
        TextProcessingResult result = processor.ProcessTemplate(template, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("0. First", result.ProcessedText);
        Assert.Contains("1. Second", result.ProcessedText);
    }

    [Fact]
    public void ProcessTemplate_LoopWithMetadata_ShowsFirstLast()
    {
        // Arrange
        var processor = new TextTemplateProcessor();
        string template = @"{{#foreach Items}}
{{Name}}{{#if @last}} (last){{/if}}
{{/foreach}}";
        var data = new Dictionary<string, object>
        {
            ["Items"] = new[]
            {
                new { Name = "A" },
                new { Name = "B" },
                new { Name = "C" }
            }
        };

        // Act
        TextProcessingResult result = processor.ProcessTemplate(template, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("C (last)", result.ProcessedText);
        Assert.DoesNotContain("A (last)", result.ProcessedText);
        Assert.DoesNotContain("B (last)", result.ProcessedText);
    }

    [Fact]
    public void ProcessTemplate_EmptyCollection_RemovesLoop()
    {
        // Arrange
        var processor = new TextTemplateProcessor();
        string template = @"Items:
{{#foreach Items}}
- {{Name}}
{{/foreach}}
Done";
        var data = new Dictionary<string, object>
        {
            ["Items"] = Array.Empty<object>()
        };

        // Act
        TextProcessingResult result = processor.ProcessTemplate(template, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("Items:", result.ProcessedText);
        Assert.Contains("Done", result.ProcessedText);
        Assert.DoesNotContain("{{#foreach", result.ProcessedText);
    }

    #endregion

    #region Nested Structures Tests

    [Fact]
    public void ProcessTemplate_NestedLoops_Expands()
    {
        // Arrange
        var processor = new TextTemplateProcessor();
        string template = @"{{#foreach Categories}}
Category: {{Name}}
{{#foreach Items}}
  - {{Name}}
{{/foreach}}
{{/foreach}}";
        var data = new Dictionary<string, object>
        {
            ["Categories"] = new[]
            {
                new
                {
                    Name = "Cat1",
                    Items = new[] { new { Name = "A" }, new { Name = "B" } }
                },
                new
                {
                    Name = "Cat2",
                    Items = new[] { new { Name = "C" } }
                }
            }
        };

        // Act
        TextProcessingResult result = processor.ProcessTemplate(template, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("Category: Cat1", result.ProcessedText);
        Assert.Contains("  - A", result.ProcessedText);
        Assert.Contains("  - B", result.ProcessedText);
        Assert.Contains("Category: Cat2", result.ProcessedText);
        Assert.Contains("  - C", result.ProcessedText);
    }

    [Fact]
    public void ProcessTemplate_ConditionalInsideLoop_Evaluates()
    {
        // Arrange
        var processor = new TextTemplateProcessor();
        string template = @"{{#foreach Items}}
{{Name}}{{#if IsActive}} (active){{/if}}
{{/foreach}}";
        var data = new Dictionary<string, object>
        {
            ["Items"] = new[]
            {
                new { Name = "A", IsActive = true },
                new { Name = "B", IsActive = false },
                new { Name = "C", IsActive = true }
            }
        };

        // Act
        TextProcessingResult result = processor.ProcessTemplate(template, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("A (active)", result.ProcessedText);
        Assert.DoesNotContain("B (active)", result.ProcessedText);
        Assert.Contains("C (active)", result.ProcessedText);
    }

    [Fact]
    public void ProcessTemplate_LoopInsideConditional_Expands()
    {
        // Arrange
        var processor = new TextTemplateProcessor();
        string template = @"{{#if HasItems}}
Items:
{{#foreach Items}}
- {{Name}}
{{/foreach}}
{{/if}}";
        var data = new Dictionary<string, object>
        {
            ["HasItems"] = true,
            ["Items"] = new[]
            {
                new { Name = "A" },
                new { Name = "B" }
            }
        };

        // Act
        TextProcessingResult result = processor.ProcessTemplate(template, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("- A", result.ProcessedText);
        Assert.Contains("- B", result.ProcessedText);
    }

    #endregion

    #region Integration/Real-World Scenarios

    [Fact]
    public void ProcessTemplate_EmailTemplate_ProcessesCorrectly()
    {
        // Arrange
        var options = new PlaceholderReplacementOptions
        {
            Culture = System.Globalization.CultureInfo.InvariantCulture
        };
        var processor = new TextTemplateProcessor(options);
        string template = @"Dear {{CustomerName}},

Thank you for your order #{{OrderId}}.

{{#if IsVip}}
As a VIP customer, you'll receive free shipping!
{{#else}}
Your order will arrive in 3-5 business days.
{{/if}}

Order Details:
{{#foreach Items}}
- {{Name}}: ${{Price}}
{{/foreach}}

Total: ${{Total}}

Best regards,
The Team";

        var data = new Dictionary<string, object>
        {
            ["CustomerName"] = "Alice Smith",
            ["OrderId"] = 12345,
            ["IsVip"] = true,
            ["Items"] = new[]
            {
                new { Name = "Widget", Price = 29.99 },
                new { Name = "Gadget", Price = 49.99 }
            },
            ["Total"] = 79.98
        };

        // Act
        TextProcessingResult result = processor.ProcessTemplate(template, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("Dear Alice Smith", result.ProcessedText);
        Assert.Contains("order #12345", result.ProcessedText);
        Assert.Contains("As a VIP customer", result.ProcessedText);
        Assert.DoesNotContain("3-5 business days", result.ProcessedText);
        Assert.Contains("- Widget: $29.99", result.ProcessedText);
        Assert.Contains("- Gadget: $49.99", result.ProcessedText);
        Assert.Contains("Total: $79.98", result.ProcessedText);
        Assert.Equal(7, result.ReplacementCount);
    }

    [Fact]
    public void ProcessTemplate_NotificationTemplate_WithConditionals()
    {
        // Arrange
        var processor = new TextTemplateProcessor();
        string template = @"Hi {{Name}},

{{#if UnreadCount > 0}}
You have {{UnreadCount}} unread messages.
{{/if}}

{{#if TasksDue}}
You have tasks due today:
{{#foreach Tasks}}
- {{Title}} (due {{DueDate}})
{{/foreach}}
{{/if}}";

        var data = new Dictionary<string, object>
        {
            ["Name"] = "Bob",
            ["UnreadCount"] = 5,
            ["TasksDue"] = true,
            ["Tasks"] = new[]
            {
                new { Title = "Review PR", DueDate = "Today" },
                new { Title = "Update docs", DueDate = "Today" }
            }
        };

        // Act
        TextProcessingResult result = processor.ProcessTemplate(template, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("Hi Bob", result.ProcessedText);
        Assert.Contains("You have 5 unread messages", result.ProcessedText);
        Assert.Contains("- Review PR (due Today)", result.ProcessedText);
        Assert.Contains("- Update docs (due Today)", result.ProcessedText);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void ProcessTemplate_UnmatchedIfTag_ThrowsException()
    {
        // Arrange
        var processor = new TextTemplateProcessor();
        string template = "{{#if IsActive}}Active";
        var data = new Dictionary<string, object> { ["IsActive"] = true };

        // Act
        TextProcessingResult result = processor.ProcessTemplate(template, data);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Unmatched", result.ErrorMessage);
    }

    [Fact]
    public void ProcessTemplate_UnmatchedForeachTag_ThrowsException()
    {
        // Arrange
        var processor = new TextTemplateProcessor();
        string template = "{{#foreach Items}}Item";
        var data = new Dictionary<string, object> { ["Items"] = new[] { 1, 2 } };

        // Act
        TextProcessingResult result = processor.ProcessTemplate(template, data);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Unmatched", result.ErrorMessage);
    }

    [Fact]
    public void ProcessTemplate_NullTemplateText_ThrowsArgumentNullException()
    {
        // Arrange
        var processor = new TextTemplateProcessor();
        var data = new Dictionary<string, object>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            processor.ProcessTemplate(null!, data));
    }

    [Fact]
    public void ProcessTemplate_NullData_ThrowsArgumentNullException()
    {
        // Arrange
        var processor = new TextTemplateProcessor();
        string template = "Hello";

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            processor.ProcessTemplate(template, null!));
    }

    #endregion
}
