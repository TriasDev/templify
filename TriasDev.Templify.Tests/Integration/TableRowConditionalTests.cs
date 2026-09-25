// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Integration tests for table-level template constructs (issue #145):
/// conditional table rows ({{#if}} / {{#elseif}} / {{#else}} / {{/if}} markers in their own rows),
/// their interaction with table-row loops and body loops, and loops confined to a single cell.
/// Every test also validates the produced document against the OOXML schema.
/// </summary>
public sealed class TableRowConditionalTests
{
    private const int Columns = 2;

    #region Conditional table rows

    [Fact]
    public void ProcessTemplate_TableRowConditional_True_KeepsBodyRowsAndRemovesMarkerRows()
    {
        // Arrange
        MemoryStream template = BuildDocument(Table("Header", "{{#if Show}}", "Body", "{{/if}}", "Footer"));
        Dictionary<string, object> data = new Dictionary<string, object> { ["Show"] = true };

        // Act
        MemoryStream output = Process(template, data);

        // Assert
        Assert.Equal(new[] { "Header", "Body", "Footer" }, GetFirstColumn(output, 0));
    }

    [Fact]
    public void ProcessTemplate_TableRowConditional_False_RemovesBodyAndMarkerRows()
    {
        // Arrange
        MemoryStream template = BuildDocument(Table("Header", "{{#if Show}}", "Body", "{{/if}}", "Footer"));
        Dictionary<string, object> data = new Dictionary<string, object> { ["Show"] = false };

        // Act
        MemoryStream output = Process(template, data);

        // Assert
        Assert.Equal(new[] { "Header", "Footer" }, GetFirstColumn(output, 0));
    }

    [Fact]
    public void ProcessTemplate_TableRowConditional_BodyRowPlaceholders_AreReplaced()
    {
        // Arrange
        MemoryStream template = BuildDocument(Table("{{#if Show}}", "Name: {{Name}}", "{{/if}}"));
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Show"] = true,
            ["Name"] = "Alice"
        };

        // Act
        MemoryStream output = Process(template, data);

