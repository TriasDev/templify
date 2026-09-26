// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Odt;

/// <summary>
/// Opens processed documents with LibreOffice headless and checks what LibreOffice reads.
/// Local only: skipped when LibreOffice is not installed (and on CI unless TEMPLIFY_SOFFICE is set).
/// </summary>
[Trait("Category", "LibreOffice")]
public sealed class OdtLibreOfficeRoundTripTests
{
    [Fact]
    public void Placeholders_RoundTripThroughLibreOffice()
    {
        LibreOfficeRunner.RequireExecutable();

        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddHeading("Offer {{Number}}")
            .AddXml("<text:p>Dear <text:span text:style-name=\"T1\">{{Cust</text:span>omer.Name}},</text:p>")
            .AddParagraph("Total: {{Total:number:N2}} | {{Date:date:yyyy-MM-dd}} | {{Flag:yesno}}")
            .AddXml("<text:p>[{{Spaced}}]<text:tab/>{{Multi}}</text:p>")
            .AddTable(new[] { "Item", "{{Item}}" })
            .AddParagraph("Escaped: {{Special}}");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Number"] = 42,
            ["Customer"] = new Dictionary<string, object> { ["Name"] = "Anna Müller" },
            ["Total"] = 1234.5m,
            ["Date"] = new DateTime(2026, 9, 26),
            ["Flag"] = true,
            ["Spaced"] = " a  b ",
            ["Multi"] = "Line 1\nLine 2",
            ["Item"] = "Widget",
            ["Special"] = "<Tom & \"Jerry\">",
        };

        byte[] output = ProcessToBytes(template, data);
        string[] lines = LibreOfficeRunner.ConvertToTextLines(output);

