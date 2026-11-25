// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Odt;

namespace TriasDev.Templify.Builder.Tests;

public sealed class StyleSheetTests
{
    private static readonly XNamespace _w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace _style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace _text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace _fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
    private static readonly XNamespace _office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace _dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace _meta = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";

    private static readonly ParagraphStyleKey _body = new ParagraphStyleKey("Body", "Body Copy");
    private static readonly ParagraphStyleKey _heading = new ParagraphStyleKey("Heading1", "Heading One");
    private static readonly ParagraphStyleKey _subHeading = new ParagraphStyleKey("Heading2", "Heading Two");
    private static readonly ParagraphStyleKey _note = new ParagraphStyleKey("Note", "Note");
    private static readonly CharacterStyleKey _strong = new CharacterStyleKey("Strong", "Strong Text");

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private static StyleSheet Sheet() => StyleSheet.Create(s =>
    {
        s.Defaults(d => d.Font(new FontFace("Liberation Sans")).Size(10.5.Pt()).Color(Color.FromHex("171717")));
        s.Paragraph(_body, p => p.SpaceAfter(8.Pt()).MinimumLineHeight(15.Pt()));
        s.Paragraph(_heading, p => p.BasedOn(_body).Next(_body).Font(new FontFace("Liberation Serif")).Size(16.Pt())
            .Bold().Color(Color.FromHex("#1F4E79")).SpaceBefore(20.Pt()).KeepWithNext().KeepLinesTogether().OutlineLevel(1));
        // Inherits its outline level from nowhere: level 2 set here; the level 1 heading's other properties inherited.
        s.Paragraph(_subHeading, p => p.BasedOn(_heading).Size(13.Pt()).OutlineLevel(2));
        s.Paragraph(_note, p => p.BasedOn(_body).Italic().Alignment(TextAlignment.Justify).IndentStart(10.Mm())
            .IndentEnd(5.Mm()).FirstLineIndent(-4.5.Mm()).LineSpacing(1.15));
        s.Character(_strong, c => c.Bold().Color(Color.FromRgb(0x15, 0x80, 0x3D)));
    });

    private static TestDefinition Document() => new TestDefinition(template =>
    {
        template.Styles(Sheet());
        template.Properties(p => p.Title("Order confirmation").Author("Example Ltd").Subject("Orders"));
        TemplatePath order = template.Data["order"];
        template.Content(c =>
        {
            c.Paragraph(_heading, "Order");
            c.Paragraph(_subHeading, order["number"]);
            c.Paragraph(_body, x =>
            {
                x.Text("Customer: ");
                x.Value(order["customer"]);
            });
            c.Paragraph(_note, "All prices include VAT.");
            c.Paragraph("Unstyled");
        });
    });

    private const string OrderJson = """{ "order": { "number": "A-17", "customer": "Alice" } }""";

    [Theory]
    [MemberData(nameof(Formats))]
    public void StyledDocument_IsFilledByTemplify(TemplateFormat format)
    {
        (_, byte[] filled) = BuilderHarness.BuildAndFill(Document(), format, OrderJson);

        Assert.Equal(new[] { "Order", "A-17", "Customer: Alice", "All prices include VAT.", "Unstyled" }, BuilderHarness.ParagraphTexts(format, filled));
    }

