// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Xml.Linq;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Odt;

/// <summary>
/// Markdown in values rendered with automatic text styles, and document properties in meta.xml.
/// </summary>
public sealed class OdtMarkdownTests
{
    private static readonly XNamespace _text = OdtDocumentVerifier.Text;
    private static readonly XNamespace _style = OdtDocumentVerifier.Style;
    private static readonly XNamespace _fo = OdtDocumentVerifier.Fo;
    private static readonly XNamespace _office = OdtDocumentVerifier.Office;

    private static XElement? FindStyle(XDocument part, string? name) =>
        part.Root!.Element(_office + "automatic-styles")?.Elements(_style + "style")
            .FirstOrDefault(s => (string?)s.Attribute(_style + "name") == name);

    private static XElement? TextProperties(XDocument part, XElement span) =>
        FindStyle(part, (string?)span.Attribute(_text + "style-name"))?.Element(_style + "text-properties");

    [Fact]
    public void Bold_BecomesSpanWithBoldAutomaticStyle()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("Hello {{Name}}!"),
            new Dictionary<string, object> { ["Name"] = "my **dear** friend" });

        XElement paragraph = output.Body.Element(_text + "p")!;
        Assert.Equal("Hello my dear friend!", OdtDocumentVerifier.RenderText(paragraph));

        XElement span = Assert.Single(paragraph.Elements(_text + "span"));
        Assert.Equal("dear", span.Value);
        XElement properties = TextProperties(output.ContentXml, span)!;
        Assert.Equal("bold", (string?)properties.Attribute(_fo + "font-weight"));
        Assert.Equal("text", (string?)FindStyle(output.ContentXml, (string?)span.Attribute(_text + "style-name"))!.Attribute(_style + "family"));
    }

    [Theory]
    [InlineData("*it*", "italic", null, null)]
    [InlineData("~~gone~~", null, null, "solid")]
    [InlineData("***both***", "italic", "bold", null)]
    public void ItalicStrikeAndCombined_AreRendered(string value, string? fontStyle, string? fontWeight, string? lineThrough)
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{Value}}"),
            new Dictionary<string, object> { ["Value"] = value });

        XElement span = output.Body.Descendants(_text + "span").Single();
        XElement properties = TextProperties(output.ContentXml, span)!;
        Assert.Equal(fontStyle, (string?)properties.Attribute(_fo + "font-style"));
        Assert.Equal(fontWeight, (string?)properties.Attribute(_fo + "font-weight"));
        Assert.Equal(lineThrough, (string?)properties.Attribute(_style + "text-line-through-style"));
    }

    [Fact]
    public void IdenticalFormatting_ReusesOneStyle_AndNamesDoNotCollide()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{A}}").AddParagraph("{{B}}").AddParagraph("{{C}}"),
            new Dictionary<string, object> { ["A"] = "**a**", ["B"] = "**b**", ["C"] = "*c*" });

        List<string> names = output.Body.Descendants(_text + "span").Select(s => (string)s.Attribute(_text + "style-name")!).ToList();
        Assert.Equal(3, names.Count);
        Assert.Equal(names[0], names[1]);
        Assert.NotEqual(names[0], names[2]);

        // The builder predefines T1 (bold, but only fo:font-weight) and T2; new styles must not reuse those names.
        Assert.DoesNotContain("T1", names);
        Assert.DoesNotContain("T2", names);
        Assert.Equal(
            output.ContentXml.Descendants(_style + "style").Count(),
            output.ContentXml.Descendants(_style + "style").Select(s => (string?)s.Attribute(_style + "name")).Distinct().Count());
    }

    [Fact]
    public void Markdown_IsNestedInTemplateSpan_SoFormattingCombines()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder()
                .AddAutomaticStyles("<style:style style:name=\"Red\" style:family=\"text\"><style:text-properties fo:color=\"#ff0000\"/></style:style>")
                .AddXml("<text:p><text:span text:style-name=\"Red\">{{Value}}</text:span></text:p>"),
            new Dictionary<string, object> { ["Value"] = "x **y**" });

        XElement red = output.Body.Descendants(_text + "span").First();
        Assert.Equal("Red", (string?)red.Attribute(_text + "style-name"));
        XElement bold = Assert.Single(red.Elements(_text + "span"));
        Assert.Equal("y", bold.Value);
        Assert.Equal("x y", OdtDocumentVerifier.RenderText(output.Body.Element(_text + "p")!));
    }

    [Fact]
    public void MarkdownWithNewlines_CombinesSpansAndLineBreaks()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{Value}}"),
            new Dictionary<string, object> { ["Value"] = "**Title**\nplain *text*" });

        Assert.Equal("Title\nplain text", output.GetParagraphTexts()[0]);
        Assert.Equal(2, output.Body.Descendants(_text + "span").Count());
        Assert.Single(output.Body.Descendants(_text + "line-break"));
    }

    [Fact]
    public void RawFormat_And_DisabledMarkdown_KeepLiteralText()
    {
        PlaceholderReplacementOptions disabled = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture,
            EnableMarkdown = false,
        };

        (_, OdtDocumentVerifier raw) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{Value:raw}}"),
            new Dictionary<string, object> { ["Value"] = "**bold**" });
        (_, OdtDocumentVerifier off) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{Value}}"),
            new Dictionary<string, object> { ["Value"] = "**bold**" },
            disabled);

        Assert.Equal("**bold**", raw.GetParagraphTexts()[0]);
        Assert.Equal("**bold**", off.GetParagraphTexts()[0]);
        Assert.Empty(raw.Body.Descendants(_text + "span"));
    }

    [Fact]
    public void MalformedMarkdown_IsPlainText()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{Value}}"),
            new Dictionary<string, object> { ["Value"] = "**unclosed" });

        Assert.Equal("**unclosed", output.GetParagraphTexts()[0]);
    }

    [Fact]
    public void MarkdownInHeader_CreatesStyleInStylesXml()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{Value}}").AddHeaderParagraph("{{Value}}"),
            new Dictionary<string, object> { ["Value"] = "**b**" });

        XElement headerSpan = output.StylesXml!.Descendants(_text + "span").Single();
        Assert.Equal("bold", (string?)TextProperties(output.StylesXml, headerSpan)!.Attribute(_fo + "font-weight"));
        XElement bodySpan = output.Body.Descendants(_text + "span").Single();
        Assert.NotNull(TextProperties(output.ContentXml, bodySpan));
    }

    [Fact]
    public void MarkdownInLoop_ReusesStyleForAllItems()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{#foreach Items}}").AddParagraph("{{.}}").AddParagraph("{{/foreach}}"),
            new Dictionary<string, object> { ["Items"] = new List<string> { "**a**", "**b**" } });

        Assert.Single(output.Body.Descendants(_text + "span").Select(s => (string?)s.Attribute(_text + "style-name")).Distinct());
        Assert.Equal(new[] { "a", "b" }, output.GetParagraphTexts());
    }

    [Fact]
    public void DocumentProperties_AreWrittenToMeta()
    {
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture,
            DocumentProperties = new DocumentProperties
            {
                Author = "Alice",
                LastModifiedBy = "Bob",
                Title = "Offer 42",
                Subject = "Sales",
                Description = "Generated",
                Keywords = "offer, sales",
                Category = "Contracts",
            },
        };

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("x"),
            new Dictionary<string, object>(),
            options);

        XDocument meta = XDocument.Parse(output.GetEntryString("meta.xml"));
        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        XNamespace m = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";
        Assert.Equal("Alice", meta.Descendants(m + "initial-creator").Single().Value);
        Assert.Equal("Bob", meta.Descendants(dc + "creator").Single().Value);
        Assert.Equal("Offer 42", meta.Descendants(dc + "title").Single().Value);
        Assert.Equal("Sales", meta.Descendants(dc + "subject").Single().Value);
        Assert.Equal("Generated", meta.Descendants(dc + "description").Single().Value);
        Assert.Equal("offer, sales", meta.Descendants(m + "keyword").Single().Value);
        XElement category = meta.Descendants(m + "user-defined").Single();
        Assert.Equal("Category", (string?)category.Attribute(m + "name"));
        Assert.Equal("Contracts", category.Value);

        // The generator written by the template is kept.
        Assert.Equal("Templify.Tests", meta.Descendants(m + "generator").Single().Value);
    }

    [Fact]
    public void DocumentProperties_NullValuesKeepTemplateValues_AndNoPropertiesKeepMetaUnchanged()
    {
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture,
            DocumentProperties = new DocumentProperties { Subject = "Only subject" },
        };
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddParagraph("x");

        (_, OdtDocumentVerifier withProperties) = OdtTestHelper.Process(template, new Dictionary<string, object>(), options);
        (_, OdtDocumentVerifier without) = OdtTestHelper.Process(template, new Dictionary<string, object>());

        XDocument meta = XDocument.Parse(withProperties.GetEntryString("meta.xml"));
        Assert.Equal("Test", meta.Descendants(XNamespace.Get("http://purl.org/dc/elements/1.1/") + "title").Single().Value);
        Assert.Contains("Only subject", withProperties.GetEntryString("meta.xml"), StringComparison.Ordinal);
        Assert.DoesNotContain("subject", without.GetEntryString("meta.xml"), StringComparison.Ordinal);
    }

    [Fact]
    public void DocumentProperties_WithoutMetaPart_CreateItAndListItInManifest()
    {
        byte[] template = new OdtDocumentBuilder().AddParagraph("x").ToBytes();
        using MemoryStream stripped = new MemoryStream();
        using (System.IO.Compression.ZipArchive source = new System.IO.Compression.ZipArchive(new MemoryStream(template)))
        using (System.IO.Compression.ZipArchive target = new System.IO.Compression.ZipArchive(stripped, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (System.IO.Compression.ZipArchiveEntry entry in source.Entries.Where(e => e.FullName != "meta.xml"))
            {
                System.IO.Compression.ZipArchiveEntry copy = target.CreateEntry(
                    entry.FullName,
                    entry.FullName == "mimetype" ? System.IO.Compression.CompressionLevel.NoCompression : System.IO.Compression.CompressionLevel.Optimal);
                using Stream from = entry.Open();
                using Stream to = copy.Open();
                from.CopyTo(to);
            }
        }

        OdtTemplateProcessor processor = new OdtTemplateProcessor(new PlaceholderReplacementOptions
        {
            DocumentProperties = new DocumentProperties { Title = "New" },
        });
        ProcessingResult result = processor.ProcessTemplate(stripped.ToArray(), new Dictionary<string, object>(), out byte[] output);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        OdtDocumentVerifier verifier = new OdtDocumentVerifier(output);
        verifier.AssertValidOdtPackage();
        Assert.Contains("meta.xml", verifier.EntryNames);
        Assert.Contains("<dc:title>New</dc:title>", verifier.GetEntryString("meta.xml"), StringComparison.Ordinal);
        Assert.Contains("full-path=\"meta.xml\"", verifier.GetEntryString("META-INF/manifest.xml"), StringComparison.Ordinal);
    }
}
