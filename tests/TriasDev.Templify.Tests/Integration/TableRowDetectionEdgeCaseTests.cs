// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Table row loop and row conditional detection edge cases (issue #178): cells wrapped in
/// cell-level content controls (<c>SdtCell</c>) and text boxes anchored in table cells.
/// </summary>
public sealed class TableRowDetectionEdgeCaseTests
{
    private static Run TextRun(string text) =>
        new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

    private static Paragraph P(string text) => new Paragraph(TextRun(text));

    private static Run VmlTextBoxRun(params Paragraph[] content) =>
        new Run(new Picture(
            new DocumentFormat.OpenXml.Vml.Shape(
                new DocumentFormat.OpenXml.Vml.TextBox(new TextBoxContent(content)))
            {
                Id = "tb" + Guid.NewGuid().ToString("N")[..8],
                Style = "width:72pt;height:36pt",
            }));

    private static TableRow Row(params OpenXmlElement[] cells) => new TableRow(cells);

    private static TableRow Row(string text) => new TableRow(new TableCell(P(text)));

    private static SdtCell CellControl(string text) =>
        new SdtCell(new SdtProperties(), new SdtContentCell(new TableCell(P(text))));

    private static Table Table(params TableRow[] rows) =>
        new Table(new OpenXmlElement[]
        {
            new TableProperties(new TableWidth { Width = "0", Type = TableWidthUnitValues.Auto }),
            new TableGrid(new GridColumn { Width = "4000" }, new GridColumn { Width = "4000" }),
        }.Concat(rows));

    private static DocumentVerifier Process(Table table, Dictionary<string, object> data)
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddElement(table);

        MemoryStream output = new MemoryStream();
        ProcessingResult result = new DocumentTemplateProcessor().ProcessTemplate(builder.ToStream(), output, data);
        Assert.True(result.IsSuccess, result.ErrorMessage);

        DocumentVerifier verifier = new DocumentVerifier(output);
        List<string> errors = verifier.GetValidationErrors();
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
        return verifier;
    }

    /// <summary>The own text of the rows' cells (text box content excluded), row by row.</summary>
    private static List<string> RowTexts(DocumentVerifier verifier) =>
        verifier.Document.MainDocumentPart!.Document!.Body!
            .Descendants<TableRow>()
            .Select(row => string.Concat(row.Descendants<TableCell>()
                .SelectMany(c => c.Elements<Paragraph>())
                .Select(p => string.Concat(p.Elements<Run>().SelectMany(r => r.Elements<Text>()).Select(t => t.Text)))))
            .ToList();

    [Theory]
    [InlineData(true, new[] { "Header", "Body", "Footer" })]
    [InlineData(false, new[] { "Header", "Footer" })]
    public void RowConditional_MarkersInCellContentControls_AreRowLevel(bool show, string[] expected)
    {
        using DocumentVerifier verifier = Process(
            Table(
                Row("Header"),
                Row(CellControl("{{#if Show}}")),
                Row("Body"),
                Row(CellControl("{{/if}}")),
                Row("Footer")),
            new Dictionary<string, object> { ["Show"] = show });

        Assert.Equal(expected, RowTexts(verifier));
    }

    [Fact]
    public void RowConditional_ElseInCellContentControl_IsRowLevel()
    {
        using DocumentVerifier verifier = Process(
            Table(
                Row("{{#if Show}}"),
                Row("A"),
                Row(new TableCell(P("")), CellControl("{{#else}}")),
                Row("B"),
                Row("{{/if}}")),
            new Dictionary<string, object> { ["Show"] = false });

        Assert.Equal(new[] { "B" }, RowTexts(verifier));
    }

    [Fact]
    public void InlineConditional_InCellContentControl_InsideRowConditional_IsCellLevel()
    {
        using DocumentVerifier verifier = Process(
            Table(
                Row("{{#if Show}}"),
                Row(new TableCell(P("Name")), CellControl("{{#if Vip}}VIP{{#else}}Regular{{/if}}")),
                Row("{{/if}}")),
            new Dictionary<string, object> { ["Show"] = true, ["Vip"] = true });

        Assert.Equal(new[] { "NameVIP" }, RowTexts(verifier));
    }

    [Fact]
    public void RowLoop_TextBoxWithLoopInMarkerCell_DoesNotHijackRowLoop()
    {
        // The start marker cell anchors a text box with its own (complete) loop before the
        // row loop marker. The text box is walked separately; it must not be taken for the
        // row loop's start marker.
        TableRow startRow = Row(new TableCell(new Paragraph(
            VmlTextBoxRun(P("{{#foreach Tags}}{{.}}{{/foreach}}")),
            TextRun("{{#foreach Items}}"))));

        using DocumentVerifier verifier = Process(
            Table(startRow, Row("{{.}}"), Row("{{/foreach}}")),
            new Dictionary<string, object>
            {
                ["Items"] = new List<string> { "a", "b" },
                ["Tags"] = new List<string> { "x" },
            });

        Assert.Equal(new[] { "a", "b" }, RowTexts(verifier));
    }

    [Fact]
    public void RowLoop_TextBoxWithLoopMarkersInContentCell_IsProcessedInsideTextBox()
    {
        // A multi-paragraph loop inside a text box in a content row is a text box loop,
        // processed per row loop item.
        TableRow contentRow = Row(new TableCell(new Paragraph(
            TextRun("{{.}}"),
            VmlTextBoxRun(P("{{#foreach Tags}}"), P("{{.}}"), P("{{/foreach}}")))));

        using DocumentVerifier verifier = Process(
            Table(Row("{{#foreach Items}}"), contentRow, Row("{{/foreach}}")),
            new Dictionary<string, object>
            {
                ["Items"] = new List<string> { "a", "b" },
                ["Tags"] = new List<string> { "x", "y" },
            });

        Assert.Equal(new[] { "a", "b" }, RowTexts(verifier));
        List<string> boxTexts = verifier.Document.MainDocumentPart!.Document!.Body!
            .Descendants<TextBoxContent>()
            .Select(box => string.Join("|", box.Elements<Paragraph>().Select(p => p.InnerText)))
            .ToList();
        Assert.Equal(new[] { "x|y", "x|y" }, boxTexts);
    }

    [Fact]
    public void RowConditional_TextBoxInMarkerCell_DoesNotAffectRowMarker()
    {
        // The row marker's paragraph anchors a text box between the characters of the marker.
        // Detection reads the paragraph's own text, so the marker is still recognized.
        TableRow ifRow = Row(new TableCell(new Paragraph(
            TextRun("{{#if Sh"),
            VmlTextBoxRun(P("Note")),
            TextRun("ow}}"))));

        using DocumentVerifier verifier = Process(
            Table(ifRow, Row("Body"), Row("{{/if}}"), Row("Footer")),
            new Dictionary<string, object> { ["Show"] = true });

        Assert.Equal(new[] { "Body", "Footer" }, RowTexts(verifier));
    }
}
