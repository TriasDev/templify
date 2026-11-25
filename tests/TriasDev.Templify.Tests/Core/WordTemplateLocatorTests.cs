// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Core;

public sealed class WordTemplateLocatorTests
{
    [Fact]
    public void Word_BodyParagraph() =>
        Assert.Equal("body, paragraph 3: 'Hello {{Na'", Describe(new DocumentBuilder().AddParagraph("a").AddParagraph("b").AddParagraph("Hello {{Na"), "Hello {{Na"));

    [Fact]
    public void Word_TableCellParagraph()
    {
        DocumentBuilder builder = new DocumentBuilder().AddTableWithCellParagraphs(2, 3, (r, c) => r == 1 && c == 2 ? ["x"] : ["-"]);

        Assert.Equal("body, table 1, row 2, cell 3, paragraph 1: 'x'", Describe(builder, "x"));
    }

    [Fact]
    public void Word_Row()
    {
        DocumentBuilder builder = new DocumentBuilder().AddTableWithCellParagraphs(1, 2, (_, c) => c == 0 ? ["{{#foreach Items}}"] : ["H"]);

        Assert.Equal("body, table 1, row 1: '{{#foreach Items}}H'", Describe(builder, doc => Body(doc).Descendants<TableRow>().First()));
    }

    [Fact]
    public void Word_NestedTable()
    {
        Table inner = new Table(new TableRow(new TableCell(P("n"))));
        Table outer = new Table(new TableRow(new TableCell(inner, P(""))));

        Assert.Equal(
            "body, table 1, row 1, cell 1, table 1, row 1, cell 1, paragraph 1: 'n'",
            Describe(new DocumentBuilder().AddElement(outer), "n"));
    }

    [Fact]
    public void Word_HeaderDefault() =>
        Assert.Equal("header (default), paragraph 1: 'h'",
            Describe(new DocumentBuilder().AddParagraph("b").AddHeaderWithParagraphs(HeaderFooterValues.Default, "h"), "h"));

    [Fact]
    public void Word_HeaderFirst() =>
        Assert.Equal("header (first), paragraph 1: 'f'",
            Describe(new DocumentBuilder().AddParagraph("b").AddHeaderWithParagraphs(HeaderFooterValues.First, "f"), "f"));

    [Fact]
    public void Word_FooterEven() =>
        Assert.Equal("footer (even), paragraph 2: 'f2'",
            Describe(new DocumentBuilder().AddParagraph("b").AddFooterWithParagraphs(HeaderFooterValues.Even, "f1", "f2"), "f2"));

    [Fact]
    public void Word_SecondHeaderPartSameType()
    {
        DocumentBuilder builder = new DocumentBuilder();
        HeaderPart first = builder.MainPart.AddNewPart<HeaderPart>();
        first.Header = new Header(P("h1"));
        HeaderPart second = builder.MainPart.AddNewPart<HeaderPart>();
        second.Header = new Header(P("h2"));
        builder.AddElement(new Paragraph(
            new ParagraphProperties(new SectionProperties(new HeaderReference { Type = HeaderFooterValues.Default, Id = builder.MainPart.GetIdOfPart(first) })),
            new Run(new Text("section 1"))));
        builder.AddElement(new SectionProperties(new HeaderReference { Type = HeaderFooterValues.Default, Id = builder.MainPart.GetIdOfPart(second) }));

        Assert.Equal("header (default), paragraph 1: 'h1'", Describe(builder, "h1"));
    }

    [Fact]
    public void Word_SecondHeaderPartSameType_IsNumbered()
    {
        DocumentBuilder builder = new DocumentBuilder();
        HeaderPart first = builder.MainPart.AddNewPart<HeaderPart>();
        first.Header = new Header(P("h1"));
        HeaderPart second = builder.MainPart.AddNewPart<HeaderPart>();
        second.Header = new Header(P("h2"));
        builder.AddElement(new Paragraph(
            new ParagraphProperties(new SectionProperties(new HeaderReference { Type = HeaderFooterValues.Default, Id = builder.MainPart.GetIdOfPart(first) })),
            new Run(new Text("section 1"))));
        builder.AddElement(new SectionProperties(new HeaderReference { Type = HeaderFooterValues.Default, Id = builder.MainPart.GetIdOfPart(second) }));

        Assert.Equal("header 2 (default), paragraph 1: 'h2'", Describe(builder, "h2"));
    }

    [Fact]
    public void Word_Footnote()
    {
        DocumentBuilder builder = new DocumentBuilder();
        FootnotesPart part = builder.MainPart.AddNewPart<FootnotesPart>();
        part.Footnotes = new Footnotes(
            new Footnote(new Paragraph(new Run(new SeparatorMark()))) { Type = FootnoteEndnoteValues.Separator, Id = -1 },
            new Footnote(P("n")) { Id = 1 });
        builder.AddElement(new Paragraph(new Run(new Text("b")), new Run(new FootnoteReference { Id = 1 })));

        Assert.Equal("footnote 1, paragraph 1: 'n'", Describe(builder, "n"));
    }

    [Fact]
    public void Word_TextBox()
    {
        Paragraph anchor = new Paragraph(
            new Run(new Text("anchor")),
            new Run(new Picture(new DocumentFormat.OpenXml.Vml.Shape(
                new DocumentFormat.OpenXml.Vml.TextBox(new TextBoxContent(P("t"))))
            { Id = "tb1", Style = "width:72pt;height:36pt" })));

        Assert.Equal("body, paragraph 2, text box 1, paragraph 1: 't'", Describe(new DocumentBuilder().AddParagraph("a").AddElement(anchor), "t"));
    }

    [Fact]
    public void Word_ContentControl() =>
        Assert.Equal("body, content control 1, paragraph 2: 'c'",
            Describe(new DocumentBuilder().AddElement(new SdtBlock(new SdtProperties(), new SdtContentBlock(P("x"), P("c")))), "c"));

    [Fact]
    public void Word_LongText_IsCut()
    {
        string text = new string('a', 150);

        string location = Describe(new DocumentBuilder().AddParagraph(text), text);

        Assert.Equal("body, paragraph 1: '" + new string('a', 100) + "…'", location);
    }

    private static Paragraph P(string text) => new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    private static Body Body(WordprocessingDocument document) => document.MainDocumentPart!.Document!.Body!;

    private static string Describe(DocumentBuilder builder, string paragraphText) =>
        Describe(builder, doc => doc.MainDocumentPart!.RootElements()
            .SelectMany(root => root.Descendants<Paragraph>())
            .First(p => p.InnerText == paragraphText));

    private static string Describe(DocumentBuilder builder, Func<WordprocessingDocument, OpenXmlElement> find)
    {
        using MemoryStream stream = builder.ToStream();
        using WordprocessingDocument document = WordprocessingDocument.Open(stream, false);
        TriasDev.Templify.Core.WordTemplateLocator locator = new TriasDev.Templify.Core.WordTemplateLocator(document);
        return locator.Describe(find(document));
    }
}

internal static class MainDocumentPartTestExtensions
{
    /// <summary>The body, headers, footers and notes of the document.</summary>
    public static IEnumerable<OpenXmlElement> RootElements(this MainDocumentPart main)
    {
        yield return main.Document!.Body!;
        foreach (HeaderPart header in main.HeaderParts)
        {
            yield return header.Header!;
        }

        foreach (FooterPart footer in main.FooterParts)
        {
            yield return footer.Footer!;
        }

        if (main.FootnotesPart?.Footnotes != null)
        {
            yield return main.FootnotesPart.Footnotes;
        }
    }
}
