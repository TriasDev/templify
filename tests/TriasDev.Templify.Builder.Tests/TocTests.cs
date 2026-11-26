// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Odt;

namespace TriasDev.Templify.Builder.Tests;

public sealed class TocTests
{
    private static readonly XNamespace _w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace _text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private static readonly ParagraphStyleKey _h1 = new ParagraphStyleKey("H1", "Heading Level One");
    private static readonly ParagraphStyleKey _h2 = new ParagraphStyleKey("H2", "Heading Level Two");

    private static TestDefinition Report() => new TestDefinition(t =>
    {
        t.Styles(StyleSheet.Create(s =>
        {
            s.Paragraph(_h1, p => p.Size(16.Pt()).OutlineLevel(1));
            s.Paragraph(_h2, p => p.Size(13.Pt()).OutlineLevel(2));
        }));
        t.Content(c =>
        {
            c.Paragraph("Contents");
            c.TableOfContents(2);
            c.PageBreak();
            c.Paragraph(_h1, x =>
            {
                x.Text("Chapter ");
                x.Value(t.Data["name"]);
            });
            c.Paragraph(_h2, "Section");
        });
    });

    [Fact]
    public void Docx_TocIsADirtyFieldOverOutlineLevels()
    {
        GeneratedTemplate template = BuilderHarness.Build(Report(), TemplateFormat.Docx);
        XElement body = BuilderHarness.ReadXml(template.Content.ToArray(), "word/document.xml").Root!.Element(_w + "body")!;

        // The field's end sits in a paragraph of its own: LibreOffice imports a field whose result is empty and in one
        // paragraph as text, so a converter that refreshes indexes (Stirling-PDF) would leave the contents empty (#295).
        XElement toc = body.Elements(_w + "p").ElementAt(1);
        XElement end = body.Elements(_w + "p").ElementAt(2);
        Assert.Equal(new[] { "begin", "separate" }, toc.Descendants(_w + "fldChar").Select(f => f.Attribute(_w + "fldCharType")!.Value));
        Assert.Equal(new[] { "end" }, end.Descendants(_w + "fldChar").Select(f => f.Attribute(_w + "fldCharType")!.Value));
        Assert.Equal("true", toc.Descendants(_w + "fldChar").First().Attribute(_w + "dirty")!.Value);
        string instruction = toc.Descendants(_w + "instrText").Single().Value;
        Assert.Equal(" TOC \\o \"1-2\" \\h \\z \\u ", instruction);
        Assert.DoesNotContain("\\t", instruction, StringComparison.Ordinal);

        // Filled with UpdateFieldsOnOpen = Auto, the engine asks Word to update the fields on open.
        GeneratedTemplate built = BuilderHarness.Build(Report(), TemplateFormat.Docx);
        using Stream input = built.OpenRead();
        using MemoryStream output = new MemoryStream();
        new TemplateProcessor(new PlaceholderReplacementOptions { UpdateFieldsOnOpen = UpdateFieldsOnOpenMode.Auto }).ProcessTemplate(input, output, """{ "name": "Alpha" }""");
        Assert.Equal("true", BuilderHarness.ReadXml(output.ToArray(), "word/settings.xml").Descendants(_w + "updateFields").Single().Attribute(_w + "val")!.Value);
    }

    [Fact]
    public void Odt_TocIsAnIndexOverOutlineLevels()
    {
        GeneratedTemplate template = BuilderHarness.Build(Report(), TemplateFormat.Odt);
        XElement toc = BuilderHarness.ReadXml(template.Content.ToArray(), "content.xml").Descendants(_text + "table-of-content").Single();

        Assert.Equal("TemplifyToc1", toc.Attribute(_text + "name")!.Value);
        XElement source = toc.Element(_text + "table-of-content-source")!;
        Assert.Equal("2", source.Attribute(_text + "outline-level")!.Value);
        Assert.Equal(new[] { "1", "2" }, source.Elements(_text + "table-of-content-entry-template").Select(e => e.Attribute(_text + "outline-level")!.Value));
        Assert.Empty(toc.Element(_text + "index-body")!.Elements());
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Toc_IsFilledAndWarnsThatOnlyWordBuildsIt(TemplateFormat format)
    {
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Report(), format, """{ "name": "Alpha" }""");

        BuildDiagnostic warning = Assert.Single(template.Warnings);
        Assert.Equal("TB9001", warning.Code);
        Assert.Contains("LibreOffice", warning.Message, StringComparison.Ordinal);
        Assert.Contains("Chapter Alpha", BuilderHarness.ParagraphTexts(format, filled));
        Assert.Empty(BuilderHarness.Build(new TestDefinition(t => t.Content(c => c.Paragraph("x"))), format).Warnings);
    }

    [Fact]
    public void Toc_LevelIsChecked()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BuilderHarness.Build(new TestDefinition(t => t.Content(c => c.TableOfContents(0))), TemplateFormat.Docx));
        Assert.Throws<ArgumentOutOfRangeException>(() => BuilderHarness.Build(new TestDefinition(t => t.Content(c => c.TableOfContents(10))), TemplateFormat.Docx));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    [Trait("Category", "LibreOffice")]
    public void LibreOffice_ConvertsADocumentWithAToc(TemplateFormat format)
    {
        LibreOfficeRunner.RequireExecutable();
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Report(), format, """{ "name": "Alpha" }""");

        string text = System.Text.Encoding.UTF8.GetString(LibreOfficeRunner.Convert(filled, template.FileExtension.TrimStart('.'), "txt:Text (encoded):UTF8", "txt"));

        Assert.Contains("Chapter Alpha", text, StringComparison.Ordinal);
        Assert.Contains("Section", text, StringComparison.Ordinal);
    }
}
