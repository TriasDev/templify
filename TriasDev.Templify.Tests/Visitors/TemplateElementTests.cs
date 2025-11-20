// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.Visitors;

namespace TriasDev.Templify.Tests.Visitors;

/// <summary>
/// Unit tests for the TemplateElement domain model.
/// Tests the discriminated union pattern and factory methods.
/// </summary>
public sealed class TemplateElementTests
{
    [Fact]
    public void FromConditional_CreatesConditionalElement()
    {
        // Arrange
        ConditionalBlock block = CreateTestConditionalBlock();

        // Act
        TemplateElement element = TemplateElement.FromConditional(block);

        // Assert
        Assert.Equal(TemplateElementType.Conditional, element.Type);
        Assert.Same(block, element.Conditional);
        Assert.Null(element.Loop);
        Assert.Null(element.Placeholder);
        Assert.Null(element.Paragraph);
        Assert.Null(element.Element);
    }

    [Fact]
    public void FromConditional_NullBlock_ThrowsArgumentNullException()
    {
        // Act & Assert
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            TemplateElement.FromConditional(null!));

        Assert.Equal("conditional", exception.ParamName);
    }

    [Fact]
    public void FromLoop_CreatesLoopElement()
    {
        // Arrange
        LoopBlock block = CreateTestLoopBlock();

        // Act
        TemplateElement element = TemplateElement.FromLoop(block);

        // Assert
        Assert.Equal(TemplateElementType.Loop, element.Type);
        Assert.Same(block, element.Loop);
        Assert.Null(element.Conditional);
        Assert.Null(element.Placeholder);
        Assert.Null(element.Paragraph);
        Assert.Null(element.Element);
    }

    [Fact]
    public void FromLoop_NullBlock_ThrowsArgumentNullException()
    {
        // Act & Assert
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            TemplateElement.FromLoop(null!));

        Assert.Equal("loop", exception.ParamName);
    }

    [Fact]
    public void FromPlaceholder_CreatesPlaceholderElement()
    {
        // Arrange
        PlaceholderMatch placeholder = new PlaceholderMatch
        {
            VariableName = "Name",
            FullMatch = "{{Name}}",
            StartIndex = 0,
            Length = 8
        };
        Paragraph paragraph = new Paragraph(new Run(new Text("{{Name}}")));

        // Act
        TemplateElement element = TemplateElement.FromPlaceholder(placeholder, paragraph);

        // Assert
        Assert.Equal(TemplateElementType.Placeholder, element.Type);
        Assert.Equal(placeholder, element.Placeholder);
        Assert.Same(paragraph, element.Paragraph);
        Assert.Null(element.Conditional);
        Assert.Null(element.Loop);
        Assert.Null(element.Element);
    }

    [Fact]
    public void FromPlaceholder_NullParagraph_ThrowsArgumentNullException()
    {
        // Arrange
        PlaceholderMatch placeholder = new PlaceholderMatch
        {
            VariableName = "Name",
            FullMatch = "{{Name}}",
            StartIndex = 0,
            Length = 8
        };

        // Act & Assert
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            TemplateElement.FromPlaceholder(placeholder, null!));

        Assert.Equal("paragraph", exception.ParamName);
    }

    [Fact]
    public void FromParagraph_CreatesParagraphElement()
    {
        // Arrange
        Paragraph paragraph = new Paragraph(new Run(new Text("Hello World")));

        // Act
        TemplateElement element = TemplateElement.FromParagraph(paragraph);

        // Assert
        Assert.Equal(TemplateElementType.Paragraph, element.Type);
        Assert.Same(paragraph, element.Paragraph);
        Assert.Same(paragraph, element.Element);
        Assert.Null(element.Conditional);
        Assert.Null(element.Loop);
        Assert.Null(element.Placeholder);
    }

    [Fact]
    public void FromParagraph_NullParagraph_ThrowsArgumentNullException()
    {
        // Act & Assert
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            TemplateElement.FromParagraph(null!));

        Assert.Equal("paragraph", exception.ParamName);
    }

    [Fact]
    public void Unknown_CreatesUnknownElement()
    {
        // Arrange
        Table table = new Table();

        // Act
        TemplateElement element = TemplateElement.Unknown(table);

        // Assert
        Assert.Equal(TemplateElementType.Unknown, element.Type);
        Assert.Same(table, element.Element);
        Assert.Null(element.Conditional);
        Assert.Null(element.Loop);
        Assert.Null(element.Placeholder);
        Assert.Null(element.Paragraph);
    }

    [Fact]
    public void Unknown_NullElement_ThrowsArgumentNullException()
    {
        // Act & Assert
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            TemplateElement.Unknown(null!));

        Assert.Equal("element", exception.ParamName);
    }

    // Helper methods to create test data

    private static ConditionalBlock CreateTestConditionalBlock()
    {
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#if IsActive}}")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/if}}")));
        List<DocumentFormat.OpenXml.OpenXmlElement> ifContent = new List<DocumentFormat.OpenXml.OpenXmlElement>
        {
            new Paragraph(new Run(new Text("Active")))
        };

        return new ConditionalBlock(
            conditionExpression: "IsActive",
            ifContentElements: ifContent,
            elseContentElements: new List<DocumentFormat.OpenXml.OpenXmlElement>(),
            startMarker: startMarker,
            elseMarker: null,
            endMarker: endMarker,
            isTableRowConditional: false,
            nestingLevel: 0);
    }

    private static LoopBlock CreateTestLoopBlock()
    {
        Paragraph startMarker = new Paragraph(new Run(new Text("{{#foreach Items}}")));
        Paragraph endMarker = new Paragraph(new Run(new Text("{{/foreach}}")));
        List<DocumentFormat.OpenXml.OpenXmlElement> content = new List<DocumentFormat.OpenXml.OpenXmlElement>
        {
            new Paragraph(new Run(new Text("{{.}}")))
        };

        return new LoopBlock(
            collectionName: "Items",
            contentElements: content,
            startMarker: startMarker,
            endMarker: endMarker,
            isTableRowLoop: false,
            emptyBlock: null);
    }
}