    [Fact]
    public void Docx_WritesNamedStylesAndReferencesThem()
    {
        byte[] package = BuilderHarness.Build(Document(), TemplateFormat.Docx).Content.ToArray();
        XElement styles = BuilderHarness.ReadXml(package, "word/styles.xml").Root!;
        XElement Style(string id) => styles.Elements(_w + "style").Single(s => (string?)s.Attribute(_w + "styleId") == id);

        Assert.Equal(
            new[] { "Normal", "DefaultParagraphFont", "Body", "Heading1", "Heading2", "Note", "Strong" },
            styles.Elements(_w + "style").Select(s => (string?)s.Attribute(_w + "styleId")));
        Assert.Equal("Heading One", Style("Heading1").Element(_w + "name")!.Attribute(_w + "val")!.Value);
        Assert.Equal("Body", Style("Heading1").Element(_w + "basedOn")!.Attribute(_w + "val")!.Value);
        Assert.Equal("Body", Style("Heading1").Element(_w + "next")!.Attribute(_w + "val")!.Value);
        Assert.Equal("Normal", Style("Body").Element(_w + "basedOn")!.Attribute(_w + "val")!.Value);
        Assert.Equal("DefaultParagraphFont", Style("Strong").Element(_w + "basedOn")!.Attribute(_w + "val")!.Value);

        XElement heading = Style("Heading1");
        Assert.Equal("Liberation Serif", heading.Element(_w + "rPr")!.Element(_w + "rFonts")!.Attribute(_w + "ascii")!.Value);
        Assert.Equal("32", heading.Element(_w + "rPr")!.Element(_w + "sz")!.Attribute(_w + "val")!.Value);
        Assert.Equal("1F4E79", heading.Element(_w + "rPr")!.Element(_w + "color")!.Attribute(_w + "val")!.Value);
        Assert.NotNull(heading.Element(_w + "rPr")!.Element(_w + "b"));
        Assert.NotNull(heading.Element(_w + "pPr")!.Element(_w + "keepNext"));
        Assert.NotNull(heading.Element(_w + "pPr")!.Element(_w + "keepLines"));
        Assert.Equal("400", heading.Element(_w + "pPr")!.Element(_w + "spacing")!.Attribute(_w + "before")!.Value);
        Assert.Equal("0", heading.Element(_w + "pPr")!.Element(_w + "outlineLvl")!.Attribute(_w + "val")!.Value);

        XElement body = Style("Body").Element(_w + "pPr")!.Element(_w + "spacing")!;
        Assert.Equal("160", body.Attribute(_w + "after")!.Value);
        Assert.Equal("300", body.Attribute(_w + "line")!.Value);
        Assert.Equal("atLeast", body.Attribute(_w + "lineRule")!.Value);

        XElement note = Style("Note").Element(_w + "pPr")!;
        Assert.Equal("276", note.Element(_w + "spacing")!.Attribute(_w + "line")!.Value);
        Assert.Equal("auto", note.Element(_w + "spacing")!.Attribute(_w + "lineRule")!.Value);
        Assert.Equal("567", note.Element(_w + "ind")!.Attribute(_w + "left")!.Value);
        Assert.Equal("283", note.Element(_w + "ind")!.Attribute(_w + "right")!.Value);
        Assert.Equal("255", note.Element(_w + "ind")!.Attribute(_w + "hanging")!.Value);
        Assert.Equal("both", note.Element(_w + "jc")!.Attribute(_w + "val")!.Value);

        XElement defaults = styles.Element(_w + "docDefaults")!.Element(_w + "rPrDefault")!.Element(_w + "rPr")!;
        Assert.Equal("Liberation Sans", defaults.Element(_w + "rFonts")!.Attribute(_w + "ascii")!.Value);
        Assert.Equal("21", defaults.Element(_w + "sz")!.Attribute(_w + "val")!.Value);
        Assert.Equal("en-US", defaults.Element(_w + "lang")!.Attribute(_w + "val")!.Value);

        XElement documentBody = BuilderHarness.ReadXml(package, "word/document.xml").Root!.Element(_w + "body")!;
        Assert.Equal(
            new string?[] { "Heading1", "Heading2", "Body", "Note", null },
            documentBody.Elements(_w + "p").Select(p => (string?)p.Element(_w + "pPr")?.Element(_w + "pStyle")?.Attribute(_w + "val")));
        Assert.Empty(documentBody.Descendants(_w + "rPr"));
    }

