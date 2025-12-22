// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Integration tests for elseif conditional block functionality.
/// Tests the {{#if}}...{{#elseif}}...{{else}}...{{/if}} syntax.
/// </summary>
public sealed class ElseIfTests
{
    [Fact]
    public void ProcessTemplate_ElseIf_FirstConditionTrue_KeepsFirstBranch()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Status = Active}}");
        builder.AddParagraph("Status is Active");
        builder.AddParagraph("{{#elseif Status = Pending}}");
        builder.AddParagraph("Status is Pending");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("Unknown status");
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
        Assert.Equal("Status is Active", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_ElseIf_SecondConditionTrue_KeepsElseIfBranch()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Status = Active}}");
        builder.AddParagraph("Status is Active");
        builder.AddParagraph("{{#elseif Status = Pending}}");
        builder.AddParagraph("Status is Pending");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("Unknown status");
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
        Assert.Equal("Status is Pending", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_ElseIf_NoConditionMatches_KeepsElseBranch()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Status = Active}}");
        builder.AddParagraph("Status is Active");
        builder.AddParagraph("{{#elseif Status = Pending}}");
        builder.AddParagraph("Status is Pending");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("Unknown status");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Status"] = "Archived"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Unknown status", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_ElseIf_NoConditionMatchesNoElse_RemovesAllContent()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Before");
        builder.AddParagraph("{{#if Status = Active}}");
        builder.AddParagraph("Status is Active");
        builder.AddParagraph("{{#elseif Status = Pending}}");
        builder.AddParagraph("Status is Pending");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("After");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Status"] = "Archived"
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
    public void ProcessTemplate_MultipleElseIfBranches_MatchesSecondElseIf()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Score >= 90}}");
        builder.AddParagraph("Grade: A");
        builder.AddParagraph("{{#elseif Score >= 80}}");
        builder.AddParagraph("Grade: B");
        builder.AddParagraph("{{#elseif Score >= 70}}");
        builder.AddParagraph("Grade: C");
        builder.AddParagraph("{{#elseif Score >= 60}}");
        builder.AddParagraph("Grade: D");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("Grade: F");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Score"] = 75
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Grade: C", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_ElseIfWithBooleanConditions()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if IsAdmin}}");
        builder.AddParagraph("Admin user");
        builder.AddParagraph("{{#elseif IsModerator}}");
        builder.AddParagraph("Moderator user");
        builder.AddParagraph("{{#elseif IsVerified}}");
        builder.AddParagraph("Verified user");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("Guest user");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["IsAdmin"] = false,
            ["IsModerator"] = false,
            ["IsVerified"] = true
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Verified user", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_ElseIfWithPlaceholders_ReplacesInMatchingBranch()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Type = Premium}}");
        builder.AddParagraph("Welcome, Premium {{Name}}!");
        builder.AddParagraph("{{#elseif Type = Standard}}");
        builder.AddParagraph("Welcome, {{Name}}!");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("Welcome, Guest!");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Type"] = "Standard",
            ["Name"] = "Alice"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Welcome, Alice!", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_NestedElseIf_ProcessesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if HasSubscription}}");
        builder.AddParagraph("{{#if Plan = Pro}}");
        builder.AddParagraph("Pro plan");
        builder.AddParagraph("{{#elseif Plan = Basic}}");
        builder.AddParagraph("Basic plan");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("Unknown plan");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("No subscription");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["HasSubscription"] = true,
            ["Plan"] = "Basic"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Basic plan", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_ElseIfInsideLoop_ProcessesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddParagraph("{{#if Status = Active}}");
        builder.AddParagraph("{{Name}} - Active");
        builder.AddParagraph("{{#elseif Status = Pending}}");
        builder.AddParagraph("{{Name}} - Pending");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("{{Name}} - Inactive");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<object>
            {
                new { Name = "Item1", Status = "Active" },
                new { Name = "Item2", Status = "Pending" },
                new { Name = "Item3", Status = "Archived" }
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
        Assert.Equal("Item1 - Active", verifier.GetParagraphText(0));
        Assert.Equal("Item2 - Pending", verifier.GetParagraphText(1));
        Assert.Equal("Item3 - Inactive", verifier.GetParagraphText(2));
    }

    [Fact]
    public void ProcessTemplate_ElseIfWithAndOperator()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if IsVIP and HasDiscount}}");
        builder.AddParagraph("VIP with discount");
        builder.AddParagraph("{{#elseif IsVIP}}");
        builder.AddParagraph("VIP without discount");
        builder.AddParagraph("{{#elseif HasDiscount}}");
        builder.AddParagraph("Regular with discount");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("Regular customer");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["IsVIP"] = false,
            ["HasDiscount"] = true
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Regular with discount", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_ElseIfCaseInsensitive()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#IF Condition1}}");
        builder.AddParagraph("First");
        builder.AddParagraph("{{#ELSEIF Condition2}}");
        builder.AddParagraph("Second");
        builder.AddParagraph("{{ELSE}}");
        builder.AddParagraph("Default");
        builder.AddParagraph("{{/IF}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Condition1"] = false,
            ["Condition2"] = true
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Second", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_ElseIfWithQuotedStrings()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Category = \"Home & Garden\"}}");
        builder.AddParagraph("Home category");
        builder.AddParagraph("{{#elseif Category = \"Sports & Outdoors\"}}");
        builder.AddParagraph("Sports category");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("Other category");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Category"] = "Sports & Outdoors"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Sports category", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_ElseAfterElseIf_ThrowsException()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Condition1}}");
        builder.AddParagraph("First");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("Default");
        builder.AddParagraph("{{#elseif Condition2}}");  // Invalid: elseif after else
        builder.AddParagraph("Second");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Condition1"] = false,
            ["Condition2"] = true
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act & Assert
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => processor.ProcessTemplate(templateStream, outputStream, data));

        Assert.Contains("elseif", exception.Message.ToLower());
        Assert.Contains("else", exception.Message.ToLower());
    }

    [Fact]
    public void ProcessTemplate_ElseIfWithNestedProperties()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Customer.Type = VIP}}");
        builder.AddParagraph("VIP Customer");
        builder.AddParagraph("{{#elseif Customer.Type = Premium}}");
        builder.AddParagraph("Premium Customer");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("Standard Customer");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Customer"] = new { Type = "Premium", Name = "Alice" }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Premium Customer", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_ElseIfWithMultipleContentParagraphs()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Level = High}}");
        builder.AddParagraph("High priority:");
        builder.AddParagraph("This needs immediate attention");
        builder.AddParagraph("{{#elseif Level = Medium}}");
        builder.AddParagraph("Medium priority:");
        builder.AddParagraph("Please review when possible");
        builder.AddParagraph("{{else}}");
        builder.AddParagraph("Low priority:");
        builder.AddParagraph("No rush needed");
        builder.AddParagraph("{{/if}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Level"] = "Medium"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(2, verifier.GetParagraphCount());
        Assert.Equal("Medium priority:", verifier.GetParagraphText(0));
        Assert.Equal("Please review when possible", verifier.GetParagraphText(1));
    }

    #region Inline ElseIf Tests

    [Fact]
    public void ProcessTemplate_InlineElseIf_FirstConditionTrue()
    {
        // Arrange: Inline conditional with elseif
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(
            ("Status: ", null),
            ("{{#if Status = Active}}", null),
            ("Active", null),
            ("{{#elseif Status = Pending}}", null),
            ("Pending", null),
            ("{{else}}", null),
            ("Inactive", null),
            ("{{/if}}", null)
        );

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
        Assert.Equal("Status: Active", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_InlineElseIf_ElseIfConditionTrue()
    {
        // Arrange: Inline conditional with elseif, elseif branch matches
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(
            ("Status: ", null),
            ("{{#if Status = Active}}", null),
            ("Active", null),
            ("{{#elseif Status = Pending}}", null),
            ("Pending", null),
            ("{{else}}", null),
            ("Inactive", null),
            ("{{/if}}", null)
        );

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
        Assert.Equal("Status: Pending", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_InlineElseIf_ElseConditionMatches()
    {
        // Arrange: Inline conditional with elseif, else branch matches
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(
            ("Status: ", null),
            ("{{#if Status = Active}}", null),
            ("Active", null),
            ("{{#elseif Status = Pending}}", null),
            ("Pending", null),
            ("{{else}}", null),
            ("Inactive", null),
            ("{{/if}}", null)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Status"] = "Archived"
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
    public void ProcessTemplate_InlineElseIf_MultipleElseIfBranches()
    {
        // Arrange: Inline conditional with multiple elseif branches
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(
            ("Grade: ", null),
            ("{{#if Score >= 90}}", null),
            ("A", null),
            ("{{#elseif Score >= 80}}", null),
            ("B", null),
            ("{{#elseif Score >= 70}}", null),
            ("C", null),
            ("{{else}}", null),
            ("F", null),
            ("{{/if}}", null)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Score"] = 75
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Grade: C", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_InlineElseIf_NestedWithinInline()
    {
        // Arrange: Nested inline elseif conditionals
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(
            ("{{#if Level1}}", null),
            ("L1 ", null),
            ("{{#if Level2 = A}}", null),
            ("A", null),
            ("{{#elseif Level2 = B}}", null),
            ("B", null),
            ("{{else}}", null),
            ("C", null),
            ("{{/if}}", null),
            ("{{/if}}", null)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Level1"] = true,
            ["Level2"] = "B"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("L1 B", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_InlineElseIf_WithPlaceholders()
    {
        // Arrange: Inline elseif with placeholders in branches
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(
            ("Welcome, ", null),
            ("{{#if Role = Admin}}", null),
            ("Administrator {{Name}}", null),
            ("{{#elseif Role = User}}", null),
            ("User {{Name}}", null),
            ("{{else}}", null),
            ("Guest", null),
            ("{{/if}}", null),
            ("!", null)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Role"] = "User",
            ["Name"] = "Alice"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Welcome, User Alice!", verifier.GetParagraphText(0));
    }

    #endregion
}
