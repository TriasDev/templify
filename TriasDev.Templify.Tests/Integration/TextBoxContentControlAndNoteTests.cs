// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Text boxes, content controls (block, row and cell level), footnotes and endnotes are processed
/// like the document body; comments are intentionally left unprocessed (issue #144).
/// </summary>
public sealed class TextBoxContentControlAndNoteTests
{
    private const string Namespaces =
        "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" " +
        "xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\" " +
        "xmlns:wps=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\" " +
        "xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" " +
        "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" " +
        "xmlns:v=\"urn:schemas-microsoft-com:vml\"";

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

    /// <summary>
    /// A DrawingML text box as Word writes it: mc:AlternateContent with a wps shape in the
    /// Choice and an equivalent VML text box in the Fallback.
    /// </summary>
    private static Run DrawingMlTextBoxRun(string boxXml) => new Run(
        "<w:r " + Namespaces + ">" +
        "<mc:AlternateContent><mc:Choice Requires=\"wps\"><w:drawing>" +
        "<wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\">" +
        "<wp:extent cx=\"914400\" cy=\"457200\"/><wp:docPr id=\"1\" name=\"Text Box 1\"/>" +
        "<a:graphic><a:graphicData uri=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\">" +
        "<wps:wsp><wps:cNvSpPr txBox=\"1\"/>" +
        "<wps:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"914400\" cy=\"457200\"/></a:xfrm>" +
        "<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></wps:spPr>" +
        "<wps:txbx><w:txbxContent>" + boxXml + "</w:txbxContent></wps:txbx><wps:bodyPr/></wps:wsp>" +
        "</a:graphicData></a:graphic></wp:inline></w:drawing></mc:Choice>" +
        "<mc:Fallback><w:pict><v:shape id=\"tb1\" style=\"width:72pt;height:36pt\">" +
        "<v:textbox><w:txbxContent>" + boxXml + "</w:txbxContent></v:textbox></v:shape></w:pict></mc:Fallback>" +
        "</mc:AlternateContent></w:r>");

    private static string XmlParagraph(string text) =>
        "<w:p><w:r><w:t xml:space=\"preserve\">" + text + "</w:t></w:r></w:p>";

    private static DocumentVerifier Process(
        Action<DocumentBuilder> build,
        Dictionary<string, object> data,
        bool validate = true)
    {
        DocumentBuilder builder = new DocumentBuilder();
        build(builder);

        MemoryStream output = new MemoryStream();
        ProcessingResult result = new DocumentTemplateProcessor().ProcessTemplate(builder.ToStream(), output, data);
        Assert.True(result.IsSuccess, result.ErrorMessage);

        DocumentVerifier verifier = new DocumentVerifier(output);
        if (validate)
        {
            List<string> errors = verifier.GetValidationErrors();
            Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
        }

        return verifier;
    }

    private static Body BodyOf(DocumentVerifier verifier) => verifier.Document.MainDocumentPart!.Document!.Body!;

    private static Table Table(params OpenXmlElement[] rows) =>
        new Table(new OpenXmlElement[] { new TableProperties(new TableWidth { Width = "0", Type = TableWidthUnitValues.Auto }), new TableGrid(new GridColumn { Width = "4000" }, new GridColumn { Width = "4000" }) }.Concat(rows));

    private static List<string> TextBoxParagraphTexts(OpenXmlElement root) =>
        root.Descendants<TextBoxContent>()
            .SelectMany(box => box.Elements<Paragraph>())
            .Select(p => p.InnerText)
            .ToList();

    // ---------- Text boxes ----------