    [Fact]
    public void Odt_WritesNamedStylesAndHeadings()
    {
        byte[] package = BuilderHarness.Build(Document(), TemplateFormat.Odt).Content.ToArray();
        XElement styles = BuilderHarness.ReadXml(package, "styles.xml").Root!.Element(_office + "styles")!;
        XElement Style(string name) => styles.Elements(_style + "style").Single(s => (string?)s.Attribute(_style + "name") == name);

        Assert.Equal(
            new[] { "Standard", "Body", "Heading1", "Heading2", "Note", "Strong" },
            styles.Elements(_style + "style").Select(s => (string?)s.Attribute(_style + "name")));
        Assert.Equal("Heading One", Style("Heading1").Attribute(_style + "display-name")!.Value);
        Assert.Equal("Body", Style("Heading1").Attribute(_style + "parent-style-name")!.Value);
        Assert.Equal("Body", Style("Heading1").Attribute(_style + "next-style-name")!.Value);
        Assert.Equal("1", Style("Heading1").Attribute(_style + "default-outline-level")!.Value);
        Assert.Equal("Standard", Style("Body").Attribute(_style + "parent-style-name")!.Value);
        Assert.Equal("text", Style("Strong").Attribute(_style + "family")!.Value);

        XElement headingText = Style("Heading1").Element(_style + "text-properties")!;
        Assert.Equal("Liberation Serif", headingText.Attribute(_style + "font-name")!.Value);
        Assert.Equal("16pt", headingText.Attribute(_fo + "font-size")!.Value);
        Assert.Equal("#1f4e79", headingText.Attribute(_fo + "color")!.Value);
        Assert.Equal("bold", headingText.Attribute(_fo + "font-weight")!.Value);
        XElement headingParagraph = Style("Heading1").Element(_style + "paragraph-properties")!;
        Assert.Equal("20pt", headingParagraph.Attribute(_fo + "margin-top")!.Value);
        Assert.Equal("always", headingParagraph.Attribute(_fo + "keep-with-next")!.Value);
        Assert.Equal("always", headingParagraph.Attribute(_fo + "keep-together")!.Value);

        XElement bodyParagraph = Style("Body").Element(_style + "paragraph-properties")!;
        Assert.Equal("8pt", bodyParagraph.Attribute(_fo + "margin-bottom")!.Value);
        Assert.Equal("15pt", bodyParagraph.Attribute(_style + "line-height-at-least")!.Value);

        XElement noteParagraph = Style("Note").Element(_style + "paragraph-properties")!;
        Assert.Equal("115%", noteParagraph.Attribute(_fo + "line-height")!.Value);
        Assert.Equal("justify", noteParagraph.Attribute(_fo + "text-align")!.Value);
        Assert.Equal("28.346pt", noteParagraph.Attribute(_fo + "margin-left")!.Value);
        Assert.Equal("-12.756pt", noteParagraph.Attribute(_fo + "text-indent")!.Value);

        XElement defaults = styles.Element(_style + "default-style")!.Element(_style + "text-properties")!;
        Assert.Equal("Liberation Sans", defaults.Attribute(_style + "font-name")!.Value);
        Assert.Equal("10.5pt", defaults.Attribute(_fo + "font-size")!.Value);
        Assert.Equal("en", defaults.Attribute(_fo + "language")!.Value);
        Assert.Equal("US", defaults.Attribute(_fo + "country")!.Value);

        XElement fonts = BuilderHarness.ReadXml(package, "styles.xml").Root!.Element(_office + "font-face-decls")!;
        Assert.Equal(new[] { "Liberation Sans", "Liberation Serif" }, fonts.Elements().Select(f => (string?)f.Attribute(_style + "name")));

        XElement text = BuilderHarness.ReadXml(package, "content.xml").Root!.Element(_office + "body")!.Element(_office + "text")!;
        List<XElement> paragraphs = text.Elements().ToList();
        Assert.Equal(new[] { "h", "h", "p", "p", "p" }, paragraphs.Select(p => p.Name.LocalName));
        Assert.Equal(new[] { "Heading1", "Heading2", "Body", "Note", "Standard" }, paragraphs.Select(p => (string?)p.Attribute(_text + "style-name")));
        Assert.Equal(new[] { "1", "2" }, paragraphs.Take(2).Select(p => (string?)p.Attribute(_text + "outline-level")));
    }

