// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Converter.Converters;
using TriasDev.Templify.Converter.Models;
using TriasDev.Templify.Core;
using static TriasDev.Templify.Converter.Tests.TestDocuments;

namespace TriasDev.Templify.Converter.Tests;

/// <summary>
/// Convert a synthetic OpenXMLTemplates document, then process the result with the core
/// <see cref="DocumentTemplateProcessor"/> and check the rendered output.
/// </summary>
public class RoundTripTests
{
    private static Dictionary<string, object> Data() => new()
    {
        ["customer"] = new Dictionary<string, object> { ["name"] = "Alice" },
        ["is_active"] = true,
        ["status"] = "gold",
        ["vip"] = false,
        ["count"] = 10,
        ["enabled"] = true,
        ["show_footer"] = false,
        ["items"] = new List<object>
        {
            new Dictionary<string, object> { ["name"] = "A" },
            new Dictionary<string, object> { ["name"] = "B" },
        },
        ["lines"] = new List<object>
        {
            new Dictionary<string, object> { ["product"] = "P1", ["qty"] = 1 },
            new Dictionary<string, object> { ["product"] = "P2", ["qty"] = 2 },
        },
    };

    private static void CreateScenario(string path)
    {
        Create(
            path,
            body: new OpenXmlElement[]
            {
                Para(TextRun("Hello "), InlineVariable("customer.name"), TextRun("!")),
                BlockControl("conditionalRemove_is_active", Para("Active customer")),
                BlockControl("conditionalRemove_status_eq_gold_or_vip", Para("Premium")),
                BlockControl("conditionalRemove_count_gt_5_and_enabled", Para("Many")),
                BlockControl("conditionalRemove_count_gt_5_not", Para("Few")),
                Para(TextRun("Inline: "), InlineControl("conditionalRemove_vip", TextRun("VIP!")), TextRun("end")),
                BlockControl("repeating_items", Para(TextRun("Item: "), InlineVariable("name"))),
                Table(
                    Row(Cell(TextRun("Product")), Cell(TextRun("Qty"))),
                    RowControl("repeating_lines", Row(Cell(InlineVariable("product")), Cell(InlineVariable("qty"))))),
                Para("After table"),
            },
            header: new OpenXmlElement[] { Para(TextRun("Invoice for "), InlineVariable("customer.name")) },
            footer: new OpenXmlElement[] { BlockControl("conditionalRemove_show_footer", Para("Footer text")) });
    }

    [Fact]
    public void Convert_ThenProcess_RendersExpectedOutput()
    {
        using TempDirectory dir = new();
        string input = dir.File("template.docx");
        string output = dir.File("template-templify.docx");
        CreateScenario(input);

        ConversionResult result = new TemplateConverter().ConvertTemplate(input, output);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
        Assert.Equal(13, result.TotalControls);
        Assert.Equal(13, result.ConvertedControls);
        Assert.Empty(SchemaErrors(output));
        Assert.Equal(0, CountSdts(output));

        List<string> converted = BodyParagraphs(output);
        Assert.Contains("{{#if is_active}}", converted);
        Assert.Contains("{{#if status = \"gold\" or vip}}", converted);
        Assert.Contains("{{#if count > 5 and enabled}}", converted);
        Assert.Contains("{{#if not count > 5}}", converted);
        Assert.Contains("Inline: {{#if vip}}VIP!{{/if}}end", converted);
        Assert.Contains("{{#foreach items}}", converted);
        Assert.Contains("{{#foreach lines}}", converted);
        Assert.Contains("Invoice for {{customer.name}}", HeaderText(output));

        using FileStream template = File.OpenRead(output);
        using MemoryStream processed = new();
        ProcessingResult processing = new DocumentTemplateProcessor().ProcessTemplate(template, processed, Data());

        Assert.True(processing.IsSuccess, processing.ErrorMessage);
        Assert.Empty(processing.Warnings);
        Assert.Empty(processing.MissingVariables);
        Assert.Empty(SchemaErrors(processed));

        List<string> paragraphs = BodyParagraphs(processed);
        Assert.Contains("Hello Alice!", paragraphs);
        Assert.Contains("Active customer", paragraphs);
        Assert.Contains("Premium", paragraphs);
        Assert.Contains("Many", paragraphs);
        Assert.DoesNotContain("Few", paragraphs);
        Assert.Contains("Inline: end", paragraphs);
        Assert.Contains("Item: A", paragraphs);
        Assert.Contains("Item: B", paragraphs);
        Assert.Contains("After table", paragraphs);
        Assert.DoesNotContain(paragraphs, p => p.Contains("{{"));

        List<string> rows = TableRowTexts(processed);
        Assert.Equal(new[] { "ProductQty", "P11", "P22" }, rows);

        Assert.Equal("Invoice for Alice", HeaderText(processed));
        processed.Position = 0;
        using WordprocessingDocument doc = WordprocessingDocument.Open(processed, false);
        Assert.DoesNotContain("Footer text", doc.MainDocumentPart!.FooterParts.Single().Footer!.InnerText);
    }

