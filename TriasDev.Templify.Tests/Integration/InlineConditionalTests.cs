// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Integration tests for inline conditional blocks ({{#if}} and {{/if}} in the same paragraph).
/// These test scenarios where conditionals are within a single paragraph alongside other content.
/// </summary>
public sealed class InlineConditionalTests
{
    [Fact]
    public void ProcessTemplate_InlineConditional_ContentBeforeIf_ConditionFalse_PreservesContentBefore()
    {
        // Arrange: Paragraph with content BEFORE the inline conditional
        // Template: "{{Street1}}{{#if Street2}} {{Street2}}{{/if}}"
        // When Street2 is null, should output just Street1
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(
            ("{{Street1}}", null),
            ("{{#if Street2}}", null),
            (" ", null),
            ("{{Street2}}", null),
            ("{{/if}}", null)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Street1"] = "Main Street 123",
            // Street2 is missing/null - condition should be false
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        // The content before {{#if}} should be preserved
        Assert.Equal("Main Street 123", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_InlineConditional_ContentAfterIf_ConditionFalse_PreservesContentAfter()
    {
        // Arrange: Paragraph with content AFTER the inline conditional
        // Template: "{{#if OptionalPrefix}}{{OptionalPrefix}} {{/if}}{{Name}}"
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(
            ("{{#if OptionalPrefix}}", null),
            ("{{OptionalPrefix}} ", null),
            ("{{/if}}", null),
            ("{{Name}}", null)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            // OptionalPrefix is missing - condition should be false
            ["Name"] = "John Doe"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        // The content after {{/if}} should be preserved
        Assert.Equal("John Doe", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_InlineConditional_ContentBeforeAndAfter_ConditionFalse_PreservesBoth()
    {
        // Arrange: Paragraph with content BEFORE and AFTER the inline conditional
        // Template: "Name: {{#if Title}}{{Title}} {{/if}}{{FirstName}} {{LastName}}"
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(
            ("Name: ", null),
            ("{{#if Title}}", null),
            ("{{Title}} ", null),
            ("{{/if}}", null),
            ("{{FirstName}} {{LastName}}", null)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            // Title is missing - condition should be false
            ["FirstName"] = "John",
            ["LastName"] = "Doe"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        // Both content before and after the conditional should be preserved
        Assert.Equal("Name: John Doe", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_InlineConditional_ContentBeforeAndAfter_ConditionTrue_ShowsAll()
    {
        // Arrange: Same as above but with condition true
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(
            ("Name: ", null),
            ("{{#if Title}}", null),
            ("{{Title}} ", null),
            ("{{/if}}", null),
            ("{{FirstName}} {{LastName}}", null)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Title"] = "Dr.",
            ["FirstName"] = "John",
            ["LastName"] = "Doe"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        // All content including the conditional content should be shown
        Assert.Equal("Name: Dr. John Doe", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_InlineConditional_MultipleInSameParagraph_MixedConditions()
    {
        // Arrange: Two separate paragraphs, each with one inline conditional
        // This tests that multiple inline conditionals work (just in separate paragraphs)
        // Note: Multiple inline conditionals in the SAME paragraph is a more complex scenario
        // that would require multiple detection passes
        DocumentBuilder builder = new DocumentBuilder();

        // First paragraph: prefix conditional
        builder.AddParagraphWithRuns(
            ("{{#if Prefix}}", null),
            ("{{Prefix}} ", null),
            ("{{/if}}", null),
            ("{{Name}}", null)
        );

        // Second paragraph: suffix conditional
        builder.AddParagraphWithRuns(
            ("Title: ", null),
            ("{{Name}}", null),
            ("{{#if Suffix}}", null),
            (", {{Suffix}}", null),
            ("{{/if}}", null)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            // Prefix is missing
            ["Name"] = "John",
            ["Suffix"] = "PhD"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(2, verifier.GetParagraphCount());
        // First paragraph: prefix condition false, so just Name
        Assert.Equal("John", verifier.GetParagraphText(0));
        // Second paragraph: suffix condition true, so Title + Name + Suffix
        Assert.Equal("Title: John, PhD", verifier.GetParagraphText(1));
    }

    [Fact]
    public void ProcessTemplate_InlineConditional_WithElse_ConditionFalse_ShowsElseBranch()
    {
        // Arrange: Inline conditional with else branch
        // Template: "Status: {{#if IsActive}}Active{{else}}Inactive{{/if}}"
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(
            ("Status: ", null),
            ("{{#if IsActive}}", null),
            ("Active", null),
            ("{{else}}", null),
            ("Inactive", null),
            ("{{/if}}", null)
        );

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
    public void ProcessTemplate_InlineConditional_WithElse_ConditionTrue_ShowsIfBranch()
    {
        // Arrange: Same as above but condition true
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(
            ("Status: ", null),
            ("{{#if IsActive}}", null),
            ("Active", null),
            ("{{else}}", null),
            ("Inactive", null),
            ("{{/if}}", null)
        );

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
    public void ProcessTemplate_InlineConditional_NestedPath_ConditionFalse_PreservesOther()
    {
        // Arrange: Real-world scenario - address with optional street2
        // Template: "{{Address.Street1}}{{#if Address.Street2}}\n{{Address.Street2}}{{/if}}"
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(
            ("{{Address.Street1}}", null),
            ("{{#if Address.Street2}}", null),
            (", {{Address.Street2}}", null),
            ("{{/if}}", null)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Address"] = new Dictionary<string, object>
            {
                ["Street1"] = "123 Main St"
                // Street2 is missing
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
        Assert.Equal("123 Main St", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_InlineConditional_EmptyConditionContent_ConditionFalse_PreservesOther()
    {
        // Arrange: Edge case - conditional with no content, just markers
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(
            ("Before", null),
            ("{{#if Missing}}", null),
            ("{{/if}}", null),
            ("After", null)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>();

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("BeforeAfter", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_InlineConditional_WithComparison_ConditionFalse_PreservesOther()
    {
        // Arrange: Inline conditional with comparison operator
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(
            ("Total: ", null),
            ("{{Amount}}", null),
            ("{{#if Amount > 100}}", null),
            (" (Large order)", null),
            ("{{/if}}", null)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Amount"] = 50
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Total: 50", verifier.GetParagraphText(0));
    }

    #region Multiple Inline Conditionals in Same Paragraph

    [Fact]
    public void ProcessTemplate_MultipleInlineConditionals_SameParagraph_BothTrue()
    {
        // Arrange: Two inline conditionals in same paragraph, both conditions true
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(
            ("{{#if Prefix}}", null),
            ("{{Prefix}} ", null),
            ("{{/if}}", null),
            ("{{Name}}", null),
            ("{{#if Suffix}}", null),
            (", {{Suffix}}", null),
            ("{{/if}}", null)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Prefix"] = "Dr.",
            ["Name"] = "John",
            ["Suffix"] = "PhD"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Dr. John, PhD", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_MultipleInlineConditionals_SameParagraph_BothFalse()
    {
        // Arrange: Two inline conditionals in same paragraph, both conditions false
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(
            ("{{#if Prefix}}", null),
            ("{{Prefix}} ", null),
            ("{{/if}}", null),
            ("{{Name}}", null),
            ("{{#if Suffix}}", null),
            (", {{Suffix}}", null),
            ("{{/if}}", null)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            // Prefix missing
            ["Name"] = "John"
            // Suffix missing
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("John", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_MultipleInlineConditionals_SameParagraph_FirstFalseSecondTrue()
    {
        // Arrange: Two inline conditionals, first false, second true
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(
            ("{{#if Prefix}}", null),
            ("{{Prefix}} ", null),
            ("{{/if}}", null),
            ("{{Name}}", null),
            ("{{#if Suffix}}", null),
            (", {{Suffix}}", null),
            ("{{/if}}", null)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            // Prefix missing - first condition false
            ["Name"] = "John",
            ["Suffix"] = "PhD" // second condition true
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("John, PhD", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_MultipleInlineConditionals_SameParagraph_FirstTrueSecondFalse()
    {
        // Arrange: Two inline conditionals, first true, second false
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(
            ("{{#if Prefix}}", null),
            ("{{Prefix}} ", null),
            ("{{/if}}", null),
            ("{{Name}}", null),
            ("{{#if Suffix}}", null),
            (", {{Suffix}}", null),
            ("{{/if}}", null)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Prefix"] = "Dr.", // first condition true
            ["Name"] = "John"
            // Suffix missing - second condition false
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Dr. John", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_ThreeInlineConditionals_SameParagraph_MixedConditions()
    {
        // Arrange: Three inline conditionals in same paragraph
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(
            ("{{#if Greeting}}", null),
            ("{{Greeting}} ", null),
            ("{{/if}}", null),
            ("{{#if Title}}", null),
            ("{{Title}} ", null),
            ("{{/if}}", null),
            ("{{Name}}", null),
            ("{{#if Suffix}}", null),
            (", {{Suffix}}", null),
            ("{{/if}}", null)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Greeting"] = "Hello", // true
            // Title missing - false
            ["Name"] = "John",
            ["Suffix"] = "Esq." // true
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Hello John, Esq.", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_MultipleInlineConditionals_WithElse_SameParagraph()
    {
        // Arrange: Two inline conditionals with else branches
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(
            ("{{#if IsFormal}}", null),
            ("Dear ", null),
            ("{{else}}", null),
            ("Hi ", null),
            ("{{/if}}", null),
            ("{{Name}}", null),
            ("{{#if HasTitle}}", null),
            (" ({{Title}})", null),
            ("{{else}}", null),
            (" (no title)", null),
            ("{{/if}}", null)
        );

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["IsFormal"] = false, // should show "Hi "
            ["Name"] = "Alice",
            ["HasTitle"] = true,
            ["Title"] = "Manager"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Hi Alice (Manager)", verifier.GetParagraphText(0));
    }

    #endregion
}
