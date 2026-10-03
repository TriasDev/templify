// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Tests.Helpers;
using Vml = DocumentFormat.OpenXml.Vml;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Placeholders in less common document structures (nested tables, hyperlinks, runs split by proofing marks or bookmarks),
/// and schema validity of the processed output for representative templates.
/// </summary>
public sealed class DocumentStructureTests
{
    private static readonly Dictionary<string, object> _data = new Dictionary<string, object>
    {
        ["Name"] = "Alice",
        ["Show"] = true,
        ["Hide"] = false,
        ["Items"] = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object> { ["Title"] = "First", ["Price"] = 1.5m },
            new Dictionary<string, object> { ["Title"] = "Second", ["Price"] = 20m }
        },
        ["Bio"] = "Line one\nLine **two**",
        ["Empty"] = new List<string>()
    };

    // ---------- Output schema validity ----------

    public static TheoryData<string> Templates() => new TheoryData<string>
    {
        "placeholders", "paragraphLoop", "conditionals", "tableRowLoop", "tableRowConditional",
        "nestedTables", "markdownAndNewlines", "textBox", "headerFooter", "emptyLoopInCell"
    };

    [Theory]
    [MemberData(nameof(Templates))]
    public void ProcessTemplate_RepresentativeTemplate_ProducesSchemaValidOutput(string template)
    {
        DocumentBuilder builder = new DocumentBuilder();
        Build(builder, template);
        using MemoryStream templateStream = builder.ToStream();
        using DocumentVerifier templateVerifier = new DocumentVerifier(templateStream);
        AssertSchemaValid(templateVerifier);

        using TemplateTestRun run = TemplateTestHarness.Process(templateStream, _data);

        Assert.True(run.Result.IsSuccess, run.Result.ErrorMessage);
        AssertSchemaValid(run.Verifier);
        string text = run.Verifier.Document.MainDocumentPart!.Document!.Body!.InnerText;
        Assert.DoesNotContain("{{", text);
    }

    private static void Build(DocumentBuilder builder, string template)
    {
        switch (template)
        {
            case "placeholders":
                builder.AddParagraph("Hello {{Name}}, total {{Items[1].Price:number:N2}}");
                builder.AddParagraphWithRuns(("Split {{Na", null), ("me}} here", DocumentBuilder.CreateFormatting(bold: true)));
                break;
            case "paragraphLoop":
                builder.AddParagraph("{{#foreach Items}}");
                builder.AddParagraph("{{@number}}. {{Title}} {{Price}}");
                builder.AddParagraph("{{/foreach}}");
                break;
            case "conditionals":
                builder.AddParagraph("{{#if Show}}");
                builder.AddParagraph("Visible {{Name}}");
                builder.AddParagraph("{{#elseif Hide}}");
                builder.AddParagraph("Never");
                builder.AddParagraph("{{#else}}");
                builder.AddParagraph("Fallback");
                builder.AddParagraph("{{/if}}");
                builder.AddParagraph("Inline {{#if Show}}yes{{#else}}no{{/if}}");
                break;
            case "tableRowLoop":
                builder.AddElement(Tbl(
                    Row("Title", "Price"),
                    Row("{{#foreach Items}}", ""),
                    Row("{{Title}}", "{{Price}}"),
                    Row("{{/foreach}}", "")));
                break;
            case "tableRowConditional":
                builder.AddElement(Tbl(Row("Header"), Row("{{#if Hide}}"), Row("Hidden row"), Row("{{/if}}")));
                builder.AddParagraph("After");
                break;
            case "nestedTables":
                builder.AddElement(Tbl(new TableRow(new TableCell(Tbl(Row("Inner {{Name}}")), new Paragraph()))));
                builder.AddParagraph("After");
                break;
            case "markdownAndNewlines":
                builder.AddParagraph("Bio: {{Bio}}");
                builder.AddParagraphWithRuns(("Bio split: {{B", null), ("io}}", null));
                break;
            case "textBox":
                builder.AddElement(new Paragraph(TextBoxRun("Box {{Name}}")));
                break;
            case "headerFooter":
                builder.AddParagraph("Body {{Name}}");
                builder.AddHeader("Header {{Name}}");
                builder.AddFooter("Footer {{Name}}");
                break;
            case "emptyLoopInCell":
                builder.AddElement(Tbl(new TableRow(new TableCell(P("{{#foreach Empty}}"), P("{{.}}"), P("{{/foreach}}")))));
                builder.AddParagraph("After");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(template), template, null);
        }
    }

    // ---------- Structures ----------

    [Fact]
    public void ProcessTemplate_PlaceholderInNestedTable_IsReplaced()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddElement(Tbl(new TableRow(new TableCell(P("Outer {{Name}}"), Tbl(Row("Inner {{Name}}")), new Paragraph()))));

        using TemplateTestRun run = TemplateTestHarness.Process(builder, _data);

        Table outer = run.Verifier.Document.MainDocumentPart!.Document!.Body!.Elements<Table>().Single();
        Table inner = outer.Descendants<Table>().Single();
        Assert.Equal("Outer Alice", outer.Descendants<TableCell>().First().Elements<Paragraph>().First().InnerText);
        Assert.Equal("Inner Alice", inner.InnerText);
    }

    [Fact]
    public void ProcessTemplate_RowLoopInNestedTable_ExpandsInnerRows()
    {
        Table inner = Tbl(Row("{{#foreach Items}}"), Row("{{Title}}"), Row("{{/foreach}}"));
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddElement(Tbl(new TableRow(new TableCell(inner, new Paragraph()))));

        using TemplateTestRun run = TemplateTestHarness.Process(builder, _data);

        Table outputInner = run.Verifier.Document.MainDocumentPart!.Document!.Body!.Descendants<Table>().Skip(1).Single();
        Assert.Equal(new[] { "First", "Second" }, outputInner.Elements<TableRow>().Select(r => r.InnerText));
        AssertSchemaValid(run.Verifier);
    }

    [Fact]
    public void ProcessTemplate_PlaceholderInsideHyperlink_IsReplacedAndLinkKept()
    {
        DocumentBuilder builder = new DocumentBuilder();
        HyperlinkRelationship relationship = builder.MainPart.AddHyperlinkRelationship(new Uri("https://example.com"), true);
        builder.AddElement(new Paragraph(
            new Run(new Text("Visit ") { Space = SpaceProcessingModeValues.Preserve }),
            new Hyperlink(new Run(new Text("{{Name}}'s page"))) { Id = relationship.Id }));

        using TemplateTestRun run = TemplateTestHarness.Process(builder, _data);

        Paragraph paragraph = run.Verifier.GetParagraph(0);
        Assert.Equal("Visit Alice's page", paragraph.InnerText);
        Hyperlink hyperlink = Assert.Single(paragraph.Elements<Hyperlink>());
        Assert.Equal("Alice's page", hyperlink.InnerText);
        Assert.Equal(relationship.Id, hyperlink.Id?.Value);
    }

    [Fact]
    public void ProcessTemplate_PlaceholderSplitByProofingMarks_IsReplaced()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddElement(new Paragraph(
            new Run(new Text("Hello {{Na")),
            new ProofError { Type = ProofingErrorValues.SpellStart },
            new Run(new Text("me}}!")),
            new ProofError { Type = ProofingErrorValues.SpellEnd }));

        using TemplateTestRun run = TemplateTestHarness.Process(builder, _data);

        Assert.Equal("Hello Alice!", run.Verifier.GetParagraphText(0));
        AssertSchemaValid(run.Verifier);
    }

    [Fact]
    public void ProcessTemplate_PlaceholderSplitByBookmark_IsReplacedAndBookmarkKept()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddElement(new Paragraph(
            new Run(new Text("Hello {{Na")),
            new BookmarkStart { Id = "1", Name = "greeting" },
            new Run(new Text("me}}!")),
            new BookmarkEnd { Id = "1" }));

        using TemplateTestRun run = TemplateTestHarness.Process(builder, _data);

        Paragraph paragraph = run.Verifier.GetParagraph(0);
        Assert.Equal("Hello Alice!", paragraph.InnerText);
        Assert.Equal("greeting", Assert.Single(paragraph.Elements<BookmarkStart>()).Name?.Value);
        Assert.Single(paragraph.Elements<BookmarkEnd>());
        AssertSchemaValid(run.Verifier);
    }

    [Fact]
    public void ProcessTemplate_PlaceholderAroundBookmarkInOneRun_KeepsTextOutsideThePlaceholder()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddElement(new Paragraph(
            new BookmarkStart { Id = "2", Name = "whole" },
            new Run(new Text("Dear {{Name}},") { Space = SpaceProcessingModeValues.Preserve }),
            new BookmarkEnd { Id = "2" }));

        using TemplateTestRun run = TemplateTestHarness.Process(builder, _data);

        Paragraph paragraph = run.Verifier.GetParagraph(0);
        Assert.Equal("Dear Alice,", paragraph.InnerText);
        Assert.Single(paragraph.Elements<BookmarkStart>());
        Assert.Single(paragraph.Elements<BookmarkEnd>());
    }

    private static Paragraph P(string text) => new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    private static TableRow Row(params string[] cells) =>
        new TableRow(cells.Select(c => new TableCell(P(c))));

    /// <summary>
    /// A schema-valid table (tblPr and tblGrid are required before the rows).
    /// </summary>
    private static Table Tbl(params TableRow[] rows)
    {
        int columns = rows.Max(r => r.Elements<TableCell>().Count());
        Table table = new Table(
            new TableProperties(new TableWidth { Width = "0", Type = TableWidthUnitValues.Auto }),
            new TableGrid(Enumerable.Range(0, columns).Select(_ => new GridColumn { Width = "2000" })));
        table.Append(rows);
        return table;
    }

    private static void AssertSchemaValid(DocumentVerifier verifier)
    {
        List<string> errors = verifier.GetValidationErrors();
        Assert.True(errors.Count == 0, "Schema errors:\n" + string.Join("\n", errors));
    }

    /// <summary>
    /// A VML text box run (<c>w:pict/v:shape/v:textbox/w:txbxContent</c>) holding one paragraph.
    /// </summary>
    private static Run TextBoxRun(string text) =>
        new Run(new Picture(
            new Vml.Shape(new Vml.TextBox(new TextBoxContent(new Paragraph(new Run(new Text(text))))))
            {
                Id = "tb1",
                Style = "width:72pt;height:36pt",
            }));
}
