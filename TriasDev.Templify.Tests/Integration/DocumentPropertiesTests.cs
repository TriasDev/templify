// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Integration tests for document metadata properties (Author, Title, etc.).
/// </summary>
public sealed class DocumentPropertiesTests
{
    [Fact]
    public void ProcessTemplate_SetAuthorOnly_ChangesAuthorPreservesOthers()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{Name}}!");
        builder.SetAuthor("Original Author");
        builder.SetTitle("Original Title");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "World"
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            DocumentProperties = new DocumentProperties
            {
                Author = "New Author"
            }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal("New Author", verifier.GetDocumentAuthor());
        Assert.Equal("Original Title", verifier.GetDocumentTitle());
    }

    [Fact]
    public void ProcessTemplate_SetMultipleProperties_AllSetCorrectly()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Content");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>();

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            DocumentProperties = new DocumentProperties
            {
                Author = "Test Author",
                Title = "Test Title",
                Subject = "Test Subject",
                Description = "Test Description",
                Keywords = "test, keywords",
                Category = "Test Category",
                LastModifiedBy = "Test User"
            }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal("Test Author", verifier.GetDocumentAuthor());
        Assert.Equal("Test Title", verifier.GetDocumentTitle());
        Assert.Equal("Test Subject", verifier.GetDocumentSubject());
        Assert.Equal("Test Description", verifier.GetDocumentDescription());
        Assert.Equal("test, keywords", verifier.GetDocumentKeywords());
        Assert.Equal("Test Category", verifier.GetDocumentCategory());
        Assert.Equal("Test User", verifier.GetDocumentLastModifiedBy());
    }

    [Fact]
    public void ProcessTemplate_NoDocumentProperties_PreservesOriginalAuthor()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Content");
        builder.SetAuthor("Template Author");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>();

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal("Template Author", verifier.GetDocumentAuthor());
    }

    [Fact]
    public void ProcessTemplate_DocumentPropertiesWithAllNulls_PreservesOriginalValues()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Content");
        builder.SetAuthor("Template Author");
        builder.SetTitle("Template Title");
        builder.SetSubject("Template Subject");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>();

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            DocumentProperties = new DocumentProperties()
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal("Template Author", verifier.GetDocumentAuthor());
        Assert.Equal("Template Title", verifier.GetDocumentTitle());
        Assert.Equal("Template Subject", verifier.GetDocumentSubject());
    }

    [Fact]
    public void ProcessTemplate_EmptyStringAuthor_SetsToEmpty()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Content");
        builder.SetAuthor("Template Author");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>();

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            DocumentProperties = new DocumentProperties
            {
                Author = ""
            }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal("", verifier.GetDocumentAuthor());
    }

    [Fact]
    public void ProcessTemplate_DocumentPropertiesWithPlaceholderReplacement_BothWork()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{Name}}!");

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Name"] = "World"
        };

        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            DocumentProperties = new DocumentProperties
            {
                Author = "Generated By Templify",
                Title = "Generated Document"
            }
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.ReplacementCount);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal("Hello World!", verifier.GetParagraphText(0));
        Assert.Equal("Generated By Templify", verifier.GetDocumentAuthor());
        Assert.Equal("Generated Document", verifier.GetDocumentTitle());
    }
}
