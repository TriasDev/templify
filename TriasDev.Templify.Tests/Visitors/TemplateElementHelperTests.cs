// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Visitors;

namespace TriasDev.Templify.Tests.Visitors;

/// <summary>
/// Unit tests for TemplateElementHelper shared utilities.
/// These utilities eliminate code duplication across detectors and processors.
/// </summary>
public sealed class TemplateElementHelperTests
{
    #region GetElementText Tests

    [Fact]
    public void GetElementText_Paragraph_ReturnsInnerText()
    {
        // Arrange
        Paragraph paragraph = new Paragraph(new Run(new Text("Hello {{Name}}")));

        // Act
        string? text = TemplateElementHelper.GetElementText(paragraph);

        // Assert
        Assert.Equal("Hello {{Name}}", text);
    }

    [Fact]
    public void GetElementText_ParagraphWithMultipleRuns_ReturnsConcatenatedText()
    {
        // Arrange
        Paragraph paragraph = new Paragraph(
            new Run(new Text("Hello ")),
            new Run(new Text("{{Name}}")),
            new Run(new Text(" !")));

        // Act
        string? text = TemplateElementHelper.GetElementText(paragraph);

        // Assert
        Assert.Equal("Hello {{Name}} !", text);
    }

    [Fact]
    public void GetElementText_EmptyParagraph_ReturnsEmptyString()
    {
        // Arrange
        Paragraph paragraph = new Paragraph();

        // Act
        string? text = TemplateElementHelper.GetElementText(paragraph);

        // Assert
        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void GetElementText_TableRow_ReturnsInnerText()
    {
        // Arrange
        TableRow row = new TableRow(
            new TableCell(new Paragraph(new Run(new Text("Cell 1")))),
            new TableCell(new Paragraph(new Run(new Text("Cell 2")))));

        // Act
        string? text = TemplateElementHelper.GetElementText(row);

        // Assert
        Assert.Equal("Cell 1Cell 2", text);
    }

    [Fact]
    public void GetElementText_TableCell_ReturnsInnerText()
    {
        // Arrange
        TableCell cell = new TableCell(new Paragraph(new Run(new Text("{{Name}}"))));

        // Act
        string? text = TemplateElementHelper.GetElementText(cell);

        // Assert
        Assert.Equal("{{Name}}", text);
    }

    [Fact]
    public void GetElementText_Table_ReturnsInnerText()
    {
        // Arrange
        Table table = new Table(
            new TableRow(
                new TableCell(new Paragraph(new Run(new Text("Header"))))),
            new TableRow(
                new TableCell(new Paragraph(new Run(new Text("Data"))))));

        // Act
        string? text = TemplateElementHelper.GetElementText(table);

        // Assert
        Assert.Equal("HeaderData", text);
    }

    [Fact]
    public void GetElementText_UnsupportedElement_ReturnsNull()
    {
        // Arrange
        Run run = new Run(new Text("Hello"));

        // Act
        string? text = TemplateElementHelper.GetElementText(run);

        // Assert
        Assert.Null(text);
    }

    #endregion

    #region ContainsTemplateMarker Tests

    [Fact]
    public void ContainsTemplateMarker_ParagraphWithMarker_ReturnsTrue()
    {
        // Arrange
        Paragraph paragraph = new Paragraph(new Run(new Text("{{Name}}")));

        // Act
        bool result = TemplateElementHelper.ContainsTemplateMarker(paragraph);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ContainsTemplateMarker_ParagraphWithoutMarker_ReturnsFalse()
    {
        // Arrange
        Paragraph paragraph = new Paragraph(new Run(new Text("Hello World")));

        // Act
        bool result = TemplateElementHelper.ContainsTemplateMarker(paragraph);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsTemplateMarker_EmptyParagraph_ReturnsFalse()
    {
        // Arrange
        Paragraph paragraph = new Paragraph();

        // Act
        bool result = TemplateElementHelper.ContainsTemplateMarker(paragraph);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ContainsTemplateMarker_UnsupportedElement_ReturnsFalse()
    {
        // Arrange
        Run run = new Run(new Text("{{Name}}"));

        // Act
        bool result = TemplateElementHelper.ContainsTemplateMarker(run);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region SafeRemove Tests

    [Fact]
    public void SafeRemove_ElementWithParent_RemovesSuccessfully()
    {
        // Arrange
        Paragraph parent = new Paragraph();
        Run run = new Run(new Text("test"));
        parent.Append(run);

        // Act
        TemplateElementHelper.SafeRemove(run);

        // Assert
        Assert.Empty(parent.Elements<Run>());
        Assert.Null(run.Parent);
    }

    [Fact]
    public void SafeRemove_ElementWithoutParent_DoesNotThrow()
    {
        // Arrange
        Run run = new Run(new Text("test"));

        // Act (should not throw even though run.Parent is null)
        TemplateElementHelper.SafeRemove(run);

        // Assert - no exception thrown
        Assert.Null(run.Parent);
    }

    [Fact]
    public void SafeRemove_ParagraphWithinDocument_RemovesFromDocument()
    {
        // Arrange
        Body body = new Body();
        Paragraph paragraph = new Paragraph(new Run(new Text("Test")));
        body.Append(paragraph);

        Assert.Single(body.Elements<Paragraph>());

        // Act
        TemplateElementHelper.SafeRemove(paragraph);

        // Assert
        Assert.Empty(body.Elements<Paragraph>());
    }

    #endregion

    #region SafeRemoveRange Tests

    [Fact]
    public void SafeRemoveRange_MultipleElementsWithParent_RemovesAll()
    {
        // Arrange
        Paragraph paragraph = new Paragraph();
        Run run1 = new Run(new Text("1"));
        Run run2 = new Run(new Text("2"));
        Run run3 = new Run(new Text("3"));
        paragraph.Append(run1, run2, run3);

        List<OpenXmlElement> elementsToRemove = new List<OpenXmlElement> { run1, run3 };

        // Act
        TemplateElementHelper.SafeRemoveRange(elementsToRemove);

        // Assert
        Assert.Single(paragraph.Elements<Run>());
        Assert.Same(run2, paragraph.Elements<Run>().First());
    }

    [Fact]
    public void SafeRemoveRange_EmptyList_DoesNotThrow()
    {
        // Arrange
        List<OpenXmlElement> emptyList = new List<OpenXmlElement>();

        // Act (should not throw)
        TemplateElementHelper.SafeRemoveRange(emptyList);

        // Assert - no exception
    }

    [Fact]
    public void SafeRemoveRange_MixedParentedAndOrphaned_RemovesOnlyParented()
    {
        // Arrange
        Paragraph paragraph = new Paragraph();
        Run attachedRun = new Run(new Text("attached"));
        Run orphanedRun = new Run(new Text("orphaned"));
        paragraph.Append(attachedRun);

        List<OpenXmlElement> elementsToRemove = new List<OpenXmlElement> { attachedRun, orphanedRun };

        // Act
        TemplateElementHelper.SafeRemoveRange(elementsToRemove);

        // Assert
        Assert.Empty(paragraph.Elements<Run>());
        Assert.Null(attachedRun.Parent);
        Assert.Null(orphanedRun.Parent); // Was already orphaned
    }

    #endregion

    #region CloneElement Tests

    [Fact]
    public void CloneElement_Paragraph_CreatesDeepCopy()
    {
        // Arrange
        Paragraph original = new Paragraph(
            new Run(new Text("Hello")),
            new Run(new Text(" World")));

        // Act
        Paragraph clone = TemplateElementHelper.CloneElement(original);

        // Assert
        Assert.NotSame(original, clone);
        Assert.Equal(original.InnerText, clone.InnerText);
        Assert.Equal(2, clone.Elements<Run>().Count());
    }

    [Fact]
    public void CloneElement_ModifyingClone_DoesNotAffectOriginal()
    {
        // Arrange
        Paragraph original = new Paragraph(new Run(new Text("Original")));
        Paragraph clone = TemplateElementHelper.CloneElement(original);

        // Act
        clone.Append(new Run(new Text(" Modified")));

        // Assert
        Assert.Equal("Original", original.InnerText);
        Assert.Equal("Original Modified", clone.InnerText);
    }

    [Fact]
    public void CloneElement_NestedStructure_CreatesCompleteDeepCopy()
    {
        // Arrange
        Table original = new Table(
            new TableRow(
                new TableCell(
                    new Paragraph(new Run(new Text("Cell 1"))))));

        // Act
        Table clone = TemplateElementHelper.CloneElement(original);

        // Assert
        Assert.NotSame(original, clone);
        Assert.Single(clone.Elements<TableRow>());
        Assert.Single(clone.Elements<TableRow>().First().Elements<TableCell>());
        Assert.Equal("Cell 1", clone.InnerText);
    }

    #endregion

    #region CloneElements Tests

    [Fact]
    public void CloneElements_ListOfParagraphs_CreatesDeepCopies()
    {
        // Arrange
        List<OpenXmlElement> originals = new List<OpenXmlElement>
        {
            new Paragraph(new Run(new Text("Para 1"))),
            new Paragraph(new Run(new Text("Para 2"))),
            new Paragraph(new Run(new Text("Para 3")))
        };

        // Act
        List<OpenXmlElement> clones = TemplateElementHelper.CloneElements(originals);

        // Assert
        Assert.Equal(3, clones.Count);
        Assert.All(clones.Zip(originals), pair =>
        {
            Assert.NotSame(pair.First, pair.Second);
            Assert.Equal(pair.Second.InnerText, pair.First.InnerText);
        });
    }

    [Fact]
    public void CloneElements_EmptyList_ReturnsEmptyList()
    {
        // Arrange
        List<OpenXmlElement> emptyList = new List<OpenXmlElement>();

        // Act
        List<OpenXmlElement> result = TemplateElementHelper.CloneElements(emptyList);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void CloneElements_ModifyingClones_DoesNotAffectOriginals()
    {
        // Arrange
        Paragraph original = new Paragraph(new Run(new Text("Original")));
        List<OpenXmlElement> originals = new List<OpenXmlElement> { original };

        // Act
        List<OpenXmlElement> clones = TemplateElementHelper.CloneElements(originals);
        ((Paragraph)clones[0]).Append(new Run(new Text(" Modified")));

        // Assert
        Assert.Equal("Original", original.InnerText);
        Assert.Equal("Original Modified", clones[0].InnerText);
    }

    #endregion
}
