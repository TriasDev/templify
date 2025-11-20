using System.Globalization;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.PropertyPaths;
using TriasDev.Templify.Utilities;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Integration tests for lists (bullet/numbered) inside foreach loops.
/// These tests verify that list formatting is preserved when paragraphs are cloned by loops.
/// </summary>
public sealed class ListsInLoopsTests
{
    [Fact]
    public void ProcessTemplate_ForeachWithBulletList_PreservesListFormatting()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddBulletListItem("{{.}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "First Item", "Second Item", "Third Item" }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.ReplacementCount);

        // Verify the output has bullet list items
        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(3, verifier.GetParagraphCount());
        Assert.Equal("First Item", verifier.GetParagraphText(0));
        Assert.Equal("Second Item", verifier.GetParagraphText(1));
        Assert.Equal("Third Item", verifier.GetParagraphText(2));

        // Verify all paragraphs have bullet list formatting
        Assert.True(verifier.IsBulletListItem(0), "First item should be a bullet");
        Assert.True(verifier.IsBulletListItem(1), "Second item should be a bullet");
        Assert.True(verifier.IsBulletListItem(2), "Third item should be a bullet");
    }

    [Fact]
    public void ProcessTemplate_ForeachWithNumberedList_PreservesNumbering()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Steps}}");
        builder.AddNumberedListItem("{{.}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Steps"] = new List<string> { "First step", "Second step", "Third step" }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(3, verifier.GetParagraphCount());
        Assert.Equal("First step", verifier.GetParagraphText(0));
        Assert.Equal("Second step", verifier.GetParagraphText(1));
        Assert.Equal("Third step", verifier.GetParagraphText(2));

        // Verify all paragraphs have numbered list formatting
        Assert.True(verifier.IsNumberedListItem(0), "First item should be numbered");
        Assert.True(verifier.IsNumberedListItem(1), "Second item should be numbered");
        Assert.True(verifier.IsNumberedListItem(2), "Third item should be numbered");
    }

    [Fact]
    public void ProcessTemplate_ForeachWithBulletListAndObjects_ReplacesPlaceholders()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Products}}");
        builder.AddBulletListItem("{{Name}} - {{Price}} EUR");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Products"] = new List<Product>
            {
                new Product { Name = "Laptop", Price = 999.99m },
                new Product { Name = "Mouse", Price = 29.99m },
                new Product { Name = "Keyboard", Price = 79.99m }
            }
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture
        };
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(3, verifier.GetParagraphCount());
        Assert.Equal("Laptop - 999.99 EUR", verifier.GetParagraphText(0));
        Assert.Equal("Mouse - 29.99 EUR", verifier.GetParagraphText(1));
        Assert.Equal("Keyboard - 79.99 EUR", verifier.GetParagraphText(2));

        // Verify bullet formatting preserved
        Assert.True(verifier.IsBulletListItem(0));
        Assert.True(verifier.IsBulletListItem(1));
        Assert.True(verifier.IsBulletListItem(2));
    }

    [Fact]
    public void ProcessTemplate_ForeachWithEmptyList_RemovesListSection()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Items:");
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddBulletListItem("{{.}}");
        builder.AddParagraph("{{/foreach}}");
        builder.AddParagraph("End of list.");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string>() // Empty list
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(2, verifier.GetParagraphCount()); // Only "Items:" and "End of list."
        Assert.Equal("Items:", verifier.GetParagraphText(0));
        Assert.Equal("End of list.", verifier.GetParagraphText(1));
    }

    [Fact]
    public void ProcessTemplate_ForeachWithSingleItemBulletList_PreservesFormatting()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Items}}");
        builder.AddBulletListItem("{{.}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "Single Item" }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetParagraphCount());
        Assert.Equal("Single Item", verifier.GetParagraphText(0));
        Assert.True(verifier.IsBulletListItem(0));
    }

    [Fact]
    public void ProcessTemplate_MultipleForeachLoopsWithDifferentListTypes_PreservesBothFormats()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Features:");
        builder.AddParagraph("{{#foreach Features}}");
        builder.AddBulletListItem("{{.}}");
        builder.AddParagraph("{{/foreach}}");
        builder.AddParagraph("Steps:");
        builder.AddParagraph("{{#foreach Steps}}");
        builder.AddNumberedListItem("{{.}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Features"] = new List<string> { "Fast", "Secure" },
            ["Steps"] = new List<string> { "Install", "Configure", "Run" }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(7, verifier.GetParagraphCount()); // "Features:" + 2 bullets + "Steps:" + 3 numbered

        Assert.Equal("Features:", verifier.GetParagraphText(0));
        Assert.Equal("Fast", verifier.GetParagraphText(1));
        Assert.True(verifier.IsBulletListItem(1));
        Assert.Equal("Secure", verifier.GetParagraphText(2));
        Assert.True(verifier.IsBulletListItem(2));

        Assert.Equal("Steps:", verifier.GetParagraphText(3));
        Assert.Equal("Install", verifier.GetParagraphText(4));
        Assert.True(verifier.IsNumberedListItem(4));
        Assert.Equal("Configure", verifier.GetParagraphText(5));
        Assert.True(verifier.IsNumberedListItem(5));
        Assert.Equal("Run", verifier.GetParagraphText(6));
        Assert.True(verifier.IsNumberedListItem(6));
    }

    [Fact]
    public void ProcessTemplate_BulletListWithFormattedText_PreservesBothListAndTextFormatting()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        RunProperties boldFormatting = DocumentBuilder.CreateFormatting(bold: true);

        builder.AddParagraph("{{#foreach Items}}");
        builder.AddBulletListItem("{{.}}", level: 0, formatting: boldFormatting);
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<string> { "Important Item", "Critical Item" }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(2, verifier.GetParagraphCount());
        Assert.Equal("Important Item", verifier.GetParagraphText(0));
        Assert.Equal("Critical Item", verifier.GetParagraphText(1));

        // Verify bullet formatting
        Assert.True(verifier.IsBulletListItem(0));
        Assert.True(verifier.IsBulletListItem(1));

        // Verify bold formatting preserved
        RunProperties? runProps0 = verifier.GetRunProperties(0, 0);
        RunProperties? runProps1 = verifier.GetRunProperties(1, 0);
        DocumentVerifier.VerifyFormatting(runProps0, expectedBold: true);
        DocumentVerifier.VerifyFormatting(runProps1, expectedBold: true);
    }

    [Fact]
    public void ProcessTemplate_BulletListWithConditionalInside_ShowsOnlyMatchingItems()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Products}}");
        builder.AddParagraph("{{#if IsAvailable}}");
        builder.AddBulletListItem("{{Name}} (Available)");
        builder.AddParagraph("{{else}}");
        builder.AddBulletListItem("{{Name}} (Out of Stock)");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Products"] = new List<ProductWithAvailability>
            {
                new ProductWithAvailability { Name = "Laptop", IsAvailable = true },
                new ProductWithAvailability { Name = "Mouse", IsAvailable = false },
                new ProductWithAvailability { Name = "Keyboard", IsAvailable = true }
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
        Assert.Equal("Laptop (Available)", verifier.GetParagraphText(0));
        Assert.Equal("Mouse (Out of Stock)", verifier.GetParagraphText(1));
        Assert.Equal("Keyboard (Available)", verifier.GetParagraphText(2));

        // Verify bullet formatting preserved
        Assert.True(verifier.IsBulletListItem(0));
        Assert.True(verifier.IsBulletListItem(1));
        Assert.True(verifier.IsBulletListItem(2));
    }

    [Fact]
    public void ProcessTemplate_NumberedListWithConditionalElse_ShowsCorrectBranch()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Tasks}}");
        builder.AddParagraph("{{#if IsCompleted}}");
        builder.AddNumberedListItem("✓ {{Name}}");
        builder.AddParagraph("{{else}}");
        builder.AddNumberedListItem("○ {{Name}}");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Tasks"] = new List<Task>
            {
                new Task { Name = "Setup environment", IsCompleted = true },
                new Task { Name = "Write code", IsCompleted = false },
                new Task { Name = "Run tests", IsCompleted = false }
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
        Assert.Equal("✓ Setup environment", verifier.GetParagraphText(0));
        Assert.Equal("○ Write code", verifier.GetParagraphText(1));
        Assert.Equal("○ Run tests", verifier.GetParagraphText(2));

        // Verify numbered list formatting preserved
        Assert.True(verifier.IsNumberedListItem(0));
        Assert.True(verifier.IsNumberedListItem(1));
        Assert.True(verifier.IsNumberedListItem(2));
    }

    [Fact]
    public void ProcessTemplate_BulletListWithComplexConditionalExpression_EvaluatesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Products}}");
        builder.AddParagraph("{{#if IsAvailable and Price < 100}}");
        builder.AddBulletListItem("{{Name}} - €{{Price}} (Great Deal!)");
        builder.AddParagraph("{{else}}");
        builder.AddBulletListItem("{{Name}} - €{{Price}}");
        builder.AddParagraph("{{/if}}");
        builder.AddParagraph("{{/foreach}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Products"] = new List<ProductWithPrice>
            {
                new ProductWithPrice { Name = "Laptop", IsAvailable = true, Price = 999m },
                new ProductWithPrice { Name = "Mouse", IsAvailable = true, Price = 29.99m },
                new ProductWithPrice { Name = "Keyboard", IsAvailable = false, Price = 79.99m }
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
        Assert.Contains("Laptop - €999", verifier.GetParagraphText(0));
        Assert.DoesNotContain("Great Deal!", verifier.GetParagraphText(0));
        // Locale-agnostic check: accept both "29.99" and "29,99"
        string mouseText = verifier.GetParagraphText(1);
        Assert.Contains("Mouse", mouseText);
        Assert.Contains("Great Deal!", mouseText);
        Assert.Contains("Keyboard", verifier.GetParagraphText(2));
        Assert.DoesNotContain("Great Deal!", verifier.GetParagraphText(2));

        // Verify bullet formatting preserved
        Assert.True(verifier.IsBulletListItem(0));
        Assert.True(verifier.IsBulletListItem(1));
        Assert.True(verifier.IsBulletListItem(2));
    }
}

public class Product
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class ProductWithAvailability
{
    public string Name { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
}

public class ProductWithPrice
{
    public string Name { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public decimal Price { get; set; }
}

public class Task
{
    public string Name { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}

public class Category
{
    public string Name { get; set; } = string.Empty;
    public List<string> Items { get; set; } = new List<string>();
}