    [Fact]
    public void TextBox_PlaceholderReplaced()
    {
        using DocumentVerifier verifier = Process(
            builder => builder.AddElement(new Paragraph(
                TextRun("Before {{Name}} "),
                VmlTextBoxRun(P("Box {{Name}}")),
                TextRun(" after {{Name}}"))),
            new Dictionary<string, object> { ["Name"] = "Alice" });

        Paragraph paragraph = verifier.GetParagraph(0);
        string all = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text));
        Assert.DoesNotContain("{{", all);
        Assert.Equal(3, paragraph.Descendants<Text>().Count(t => t.Text.Contains("Alice")));

        // The outer paragraph's own runs are intact and the text box keeps its own paragraph.
        List<Text> outerTexts = paragraph.Elements<Run>().SelectMany(r => r.Elements<Text>()).ToList();
        Assert.Equal(new[] { "Before Alice ", " after Alice" }, outerTexts.Select(t => t.Text));
        Assert.Equal(new[] { "Box Alice" }, TextBoxParagraphTexts(paragraph));
        Assert.Empty(paragraph.Descendants<Picture>().SelectMany(p => p.Elements<Text>()));
    }

    [Fact]
    public void DrawingMlTextBox_ReplacedInChoiceAndFallback()
    {
        using DocumentVerifier verifier = Process(
            builder => builder.AddElement(new Paragraph(
                TextRun("Before {{Name}} "),
                DrawingMlTextBoxRun(XmlParagraph("Box {{Name}}")),
                TextRun(" after {{Name}}"))),
            new Dictionary<string, object> { ["Name"] = "Alice" });

        Paragraph paragraph = verifier.GetParagraph(0);
        Assert.Equal(new[] { "Box Alice", "Box Alice" }, TextBoxParagraphTexts(paragraph));
        Assert.Single(paragraph.Descendants<AlternateContentChoice>().SelectMany(c => c.Descendants<TextBoxContent>()));
        Assert.Single(paragraph.Descendants<AlternateContentFallback>().SelectMany(c => c.Descendants<TextBoxContent>()));

        List<Text> outerTexts = paragraph.Elements<Run>().SelectMany(r => r.Elements<Text>()).ToList();
        Assert.Equal(new[] { "Before Alice ", " after Alice" }, outerTexts.Select(t => t.Text));
    }

    [Fact]
    public void TextBox_InsideLoop_UsesLoopItemContext()
    {
        using DocumentVerifier verifier = Process(
            builder =>
            {
                builder.AddParagraph("{{#foreach Items}}");
                builder.AddElement(new Paragraph(TextRun("Item: "), VmlTextBoxRun(P("{{Name}}"))));
                builder.AddParagraph("{{/foreach}}");
            },
            new Dictionary<string, object>
            {
                ["Items"] = new List<Dictionary<string, object>>
                {
                    new() { ["Name"] = "One" },
                    new() { ["Name"] = "Two" },
                },
            },
            // Loop cloning duplicates shape ids (true for any shape or drawing in a loop, not
            // specific to text boxes), which the validator reports; content is checked below.
            validate: false);

        Assert.Equal(new[] { "One", "Two" }, TextBoxParagraphTexts(BodyOf(verifier)));
    }

    [Fact]
    public void TextBox_ContainingLoopAndConditional_ProcessesThem()
    {
        using DocumentVerifier verifier = Process(
            builder => builder.AddElement(new Paragraph(
                TextRun("Outer"),
                DrawingMlTextBoxRun(
                    XmlParagraph("{{#foreach Items}}") +
                    XmlParagraph("- {{.}}") +
                    XmlParagraph("{{/foreach}}") +
                    XmlParagraph("{{#if Show}}") +
                    XmlParagraph("Hidden") +
                    XmlParagraph("{{/if}}") +
                    XmlParagraph("{{#if Show}}inline{{#else}}other{{/if}}")))),
            new Dictionary<string, object>
            {
                ["Items"] = new List<string> { "a", "b" },
                ["Show"] = false,
            });

        Paragraph paragraph = verifier.GetParagraph(0);
        List<string> expected = new List<string> { "- a", "- b", "other" };
        Assert.Equal(expected.Concat(expected), TextBoxParagraphTexts(paragraph));

        // The outer paragraph is not mistaken for a marker paragraph and stays intact.
        Assert.Equal("Outer", string.Concat(paragraph.Elements<Run>().SelectMany(r => r.Elements<Text>()).Select(t => t.Text)));
    }

    [Fact]
    public void TextBox_ConditionalRemovingAllContent_LeavesValidTextBox()
    {
        using DocumentVerifier verifier = Process(
            builder => builder.AddElement(new Paragraph(
                VmlTextBoxRun(P("{{#if Show}}"), P("Hidden"), P("{{/if}}")))),
            new Dictionary<string, object> { ["Show"] = false });

        TextBoxContent textBox = Assert.Single(BodyOf(verifier).Descendants<TextBoxContent>());
        Assert.Single(textBox.Elements<Paragraph>());
        Assert.Equal(string.Empty, textBox.InnerText);
    }

    // ---------- Content controls ----------

    [Fact]
    public void BlockSdt_PlaceholderReplaced()
    {
        using DocumentVerifier verifier = Process(
            builder => builder.AddElement(new SdtBlock(new SdtProperties(), new SdtContentBlock(P("Hello {{Name}}")))),
            new Dictionary<string, object> { ["Name"] = "Alice" });

        SdtBlock sdt = Assert.Single(BodyOf(verifier).Elements<SdtBlock>());
        Assert.Equal("Hello Alice", sdt.InnerText);
    }

    [Fact]
    public void BlockSdt_ContainingLoop_ExpandsInsideControl()
    {
        using DocumentVerifier verifier = Process(
            builder => builder.AddElement(new SdtBlock(
                new SdtProperties(),
                new SdtContentBlock(P("Items:"), P("{{#foreach Items}}"), P("- {{.}}"), P("{{/foreach}}")))),
            new Dictionary<string, object> { ["Items"] = new List<string> { "a", "b" } });

        SdtBlock sdt = Assert.Single(BodyOf(verifier).Elements<SdtBlock>());
        Assert.Equal(
            new[] { "Items:", "- a", "- b" },
            sdt.SdtContentBlock!.Elements<Paragraph>().Select(p => p.InnerText));
    }

    [Theory]
    [InlineData(true, new[] { "Always", "Shown" })]
    [InlineData(false, new[] { "Always" })]
    public void BlockSdt_ContainingConditional_EvaluatesInsideControl(bool show, string[] expected)
    {
        using DocumentVerifier verifier = Process(
            builder => builder.AddElement(new SdtBlock(
                new SdtProperties(),
                new SdtContentBlock(P("Always"), P("{{#if Show}}"), P("Shown"), P("{{/if}}")))),
            new Dictionary<string, object> { ["Show"] = show });

        SdtBlock sdt = Assert.Single(BodyOf(verifier).Elements<SdtBlock>());
        Assert.Equal(expected, sdt.SdtContentBlock!.Elements<Paragraph>().Select(p => p.InnerText));
    }

    [Fact]
    public void BlockSdt_WrappingLoopMarkers_StillWorksAsLoop()
    {
        // Content controls around the marker paragraphs (e.g. converted OpenXMLTemplates templates)
        using DocumentVerifier verifier = Process(
            builder =>
            {
                builder.AddElement(new SdtBlock(new SdtProperties(), new SdtContentBlock(P("{{#foreach Items}}"))));
                builder.AddParagraph("- {{.}}");
                builder.AddElement(new SdtBlock(new SdtProperties(), new SdtContentBlock(P("{{/foreach}}"))));
            },
            new Dictionary<string, object> { ["Items"] = new List<string> { "a", "b" } });

        Assert.Equal(new[] { "- a", "- b" }, verifier.GetAllParagraphTexts());
        Assert.Empty(BodyOf(verifier).Elements<SdtBlock>());
    }

    [Fact]
    public void BlockSdt_InTableCell_PlaceholderReplaced()
    {
        using DocumentVerifier verifier = Process(
            builder => builder.AddElement(Table(new TableRow(new TableCell(
                new SdtBlock(new SdtProperties(), new SdtContentBlock(P("Cell {{Name}}"))),
                new Paragraph())))),
            new Dictionary<string, object> { ["Name"] = "Alice" });

        Assert.Equal("Cell Alice", verifier.GetTableCellText(0, 0, 0));
    }

    [Fact]
    public void RowAndCellContentControls_PlaceholdersReplaced()
    {
        using DocumentVerifier verifier = Process(
            builder => builder.AddElement(Table(
                new TableRow(
                    new TableCell(P("Plain {{Name}}")),
                    new SdtCell(new SdtProperties(), new SdtContentCell(new TableCell(P("Cell control {{Name}}"))))),
                new SdtRow(
                    new SdtProperties(),
                    new SdtContentRow(new TableRow(new TableCell(P("Row control {{Name}}"))))))),
            new Dictionary<string, object> { ["Name"] = "Alice" });

        List<string> cellTexts = BodyOf(verifier).Descendants<TableCell>().Select(c => c.InnerText).ToList();
        Assert.Equal(new[] { "Plain Alice", "Cell control Alice", "Row control Alice" }, cellTexts);
    }

    [Fact]
    public void RowContentControl_ContainingRowLoop_Expands()
    {
        using DocumentVerifier verifier = Process(
            builder => builder.AddElement(Table(
                new TableRow(new TableCell(P("Header"))),
                new SdtRow(
                    new SdtProperties(),
                    new SdtContentRow(
                        new TableRow(new TableCell(P("{{#foreach Items}}"))),
                        new TableRow(new TableCell(P("{{.}}"))),
                        new TableRow(new TableCell(P("{{/foreach}}"))))))),
            new Dictionary<string, object> { ["Items"] = new List<string> { "a", "b" } });

        List<string> cellTexts = BodyOf(verifier).Descendants<TableCell>().Select(c => c.InnerText).ToList();
        Assert.Equal(new[] { "Header", "a", "b" }, cellTexts);
    }

    // ---------- Footnotes, endnotes, comments ----------

    [Fact]
    public void Footnote_PlaceholderReplaced()
    {
        using DocumentVerifier verifier = Process(
            builder =>
            {
                FootnotesPart part = builder.MainPart.AddNewPart<FootnotesPart>();
                part.Footnotes = new Footnotes(
                    new Footnote(new Paragraph(new Run(new SeparatorMark()))) { Type = FootnoteEndnoteValues.Separator, Id = -1 },
                    new Footnote(new Paragraph(new Run(new ContinuationSeparatorMark()))) { Type = FootnoteEndnoteValues.ContinuationSeparator, Id = 0 },
                    new Footnote(P("Note {{Name}}"), P("{{#if Show}}"), P("Conditional"), P("{{/if}}")) { Id = 1 });
                builder.AddElement(new Paragraph(TextRun("x"), new Run(new FootnoteReference { Id = 1 })));
            },
            new Dictionary<string, object> { ["Name"] = "Alice", ["Show"] = false });

        Footnotes footnotes = verifier.Document.MainDocumentPart!.FootnotesPart!.Footnotes!;
        Footnote note = footnotes.Elements<Footnote>().Single(f => f.Id?.Value == 1);
        Assert.Equal(new[] { "Note Alice" }, note.Elements<Paragraph>().Select(p => p.InnerText));

        // Separator notes are left untouched.
        Assert.Single(footnotes.Descendants<SeparatorMark>());
        Assert.Single(footnotes.Descendants<ContinuationSeparatorMark>());
    }

    [Fact]
    public void Endnote_PlaceholderAndLoopProcessed()
    {
        using DocumentVerifier verifier = Process(
            builder =>
            {
                EndnotesPart part = builder.MainPart.AddNewPart<EndnotesPart>();
                part.Endnotes = new Endnotes(
                    new Endnote(new Paragraph(new Run(new SeparatorMark()))) { Type = FootnoteEndnoteValues.Separator, Id = -1 },
                    new Endnote(new Paragraph(new Run(new ContinuationSeparatorMark()))) { Type = FootnoteEndnoteValues.ContinuationSeparator, Id = 0 },
                    new Endnote(P("By {{Name}}"), P("{{#foreach Items}}"), P("{{.}}"), P("{{/foreach}}")) { Id = 1 });
                builder.AddElement(new Paragraph(TextRun("x"), new Run(new EndnoteReference { Id = 1 })));
            },
            new Dictionary<string, object> { ["Name"] = "Alice", ["Items"] = new List<string> { "a", "b" } });

        Endnote note = verifier.Document.MainDocumentPart!.EndnotesPart!.Endnotes!
            .Elements<Endnote>().Single(e => e.Id?.Value == 1);
        Assert.Equal(new[] { "By Alice", "a", "b" }, note.Elements<Paragraph>().Select(p => p.InnerText));
    }

    [Fact]
    public void ValidateTemplate_ReportsPlaceholdersInFootnotes()
    {
        DocumentBuilder builder = new DocumentBuilder();
        FootnotesPart part = builder.MainPart.AddNewPart<FootnotesPart>();
        part.Footnotes = new Footnotes(new Footnote(P("Source: {{Source}}")) { Id = 1 });
        builder.AddElement(new Paragraph(TextRun("Body {{Content}}"), new Run(new FootnoteReference { Id = 1 })));

        ValidationResult result = new DocumentTemplateProcessor().ValidateTemplate(
            builder.ToStream(),
            new Dictionary<string, object> { ["Content"] = "x" });

        Assert.Contains("Source", result.AllPlaceholders);
        Assert.Contains("Source", result.MissingVariables);
    }

    [Fact]
    public void Comments_AreNotProcessed()
    {
        using DocumentVerifier verifier = Process(
            builder =>
            {
                WordprocessingCommentsPart part = builder.MainPart.AddNewPart<WordprocessingCommentsPart>();
                part.Comments = new Comments(new Comment(P("Check {{Name}}")) { Id = "0", Author = "Reviewer" });
                builder.AddElement(new Paragraph(
                    new CommentRangeStart { Id = "0" },
                    TextRun("Hello {{Name}}"),
                    new CommentRangeEnd { Id = "0" },
                    new Run(new CommentReference { Id = "0" })));
            },
            new Dictionary<string, object> { ["Name"] = "Alice" });

        Assert.Equal("Hello Alice", verifier.GetParagraphText(0));
        Comments comments = verifier.Document.MainDocumentPart!.WordprocessingCommentsPart!.Comments!;
        Assert.Equal("Check {{Name}}", comments.InnerText);
    }
}
