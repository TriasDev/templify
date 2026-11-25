// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Odt;

namespace TriasDev.Templify.Builder.Tests;

public sealed class RawTests
{
    private static readonly XNamespace _w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace _text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace _table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private const string Json = """{ "customer": { "name": "Acme GmbH" }, "lines": [ { "code": "A-1" }, { "code": "B-2" } ] }""";

    /// <summary>A paragraph with a document field (the author) and a placeholder, written as format XML.</summary>
    private static RawBlock AuthorLine(TemplatePath name) => new RawBlock
    {
        Uses = [name],
        Docx = ctx => new XElement(_w + "p",
            new XElement(_w + "r", new XElement(_w + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), "Prepared for ")),
            new XElement(_w + "r", new XElement(_w + "t", ctx.Placeholder(name))),
            new XElement(_w + "r", new XElement(_w + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), " by ")),
            new XElement(_w + "fldSimple", new XAttribute(_w + "instr", " AUTHOR "),
                new XElement(_w + "r", new XElement(_w + "t", "Author")))),
        Odt = ctx => new XElement(_text + "p",
            "Prepared for ",
            new XElement(_text + "span", ctx.Placeholder(name)),
            " by ",
            new XElement(_text + "initial-creator", "Author")),
    };

    [Theory]
    [MemberData(nameof(Formats))]
    public void Raw_BuildsFillsAndValidates(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c =>
        {
            c.Paragraph("Before");
            c.Raw(AuthorLine(t.Data["customer"]["name"]));
            c.Paragraph("After");
        }));

        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(definition, format, Json);

        BuilderHarness.AssertInvariants(definition, template);
        List<string> texts = BuilderHarness.ParagraphTexts(format, filled);
        Assert.Equal("Before", texts[0]);
        // The harness reads plain text only; the ODT field element is checked in Raw_ElementIsWrittenAsGiven.
        Assert.StartsWith("Prepared for Acme GmbH by ", texts[1], StringComparison.Ordinal);
        Assert.Equal("After", texts[2]);
        Assert.Contains(new ContractEntry("customer.name", ContractUsage.Value), template.Contract.Entries);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Raw_ElementIsWrittenAsGiven(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Raw(AuthorLine(t.Data["customer"]["name"]))));

        GeneratedTemplate template = BuilderHarness.Build(definition, format);

        XDocument main = BuilderHarness.ReadXml(template.Content.ToArray(), BuilderHarness.MainPart(format));
        if (format == TemplateFormat.Docx)
        {
            XElement field = Assert.Single(main.Descendants(_w + "fldSimple"));
            Assert.Equal(" AUTHOR ", (string?)field.Attribute(_w + "instr"));
        }
        else
        {
            Assert.Single(main.Descendants(_text + "initial-creator"));
        }
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Raw_InALoop_ReadsTheItem(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c =>
            c.ForEach(t.Data["lines"], "line", (line, b) =>
            {
                TemplatePath code = line["code"];
                b.Raw(new RawBlock
                {
                    Uses = [code],
                    Docx = ctx => new XElement(_w + "p", new XElement(_w + "r", new XElement(_w + "t", ctx.Placeholder(code)))),
                    Odt = ctx => new XElement(_text + "p", ctx.Placeholder(code)),
                });
            })));

        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(definition, format, Json);

        BuilderHarness.AssertInvariants(definition, template);
        Assert.Equal(new[] { "A-1", "B-2" }, BuilderHarness.ParagraphTexts(format, filled));
        Assert.Contains(new ContractEntry("lines[].code", ContractUsage.Value), template.Contract.Entries);
    }

    [Fact]
    public void Raw_UsesOutsideItsLoop_IsAScopeError()
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c =>
        {
            LoopItem? escaped = null;
            c.ForEach(t.Data["lines"], "line", (line, b) => escaped = line);
            TemplatePath code = escaped!["code"];
            c.Raw(new RawBlock
            {
                Uses = [code],
                Docx = ctx => new XElement(_w + "p"),
                Odt = ctx => new XElement(_text + "p"),
            });
        }));

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(
            () => TemplateBuilder.Build(definition, BuilderHarness.Options(TemplateFormat.Docx)));

        Assert.Equal("TB4001", Assert.Single(exception.Errors).Code);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Raw_PlaceholderOfAnUndeclaredPath_IsAnError(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c =>
        {
            TemplatePath name = t.Data["customer"]["name"];
            c.Raw(new RawBlock
            {
                Docx = ctx => new XElement(_w + "p", new XElement(_w + "r", new XElement(_w + "t", ctx.Placeholder(name)))),
                Odt = ctx => new XElement(_text + "p", ctx.Placeholder(name)),
            });
        }));

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(
            () => TemplateBuilder.Build(definition, BuilderHarness.Options(format)));

        BuildDiagnostic error = Assert.Single(exception.Errors);
        Assert.Equal("TB9102", error.Code);
        Assert.Contains("customer.name", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Raw_ElementThatCannotStandAtBlockLevel_IsAnError(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Raw(new RawBlock
        {
            Docx = ctx => new XElement(_w + "r", new XElement(_w + "t", "run")),
            Odt = ctx => new XElement(_text + "span", "span"),
        })));

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(
            () => TemplateBuilder.Build(definition, BuilderHarness.Options(format)));

        Assert.Equal("TB9101", Assert.Single(exception.Errors).Code);
    }

    [Fact]
    public void Raw_ElementInTheWrongNamespace_IsAnError()
    {
        // A text:p handed to the DOCX side is the typical copy-paste mistake.
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Raw(new RawBlock
        {
            Docx = ctx => new XElement(_text + "p", "odt"),
            Odt = ctx => new XElement(_text + "p", "odt"),
        })));

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(
            () => TemplateBuilder.Build(definition, BuilderHarness.Options(TemplateFormat.Docx)));

        Assert.Equal("TB9101", Assert.Single(exception.Errors).Code);
    }

    [Fact]
    public void Raw_NullFactories_AreRejected()
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Raw(new RawBlock { Docx = null!, Odt = ctx => new XElement(_text + "p") })));

        Assert.Throws<ArgumentNullException>(() => TemplateBuilder.Build(definition, BuilderHarness.Options(TemplateFormat.Odt)));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Raw_FrameIsTheWidthOfItsContainer(TemplateFormat format)
    {
        List<double> frames = new List<double>();
        RawBlock Probe() => new RawBlock
        {
            Docx = ctx =>
            {
                frames.Add(ctx.Frame.Points);
                return new XElement(_w + "p");
            },
            Odt = ctx =>
            {
                frames.Add(ctx.Frame.Points);
                return new XElement(_text + "p");
            },
        };

        TestDefinition definition = new TestDefinition(t =>
        {
            t.Page(p => p.Size(PageSize.A4).Margins(20.Mm()));
            t.Content(c =>
            {
                c.Raw(Probe());
                c.Table(tb =>
                {
                    tb.Columns(cols =>
                    {
                        cols.Fixed(50.Mm());
                        cols.Relative(1);
                    });
                    tb.Row(r =>
                    {
                        r.Cell(cell => cell.Raw(Probe()));
                        r.Cell("x");
                    });
                });
            });
        });

        BuilderHarness.Build(definition, format);

        Assert.Equal(2, frames.Count);
        Assert.Equal(170.Mm().Points, frames[0], 3);
        Assert.True(frames[1] < 50.Mm().Points && frames[1] > 40.Mm().Points, $"cell frame {frames[1]}");
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Raw_Table_IsATableToTheWriter(TemplateFormat format)
    {
        // A raw table that ends the body and touches a builder table: DOCX keeps them apart and ends the body with a
        // paragraph, which both need the writer to see a table.
        RawBlock table = new RawBlock
        {
            Docx = ctx => new XElement(_w + "tbl",
                new XElement(_w + "tblPr", new XElement(_w + "tblW", new XAttribute(_w + "w", "0"), new XAttribute(_w + "type", "auto"))),
                new XElement(_w + "tblGrid", new XElement(_w + "gridCol", new XAttribute(_w + "w", "2000"))),
                new XElement(_w + "tr", new XElement(_w + "tc", new XElement(_w + "p", new XElement(_w + "r", new XElement(_w + "t", "raw cell")))))),
            Odt = ctx => new XElement(_table + "table", new XAttribute(_table + "name", "RawTable"),
                new XElement(_table + "table-column"),
                new XElement(_table + "table-row", new XElement(_table + "table-cell", new XElement(_text + "p", "raw cell")))),
        };

        TestDefinition definition = new TestDefinition(t => t.Content(c =>
        {
            c.Table(tb =>
            {
                tb.Columns(cols => cols.Relative(1));
                tb.Row(r => r.Cell("builder cell"));
            });
            c.Raw(table);
        }));

        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(definition, format, "{}");

        BuilderHarness.AssertInvariants(definition, template);
        if (format == TemplateFormat.Docx)
        {
            XElement body = BuilderHarness.ReadXml(template.Content.ToArray(), "word/document.xml").Descendants(_w + "body").Single();
            Assert.Equal(new[] { "tbl", "p", "tbl", "p", "sectPr" }, body.Elements().Select(e => e.Name.LocalName));
        }

        Assert.Contains("raw cell", BuilderHarness.ReadXml(filled, BuilderHarness.MainPart(format)).ToString(SaveOptions.DisableFormatting), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Raw_InHeader_IsWritten(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(t =>
        {
            t.Page(p => p.Header(h => h.Raw(AuthorLine(t.Data["customer"]["name"]))));
            t.Content(c => c.Paragraph("Body"));
        });

        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(definition, format, Json);

        BuilderHarness.AssertInvariants(definition, template);
        Assert.Contains(new ContractEntry("customer.name", ContractUsage.Value), template.Contract.Entries);
        string part = format == TemplateFormat.Docx ? "word/header1.xml" : "styles.xml";
        Assert.Contains(">Acme GmbH<", BuilderHarness.ReadXml(filled, part).ToString(SaveOptions.DisableFormatting), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    [Trait("Category", "LibreOffice")]
    public void LibreOffice_RendersARawBlock(TemplateFormat format)
    {
        LibreOfficeRunner.RequireExecutable();
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Raw(AuthorLine(t.Data["customer"]["name"]))));
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(definition, format, Json);

        string text = System.Text.Encoding.UTF8.GetString(LibreOfficeRunner.Convert(filled, template.FileExtension.TrimStart('.'), "txt:Text (encoded):UTF8", "txt"));

        Assert.Contains("Prepared for Acme GmbH by", text, StringComparison.Ordinal);
    }
}