    [Fact]
    public void Convert_RepeatingRow_ProducesValidTableRowLoopMarkers()
    {
        using TempDirectory dir = new();
        string input = dir.File("rows.docx");
        string output = dir.File("rows-out.docx");
        Create(input, new OpenXmlElement[]
        {
            Table(
                Row(Cell(TextRun("H1")), Cell(TextRun("H2"))),
                RowControl("repeating_lines", Row(Cell(InlineVariable("product")), Cell(InlineVariable("qty"))))),
        });

        ConversionResult result = new TemplateConverter().ConvertTemplate(input, output);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
        Assert.Empty(SchemaErrors(output));

        using WordprocessingDocument doc = WordprocessingDocument.Open(output, false);
        Table table = doc.MainDocumentPart!.Document!.Body!.Elements<Table>().Single();
        Assert.Empty(table.Elements<Paragraph>());
        List<string> rows = table.Elements<TableRow>().Select(r => r.InnerText).ToList();
        Assert.Equal(new[] { "H1H2", "{{#foreach lines}}", "{{product}}{{qty}}", "{{/foreach}}" }, rows);
        Assert.All(table.Elements<TableRow>(), row => Assert.Equal(2, row.Elements<TableCell>().Count()));
    }

    [Fact]
    public void Convert_UnconvertibleControls_AreReportedAndKept()
    {
        using TempDirectory dir = new();
        string input = dir.File("bad.docx");
        string output = dir.File("bad-out.docx");
        Create(input, new OpenXmlElement[]
        {
            BlockControl("conditionalRemove_a_or", Para("dangling operator")),
            Para(TextRun("Names: "), InlineControl("repeating_names", TextRun("x"))),
            BlockControl("conditionalRemove_a_or_b_eq_1", Para("comparison after or")),
        });

        ConversionResult result = new TemplateConverter().ConvertTemplate(input, output);

        Assert.False(result.Success);
        Assert.Equal(3, result.SkippedControls);
        Assert.Equal(3, result.FailedConversions.Count);
        Assert.Equal(3, CountSdts(output));
        Assert.Empty(SchemaErrors(output));
        Assert.DoesNotContain(BodyParagraphs(output), p => p.Contains("{{#if"));
    }

    [Fact]
    public void Convert_KeepsNonOpenXmlTemplatesControlsByDefault()
    {
        using TempDirectory dir = new();
        string input = dir.File("other.docx");
        string keep = dir.File("keep.docx");
        string unwrap = dir.File("unwrap.docx");
        Create(input, new OpenXmlElement[]
        {
            BlockControl("toc", Para("Table of contents")),
            Para(TextRun("Hi "), InlineVariable("name")),
        });

        ConversionResult kept = new TemplateConverter().ConvertTemplate(input, keep);
        ConversionResult unwrapped = new TemplateConverter(new ConversionOptions { UnwrapAllControls = true }).ConvertTemplate(input, unwrap);

        Assert.True(kept.Success);
        Assert.Equal(1, CountSdts(keep));
        Assert.Contains(kept.Warnings, w => w.Contains("'toc'"));
        Assert.Contains("Hi {{name}}", BodyParagraphs(keep));

        Assert.True(unwrapped.Success);
        Assert.Equal(1, unwrapped.CleanedSdtElements);
        Assert.Equal(0, CountSdts(unwrap));
        Assert.Contains("Table of contents", BodyParagraphs(unwrap));
    }

    [Fact]
    public void Convert_PreservesHyperlinksAndBookmarksInsideControls()
    {
        using TempDirectory dir = new();
        string input = dir.File("links.docx");
        string output = dir.File("links-out.docx");
        Create(input, new OpenXmlElement[]
        {
            Para(
                TextRun("See "),
                InlineControl(
                    "conditionalRemove_show",
                    new BookmarkStart { Id = "1", Name = "mark" },
                    new Hyperlink(TextRun("the link")) { Anchor = "mark" },
                    new BookmarkEnd { Id = "1" })),
        });

        ConversionResult result = new TemplateConverter().ConvertTemplate(input, output);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
        using WordprocessingDocument doc = WordprocessingDocument.Open(output, false);
        Paragraph paragraph = doc.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().First();
        Assert.Equal("See {{#if show}}the link{{/if}}", paragraph.InnerText);
        Assert.Single(paragraph.Elements<Hyperlink>());
        Assert.Single(paragraph.Elements<BookmarkStart>());
        Assert.Single(paragraph.Elements<BookmarkEnd>());
    }

