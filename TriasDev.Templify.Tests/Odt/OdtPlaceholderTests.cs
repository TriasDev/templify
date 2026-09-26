// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Xml.Linq;
using TriasDev.Templify.Core;
using TriasDev.Templify.Formatting;
using TriasDev.Templify.Replacements;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Odt;

/// <summary>
/// Placeholder replacement in OpenDocument Text documents.
/// </summary>
public sealed class OdtPlaceholderTests
{
    private static readonly XNamespace _text = OdtDocumentVerifier.Text;

    [Fact]
    public void SimplePlaceholder_IsReplaced()
    {
        (ProcessingResult result, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("Hello {{Name}}!"),
            new Dictionary<string, object> { ["Name"] = "World" });

        Assert.Equal("Hello World!", output.GetParagraphTexts()[0]);
        Assert.Equal(1, result.ReplacementCount);
        Assert.Empty(result.MissingVariables);
        Assert.False(result.HasWarnings);
    }

    [Fact]
    public void MultiplePlaceholders_InOneParagraph_AreReplaced()
    {
        (ProcessingResult result, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{A}}-{{B}}-{{A}}"),
            new Dictionary<string, object> { ["A"] = "1", ["B"] = "2" });

        Assert.Equal("1-2-1", output.GetParagraphTexts()[0]);
        Assert.Equal(3, result.ReplacementCount);
    }

    [Fact]
    public void NestedPaths_ArrayIndexAndDictionary_AreResolved()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Customer"] = new { Name = "Alice", Address = new { City = "Berlin" } },
            ["Items"] = new List<object> { new { Name = "First" }, new { Name = "Second" } },
            ["Settings"] = new Dictionary<string, object> { ["Theme"] = "Dark" },
        };

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{Customer.Name}}, {{Customer.Address.City}}, {{Items[1].Name}}, {{Settings[Theme]}}, {{Settings.Theme}}"),
            data);

        Assert.Equal("Alice, Berlin, Second, Dark, Dark", output.GetParagraphTexts()[0]);
    }

    [Fact]
    public void PlaceholderSplitAcrossSpans_IsReplaced_WithFormattingOfFirstCharacter()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddXml(
                "<text:p>Hello <text:span text:style-name=\"T1\">{{Na</text:span><text:span text:style-name=\"T2\">me}}</text:span> and more</text:p>"),
            new Dictionary<string, object> { ["Name"] = "World" });

        XElement paragraph = output.Body.Element(_text + "p")!;
        Assert.Equal("Hello World and more", OdtDocumentVerifier.RenderText(paragraph));

        // The value is in the bold span (first character), the emptied italic span is removed.
        XElement bold = Assert.Single(paragraph.Elements(_text + "span"));
        Assert.Equal("T1", (string?)bold.Attribute(_text + "style-name"));
        Assert.Equal("World", bold.Value);
    }

    [Fact]
    public void PlaceholderSplitAcrossPlainTextAndSpan_KeepsSurroundingSpans()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddXml(
                "<text:p>{{Fir<text:span text:style-name=\"T1\">st}} bold</text:span></text:p>"),
            new Dictionary<string, object> { ["First"] = "Value" });

        XElement paragraph = output.Body.Element(_text + "p")!;
        Assert.Equal("Value bold", OdtDocumentVerifier.RenderText(paragraph));
        Assert.Equal(" bold", paragraph.Element(_text + "span")!.Value);
    }

    [Fact]
    public void PlaceholderInNestedSpansAndHyperlink_IsReplaced()
    {
        string text = OdtTestHelper.ProcessParagraphXml(
            "<text:p><text:a xlink:type=\"simple\" xlink:href=\"https://example.com\"><text:span text:style-name=\"T1\">{{Link" +
            "<text:span text:style-name=\"T2\">Text}}</text:span></text:span></text:a> after</text:p>",
            new Dictionary<string, object> { ["LinkText"] = "Example" });

        Assert.Equal("Example after", text);
    }

    [Fact]
    public void PlaceholderInHyperlink_KeepsHyperlink()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddXml(
                "<text:p>See <text:a xlink:type=\"simple\" xlink:href=\"https://example.com\">{{LinkText}}</text:a></text:p>"),
            new Dictionary<string, object> { ["LinkText"] = "Example" });

        XElement link = output.Body.Descendants(_text + "a").Single();
        Assert.Equal("Example", link.Value);
    }

    [Fact]
    public void SpacesElements_AroundPlaceholder_ArePreserved()
    {
        string text = OdtTestHelper.ProcessParagraphXml(
            "<text:p>A <text:s text:c=\"2\"/>{{Name}} <text:s/>B</text:p>",
            new Dictionary<string, object> { ["Name"] = "x" });

        Assert.Equal("A   x  B", text);
    }

    [Fact]
    public void ValueWithMultipleSpaces_RendersAllSpaces()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("[{{Value}}]"),
            new Dictionary<string, object> { ["Value"] = "a   b" });

        Assert.Equal("[a   b]", output.GetParagraphTexts()[0]);
        Assert.Contains(output.Body.Descendants(_text + "s"), s => (string?)s.Attribute(_text + "c") == "2");
    }

    [Fact]
    public void ValueWithLeadingSpace_AtParagraphStart_IsKept()
    {
        string text = OdtTestHelper.ProcessParagraphXml(
            "<text:p>{{Value}}</text:p>",
            new Dictionary<string, object> { ["Value"] = " indented" });

        Assert.Equal(" indented", text);
    }

    [Fact]
    public void ValueWithLeadingSpace_AfterLiteralSpace_IsKept()
    {
        string text = OdtTestHelper.ProcessParagraphXml(
            "<text:p>a {{Value}}</text:p>",
            new Dictionary<string, object> { ["Value"] = " b" });

        Assert.Equal("a  b", text);
    }

    [Fact]
    public void ValueWithTrailingSpace_BeforeLiteralSpace_IsKept()
    {
        string text = OdtTestHelper.ProcessParagraphXml(
            "<text:p>{{Value}} b</text:p>",
            new Dictionary<string, object> { ["Value"] = "a " });

        Assert.Equal("a  b", text);
    }

    [Fact]
    public void ValueWithTab_BecomesTabElement()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{Value}}"),
            new Dictionary<string, object> { ["Value"] = "a\tb" });

        Assert.Equal("a\tb", output.GetParagraphTexts()[0]);
        Assert.Single(output.Body.Descendants(_text + "tab"));
    }

    [Fact]
    public void TabAndLineBreakInTemplate_ArePreserved()
    {
        string text = OdtTestHelper.ProcessParagraphXml(
            "<text:p><text:tab/>{{A}}<text:line-break/>{{B}}</text:p>",
            new Dictionary<string, object> { ["A"] = "1", ["B"] = "2" });

        Assert.Equal("\t1\n2", text);
    }

    [Theory]
    [InlineData("Line1\nLine2")]
    [InlineData("Line1\r\nLine2")]
    [InlineData("Line1\rLine2")]
    public void NewlineInValue_BecomesLineBreak(string value)
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{Value}}"),
            new Dictionary<string, object> { ["Value"] = value });

        Assert.Equal("Line1\nLine2", output.GetParagraphTexts()[0]);
        Assert.Single(output.Body.Descendants(_text + "line-break"));
    }

    [Fact]
    public void NewlineInValue_WithNewlineSupportDisabled_RendersAsSpace()
    {
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture,
            EnableNewlineSupport = false,
        };

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{Value}}"),
            new Dictionary<string, object> { ["Value"] = "Line1\nLine2" },
            options);

        Assert.Equal("Line1 Line2", output.GetParagraphTexts()[0]);
        Assert.Empty(output.Body.Descendants(_text + "line-break"));
    }

    [Fact]
    public void SpecialXmlCharacters_AreEscaped()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{Value}}"),
            new Dictionary<string, object> { ["Value"] = "<b>Tom & \"Jerry\"</b> 'x'" });

        Assert.Equal("<b>Tom & \"Jerry\"</b> 'x'", output.GetParagraphTexts()[0]);
    }

    [Fact]
    public void InvalidXmlCharacters_AreRemoved()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{Value}}"),
            new Dictionary<string, object> { ["Value"] = "a\u0002b\u0000c" });

        Assert.Equal("abc", output.GetParagraphTexts()[0]);
    }

    [Fact]
    public void Heading_PlaceholderIsReplaced_AndHeadingKept()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddHeading("Chapter {{Number}}: {{Title}}", level: 2),
            new Dictionary<string, object> { ["Number"] = 3, ["Title"] = "Results" });

        XElement heading = output.Body.Element(_text + "h")!;
        Assert.Equal("Chapter 3: Results", OdtDocumentVerifier.RenderText(heading));
        Assert.Equal("2", (string?)heading.Attribute(_text + "outline-level"));
    }

    [Fact]
    public void TableCells_PlaceholdersAreReplaced()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddTable(
                new[] { "Name", "{{Name}}" },
                new[] { "City", "{{City}}" }),
            new Dictionary<string, object> { ["Name"] = "Alice", ["City"] = "Berlin" });

        Assert.Equal(new[] { "Name", "Alice", "City", "Berlin" }, output.GetParagraphTexts());
    }

    [Fact]
    public void TableHeaderRowsAndNestedTables_PlaceholdersAreReplaced()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddXml(
                "<table:table table:name=\"T\"><table:table-column table:number-columns-repeated=\"2\"/>" +
                "<table:table-header-rows><table:table-row><table:table-cell><text:p>{{H}}</text:p></table:table-cell>" +
                "<table:covered-table-cell/></table:table-row></table:table-header-rows>" +
                "<table:table-row><table:table-cell><table:table table:name=\"Inner\"><table:table-column/>" +
                "<table:table-row><table:table-cell><text:p>{{Inner}}</text:p></table:table-cell></table:table-row></table:table>" +
                "</table:table-cell><table:table-cell><text:p>{{C}}</text:p></table:table-cell></table:table-row></table:table>"),
            new Dictionary<string, object> { ["H"] = "Header", ["Inner"] = "Nested", ["C"] = "Cell" });

        Assert.Equal(new[] { "Header", "Nested", "Cell" }, output.GetParagraphTexts());
    }

    [Fact]
    public void ListsAndSections_PlaceholdersAreReplaced()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddXml(
                "<text:list><text:list-item><text:p>{{A}}</text:p><text:list><text:list-item><text:p>{{B}}</text:p>" +
                "</text:list-item></text:list></text:list-item></text:list>" +
                "<text:section text:name=\"S1\"><text:p>{{C}}</text:p></text:section>"),
            new Dictionary<string, object> { ["A"] = "a", ["B"] = "b", ["C"] = "c" });

        Assert.Equal(new[] { "a", "b", "c" }, output.GetParagraphTexts());
    }

    [Fact]
    public void TextBoxAndFootnote_PlaceholdersAreReplaced_AndAnchorsKept()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddXml(
                "<text:p>Main {{Main}}<draw:frame draw:name=\"Frame1\" text:anchor-type=\"as-char\" svg:width=\"2cm\">" +
                "<draw:text-box><text:p>Box {{Box}}</text:p></draw:text-box></draw:frame>" +
                "<text:note text:id=\"ftn1\" text:note-class=\"footnote\"><text:note-citation>1</text:note-citation>" +
                "<text:note-body><text:p>Note {{Note}}</text:p></text:note-body></text:note> end</text:p>"),
            new Dictionary<string, object> { ["Main"] = "M", ["Box"] = "B", ["Note"] = "N" });

        XElement paragraph = output.Body.Element(_text + "p")!;
        Assert.Equal("Main M end", OdtDocumentVerifier.RenderText(paragraph));
        Assert.Single(paragraph.Elements(OdtDocumentVerifier.Draw + "frame"));
        Assert.Single(paragraph.Elements(_text + "note"));
        Assert.Contains("Box B", output.GetParagraphTexts());
        Assert.Contains("Note N", output.GetParagraphTexts());
    }

    [Fact]
    public void PageAnchoredFrameInBody_PlaceholdersAreReplaced()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddXml(
                "<draw:frame draw:name=\"F\" text:anchor-type=\"page\" text:anchor-page-number=\"1\">" +
                "<draw:text-box><text:p>{{Value}}</text:p></draw:text-box></draw:frame><text:p/>"),
            new Dictionary<string, object> { ["Value"] = "Framed" });

        Assert.Equal("Framed", output.GetParagraphTexts()[0]);
    }

    [Fact]
    public void AnnotationContent_IsNotProcessed()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddXml(
                "<text:p>{{A}}<office:annotation><dc:creator>Reviewer</dc:creator><text:p>{{A}} in comment</text:p></office:annotation></text:p>"),
            new Dictionary<string, object> { ["A"] = "x" });

        XElement annotation = output.Body.Descendants(OdtDocumentVerifier.Office + "annotation").Single();
        Assert.Equal("{{A}} in comment", annotation.Element(_text + "p")!.Value);
        Assert.Equal("x", OdtDocumentVerifier.RenderText(output.Body.Element(_text + "p")!));
    }

    [Fact]
    public void TrackedDeletions_AreNotProcessed()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddXml(
                "<text:tracked-changes><text:changed-region text:id=\"ct1\"><text:deletion><office:change-info>" +
                "<dc:creator>A</dc:creator></office:change-info><text:p>{{A}}</text:p></text:deletion></text:changed-region>" +
                "</text:tracked-changes><text:p>{{A}}</text:p>"),
            new Dictionary<string, object> { ["A"] = "x" });

        Assert.Equal("{{A}}", output.Body.Descendants(_text + "deletion").Single().Element(_text + "p")!.Value);
        Assert.Equal(new[] { "x" }, output.GetParagraphTexts());
    }

    [Fact]
    public void HeadersAndFooters_PlaceholdersAreReplaced()
    {
        (ProcessingResult result, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder()
                .AddParagraph("Body")
                .AddHeaderParagraph("Header {{Company}}")
                .AddFooterXml("<text:p>Page <text:page-number text:select-page=\"current\">1</text:page-number> {{Company}}</text:p>"),
            new Dictionary<string, object> { ["Company"] = "ACME" });

        Assert.Equal(new[] { "Header ACME" }, output.GetHeaderTexts());
        Assert.Equal(new[] { "Page ACME" }, output.GetFooterTexts()); // the verifier does not render fields
        Assert.Single(output.StylesXml!.Descendants(_text + "page-number"));
        Assert.Equal(2, result.ReplacementCount);
    }

    [Fact]
    public void FieldsAndBookmarksOutsidePlaceholder_AreKept()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddXml(
                "<text:p><text:bookmark-start text:name=\"b\"/>{{A}}<text:bookmark-end text:name=\"b\"/> " +
                "<text:date>2026-01-01</text:date></text:p>"),
            new Dictionary<string, object> { ["A"] = "x" });

        XElement paragraph = output.Body.Element(_text + "p")!;
        Assert.Single(paragraph.Elements(_text + "bookmark-start"));
        Assert.Single(paragraph.Elements(_text + "bookmark-end"));
        Assert.Single(paragraph.Elements(_text + "date"));
    }

    [Fact]
    public void BookmarkInsidePlaceholder_IsKept()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddXml("<text:p>{{Na<text:bookmark text:name=\"m\"/>me}}</text:p>"),
            new Dictionary<string, object> { ["Name"] = "Value" });

        XElement paragraph = output.Body.Element(_text + "p")!;
        Assert.Equal("Value", OdtDocumentVerifier.RenderText(paragraph));
        Assert.Single(paragraph.Elements(_text + "bookmark"));
    }

    [Theory]
    [InlineData("{{Amount:currency}}", "¤1,234.50")]
    [InlineData("{{Amount:number:N2}}", "1,234.50")]
    [InlineData("{{Rate:number:P}}", "12.50 %")]
    [InlineData("{{Name:uppercase}}", "ALICE")]
    [InlineData("{{Name:lowercase}}", "alice")]
    [InlineData("{{Date:date:yyyy-MM-dd}}", "2026-09-26")]
    [InlineData("{{Flag:yesno}}", "Yes")]
    [InlineData("{{Flag:checkbox}}", "☑")]
    public void FormatSpecifiers_AreApplied(string placeholder, string expected)
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Amount"] = 1234.5m,
            ["Rate"] = 0.125m,
            ["Name"] = "Alice",
            ["Date"] = new DateTime(2026, 9, 26),
            ["Flag"] = true,
        };

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(new OdtDocumentBuilder().AddParagraph(placeholder), data);

        Assert.Equal(expected, output.GetParagraphTexts()[0]);
    }

    [Fact]
    public void Culture_IsUsedForFormatting()
    {
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions { Culture = new CultureInfo("de-DE") };

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{Amount}} | {{Amount:number:N2}} | {{Date:date:d. MMMM yyyy}}"),
            new Dictionary<string, object> { ["Amount"] = 1234.5m, ["Date"] = new DateTime(2026, 3, 1) },
            options);

        Assert.Equal("1234,5 | 1.234,50 | 1. März 2026", output.GetParagraphTexts()[0]);
    }

    [Fact]
    public void BooleanFormatterRegistry_IsUsed()
    {
        BooleanFormatterRegistry registry = new BooleanFormatterRegistry();
        registry.Register("ja", new BooleanFormatter("Ja", "Nein"));
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture,
            BooleanFormatterRegistry = registry,
        };

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{Flag:ja}}"),
            new Dictionary<string, object> { ["Flag"] = false },
            options);

        Assert.Equal("Nein", output.GetParagraphTexts()[0]);
    }

    [Fact]
    public void ExpressionPlaceholder_IsEvaluated()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddXml("<text:p>{{(Count &gt; 0 and <text:s/>IsActive):yesno}}</text:p>"),
            new Dictionary<string, object> { ["Count"] = 2, ["IsActive"] = true });

        Assert.Equal("Yes", output.GetParagraphTexts()[0]);
    }

    [Fact]
    public void InvalidExpression_AddsWarning()
    {
        (ProcessingResult result, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{(Count >)}}"),
            new Dictionary<string, object> { ["Count"] = 2 });

        Assert.Equal("{{(Count >)}}", output.GetParagraphTexts()[0]);
        Assert.Contains(result.Warnings, w => w.Type == ProcessingWarningType.ExpressionFailed);
    }

    [Fact]
    public void TextReplacements_AreApplied()
    {
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture,
            TextReplacements = TextReplacements.HtmlEntities,
        };

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{Value}}"),
            new Dictionary<string, object> { ["Value"] = "a&nbsp;&amp;&nbsp;b" },
            options);

        Assert.Equal("a & b", output.GetParagraphTexts()[0]);
    }

    [Fact]
    public void MarkdownInValue_IsInsertedAsText_ForNow()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{Value}} {{Value:raw}}"),
            new Dictionary<string, object> { ["Value"] = "**bold**" });

        Assert.Equal("**bold** **bold**", output.GetParagraphTexts()[0]);
    }

    [Fact]
    public void MissingVariable_LeaveUnchanged_IsDefault_AndReported()
    {
        (ProcessingResult result, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("Hello {{Missing}} and {{Name}}"),
            new Dictionary<string, object> { ["Name"] = "Bob" });

        Assert.Equal("Hello {{Missing}} and Bob", output.GetParagraphTexts()[0]);
        Assert.Equal(new[] { "Missing" }, result.MissingVariables);
        Assert.Contains(result.Warnings, w => w.Type == ProcessingWarningType.MissingVariable && w.VariableName == "Missing");
        Assert.Equal(1, result.ReplacementCount);
    }

    [Fact]
    public void MissingVariable_ReplaceWithEmpty_RemovesPlaceholder()
    {
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            Culture = CultureInfo.InvariantCulture,
            MissingVariableBehavior = MissingVariableBehavior.ReplaceWithEmpty,
        };

        (ProcessingResult result, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddXml("<text:p>[<text:span text:style-name=\"T1\">{{Missing}}</text:span>]</text:p>"),
            new Dictionary<string, object>(),
            options);

        Assert.Equal("[]", output.GetParagraphTexts()[0]);
        Assert.Empty(output.Body.Descendants(_text + "span"));
        Assert.Equal(new[] { "Missing" }, result.MissingVariables);
    }

    [Fact]
    public void MissingVariable_ThrowException_Throws()
    {
        OdtTemplateProcessor processor = new OdtTemplateProcessor(new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.ThrowException,
        });
        byte[] template = new OdtDocumentBuilder().AddParagraph("{{Missing}}").ToBytes();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => processor.ProcessTemplate(template, new Dictionary<string, object>(), out _));

        Assert.Equal(typeof(InvalidOperationException), exception.GetType());
        Assert.Contains("Missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NullValue_IsReplacedWithEmpty()
    {
        (ProcessingResult result, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("[{{Value}}]"),
            new Dictionary<string, object> { ["Value"] = null! });

        Assert.Equal("[]", output.GetParagraphTexts()[0]);
        Assert.Empty(result.MissingVariables);
    }

    [Fact]
    public void LoopMarkers_AreLeftAsText_ForNow()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{#foreach Items}}{{Name}}{{/foreach}}"),
            new Dictionary<string, object> { ["Items"] = new List<string> { "a" }, ["Name"] = "N" });

        // Marker paragraphs are not processed for placeholders, as in Word documents.
        Assert.Equal("{{#foreach Items}}{{Name}}{{/foreach}}", output.GetParagraphTexts()[0]);
    }

    [Fact]
    public void ReplacedValue_IsNotReprocessedAsTemplate()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddParagraph("{{A}}{{B}}"),
            new Dictionary<string, object> { ["A"] = "{{B}}", ["B"] = "b" });

        Assert.Equal("{{B}}b", output.GetParagraphTexts()[0]);
    }

    [Fact]
    public void EmptyParagraphsAndParagraphsWithoutPlaceholders_AreUnchanged()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddXml("<text:p/><text:p text:style-name=\"P1\">Plain <text:span text:style-name=\"T1\">text</text:span></text:p>");

        (ProcessingResult result, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object>());

        Assert.Equal(0, result.ReplacementCount);
        Assert.Equal(new[] { string.Empty, "Plain text" }, output.GetParagraphTexts());
    }
}