    [Fact]
    public void Odt_HeadingLevelIsInheritedThroughBasedOn()
    {
        ParagraphStyleKey parent = new ParagraphStyleKey("Parent", "Parent");
        ParagraphStyleKey child = new ParagraphStyleKey("Child", "Child");
        TestDefinition definition = new TestDefinition(template =>
        {
            template.Styles(StyleSheet.Create(s =>
            {
                s.Paragraph(parent, p => p.OutlineLevel(3));
                s.Paragraph(child, p => p.BasedOn(parent).Bold());
            }));
            template.Content(c => c.Paragraph(child, "Inherited heading"));
        });

        byte[] package = BuilderHarness.Build(definition, TemplateFormat.Odt).Content.ToArray();
        XElement paragraph = BuilderHarness.ReadXml(package, "content.xml").Root!.Element(_office + "body")!.Element(_office + "text")!.Elements().Single();

        Assert.Equal("h", paragraph.Name.LocalName);
        Assert.Equal("3", paragraph.Attribute(_text + "outline-level")!.Value);
    }

    [Theory]
    [InlineData("de-DE", "de", "DE")]
    [InlineData("en", "en", null)]
    public void Language_IsWrittenInTheDefaults(string culture, string language, string? country)
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c => c.Paragraph("x")));

        byte[] docx = TemplateBuilder.Build(definition, BuilderHarness.Options(TemplateFormat.Docx, culture)).Content.ToArray();
        XElement docxDefaults = BuilderHarness.ReadXml(docx, "word/styles.xml").Root!.Element(_w + "docDefaults")!;
        Assert.Equal(culture, docxDefaults.Descendants(_w + "lang").Single().Attribute(_w + "val")!.Value);

        byte[] odt = TemplateBuilder.Build(definition, BuilderHarness.Options(TemplateFormat.Odt, culture)).Content.ToArray();
        XElement odtDefaults = BuilderHarness.ReadXml(odt, "styles.xml").Root!.Element(_office + "styles")!.Element(_style + "default-style")!.Element(_style + "text-properties")!;
        Assert.Equal(language, odtDefaults.Attribute(_fo + "language")!.Value);
        Assert.Equal(country, (string?)odtDefaults.Attribute(_fo + "country"));
    }

    [Fact]
    public void DocumentProperties_AreWrittenInBothFormats()
    {
        byte[] docx = BuilderHarness.Build(Document(), TemplateFormat.Docx).Content.ToArray();
        XElement core = BuilderHarness.ReadXml(docx, "docProps/core.xml").Root!;
        Assert.Equal("Order confirmation", core.Element(_dc + "title")!.Value);
        Assert.Equal("Example Ltd", core.Element(_dc + "creator")!.Value);
        Assert.Equal("Orders", core.Element(_dc + "subject")!.Value);

        byte[] odt = BuilderHarness.Build(Document(), TemplateFormat.Odt).Content.ToArray();
        XElement meta = BuilderHarness.ReadXml(odt, "meta.xml").Root!.Element(_office + "meta")!;
        Assert.Equal("Order confirmation", meta.Element(_dc + "title")!.Value);
        Assert.Equal("Example Ltd", meta.Element(_meta + "initial-creator")!.Value);
        Assert.Equal("Orders", meta.Element(_dc + "subject")!.Value);
    }

    [Fact]
    public void Build_ReportsEveryStyleProblem()
    {
        ParagraphStyleKey a = new ParagraphStyleKey("A", "Alpha");
        ParagraphStyleKey b = new ParagraphStyleKey("B", "Beta");
        ParagraphStyleKey alsoAlpha = new ParagraphStyleKey("C", "Alpha");
        ParagraphStyleKey missing = new ParagraphStyleKey("Missing", "Missing");
        CharacterStyleKey clash = new CharacterStyleKey("A", "Clash");

        TestDefinition definition = new TestDefinition(template =>
        {
            template.Styles(StyleSheet.Create(s =>
            {
                s.Paragraph(a, p => p.BasedOn(b));
                s.Paragraph(b, p => p.BasedOn(a).Next(missing));
                s.Paragraph(alsoAlpha, p => p.BasedOn(missing));
                s.Character(clash, c => c.Bold());
            }));
            template.Styles(StyleSheet.Create(_ => { }));
            template.Content(c =>
            {
                c.Paragraph(missing, "uses an undefined style");
                c.Paragraph(missing, "reported once");
            });
        });

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(
            () => TemplateBuilder.Build(definition, BuilderHarness.Options(TemplateFormat.Odt)));

        Assert.Equal(
            new[] { "TB2000", "TB2001", "TB2002", "TB2003", "TB2004", "TB2005", "TB2010" },
            exception.Errors.Select(e => e.Code).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Build_StyleWithoutStyleSheet_IsAnError()
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c => c.Paragraph(_body, "x")));

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(
            () => TemplateBuilder.Build(definition, BuilderHarness.Options(TemplateFormat.Docx)));

        Assert.Contains("'Body'", Assert.Single(exception.Errors).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1Heading")]
    [InlineData("Heading 1")]
    [InlineData("Ünicode")]
    [InlineData("Normal")]
    [InlineData("standard")]
    [InlineData("DefaultParagraphFont")]
    public void StyleKey_InvalidOrReservedId_Throws(string id)
    {
        Assert.Throws<ArgumentException>(() => new ParagraphStyleKey(id, "Name"));
        Assert.Throws<ArgumentException>(() => new CharacterStyleKey(id, "Name"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Tab\tName")]
    public void StyleKey_InvalidDisplayName_Throws(string displayName)
    {
        Assert.Throws<ArgumentException>(() => new ParagraphStyleKey("Valid_id-1", displayName));
    }

    [Fact]
    public void StyleValues_OutOfRange_ThrowWhereWritten()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StyleSheet.Create(s => s.Paragraph(_body, p => p.Size(0.Pt()))));
        Assert.Throws<ArgumentOutOfRangeException>(() => StyleSheet.Create(s => s.Defaults(d => d.Size(2000.Pt()))));
        Assert.Throws<ArgumentOutOfRangeException>(() => StyleSheet.Create(s => s.Paragraph(_body, p => p.SpaceAfter(-1.Pt()))));
        Assert.Throws<ArgumentOutOfRangeException>(() => StyleSheet.Create(s => s.Paragraph(_body, p => p.LineHeight(Length.Zero))));
        Assert.Throws<ArgumentOutOfRangeException>(() => StyleSheet.Create(s => s.Paragraph(_body, p => p.LineSpacing(0.1))));
        Assert.Throws<ArgumentOutOfRangeException>(() => StyleSheet.Create(s => s.Paragraph(_body, p => p.OutlineLevel(0))));
        Assert.Throws<ArgumentOutOfRangeException>(() => StyleSheet.Create(s => s.Paragraph(_body, p => p.OutlineLevel(10))));
        Assert.Throws<ArgumentOutOfRangeException>(() => StyleSheet.Create(s => s.Paragraph(_body, p => p.Alignment((TextAlignment)42))));
    }

    [Fact]
    public void StyleComposers_UsedAfterCreate_Throw()
    {
        StyleSheetComposer? sheet = null;
        ParagraphStyleComposer? paragraph = null;
        CharacterStyleComposer? character = null;
        DefaultStyleComposer? defaults = null;
        StyleSheet.Create(s =>
        {
            sheet = s;
            s.Defaults(d => defaults = d);
            s.Paragraph(_body, p => paragraph = p);
            s.Character(_strong, c => character = c);
        });

        Assert.Throws<InvalidOperationException>(() => sheet!.Paragraph(_note, _ => { }));
        Assert.Throws<InvalidOperationException>(() => paragraph!.Bold());
        Assert.Throws<InvalidOperationException>(() => character!.Italic());
        Assert.Throws<InvalidOperationException>(() => defaults!.Size(12.Pt()));
    }

    [Fact]
    public void Units_ConvertAndRound()
    {
        Assert.Equal(Length.FromPoints(12), 12.Pt());
        Assert.Equal(72.Pt(), 25.4.Mm());
        Assert.Equal("10.5pt", 10.5.Pt().ToString());
        Assert.Equal("28.346pt", 10.Mm().ToString());
        Assert.Equal(12.5, (10.Pt() + 2.5.Pt()).Points);
        Assert.Equal(-3.Pt(), -(3.Pt()));
        Assert.True(1.Mm() > 2.Pt());
        Assert.Equal(25.4, 72.Pt().Millimeters, 6);
        Assert.Throws<ArgumentOutOfRangeException>(() => Length.FromPoints(double.NaN));
    }

    [Theory]
    [InlineData("1F4E79")]
    [InlineData("#1f4e79")]
    public void Color_ParsesHex(string hex)
    {
        Color color = Color.FromHex(hex);

        Assert.Equal(Color.FromRgb(0x1F, 0x4E, 0x79), color);
        Assert.Equal("#1F4E79", color.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("12345G")]
    [InlineData("#1234567")]
    public void Color_InvalidHex_Throws(string hex)
    {
        Assert.Throws<ArgumentException>(() => Color.FromHex(hex));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" Padded")]
    [InlineData("It's")]
    public void FontFace_InvalidFamily_Throws(string family)
    {
        Assert.Throws<ArgumentException>(() => new FontFace(family));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    [Trait("Category", "LibreOffice")]
    public void LibreOffice_KeepsTheNamedStylesWhenConverting(TemplateFormat format)
    {
        LibreOfficeRunner.RequireExecutable();
        byte[] template = BuilderHarness.Build(Document(), format).Content.ToArray();

        if (format == TemplateFormat.Docx)
        {
            // DOCX → ODT: LibreOffice read our styles and writes them under their display names.
            byte[] converted = LibreOfficeRunner.Convert(template, "docx", "odt", "odt");
            XElement styles = BuilderHarness.ReadXml(converted, "styles.xml").Root!.Element(_office + "styles")!;
            List<string> names = styles.Elements(_style + "style")
                .Select(s => (string?)s.Attribute(_style + "display-name") ?? (string)s.Attribute(_style + "name")!)
                .ToList();
            Assert.Contains("Heading One", names);
            Assert.Contains("Body Copy", names);
            Assert.Contains("Strong Text", names);

            XElement heading = styles.Elements(_style + "style").First(s => ((string?)s.Attribute(_style + "display-name") ?? (string?)s.Attribute(_style + "name")) == "Heading One");
            Assert.Equal("16pt", heading.Element(_style + "text-properties")!.Attribute(_fo + "font-size")!.Value);
        }
        else
        {
            // ODT → DOCX: Word sees the display names as style names.
            byte[] converted = LibreOfficeRunner.Convert(template, "odt", "docx:MS Word 2007 XML", "docx");
            XElement styles = BuilderHarness.ReadXml(converted, "word/styles.xml").Root!;
            List<string> names = styles.Elements(_w + "style").Select(s => s.Element(_w + "name")!.Attribute(_w + "val")!.Value).ToList();
            Assert.Contains("Heading One", names);
            Assert.Contains("Body Copy", names);
            Assert.Contains("Strong Text", names);
        }

        (_, byte[] filled) = BuilderHarness.BuildAndFill(Document(), format, OrderJson);
        Assert.NotEmpty(LibreOfficeRunner.Convert(filled, format == TemplateFormat.Docx ? "docx" : "odt", "pdf", "pdf"));
    }
}