    [Fact]
    public void Convert_RowConditional_ProducesMarkerRows()
    {
        using TempDirectory dir = new();
        string input = dir.File("rowif.docx");
        string output = dir.File("rowif-out.docx");
        Create(input, new OpenXmlElement[]
        {
            Table(
                Row(Cell(TextRun("Always")), Cell(TextRun("row"))),
                RowControl("conditionalRemove_show_row", Row(Cell(TextRun("Hidden")), Cell(TextRun("row"))))),
        });

        ConversionResult result = new TemplateConverter().ConvertTemplate(input, output);

        Assert.Empty(SchemaErrors(output));
        using (FileStream stream = File.OpenRead(output))
        {
            Assert.Equal(new[] { "Alwaysrow", "{{#if show_row}}", "Hiddenrow", "{{/if}}" }, TableRowTexts(stream));
        }

        Assert.Contains(result.Warnings, w => w.Contains("#145"));

        // Until the core supports table-row conditionals (#145) the dry run reports an error
        // instead of claiming success; once supported, the output must render correctly.
        if (result.Success)
        {
            using FileStream template = File.OpenRead(output);
            using MemoryStream processed = new();
            ProcessingResult processing = new DocumentTemplateProcessor().ProcessTemplate(
                template, processed, new Dictionary<string, object> { ["show_row"] = false });
            Assert.True(processing.IsSuccess, processing.ErrorMessage);
            Assert.Equal(new[] { "Alwaysrow" }, TableRowTexts(processed));
        }
        else
        {
            Assert.Contains(result.Errors, e => e.Contains("show_row"));
        }
    }

    [Fact]
    public void Convert_NestedLoops_RenderCorrectly()
    {
        using TempDirectory dir = new();
        string input = dir.File("nested.docx");
        string output = dir.File("nested-out.docx");
        Create(input, new OpenXmlElement[]
        {
            BlockControl(
                "repeating_groups",
                Para(TextRun("Group "), InlineVariable("title")),
                BlockControl("repeating_members", Para(TextRun("- "), InlineVariable("name")))),
        });

        ConversionResult result = new TemplateConverter().ConvertTemplate(input, output);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
        Assert.Empty(SchemaErrors(output));

        Dictionary<string, object> data = new()
        {
            ["groups"] = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["title"] = "G1",
                    ["members"] = new List<object>
                    {
                        new Dictionary<string, object> { ["name"] = "Ann" },
                        new Dictionary<string, object> { ["name"] = "Bob" },
                    },
                },
                new Dictionary<string, object>
                {
                    ["title"] = "G2",
                    ["members"] = new List<object> { new Dictionary<string, object> { ["name"] = "Cid" } },
                },
            },
        };

        using FileStream template = File.OpenRead(output);
        using MemoryStream processed = new();
        ProcessingResult processing = new DocumentTemplateProcessor().ProcessTemplate(template, processed, data);

        Assert.True(processing.IsSuccess, processing.ErrorMessage);
        Assert.Empty(processing.Warnings);
        List<string> paragraphs = BodyParagraphs(processed).Where(p => p.Length > 0).ToList();
        Assert.Equal(new[] { "Group G1", "- Ann", "- Bob", "Group G2", "- Cid" }, paragraphs);
        Assert.Empty(SchemaErrors(processed));
    }

    [Fact]
    public void Convert_CellConditional_WrapsCellContent()
    {
        using TempDirectory dir = new();
        string input = dir.File("cell.docx");
        string output = dir.File("cell-out.docx");
        Create(input, new OpenXmlElement[]
        {
            Table(new TableRow(
                Cell(TextRun("Left")),
                new SdtCell(Props("conditionalRemove_vip"), new SdtContentCell(Cell(TextRun("VIP")))))),
        });

        ConversionResult result = new TemplateConverter().ConvertTemplate(input, output);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
        Assert.Empty(SchemaErrors(output));
        Assert.Contains(result.Warnings, w => w.Contains("cell-level"));

        using FileStream template = File.OpenRead(output);
        using MemoryStream processed = new();
        ProcessingResult processing = new DocumentTemplateProcessor().ProcessTemplate(
            template, processed, new Dictionary<string, object> { ["vip"] = false });

        Assert.True(processing.IsSuccess, processing.ErrorMessage);
        Assert.Equal(new[] { "Left" }, TableRowTexts(processed));
        Assert.Empty(SchemaErrors(processed));
    }

    [Fact]
    public void Convert_DoesNotModifyInput()
    {
        using TempDirectory dir = new();
        string input = dir.File("input.docx");
        CreateScenario(input);
        byte[] before = File.ReadAllBytes(input);

        new TemplateConverter().ConvertTemplate(input, dir.File("out.docx"));

        Assert.Equal(before, File.ReadAllBytes(input));
        Assert.Empty(Directory.GetFiles(dir.Path, "*.tmp"));
    }
}