        // Assert
        Assert.Equal(new[] { "Name: Alice" }, GetFirstColumn(output, 0));
    }

    [Theory]
    [InlineData(true, "A")]
    [InlineData(false, "B")]
    public void ProcessTemplate_TableRowConditional_WithElse_KeepsMatchingBranchRows(bool show, string expected)
    {
        // Arrange
        MemoryStream template = BuildDocument(Table("{{#if Show}}", "A", "{{#else}}", "B", "{{/if}}"));
        Dictionary<string, object> data = new Dictionary<string, object> { ["Show"] = show };

        // Act
        MemoryStream output = Process(template, data);

        // Assert
        Assert.Equal(new[] { expected }, GetFirstColumn(output, 0));
    }

    [Theory]
    [InlineData("a", "A")]
    [InlineData("b", "B")]
    [InlineData("z", "C")]
    public void ProcessTemplate_TableRowConditional_WithElseIf_KeepsMatchingBranchRows(string status, string expected)
    {
        // Arrange
        MemoryStream template = BuildDocument(Table(
            "{{#if Status = \"a\"}}", "A",
            "{{#elseif Status = \"b\"}}", "B",
            "{{#else}}", "C",
            "{{/if}}"));
        Dictionary<string, object> data = new Dictionary<string, object> { ["Status"] = status };

        // Act
        MemoryStream output = Process(template, data);

        // Assert
        Assert.Equal(new[] { expected }, GetFirstColumn(output, 0));
    }

    [Theory]
    [InlineData(true, true, new[] { "Outer", "Inner", "After" })]
    [InlineData(true, false, new[] { "Outer", "After" })]
    [InlineData(false, true, new[] { "After" })]
    public void ProcessTemplate_NestedTableRowConditionals_EvaluateIndependently(bool outer, bool inner, string[] expected)
    {
        // Arrange
        MemoryStream template = BuildDocument(Table(
            "{{#if Outer}}", "Outer",
            "{{#if Inner}}", "Inner", "{{/if}}",
            "{{/if}}",
            "After"));
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Outer"] = outer,
            ["Inner"] = inner
        };

        // Act
        MemoryStream output = Process(template, data);

        // Assert
        Assert.Equal(expected, GetFirstColumn(output, 0));
    }

    [Fact]
    public void ProcessTemplate_TableRowConditional_InlineCellConditionalInBody_StaysCellLevel()
    {
        // Arrange: the body row contains a conditional that is confined to one cell.
        MemoryStream template = BuildDocument(Table("{{#if Show}}", "{{Name}}{{#if Vip}} (VIP){{/if}}", "{{/if}}"));
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Show"] = true,
            ["Name"] = "Alice",
            ["Vip"] = true
        };

        // Act
        MemoryStream output = Process(template, data);

        // Assert
        Assert.Equal(new[] { "Alice (VIP)" }, GetFirstColumn(output, 0));
    }

    [Fact]
    public void ProcessTemplate_TableRowConditional_WrappingTableRowLoop_True_ExpandsLoop()
    {
        // Arrange
        MemoryStream template = BuildDocument(Table(
            "Header",
            "{{#if Show}}",
            "{{#foreach Items}}", "{{Name}}", "{{/foreach}}",
            "{{/if}}"));
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Show"] = true,
            ["Items"] = CreateItems()
        };

        // Act
        MemoryStream output = Process(template, data);

        // Assert
        Assert.Equal(new[] { "Header", "A", "B", "C" }, GetFirstColumn(output, 0));
    }

    [Fact]
    public void ProcessTemplate_TableRowConditional_WrappingTableRowLoop_False_RemovesLoop()
    {
        // Arrange
        MemoryStream template = BuildDocument(Table(
            "Header",
            "{{#if Show}}",
            "{{#foreach Items}}", "{{Name}}", "{{/foreach}}",
            "{{/if}}"));
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Show"] = false,
            ["Items"] = CreateItems()
        };

        // Act
        MemoryStream output = Process(template, data);

        // Assert
        Assert.Equal(new[] { "Header" }, GetFirstColumn(output, 0));
    }

    #endregion

    #region Conditional table rows inside loops

    [Fact]
    public void ProcessTemplate_TableRowConditional_InsideTableRowLoop_UsesItemContext()
    {
        // Arrange
        MemoryStream template = BuildDocument(Table(
            "Header",
            "{{#foreach Items}}",
            "{{#if Active}}", "{{Name}}", "{{/if}}",
            "{{/foreach}}"));
        Dictionary<string, object> data = new Dictionary<string, object> { ["Items"] = CreateItems() };

        // Act
        MemoryStream output = Process(template, data);

        // Assert: B is inactive
        Assert.Equal(new[] { "Header", "A", "C" }, GetFirstColumn(output, 0));
    }

    [Fact]
    public void ProcessTemplate_TableRowConditionalWithElse_InsideNamedTableRowLoop_UsesItemContext()
    {
        // Arrange
        MemoryStream template = BuildDocument(Table(
            "{{#foreach item in Items}}",
            "{{#if item.Active}}", "{{item.Name}} active", "{{#else}}", "{{item.Name}} inactive", "{{/if}}",
            "{{/foreach}}"));
        Dictionary<string, object> data = new Dictionary<string, object> { ["Items"] = CreateItems() };

        // Act
        MemoryStream output = Process(template, data);

        // Assert
        Assert.Equal(new[] { "A active", "B inactive", "C active" }, GetFirstColumn(output, 0));
    }

    [Fact]
    public void ProcessTemplate_InlineCellConditional_InsideTableRowLoop_KeepsRow()
    {
        // Arrange: a conditional confined to one cell of a loop row must not be treated as a row conditional.
        MemoryStream template = BuildDocument(Table(
            "{{#foreach Items}}",
            "{{Name}}{{#if Active}} *{{/if}}",
            "{{/foreach}}"));
        Dictionary<string, object> data = new Dictionary<string, object> { ["Items"] = CreateItems() };

        // Act
        MemoryStream output = Process(template, data);

        // Assert
        Assert.Equal(new[] { "A *", "B", "C *" }, GetFirstColumn(output, 0));
    }

    [Fact]
    public void ProcessTemplate_TableRowConditional_InsideBodyLoop_UsesItemContext()
    {
        // Arrange: a paragraph-level loop whose content is a table with a row conditional.
        MemoryStream template = BuildDocument(
            Paragraph("{{#foreach Items}}"),
            Table("{{#if Active}}", "{{Name}}", "{{/if}}", "Always"),
            Paragraph("{{/foreach}}"));
        Dictionary<string, object> data = new Dictionary<string, object> { ["Items"] = CreateItems() };

        // Act
        MemoryStream output = Process(template, data);

        // Assert
        Assert.Equal(new[] { "A", "Always" }, GetFirstColumn(output, 0));
        Assert.Equal(new[] { "Always" }, GetFirstColumn(output, 1));
        Assert.Equal(new[] { "C", "Always" }, GetFirstColumn(output, 2));
    }

    #endregion

    #region Headers

    [Theory]
    [InlineData(true, new[] { "Title", "Draft" })]
    [InlineData(false, new[] { "Title" })]
    public void ProcessTemplate_TableRowConditional_InHeaderTable_IsProcessed(bool isDraft, string[] expected)
    {
        // Arrange
        MemoryStream template = BuildDocument(
            new OpenXmlElement[] { Paragraph("Body") },
            new OpenXmlElement[] { Table("Title", "{{#if IsDraft}}", "Draft", "{{/if}}") });
        Dictionary<string, object> data = new Dictionary<string, object> { ["IsDraft"] = isDraft };

        // Act
        MemoryStream output = Process(template, data);

        // Assert
        output.Position = 0;
        using WordprocessingDocument document = WordprocessingDocument.Open(output, false);
        Header header = document.MainDocumentPart!.HeaderParts.Single().Header!;
        List<string> rows = header.Elements<Table>().Single().Elements<TableRow>()
            .Select(r => r.Elements<TableCell>().First().InnerText)
            .ToList();
        Assert.Equal(expected, rows);
    }

    [Fact]
    public void ProcessTemplate_AllRowsOfHeaderTableRemoved_HeaderStaysValid()
    {
        // Arrange: the table is the header's only content.
        MemoryStream template = BuildDocument(
            new OpenXmlElement[] { Paragraph("Body") },
            new OpenXmlElement[] { Table("{{#if IsDraft}}", "Draft", "{{/if}}") });
        Dictionary<string, object> data = new Dictionary<string, object> { ["IsDraft"] = false };

        // Act
        MemoryStream output = Process(template, data);

        // Assert
        output.Position = 0;
        using WordprocessingDocument document = WordprocessingDocument.Open(output, false);
        Header header = document.MainDocumentPart!.HeaderParts.Single().Header!;
        Assert.Empty(header.Elements<Table>());
        Assert.Single(header.Elements<Paragraph>());
    }

    #endregion

    #region Tables whose rows are all removed

    [Fact]
    public void ProcessTemplate_AllTableRowsRemovedByConditional_RemovesTable()
    {
        // Arrange: a <w:tbl> without any <w:tr> is rejected by Word, so the table must go.
        MemoryStream template = BuildDocument(
            Paragraph("Before"),
            Table("{{#if Show}}", "Body", "{{/if}}"),
            Paragraph("After"));
        Dictionary<string, object> data = new Dictionary<string, object> { ["Show"] = false };

        // Act
        MemoryStream output = Process(template, data);

        // Assert
        using DocumentVerifier verifier = new DocumentVerifier(output);
        Assert.Equal(0, verifier.GetTableCount());
        Assert.Equal(new[] { "Before", "After" }, verifier.GetAllParagraphTexts());
    }

    [Fact]
    public void ProcessTemplate_AllTableRowsRemovedByEmptyLoop_RemovesTable()
    {
        // Arrange
        MemoryStream template = BuildDocument(
            Paragraph("Before"),
            Table("{{#foreach Items}}", "{{Name}}", "{{/foreach}}"),
            Paragraph("After"));
        Dictionary<string, object> data = new Dictionary<string, object> { ["Items"] = new List<Item>() };

        // Act
        MemoryStream output = Process(template, data);

        // Assert
        using DocumentVerifier verifier = new DocumentVerifier(output);
        Assert.Equal(0, verifier.GetTableCount());
        Assert.Equal(new[] { "Before", "After" }, verifier.GetAllParagraphTexts());
    }

    [Fact]
    public void ProcessTemplate_AllRowsOfNestedTableRemoved_OuterCellKeepsParagraph()
    {
        // Arrange: the nested table is the only content of the outer cell (no trailing paragraph).
        Table inner = Table("{{#if Show}}", "Body", "{{/if}}");
        Table outer = new Table(
            new TableProperties(new TableWidth { Width = "0", Type = TableWidthUnitValues.Auto }),
            new TableGrid(new GridColumn { Width = "4000" }),
            new TableRow(new TableCell(inner)));
        MemoryStream template = BuildDocument(outer);
        Dictionary<string, object> data = new Dictionary<string, object> { ["Show"] = false };

        // Act
        MemoryStream output = Process(template, data);

        // Assert
        using DocumentVerifier verifier = new DocumentVerifier(output);
        Assert.Equal(1, verifier.GetTableCount());
        Assert.True(verifier.DoesTableCellEndWithParagraph(0, 0, 0));
    }

    #endregion

    #region Loops confined to a single cell

    [Fact]
    public void ProcessTemplate_NamedLoop_InsideSingleTableCell_RepeatsParagraphsInCell()
    {
        // Arrange
        MemoryStream template = BuildDocument(CellTable("{{#foreach item in Items}}", "{{item.Name}}", "{{/foreach}}"));
        Dictionary<string, object> data = new Dictionary<string, object> { ["Items"] = CreateItems() };

        // Act
        MemoryStream output = Process(template, data);

        // Assert
        Assert.Equal(new[] { "A", "B", "C" }, GetCellParagraphs(output));
    }

    [Fact]
    public void ProcessTemplate_ImplicitLoop_InsideSingleTableCell_RepeatsParagraphsInCell()
    {
        // Arrange
        MemoryStream template = BuildDocument(CellTable("{{#foreach Items}}", "{{Name}}", "{{/foreach}}"));
        Dictionary<string, object> data = new Dictionary<string, object> { ["Items"] = CreateItems() };

        // Act
        MemoryStream output = Process(template, data);

        // Assert
        Assert.Equal(new[] { "A", "B", "C" }, GetCellParagraphs(output));
    }

    [Fact]
    public void ProcessTemplate_NamedLoopWithNestedPath_InsideSingleTableCell_RepeatsParagraphsInCell()
    {
        // Arrange
        MemoryStream template = BuildDocument(CellTable("{{#foreach item in Order.Items}}", "{{item.Name}}", "{{/foreach}}"));
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Order"] = new Dictionary<string, object> { ["Items"] = CreateItems() }
        };

        // Act
        MemoryStream output = Process(template, data);

        // Assert
        Assert.Equal(new[] { "A", "B", "C" }, GetCellParagraphs(output));
    }

    [Fact]
    public void ProcessTemplate_CellLoop_InsideTableRowLoop_RepeatsRowsAndCellParagraphs()
    {
        // Arrange: a row loop whose content row has a loop confined to its first cell.
        Table table = Table("{{#foreach Groups}}", "{{/foreach}}");
        TableRow contentRow = new TableRow(
            new TableCell(Paragraph("{{Name}}:"), Paragraph("{{#foreach Members}}"), Paragraph("{{.}}"), Paragraph("{{/foreach}}")),
            new TableCell(new Paragraph()));
        table.Elements<TableRow>().First().InsertAfterSelf(contentRow);
        MemoryStream template = BuildDocument(table);
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Groups"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { ["Name"] = "G1", ["Members"] = new List<string> { "a", "b" } },
                new Dictionary<string, object> { ["Name"] = "G2", ["Members"] = new List<string> { "c" } }
            }
        };

        // Act
        MemoryStream output = Process(template, data);

        // Assert
        Assert.Equal(new[] { "G1:ab", "G2:c" }, GetFirstColumn(output, 0));
    }

    #endregion

    #region Detector

    [Fact]
    public void DetectTableRowConditionals_ConditionalsConfinedToOneCell_AreNotRowConditionals()
    {
        // Arrange
        List<TableRow> rows = Table("{{#if A}}inline{{/if}}", "{{#if B}}x{{#else}}y{{/if}}")
            .Elements<TableRow>()
            .ToList();

        // Act
        IReadOnlyList<TriasDev.Templify.Conditionals.ConditionalBlock> conditionals = TriasDev.Templify.Conditionals.ConditionalDetector.DetectTableRowConditionals(rows);

        // Assert
        Assert.Empty(conditionals);
    }

    [Fact]
    public void DetectTableRowConditionals_RowWithTwoRowLevelMarkers_Throws()
    {
        // Arrange: {{#if}} opens in the first cell and closes in the second cell of the same row.
        TableRow row = new TableRow(
            new TableCell(Paragraph("{{#if A}}")),
            new TableCell(Paragraph("{{/if}}")));

        // Act & Assert
        TemplateSyntaxException exception = Assert.Throws<TemplateSyntaxException>(
            () => TriasDev.Templify.Conditionals.ConditionalDetector.DetectTableRowConditionals(new List<TableRow> { row }));
        Assert.Contains("own row", exception.Message);
    }

    [Fact]
    public void DetectTableRowConditionals_UnclosedRowConditional_Throws()
    {
        // Arrange
        List<TableRow> rows = Table("{{#if A}}", "Body").Elements<TableRow>().ToList();

        // Act & Assert
        TemplateSyntaxException exception = Assert.Throws<TemplateSyntaxException>(
            () => TriasDev.Templify.Conditionals.ConditionalDetector.DetectTableRowConditionals(rows));
        Assert.Contains("has no matching", exception.Message);
    }

    #endregion

    #region Helpers

    private sealed class Item
    {
        public string Name { get; set; } = string.Empty;
        public bool Active { get; set; }
    }

    private static List<Item> CreateItems()
    {
        return new List<Item>
        {
            new Item { Name = "A", Active = true },
            new Item { Name = "B", Active = false },
            new Item { Name = "C", Active = true }
        };
    }

    private static Paragraph Paragraph(string text)
    {
        return new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    /// <summary>
    /// Builds a schema-valid table with one row per text. The text goes into the first cell,
    /// the remaining cells are empty.
    /// </summary>
    private static Table Table(params string[] firstColumnTexts)
    {
        Table table = new Table(
            new TableProperties(new TableWidth { Width = "0", Type = TableWidthUnitValues.Auto }),
            new TableGrid(Enumerable.Range(0, Columns).Select(_ => new GridColumn { Width = "2000" })));

        foreach (string text in firstColumnTexts)
        {
            TableRow row = new TableRow(new TableCell(Paragraph(text)));
            for (int col = 1; col < Columns; col++)
            {
                row.Append(new TableCell(new Paragraph()));
            }

            table.Append(row);
        }

        return table;
    }

    /// <summary>
    /// Builds a 1x1 table whose single cell contains one paragraph per text.
    /// </summary>
    private static Table CellTable(params string[] paragraphTexts)
    {
        return new Table(
            new TableProperties(new TableWidth { Width = "0", Type = TableWidthUnitValues.Auto }),
            new TableGrid(new GridColumn { Width = "4000" }),
            new TableRow(new TableCell(paragraphTexts.Select(Paragraph))));
    }

    private static MemoryStream BuildDocument(params OpenXmlElement[] bodyElements)
    {
        return BuildDocument(bodyElements, headerElements: null);
    }

    private static MemoryStream BuildDocument(OpenXmlElement[] bodyElements, OpenXmlElement[]? headerElements)
    {
        MemoryStream stream = new MemoryStream();
        using (WordprocessingDocument document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = document.AddMainDocumentPart();
            Body body = new Body(bodyElements);
            mainPart.Document = new Document(body);

            if (headerElements != null)
            {
                HeaderPart headerPart = mainPart.AddNewPart<HeaderPart>();
                headerPart.Header = new Header(headerElements);
                headerPart.Header.Save();
                body.Append(new SectionProperties(new HeaderReference
                {
                    Type = HeaderFooterValues.Default,
                    Id = mainPart.GetIdOfPart(headerPart)
                }));
            }

            mainPart.Document.Save();
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// Processes the template, asserts success and asserts the output is schema-valid OOXML.
    /// </summary>
    private static MemoryStream Process(MemoryStream template, Dictionary<string, object> data)
    {
        DocumentTemplateProcessor processor = new DocumentTemplateProcessor();
        MemoryStream output = new MemoryStream();

        ProcessingResult result = processor.ProcessTemplate(template, output, data);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        AssertValidOpenXml(output);
        return output;
    }

    private static void AssertValidOpenXml(MemoryStream output)
    {
        output.Position = 0;
        using (WordprocessingDocument document = WordprocessingDocument.Open(output, false))
        {
            List<ValidationErrorInfo> errors = new OpenXmlValidator().Validate(document).ToList();
            Assert.True(
                errors.Count == 0,
                "Output is not valid OOXML:\n" + string.Join("\n", errors.Select(e => $"{e.Path?.XPath}: {e.Description}")));

            foreach (Table table in document.MainDocumentPart!.Document!.Body!.Descendants<Table>())
            {
                Assert.True(table.Elements<TableRow>().Any(), "A table must contain at least one row.");
            }
        }

        output.Position = 0;
    }

    private static List<string> GetFirstColumn(MemoryStream output, int tableIndex)
    {
        using DocumentVerifier verifier = new DocumentVerifier(output);
        return verifier.GetTableCellTexts(tableIndex).Select(row => row[0]).ToList();
    }

    private static List<string> GetCellParagraphs(MemoryStream output)
    {
        output.Position = 0;
        using WordprocessingDocument document = WordprocessingDocument.Open(output, false);
        TableCell cell = document.MainDocumentPart!.Document!.Body!.Descendants<TableCell>().Single();
        return cell.Elements<Paragraph>().Select(p => p.InnerText).ToList();
    }

    #endregion
}