        Assert.Equal("Offer 42", lines[0]);
        Assert.Equal("Dear Anna Müller,", lines[1]);
        Assert.Equal("Total: 1,234.50 | 2026-09-26 | Yes", lines[2]);
        Assert.Equal("[ a  b ]\tLine 1", lines[3]);
        Assert.Equal("Line 2", lines[4]);
        Assert.Contains("Widget", string.Join("\n", lines), StringComparison.Ordinal);
        Assert.Equal("Escaped: <Tom & \"Jerry\">", lines[^1]);
    }

    [Fact]
    public void Template_Ott_OpensAsOdtAndConvertsToPdf()
    {
        LibreOfficeRunner.RequireExecutable();

        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AsTemplate()
            .AddHeaderParagraph("Header {{Company}}")
            .AddFooterParagraph("Footer {{Company}}")
            .AddParagraph("Body {{Company}}");

        byte[] output = ProcessToBytes(template, new Dictionary<string, object> { ["Company"] = "ACME" });

        string[] lines = LibreOfficeRunner.ConvertToTextLines(output);
        Assert.Contains("Body ACME", lines);

        byte[] pdf = LibreOfficeRunner.Convert(output, "odt", "pdf", "pdf");
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4), StringComparison.Ordinal);
    }

    [Fact]
    public void LibreOfficeGeneratedDocument_IsProcessed()
    {
        LibreOfficeRunner.RequireExecutable();

        // Let LibreOffice write a real document (settings, thumbnail, manifest.rdf, LibreOffice namespaces).
        byte[] source = Encoding.UTF8.GetBytes("Hello {{Name}}  and  {{Other}}\n\tTabbed {{Name}}\n");
        byte[] template = LibreOfficeRunner.Convert(source, "txt", "odt", "odt");

        OdtTemplateProcessor processor = new OdtTemplateProcessor(new PlaceholderReplacementOptions { Culture = CultureInfo.InvariantCulture });
        ProcessingResult result = processor.ProcessTemplate(
            template,
            new Dictionary<string, object> { ["Name"] = "World", ["Other"] = "x" },
            out byte[] output);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(3, result.ReplacementCount);
        OdtDocumentVerifier verifier = new OdtDocumentVerifier(output);
        verifier.AssertValidOdtPackage();

        // The template's preview thumbnail (showing the placeholders) is not carried over.
        Assert.Contains(new OdtDocumentVerifier(template).EntryNames, n => n == "Thumbnails/thumbnail.png");
        Assert.DoesNotContain(verifier.EntryNames, n => n.StartsWith("Thumbnails/", StringComparison.Ordinal));

        string[] lines = LibreOfficeRunner.ConvertToTextLines(output);
        Assert.Equal("Hello World  and  x", lines[0]);
        Assert.Equal("\tTabbed World", lines[1]);
    }

    [Fact]
    public void Conditionals_RoundTripThroughLibreOffice()
    {
        LibreOfficeRunner.RequireExecutable();

        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("{{#if Premium}}")
            .AddParagraph("Premium customer")
            .AddParagraph("{{#else}}")
            .AddParagraph("Standard customer")
            .AddParagraph("{{/if}}")
            .AddXml("<text:p>Dear {{#if IsMale}}Mr.{{#else}}<text:span text:style-name=\"T1\">Ms.</text:span>{{/if}} {{Name}}</text:p>")
            .AddTable(new[] { "{{#if ShowRow}}" }, new[] { "Hidden row" }, new[] { "{{/if}}" }, new[] { "Kept row" })
            .AddXml(
                "<text:list><text:list-item><text:p>First</text:p></text:list-item>" +
                "<text:list-item><text:p>{{#if ShowItem}}</text:p></text:list-item>" +
                "<text:list-item><text:p>Hidden item</text:p></text:list-item>" +
                "<text:list-item><text:p>{{/if}}</text:p></text:list-item>" +
                "<text:list-item><text:p>Last</text:p></text:list-item></text:list>")
            .AddParagraph("End");

        byte[] output = ProcessToBytes(template, new Dictionary<string, object>
        {
            ["Premium"] = false,
            ["IsMale"] = false,
            ["Name"] = "Smith",
            ["ShowRow"] = false,
            ["ShowItem"] = false,
        });

        string[] lines = LibreOfficeRunner.ConvertToTextLines(output);
        string all = string.Join("\n", lines);
        Assert.Equal("Standard customer", lines[0]);
        Assert.Equal("Dear Ms. Smith", lines[1]);
        Assert.Contains("Kept row", all, StringComparison.Ordinal);
        Assert.DoesNotContain("Hidden", all, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", all, StringComparison.Ordinal);
        Assert.Contains("First", all, StringComparison.Ordinal);
        Assert.Contains("Last", all, StringComparison.Ordinal);
        Assert.Equal("End", lines[^1]);
    }

    [Fact]
    public void Loops_RoundTripThroughLibreOffice()
    {
        LibreOfficeRunner.RequireExecutable();

        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("{{#foreach Items}}")
            .AddXml("<text:p>{{@number}}. <text:span text:style-name=\"T1\">{{Name}}</text:span></text:p>")
            .AddParagraph("{{/foreach}}")
            .AddTable(new[] { "Name" }, new[] { "{{#foreach Items}}" }, new[] { "Row {{Name}}" }, new[] { "{{/foreach}}" })
            .AddXml(
                "<text:list><text:list-item><text:p>{{#foreach Items}}</text:p></text:list-item>" +
                "<text:list-item><text:p>Bullet {{Name}}</text:p></text:list-item>" +
                "<text:list-item><text:p>{{/foreach}}</text:p></text:list-item></text:list>")
            .AddParagraph("End");

        byte[] output = ProcessToBytes(template, new Dictionary<string, object>
        {
            ["Items"] = new List<Dictionary<string, object>>
            {
                new() { ["Name"] = "Alpha" },
                new() { ["Name"] = "Beta" },
            },
        });

        string[] lines = LibreOfficeRunner.ConvertToTextLines(output);
        string all = string.Join("\n", lines);
        Assert.Equal("1. Alpha", lines[0]);
        Assert.Equal("2. Beta", lines[1]);
        Assert.Contains("Row Alpha", all, StringComparison.Ordinal);
        Assert.Contains("Row Beta", all, StringComparison.Ordinal);
        Assert.Contains("Bullet Alpha", all, StringComparison.Ordinal);
        Assert.Contains("Bullet Beta", all, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", all, StringComparison.Ordinal);
        Assert.Equal("End", lines[^1]);
    }

    [Fact]
    public void ClonedFramesTablesAndSections_KeepTheirUniqueNamesInLibreOffice()
    {
        LibreOfficeRunner.RequireExecutable();

        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("{{#foreach Items}}")
            .AddXml(
                "<text:p>{{Name}}<draw:frame draw:name=\"Box\" text:anchor-type=\"as-char\" svg:width=\"4cm\" svg:height=\"1cm\">" +
                "<draw:text-box><text:p>box {{Name}}</text:p></draw:text-box></draw:frame></text:p>")
            .AddXml("<table:table table:name=\"Prices\"><table:table-column/><table:table-row><table:table-cell><text:p>cell {{Name}}</text:p></table:table-cell></table:table-row></table:table>")
            .AddXml("<text:section text:name=\"Details\"><text:p>section {{Name}}</text:p></text:section>")
            .AddParagraph("{{/foreach}}");

        byte[] output = ProcessToBytes(template, new Dictionary<string, object>
        {
            ["Items"] = new List<Dictionary<string, object>> { new() { ["Name"] = "A" }, new() { ["Name"] = "B" } },
        });

        // Let LibreOffice load and save the document again: it keeps unique names and renames duplicates.
        OdtDocumentVerifier resaved = new OdtDocumentVerifier(LibreOfficeRunner.Convert(output, "odt", "odt", "odt"));
        Assert.Equal(
            new[] { "Box", "Box_2" },
            resaved.Body.Descendants(OdtDocumentVerifier.Draw + "frame").Select(f => (string)f.Attribute(OdtDocumentVerifier.Draw + "name")!));
        Assert.Equal(
            new[] { "Prices", "Prices_2" },
            resaved.Body.Descendants(OdtDocumentVerifier.Table + "table").Select(t => (string)t.Attribute(OdtDocumentVerifier.Table + "name")!));
        Assert.Equal(
            new[] { "Details", "Details_2" },
            resaved.Body.Descendants(OdtDocumentVerifier.Text + "section").Select(s => (string)s.Attribute(OdtDocumentVerifier.Text + "name")!));

        string all = string.Join("\n", LibreOfficeRunner.ConvertToTextLines(output));
        Assert.Contains("cell B", all, StringComparison.Ordinal);
        Assert.Contains("section B", all, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkdownAndDocumentProperties_SurviveLibreOfficeResave()
    {
        LibreOfficeRunner.RequireExecutable();

        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddAutomaticStyles("<style:style style:name=\"Red\" style:family=\"text\"><style:text-properties fo:color=\"#ff0000\"/></style:style>")
            .AddXml("<text:p>Note: <text:span text:style-name=\"Red\">{{Message}}</text:span></text:p>");

        OdtTemplateProcessor processor = new OdtTemplateProcessor(new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture,
            DocumentProperties = new DocumentProperties { Title = "Generated offer", Author = "Templify" },
        });
        ProcessingResult result = processor.ProcessTemplate(
            template.ToBytes(),
            new Dictionary<string, object> { ["Message"] = "plain **bold** and *italic*" },
            out byte[] output);
        Assert.True(result.IsSuccess, result.ErrorMessage);

        Assert.Equal("Note: plain bold and italic", LibreOfficeRunner.ConvertToTextLines(output)[0]);

        // After LibreOffice loads and saves the document, "bold" is still bold and red, "italic" italic.
        OdtDocumentVerifier resaved = new OdtDocumentVerifier(LibreOfficeRunner.Convert(output, "odt", "odt", "odt"));
        Assert.Contains("<dc:title>Generated offer</dc:title>", resaved.GetEntryString("meta.xml"), StringComparison.Ordinal);
        Assert.Equal("bold", GetEffectiveProperty(resaved, "bold", OdtDocumentVerifier.Fo + "font-weight"));
        Assert.Equal("#ff0000", GetEffectiveProperty(resaved, "bold", OdtDocumentVerifier.Fo + "color"));
        Assert.Equal("italic", GetEffectiveProperty(resaved, "italic", OdtDocumentVerifier.Fo + "font-style"));
        Assert.Null(GetEffectiveProperty(resaved, "plain", OdtDocumentVerifier.Fo + "font-weight"));
    }

    /// <summary>
    /// Gets a text property of the innermost span holding <paramref name="text"/>, following the span's
    /// ancestors (nested spans combine), from the automatic styles of content.xml.
    /// </summary>
    private const string NumberedListStyle =
        "<text:list-style style:name=\"L1\"><text:list-level-style-number text:level=\"1\" style:num-suffix=\".\" style:num-format=\"1\">" +
        "<style:list-level-properties text:list-level-position-and-space-mode=\"label-alignment\">" +
        "<style:list-level-label-alignment text:label-followed-by=\"space\" fo:text-indent=\"-0.635cm\" fo:margin-left=\"1.27cm\"/>" +
        "</style:list-level-properties></text:list-level-style-number></text:list-style>";

    private static string NumberedList(string id, string text) =>
        $"<text:list xml:id=\"{id}\" text:style-name=\"L1\"><text:list-item><text:p>{text}</text:p></text:list-item></text:list>";

    [Fact]
    public void NumberedListsInLoops_ContinueTheirNumberingInLibreOffice()
    {
        LibreOfficeRunner.RequireExecutable();

        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddAutomaticStyles(NumberedListStyle)
            .AddParagraph("{{#foreach Items}}")
            .AddXml(NumberedList("list1", "{{Name}}"))
            .AddParagraph("Note {{Name}}")
            .AddXml("<text:list text:style-name=\"L1\"><text:list-item><text:p>Second {{Name}}</text:p></text:list-item></text:list>")
            .AddParagraph("{{/foreach}}")
            .AddXml(NumberedList("list2", "Restarts"))
            .AddParagraph("{{#foreach Groups}}")
            .AddParagraph("Group {{Name}}")
            .AddParagraph("{{#foreach Items}}")
            .AddXml(NumberedList("list3", "{{Name}}"))
            .AddParagraph("{{/foreach}}")
            .AddParagraph("{{/foreach}}")
            .AddXml(
                "<table:table table:name=\"T\"><table:table-column/>" +
                "<table:table-row><table:table-cell><text:p>{{#foreach Items}}</text:p></table:table-cell></table:table-row>" +
                "<table:table-row><table:table-cell>" + NumberedList("list4", "Cell {{Name}}") + "</table:table-cell></table:table-row>" +
                "<table:table-row><table:table-cell><text:p>{{/foreach}}</text:p></table:table-cell></table:table-row>" +
                "</table:table>");

        List<Dictionary<string, object>> items = new List<Dictionary<string, object>>
        {
            new() { ["Name"] = "A" },
            new() { ["Name"] = "B" },
            new() { ["Name"] = "C" },
        };
        byte[] output = ProcessToBytes(template, new Dictionary<string, object>
        {
            ["Items"] = items,
            ["Groups"] = new List<Dictionary<string, object>>
            {
                new() { ["Name"] = "G1", ["Items"] = items.Take(2).ToList() },
                new() { ["Name"] = "G2", ["Items"] = items.Take(2).ToList() },
            },
        });

        string[] lines = LibreOfficeRunner.ConvertToTextLines(output).Select(l => l.Trim()).ToArray();
        Assert.Equal(
            new[]
            {
                "1. A", "Note A", "1. Second A",
                "2. B", "Note B", "2. Second B",
                "3. C", "Note C", "3. Second C",
                "1. Restarts",
                "Group G1", "1. A", "2. B",
                "Group G2", "3. A", "4. B",
                "1. Cell A", "2. Cell B", "3. Cell C",
            },
            lines);
    }

    [Fact]
    public void DocxConvertedByLibreOffice_ProcessedAsOdt_MatchesDocxProcessing()
    {
        LibreOfficeRunner.RequireExecutable();

        DocumentBuilder docx = new DocumentBuilder()
            .AddHeading("Offer {{Number}}")
            .AddParagraphWithRuns(("Dear {{Cus", null), ("tomer.Na", DocumentBuilder.CreateFormatting(bold: true)), ("me}}, welcome.", null))
            .AddParagraph("{{#if Vip}}")
            .AddParagraph("VIP: {{Discount:number:P0}} off")
            .AddParagraph("{{#else}}")
            .AddParagraph("No discount")
            .AddParagraph("{{/if}}")
            .AddParagraph("Inline: {{#if Vip}}yes{{#else}}no{{/if}} and {{Md}}")
            .AddParagraph("{{#foreach Items}}")
            .AddNumberedListItem("{{@number}}) {{Name}} {{Price:currency}}")
            .AddParagraph("{{/foreach}}")
            .AddParagraph("{{#foreach Groups}}")
            .AddParagraph("Group {{Name}}")
            .AddParagraph("{{#foreach Items}}")
            .AddNumberedListItem("{{Name}}")
            .AddParagraph("{{/foreach}}")
            .AddParagraph("{{/foreach}}")
            .AddParagraph("Bullets:")
            .AddBulletListItem("{{#foreach Items}}")
            .AddBulletListItem("{{Name}}")
            .AddBulletListItem("{{/foreach}}")
            .AddTableWithCellParagraphs(4, 2, (row, col) => row switch
            {
                0 => new[] { col == 0 ? "Name" : "Price" },
                1 => new[] { col == 0 ? "{{#foreach Items}}" : string.Empty },
                2 => new[] { col == 0 ? "{{Name}}" : "{{Price:number:N2}}" },
                _ => new[] { col == 0 ? "{{/foreach}}" : string.Empty },
            })
            .AddParagraph("Tabs\tand  double  spaces {{Spaced}} end");

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Number"] = 7,
            ["Customer"] = new Dictionary<string, object> { ["Name"] = "Anna" },
            ["Vip"] = true,
            ["Discount"] = 0.1,
            ["Md"] = "**bold** and *italic*",
            ["Items"] = new List<Dictionary<string, object>>
            {
                new() { ["Name"] = "Widget", ["Price"] = 9.5m },
                new() { ["Name"] = "Gadget", ["Price"] = 20m },
                new() { ["Name"] = "Gizmo", ["Price"] = 1m },
            },
            ["Spaced"] = " a  b ",
        };
        data["Groups"] = new List<Dictionary<string, object>>
        {
            new() { ["Name"] = "G1", ["Items"] = data["Items"] },
            new() { ["Name"] = "G2", ["Items"] = data["Items"] },
        };

        byte[] docxTemplate = docx.ToStream().ToArray();
        byte[] odtTemplate = LibreOfficeRunner.Convert(docxTemplate, "docx", "odt", "odt");
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions { Culture = CultureInfo.InvariantCulture };

        ProcessingResult docxResult = new DocumentTemplateProcessor(options).ProcessTemplate(docxTemplate, data, out byte[] docxOutput);
        ProcessingResult odtResult = new OdtTemplateProcessor(options).ProcessTemplate(odtTemplate, data, out byte[] odtOutput);
        Assert.True(docxResult.IsSuccess, docxResult.ErrorMessage);
        Assert.True(odtResult.IsSuccess, odtResult.ErrorMessage);
        Assert.Equal(docxResult.ReplacementCount, odtResult.ReplacementCount);
        new OdtDocumentVerifier(odtOutput).AssertValidOdtPackage();

        string[] docxLines = Encoding.UTF8.GetString(LibreOfficeRunner.Convert(docxOutput, "docx", "txt:Text (encoded):UTF8", "txt"))
            .TrimStart('\uFEFF').Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
        string[] odtLines = LibreOfficeRunner.ConvertToTextLines(odtOutput);
        Assert.Equal(docxLines, odtLines);
        Assert.Contains(odtLines, l => l.Trim() == "3. 3) Gizmo ¤1.00");
    }

    [Fact]
    public void HeaderRegions_ProcessedDocumentOpensInLibreOffice()
    {
        LibreOfficeRunner.RequireExecutable();

        // LibreOffice Writer itself does not display header regions (it drops them on load), so this only checks
        // that a processed document with regions stays loadable; the processing is covered by OdtContainerTests.
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddHeaderXml(
                "<style:region-left><text:p>L {{Name}}</text:p></style:region-left>" +
                "<style:region-right><text:p>{{#if Show}}</text:p><text:p>R {{Name}}</text:p><text:p>{{/if}}</text:p></style:region-right>")
            .AddParagraph("Body {{Name}}");

        byte[] output = ProcessToBytes(template, new Dictionary<string, object> { ["Name"] = "X", ["Show"] = true });

        Assert.Equal(new[] { "L X", "R X" }, new OdtDocumentVerifier(output).GetHeaderTexts());
        Assert.Equal("Body X", LibreOfficeRunner.ConvertToTextLines(output)[0]);
        byte[] pdf = LibreOfficeRunner.Convert(output, "odt", "pdf", "pdf");
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4), StringComparison.Ordinal);
    }

    [Fact]
    public void MarkdownStyle_DoesNotShadowACommonStyle_InLibreOffice()
    {
        LibreOfficeRunner.RequireExecutable();

        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddCommonStyles("<style:style style:name=\"T3\" style:family=\"text\"><style:text-properties fo:color=\"#ff0000\"/></style:style>")
            .AddXml("<text:p><text:span text:style-name=\"T3\">red</text:span> {{Bold}}</text:p>");

        byte[] output = ProcessToBytes(template, new Dictionary<string, object> { ["Bold"] = "**b**" });
        OdtDocumentVerifier resaved = new OdtDocumentVerifier(LibreOfficeRunner.Convert(output, "odt", "odt", "odt"));

        // LibreOffice still reads the red text with the common style, and only the markdown text as bold.
        System.Xml.Linq.XElement red = resaved.Body.Descendants(OdtDocumentVerifier.Text + "span").Single(s => s.Value == "red");
        Assert.Equal("T3", (string?)red.Attribute(OdtDocumentVerifier.Text + "style-name"));
        Assert.Null(GetEffectiveProperty(resaved, "red", OdtDocumentVerifier.Fo + "font-weight"));
        Assert.Equal("bold", GetEffectiveProperty(resaved, "b", OdtDocumentVerifier.Fo + "font-weight"));
    }

    private static string? GetEffectiveProperty(OdtDocumentVerifier document, string text, System.Xml.Linq.XName property)
    {
        System.Xml.Linq.XElement? automaticStyles = document.ContentXml.Root!.Element(OdtDocumentVerifier.Office + "automatic-styles");
        System.Xml.Linq.XElement span = document.Body.Descendants(OdtDocumentVerifier.Text + "span")
            .Last(s => s.Nodes().OfType<System.Xml.Linq.XText>().Any(t => t.Value.Contains(text, StringComparison.Ordinal)));

        foreach (System.Xml.Linq.XElement candidate in span.AncestorsAndSelf(OdtDocumentVerifier.Text + "span"))
        {
            string? styleName = (string?)candidate.Attribute(OdtDocumentVerifier.Text + "style-name");
            string? value = automaticStyles?.Elements(OdtDocumentVerifier.Style + "style")
                .Where(s => (string?)s.Attribute(OdtDocumentVerifier.Style + "name") == styleName)
                .Select(s => (string?)s.Element(OdtDocumentVerifier.Style + "text-properties")?.Attribute(property))
                .FirstOrDefault();
            if (value != null)
            {
                return value;
            }
        }

        return null;
    }

    private static byte[] ProcessToBytes(OdtDocumentBuilder template, Dictionary<string, object> data)
    {
        OdtTemplateProcessor processor = new OdtTemplateProcessor(new PlaceholderReplacementOptions { Culture = CultureInfo.InvariantCulture });
        ProcessingResult result = processor.ProcessTemplate(template.ToBytes(), data, out byte[] output);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        new OdtDocumentVerifier(output).AssertValidOdtPackage();
        return output;
    }
}
