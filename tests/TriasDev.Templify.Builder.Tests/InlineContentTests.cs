// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Odt;

namespace TriasDev.Templify.Builder.Tests;

public sealed partial class InlineContentTests
{
    private static readonly XNamespace _w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace _text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace _style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace _fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
    private static readonly XNamespace _office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";

    private static readonly CharacterStyleKey _strong = new CharacterStyleKey("Strong", "Strong Text");

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private static readonly (string Name, ValueFormat Format, string Path)[] _formats =
    {
        ("date", ValueFormat.Date("yyyy-MM-dd"), "date"),
        ("date with spaces and colon", ValueFormat.Date("MMMM d, yyyy HH:mm"), "date"),
        ("number", ValueFormat.Number("N2"), "amount"),
        ("currency", ValueFormat.Currency, "amount"),
        ("uppercase", ValueFormat.Uppercase, "name"),
        ("lowercase", ValueFormat.Lowercase, "name"),
        ("raw", ValueFormat.Raw, "markdown"),
        ("yesno", ValueFormat.YesNo, "flag"),
        ("truefalse", ValueFormat.TrueFalse, "flag"),
        ("onoff", ValueFormat.OnOff, "flag"),
        ("enabled", ValueFormat.Enabled, "flag"),
        ("active", ValueFormat.Active, "flag"),
        ("checkbox", ValueFormat.Checkbox, "flag"),
        ("checkmark", ValueFormat.Checkmark, "flag"),
        ("join", ValueFormat.Join(), "tags"),
        ("join with separator", ValueFormat.Join(separator: " | "), "tags"),
        ("join item path", ValueFormat.Join("owner.name"), "assets"),
        ("join item path and quoted separator", ValueFormat.Join("owner.name", "\"; \\"), "assets"),
        ("join without separator", ValueFormat.Join(separator: ""), "tags"),
        ("custom", ValueFormat.Custom("yesno"), "flag"),
    };

    private static Dictionary<string, object> FormatData() => new Dictionary<string, object>
    {
        ["date"] = new DateTime(2026, 11, 25, 14, 30, 0),
        ["amount"] = 1234.5m,
        ["name"] = "Alice Example",
        ["markdown"] = "**not bold**",
        ["flag"] = true,
        ["tags"] = new List<object?> { "alpha", null, "", "beta" },
        ["assets"] = new List<object>
        {
            new Dictionary<string, object> { ["owner"] = new Dictionary<string, object> { ["name"] = "Ann" } },
            new Dictionary<string, object> { ["owner"] = new Dictionary<string, object> { ["name"] = "Bob" } },
        },
    };

