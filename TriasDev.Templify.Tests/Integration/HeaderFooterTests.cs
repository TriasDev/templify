// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Integration tests for header and footer template processing.
/// </summary>
public sealed class HeaderFooterTests
{
    [Fact]
    public void ProcessTemplate_PlaceholderInHeader_ReplacesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddHeader("Company: {{CompanyName}}");
        builder.AddParagraph("Body content");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["CompanyName"] = "Acme Corp"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal("Company: Acme Corp", verifier.GetHeaderText());
    }

    [Fact]
    public void ProcessTemplate_PlaceholderInFooter_ReplacesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddFooter("Page {{PageInfo}}");
        builder.AddParagraph("Body content");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["PageInfo"] = "1 of 5"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal("Page 1 of 5", verifier.GetFooterText());
    }

    [Fact]
    public void ProcessTemplate_PlaceholdersInHeaderAndFooterAndBody_ReplacesAll()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddHeader("Header: {{Title}}");
        builder.AddFooter("Footer: {{Author}}");
        builder.AddParagraph("Body: {{Content}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Title"] = "My Document",
            ["Author"] = "Jane Doe",
            ["Content"] = "Hello World"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal("Header: My Document", verifier.GetHeaderText());
        Assert.Equal("Footer: Jane Doe", verifier.GetFooterText());
        Assert.Equal("Body: Hello World", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_ConditionalInHeader_EvaluatesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddHeaderWithParagraphs(
            HeaderFooterValues.Default,
            "{{#if IsDraft}}DRAFT{{/if}}",
            "{{CompanyName}}");
        builder.AddParagraph("Body content");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["IsDraft"] = true,
            ["CompanyName"] = "Acme Corp"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string headerText = verifier.GetHeaderText();
        Assert.Contains("DRAFT", headerText);
        Assert.Contains("Acme Corp", headerText);
    }

    [Fact]
    public void ProcessTemplate_ConditionalInHeader_FalseCondition_RemovesBranch()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddHeaderWithParagraphs(
            HeaderFooterValues.Default,
            "{{#if IsDraft}}DRAFT{{/if}}",
            "{{CompanyName}}");
        builder.AddParagraph("Body content");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["IsDraft"] = false,
            ["CompanyName"] = "Acme Corp"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string headerText = verifier.GetHeaderText();
        Assert.DoesNotContain("DRAFT", headerText);
        Assert.Contains("Acme Corp", headerText);
    }

    [Fact]
    public void ProcessTemplate_LoopInFooter_ExpandsCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddFooterWithParagraphs(
            HeaderFooterValues.Default,
            "{{#foreach Items}}",
            "- {{Name}}",
            "{{/foreach}}");
        builder.AddParagraph("Body content");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { ["Name"] = "Item A" },
                new Dictionary<string, object> { ["Name"] = "Item B" }
            }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string footerText = verifier.GetFooterText();
        Assert.Contains("Item A", footerText);
        Assert.Contains("Item B", footerText);
    }

    [Fact]
    public void ProcessTemplate_MultipleHeaderTypes_ReplacesAll()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddHeader("Default: {{Title}}", HeaderFooterValues.Default);
        builder.AddHeader("First: {{Title}}", HeaderFooterValues.First);
        builder.AddParagraph("Body content");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Title"] = "My Report"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal("Default: My Report", verifier.GetHeaderText(HeaderFooterValues.Default));
        Assert.Equal("First: My Report", verifier.GetHeaderText(HeaderFooterValues.First));
    }

    [Fact]
    public void ProcessTemplate_FormattingPreservedInHeader_MaintainsFormatting()
    {
        // Arrange
        RunProperties boldFormatting = DocumentBuilder.CreateFormatting(bold: true, color: "FF0000");

        DocumentBuilder builder = new DocumentBuilder();
        builder.AddHeader("Company: {{CompanyName}}", boldFormatting);
        builder.AddParagraph("Body content");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["CompanyName"] = "Acme Corp"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal("Company: Acme Corp", verifier.GetHeaderText());

        RunProperties? headerProps = verifier.GetHeaderRunProperties();
        DocumentVerifier.VerifyFormatting(headerProps, expectedBold: true, expectedColor: "FF0000");
    }

    [Fact]
    public void ProcessTemplate_MultiplePlaceholdersInHeader_ReplacesAll()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddHeader("{{CompanyName}} - {{Department}}");
        builder.AddParagraph("Body content");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["CompanyName"] = "Acme Corp",
            ["Department"] = "Engineering"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal("Acme Corp - Engineering", verifier.GetHeaderText());
    }

    [Fact]
    public void ValidateTemplate_PlaceholderInHeader_DetectedInValidation()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddHeader("Company: {{CompanyName}}");
        builder.AddParagraph("Body: {{Content}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Content"] = "Hello"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();

        // Act
        ValidationResult result = processor.ValidateTemplate(templateStream, data);

        // Assert
        Assert.Contains("CompanyName", result.AllPlaceholders);
        Assert.Contains("Content", result.AllPlaceholders);
        Assert.Contains("CompanyName", result.MissingVariables);
    }

    [Fact]
    public void ProcessTemplate_EvenPageFooter_ReplacesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddFooter("Default footer: {{Info}}", HeaderFooterValues.Default);
        builder.AddFooter("Even footer: {{Info}}", HeaderFooterValues.Even);
        builder.AddParagraph("Body content");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Info"] = "Confidential"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal("Default footer: Confidential", verifier.GetFooterText(HeaderFooterValues.Default));
        Assert.Equal("Even footer: Confidential", verifier.GetFooterText(HeaderFooterValues.Even));
    }

    [Fact]
    public void ProcessTemplate_NestedPropertyInHeader_ReplacesCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddHeader("Contact: {{Customer.Name}}");
        builder.AddParagraph("Body content");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Customer"] = new Dictionary<string, object>
            {
                ["Name"] = "Alice Smith"
            }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal("Contact: Alice Smith", verifier.GetHeaderText());
    }

    [Fact]
    public void ProcessTemplate_FormattingPreservedInFooter_MaintainsFormatting()
    {
        // Arrange
        RunProperties boldRedFormatting = DocumentBuilder.CreateFormatting(bold: true, color: "0000FF");

        DocumentBuilder builder = new DocumentBuilder();
        builder.AddFooter("Footer: {{Info}}", boldRedFormatting);
        builder.AddParagraph("Body content");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Info"] = "Confidential"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal("Footer: Confidential", verifier.GetFooterText());

        RunProperties? footerProps = verifier.GetFooterRunProperties();
        DocumentVerifier.VerifyFormatting(footerProps, expectedBold: true, expectedColor: "0000FF");
    }

    [Fact]
    public void ProcessTemplate_HeaderOnlyNoFooter_ProcessesSuccessfully()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddHeader("Header: {{Title}}");
        builder.AddParagraph("Body: {{Content}}");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Title"] = "Report",
            ["Content"] = "Hello"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal("Header: Report", verifier.GetHeaderText());
        Assert.Equal("Body: Hello", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_MarkdownInHeader_AppliesFormatting()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddHeader("{{Title}}");
        builder.AddParagraph("Body content");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Title"] = "This is **bold** text"
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        string headerText = verifier.GetHeaderText();
        Assert.Equal("This is bold text", headerText);
    }
}
