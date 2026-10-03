// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Integration tests for conditional blocks that make up the entire content of a table cell.
/// Regression tests for issue #117: removing a branch must not leave an empty &lt;w:tc&gt;,
/// which is invalid OOXML (ECMA-376 §17.4.66 requires a cell to end with a paragraph).
/// </summary>
public sealed class ConditionalTableCellTests
{
    [Fact]
    public void ProcessTemplate_ConditionalIsEntireCellContent_ConditionFalse_CellKeepsParagraph()
    {
        // Arrange: a 1x1 table whose only cell content is a block conditional.
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddTableWithCellParagraphs(1, 1, (row, col) => new[]
        {
            "{{#if ShowX}}",
            "X",
            "{{/if}}"
        });

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["ShowX"] = false
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(1, verifier.GetTableCount());

        // The X content is gone...
        Assert.Equal(string.Empty, verifier.GetTableCellText(0, 0, 0));

        // ...but the cell must remain a valid OOXML cell: at least one paragraph, ending with one.
        Assert.True(
            verifier.DoesTableCellEndWithParagraph(0, 0, 0),
            "Table cell must end with a <w:p> element, otherwise the OOXML is invalid.");
        Assert.Equal(1, verifier.GetTableCellParagraphCount(0, 0, 0));
    }

    [Fact]
    public void ProcessTemplate_ConditionalIsEntireCellContent_ConditionTrue_CellKeepsContent()
    {
        // Arrange
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddTableWithCellParagraphs(1, 1, (row, col) => new[]
        {
            "{{#if ShowX}}",
            "X",
            "{{/if}}"
        });

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["ShowX"] = true
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal("X", verifier.GetTableCellText(0, 0, 0));
        Assert.True(verifier.DoesTableCellEndWithParagraph(0, 0, 0));
    }

    [Fact]
    public void ProcessTemplate_NestedConditionalsAreEntireCellContent_AllFalse_CellKeepsParagraph()
    {
        // Arrange: mirrors the issue #117 reproduction (nested {{#if}} matrix cell).
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddTableWithCellParagraphs(1, 1, (row, col) => new[]
        {
            "{{#if a = 1}}",
            "{{#if b = 4}}",
            "X",
            "{{/if}}",
            "{{/if}}"
        });

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["a"] = 2,
            ["b"] = 3
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal(string.Empty, verifier.GetTableCellText(0, 0, 0));
        Assert.True(
            verifier.DoesTableCellEndWithParagraph(0, 0, 0),
            "Nested-conditional matrix cell must still end with a <w:p> when no branch matches.");
        Assert.Equal(1, verifier.GetTableCellParagraphCount(0, 0, 0));
    }

    [Fact]
    public void ProcessTemplate_MatrixOfConditionalCells_OnlyMatchingCellKeepsX_AllCellsStayValid()
    {
        // Arrange: 2x2 matrix, each cell an independent conditional keyed on its coordinates.
        // Only cell (0,0) matches; the other three must remain valid (non-empty) cells.
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddTableWithCellParagraphs(2, 2, (row, col) => new[]
        {
            $"{{{{#if Match_{row}_{col}}}}}",
            "X",
            "{{/if}}"
        });

        MemoryStream templateStream = builder.ToStream();

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Match_0_0"] = true,
            ["Match_0_1"] = false,
            ["Match_1_0"] = false,
            ["Match_1_1"] = false
        };

        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream outputStream = new MemoryStream();

        // Act
        ProcessingResult result = processor.ProcessTemplate(templateStream, outputStream, data);

        // Assert
        Assert.True(result.IsSuccess);

        using DocumentVerifier verifier = new DocumentVerifier(outputStream);
        Assert.Equal("X", verifier.GetTableCellText(0, 0, 0));

        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 2; col++)
            {
                Assert.True(
                    verifier.DoesTableCellEndWithParagraph(0, row, col),
                    $"Cell ({row},{col}) must end with a <w:p> element.");
            }
        }
    }
}