    [Theory]
    [MemberData(nameof(Formats))]
    public void ValueFormats_RenderAsTheEngineDefinesThem(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c =>
        {
            foreach ((string _, ValueFormat valueFormat, string path) in _formats)
            {
                c.Paragraph(x => x.Value(template.Data[path], valueFormat));
            }
        }));

        (GeneratedTemplate built, byte[] filled) = BuilderHarness.BuildAndFill(definition, format, FormatData());

        TextTemplateProcessor oracle = new TextTemplateProcessor(BuilderHarness.FillOptions);
        List<string> expected = _formats
            .Select(f => oracle.ProcessTemplate($"{{{{{f.Path}:{f.Format.Specifier}}}}}", FormatData()).ProcessedText)
            .ToList();
        List<string> actual = BuilderHarness.ParagraphTexts(format, filled);

        Assert.Equal(_formats.Select(f => $"{{{{{f.Path}:{f.Format.Specifier}}}}}"), BuilderHarness.ParagraphTexts(format, built.Content.ToArray()));
        for (int i = 0; i < _formats.Length; i++)
        {
            Assert.True(expected[i] == actual[i], $"{_formats[i].Name}: expected '{expected[i]}', got '{actual[i]}'");
        }

        // Spot checks, so the oracle cannot drift silently.
        Assert.Equal("2026-11-25", actual[0]);
        Assert.Equal("1,234.50", actual[2]);
        Assert.Equal("ALICE EXAMPLE", actual[4]);
        Assert.Equal("**not bold**", actual[6]);
        Assert.Equal("☑", actual[12]);
        Assert.Equal("alpha, beta", actual[14]);
        Assert.Equal("alpha | beta", actual[15]);
        Assert.Equal("Ann, Bob", actual[16]);
        Assert.Equal("Ann\"; \\Bob", actual[17]);
        Assert.Equal("alphabeta", actual[18]);
    }

    [Fact]
    public void ValueFormat_Specifiers()
    {
        Assert.Equal("date:yyyy-MM-dd", ValueFormat.Date("yyyy-MM-dd").Specifier);
        Assert.Equal("number:N2", ValueFormat.Number("N2").ToString());
        Assert.Equal("join", ValueFormat.Join().Specifier);
        Assert.Equal("join:owner.name", ValueFormat.Join("owner.name").Specifier);
        Assert.Equal("join:\"; \"", ValueFormat.Join(separator: "; ").Specifier);
        Assert.Equal("join:a:\"\\\"\"", ValueFormat.Join("a", "\"").Specifier);
        Assert.Equal("mine:arg", ValueFormat.Custom("mine:arg").Specifier);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a}b")]
    [InlineData("a{{b")]
    [InlineData("tab\there")]
    public void ValueFormat_InvalidPattern_Throws(string pattern)
    {
        Assert.Throws<ArgumentException>(() => ValueFormat.Date(pattern));
        Assert.Throws<ArgumentException>(() => ValueFormat.Number(pattern));
    }

    [Fact]
    public void ValueFormat_InvalidJoinOrCustom_Throws()
    {
        Assert.Throws<ArgumentException>(() => ValueFormat.Join("owner name"));
        Assert.Throws<ArgumentException>(() => ValueFormat.Join("owner."));
        Assert.Throws<ArgumentException>(() => ValueFormat.Join("items[0]"));
        Assert.Throws<ArgumentException>(() => ValueFormat.Join(separator: "a}"));
        Assert.Throws<ArgumentException>(() => ValueFormat.Join(separator: "\u201C"));
        Assert.Throws<ArgumentException>(() => ValueFormat.Custom(""));
        Assert.Throws<ArgumentException>(() => ValueFormat.Custom("my-format"));
        Assert.Throws<ArgumentException>(() => ValueFormat.Custom("mine:a}"));
    }

    private static TestDefinition StyledText() => new TestDefinition(template =>
    {
        template.Styles(StyleSheet.Create(s => s.Character(_strong, c => c.Bold())));
        TemplatePath customer = template.Data["customer"];
        template.Content(c => c.Paragraph(x =>
        {
            x.Text("Dear ");
            x.Value(_strong, customer["name"]);
            x.Text(", total ");
            x.Value(_strong, customer["total"], ValueFormat.Number("F2"));
            x.Text(_strong, " ok ");
            x.Tab();
            x.Text("end");
            x.LineBreak();
            x.Text("next line ");
        }));
    });

    private const string CustomerJson = """{ "customer": { "name": "Alice", "total": 12.5 } }""";

    [Fact]
    public void CharacterStyles_Docx_AreRunStylesAndSurviveFilling()
    {
        (GeneratedTemplate built, byte[] filled) = BuilderHarness.BuildAndFill(StyledText(), TemplateFormat.Docx, CustomerJson);

        Assert.Equal(new[] { "Dear Alice, total 12.50 ok \tend\nnext line " }, BuilderHarness.ParagraphTexts(TemplateFormat.Docx, filled));

        List<XElement> runs = BuilderHarness.ReadXml(built.Content.ToArray(), "word/document.xml").Descendants(_w + "r").ToList();
        Assert.Equal(
            new string?[] { null, "Strong", null, "Strong", "Strong", null, null, null, null },
            runs.Select(r => (string?)r.Element(_w + "rPr")?.Element(_w + "rStyle")?.Attribute(_w + "val")));

        List<XElement> filledRuns = BuilderHarness.ReadXml(filled, "word/document.xml").Descendants(_w + "r").ToList();
        XElement alice = filledRuns.Single(r => r.Value == "Alice");
        Assert.Equal("Strong", alice.Element(_w + "rPr")!.Element(_w + "rStyle")!.Attribute(_w + "val")!.Value);
    }

    [Fact]
    public void CharacterStyles_Odt_AreSpansAndSurviveFilling()
    {
        (GeneratedTemplate built, byte[] filled) = BuilderHarness.BuildAndFill(StyledText(), TemplateFormat.Odt, CustomerJson);

        Assert.Equal(new[] { "Dear Alice, total 12.50 ok \tend\nnext line " }, BuilderHarness.ParagraphTexts(TemplateFormat.Odt, filled));

        XElement paragraph = BuilderHarness.ReadXml(built.Content.ToArray(), "content.xml").Descendants(_text + "p").Single();
        List<XElement> spans = paragraph.Elements(_text + "span").ToList();
        Assert.Equal(new[] { "Strong", "Strong", "Strong" }, spans.Select(s => (string?)s.Attribute(_text + "style-name")));

        XElement filledParagraph = BuilderHarness.ReadXml(filled, "content.xml").Descendants(_text + "p").Single();
        Assert.Contains(filledParagraph.Elements(_text + "span"), s => s.Value == "Alice" && (string?)s.Attribute(_text + "style-name") == "Strong");
    }

    [Fact]
    public void CharacterStyle_NotDefined_IsAnError()
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c => c.Paragraph(x => x.Text(_strong, "x"))));

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(
            () => TemplateBuilder.Build(definition, BuilderHarness.Options(TemplateFormat.Odt)));

        Assert.Equal("TB2010", Assert.Single(exception.Errors).Code);
        Assert.Contains("character style 'Strong'", exception.Errors[0].Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("a {{ b")]
    [InlineData("{{name}}")]
    public void LiteralText_WithOpenBraces_IsAnError(string text)
    {
        TestDefinition asParagraph = new TestDefinition(template => template.Content(c => c.Paragraph(text)));
        TestDefinition asInline = new TestDefinition(template => template.Content(c => c.Paragraph(x => x.Text(text))));

        foreach (TestDefinition definition in new[] { asParagraph, asInline })
        {
            TemplateBuildException exception = Assert.Throws<TemplateBuildException>(
                () => TemplateBuilder.Build(definition, BuilderHarness.Options(TemplateFormat.Docx)));
            Assert.Equal("TB1002", Assert.Single(exception.Errors).Code);
        }
    }

    [Fact]
    public void LiteralText_OpenBracesAcrossPieces_IsAnError()
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c => c.Paragraph(x =>
        {
            x.Text("a {");
            x.Text(_strong, "{ b");
        })));

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(
            () => TemplateBuilder.Build(definition, BuilderHarness.Options(TemplateFormat.Docx)));

        Assert.Contains(exception.Errors, e => e.Code == "TB1002");
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void LiteralText_SingleBracesAndClosingBraces_AreFine(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c =>
        {
            c.Paragraph("a { b } c }} d");
            c.Paragraph(x =>
            {
                x.Text("{");
                x.Value(template.Data["v"]);
                x.Text("}");
            });
        }));

        (_, byte[] filled) = BuilderHarness.BuildAndFill(definition, format, """{ "v": "x" }""");

        Assert.Equal(new[] { "a { b } c }} d", "{x}" }, BuilderHarness.ParagraphTexts(format, filled));
    }

    [Fact]
    public void ReservedStylePrefix_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ParagraphStyleKey("TemplifyPageBreak", "Mine"));
        Assert.Throws<ArgumentException>(() => new CharacterStyleKey("templifyAnything", "Mine"));
    }

    private static TestDefinition TwoPages() => new TestDefinition(template => template.Content(c =>
    {
        c.Paragraph("first page");
        c.PageBreak();
        c.Paragraph(template.Data["v"]);
    }));

    [Theory]
    [MemberData(nameof(Formats))]
    public void PageBreak_IsAParagraphOfItsOwn(TemplateFormat format)
    {
        (GeneratedTemplate built, byte[] filled) = BuilderHarness.BuildAndFill(TwoPages(), format, """{ "v": "second page" }""");
        byte[] template = built.Content.ToArray();

        if (format == TemplateFormat.Docx)
        {
            List<XElement> paragraphs = BuilderHarness.ReadXml(template, "word/document.xml").Descendants(_w + "p").ToList();
            Assert.Equal(3, paragraphs.Count);
            Assert.Equal("page", paragraphs[1].Descendants(_w + "br").Single().Attribute(_w + "type")!.Value);
        }
        else
        {
            XDocument content = BuilderHarness.ReadXml(template, "content.xml");
            List<XElement> paragraphs = content.Descendants(_text + "p").ToList();
            Assert.Equal(3, paragraphs.Count);
            Assert.Equal("TemplifyPageBreak", paragraphs[1].Attribute(_text + "style-name")!.Value);
            XElement style = content.Root!.Element(_office + "automatic-styles")!.Elements(_style + "style").Single();
            Assert.Equal("page", style.Element(_style + "paragraph-properties")!.Attribute(_fo + "break-after")!.Value);
        }

        Assert.Equal(new[] { "first page", string.Empty, "second page" }, BuilderHarness.ParagraphTexts(format, filled));
    }

    [Fact]
    public void PageBreak_Odt_WritesTheAutomaticStyleOnlyWhenUsed()
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c => c.Paragraph("x")));

        XDocument content = BuilderHarness.ReadXml(BuilderHarness.Build(definition, TemplateFormat.Odt).Content.ToArray(), "content.xml");

        Assert.Empty(content.Root!.Element(_office + "automatic-styles")!.Elements());
    }

    [Theory]
    [MemberData(nameof(Formats))]
    [Trait("Category", "LibreOffice")]
    public void LibreOffice_PageBreakStartsANewPage(TemplateFormat format)
    {
        LibreOfficeRunner.RequireExecutable();
        (GeneratedTemplate built, byte[] filled) = BuilderHarness.BuildAndFill(TwoPages(), format, """{ "v": "second page" }""");
        string extension = built.FileExtension.TrimStart('.');

        Assert.Equal(2, PdfPageCount(LibreOfficeRunner.Convert(filled, extension, "pdf", "pdf")));
        Assert.Equal(1, PdfPageCount(LibreOfficeRunner.Convert(
            BuilderHarness.Build(new TestDefinition(t => t.Content(c => c.Paragraph("one"))), format).Content.ToArray(), extension, "pdf", "pdf")));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    [Trait("Category", "LibreOffice")]
    public void LibreOffice_RendersStyledInlineContent(TemplateFormat format)
    {
        LibreOfficeRunner.RequireExecutable();
        (GeneratedTemplate built, byte[] filled) = BuilderHarness.BuildAndFill(StyledText(), format, CustomerJson);
        string extension = built.FileExtension.TrimStart('.');

        string text = Encoding.UTF8.GetString(LibreOfficeRunner.Convert(filled, extension, "txt:Text (encoded):UTF8", "txt")).TrimStart('\uFEFF');

        Assert.StartsWith("Dear Alice, total 12.50 ok \tend", text, StringComparison.Ordinal);
        Assert.Contains("next line", text, StringComparison.Ordinal);
    }

    private static int PdfPageCount(byte[] pdf) => PageObject().Matches(Encoding.Latin1.GetString(pdf)).Count;

    [GeneratedRegex(@"/Type\s*/Page(?![a-zA-Z])")]
    private static partial Regex PageObject();
}
