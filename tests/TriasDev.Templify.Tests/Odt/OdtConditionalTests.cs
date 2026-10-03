// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Odt;

/// <summary>
/// Conditionals (<c>{{#if}}</c>/<c>{{#elseif}}</c>/<c>{{#else}}</c>/<c>{{/if}}</c>) in OpenDocument Text documents.
/// </summary>
public sealed class OdtConditionalTests
{
    private static readonly XNamespace _text = OdtDocumentVerifier.Text;
    private static readonly XNamespace _table = OdtDocumentVerifier.Table;

    private static OdtDocumentBuilder Paragraphs(params string[] texts)
    {
        OdtDocumentBuilder builder = new OdtDocumentBuilder();
        foreach (string text in texts)
        {
            builder.AddParagraph(text);
        }

        return builder;
    }

    private static List<string> Process(OdtDocumentBuilder template, Dictionary<string, object> data) =>
        OdtTestHelper.Process(template, data).Output.GetParagraphTexts();

    private static ProcessingResult ProcessExpectingFailure(OdtDocumentBuilder template, Dictionary<string, object> data)
    {
        ProcessingResult result = new OdtTemplateProcessor(OdtTestHelper.InvariantOptions())
            .ProcessTemplate(template.ToBytes(), data, out byte[] output);
        Assert.False(result.IsSuccess);
        Assert.Empty(output);
        return result;
    }

    [Theory]
    [InlineData(true, new[] { "Before", "Shown Alice", "After" })]
    [InlineData(false, new[] { "Before", "After" })]
    public void BlockConditional_KeepsOrRemovesContent(bool show, string[] expected)
    {
        List<string> texts = Process(
            Paragraphs("Before", "{{#if Show}}", "Shown {{Name}}", "{{/if}}", "After"),
            new Dictionary<string, object> { ["Show"] = show, ["Name"] = "Alice" });

        Assert.Equal(expected, texts);
    }

    [Theory]
    [InlineData("Active", "A")]
    [InlineData("Pending", "P")]
    [InlineData("Other", "E")]
    public void BlockConditional_ElseIfChain_KeepsFirstMatchingBranch(string status, string expected)
    {
        List<string> texts = Process(
            Paragraphs("{{#if Status = \"Active\"}}", "A", "{{#elseif Status = \"Pending\"}}", "P", "{{#else}}", "E", "{{/if}}"),
            new Dictionary<string, object> { ["Status"] = status });

        Assert.Equal(new[] { expected }, texts);
    }

    [Fact]
    public void BlockConditional_Nested_AreEvaluated()
    {
        List<string> texts = Process(
            Paragraphs("{{#if A}}", "a", "{{#if B}}", "b", "{{#else}}", "not b", "{{/if}}", "{{/if}}", "end"),
            new Dictionary<string, object> { ["A"] = true, ["B"] = false });

        Assert.Equal(new[] { "a", "not b", "end" }, texts);
    }

    [Fact]
    public void BlockConditional_RemovedBranch_PlaceholdersAreNotReportedMissing()
    {
        (ProcessingResult result, OdtDocumentVerifier output) = OdtTestHelper.Process(
            Paragraphs("{{#if Show}}", "{{Missing}}", "{{/if}}", "x"),
            new Dictionary<string, object> { ["Show"] = false });

        Assert.Equal(new[] { "x" }, output.GetParagraphTexts());
        Assert.Empty(result.MissingVariables);
    }

    [Fact]
    public void BlockConditional_ContainingTableAndList_RemovesThem()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("{{#if Show}}")
            .AddTable(new[] { "cell" })
            .AddXml("<text:list><text:list-item><text:p>item</text:p></text:list-item><text:list-item><text:p>item2</text:p></text:list-item></text:list>")
            .AddParagraph("{{/if}}")
            .AddParagraph("end");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Show"] = false });

        Assert.Equal(new[] { "end" }, output.GetParagraphTexts());
        Assert.Empty(output.Body.Elements(_table + "table"));
        Assert.Empty(output.Body.Elements(_text + "list"));
    }

    [Fact]
    public void BlockConditional_MarkersInHeadings_AreRemoved()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddHeading("{{#if Show}}")
            .AddHeading("Title {{Name}}")
            .AddHeading("{{/if}}");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Show"] = true, ["Name"] = "X" });

        XElement heading = Assert.Single(output.Body.Elements(_text + "h"));
        Assert.Equal("Title X", OdtDocumentVerifier.RenderText(heading));
    }

    [Theory]
    [InlineData(true, "Dear Mr. Smith, welcome")]
    [InlineData(false, "Dear Ms. Smith, welcome")]
    public void InlineConditional_WithElse_KeepsSurroundingText(bool male, string expected)
    {
        List<string> texts = Process(
            Paragraphs("Dear {{#if IsMale}}Mr.{{#else}}Ms.{{/if}} {{Name}}, welcome"),
            new Dictionary<string, object> { ["IsMale"] = male, ["Name"] = "Smith" });

        Assert.Equal(new[] { expected }, texts);
    }

    [Fact]
    public void InlineConditional_ElseIfAndNested_AreEvaluated()
    {
        List<string> texts = Process(
            Paragraphs("[{{#if A}}a{{#elseif B}}b{{#if C}}c{{/if}}{{#else}}x{{/if}}]"),
            new Dictionary<string, object> { ["A"] = false, ["B"] = true, ["C"] = true });

        Assert.Equal(new[] { "[bc]" }, texts);
    }

    [Fact]
    public void InlineConditional_MultipleInOneParagraph_AreEvaluated()
    {
        List<string> texts = Process(
            Paragraphs("{{#if A}}1{{/if}}-{{#if B}}2{{/if}}-{{#if C}}3{{/if}}"),
            new Dictionary<string, object> { ["A"] = true, ["B"] = false, ["C"] = true });

        Assert.Equal(new[] { "1--3" }, texts);
    }

    [Fact]
    public void InlineConditional_SplitAcrossSpans_KeepsFormattingOfKeptText()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            new OdtDocumentBuilder().AddXml(
                "<text:p>Status: {{#if <text:span text:style-name=\"T2\">Active}}</text:span>" +
                "<text:span text:style-name=\"T1\">on</text:span>{{#else}}off{{/if}}</text:p>"),
            new Dictionary<string, object> { ["Active"] = true });

        XElement paragraph = output.Body.Element(_text + "p")!;
        Assert.Equal("Status: on", OdtDocumentVerifier.RenderText(paragraph));
        XElement bold = Assert.Single(paragraph.Elements(_text + "span"));
        Assert.Equal("T1", (string?)bold.Attribute(_text + "style-name"));
        Assert.Equal("on", bold.Value);
    }

    [Fact]
    public void InlineConditional_WithPlaceholders_ReplacesThemInKeptBranch()
    {
        (ProcessingResult result, OdtDocumentVerifier output) = OdtTestHelper.Process(
            Paragraphs("{{#if HasCompany}}{{Company}}{{#else}}{{Missing}}{{/if}} Ltd."),
            new Dictionary<string, object> { ["HasCompany"] = true, ["Company"] = "ACME" });

        Assert.Equal(new[] { "ACME Ltd." }, output.GetParagraphTexts());
        Assert.Empty(result.MissingVariables);
    }

    [Theory]
    [InlineData("{{#if Count > 0 and IsEnabled}}yes{{/if}}", "yes")]
    [InlineData("{{#if Role in (\"Admin\", \"Owner\")}}yes{{/if}}", "yes")]
    [InlineData("{{#if Name contains \"li\"}}yes{{/if}}", "yes")]
    [InlineData("{{#if not IsEnabled}}yes{{#else}}no{{/if}}", "no")]
    [InlineData("{{#if Tags is empty}}yes{{#else}}no{{/if}}", "no")]
    [InlineData("{{#if (Count >= 3 or IsEnabled) and Missing exists}}yes{{#else}}no{{/if}}", "no")]
    public void Operators_AreEvaluated(string paragraph, string expected)
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Count"] = 2,
            ["IsEnabled"] = true,
            ["Role"] = "Owner",
            ["Name"] = "Alice",
            ["Tags"] = new List<string> { "x" },
        };

        Assert.Equal(new[] { expected }, Process(Paragraphs(paragraph), data));
    }

    [Fact]
    public void Condition_WithSpacesElement_UsesAllSpaces()
    {
        List<string> texts = Process(
            new OdtDocumentBuilder().AddXml("<text:p>{{#if Name = \"a <text:s/>b\"}}match{{#else}}no{{/if}}</text:p>"),
            new Dictionary<string, object> { ["Name"] = "a  b" });

        Assert.Equal(new[] { "match" }, texts);
    }

    [Fact]
    public void Condition_MalformedExpression_IsFalseWithWarning()
    {
        (ProcessingResult result, OdtDocumentVerifier output) = OdtTestHelper.Process(
            Paragraphs("{{#if Count >}}yes{{#else}}no{{/if}}"),
            new Dictionary<string, object> { ["Count"] = 1 });

        Assert.Equal(new[] { "no" }, output.GetParagraphTexts());
        Assert.Contains(result.Warnings, w => w.Type == ProcessingWarningType.ExpressionFailed);
    }

    [Theory]
    [InlineData(true, new[] { "Header", "Visible", "Footer" })]
    [InlineData(false, new[] { "Header", "Hidden", "Footer" })]
    public void TableRowConditional_KeepsOrRemovesRows(bool show, string[] expected)
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddTable(
            new[] { "Header" },
            new[] { "{{#if Show}}" },
            new[] { "Visible" },
            new[] { "{{#else}}" },
            new[] { "Hidden" },
            new[] { "{{/if}}" },
            new[] { "Footer" });

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Show"] = show });

        Assert.Equal(expected, output.GetParagraphTexts());
        Assert.Equal(3, output.Body.Descendants(_table + "table-row").Count());
    }

    [Fact]
    public void TableRowConditional_InMultiColumnRows_RemovesMarkerRows()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddTable(
            new[] { "{{#if Show}}", string.Empty },
            new[] { "a", "b" },
            new[] { "{{/if}}", string.Empty });

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Show"] = true });

        Assert.Equal(new[] { "a", "b" }, output.GetParagraphTexts());
    }

    [Fact]
    public void CellLevelConditionals_AreProcessedInsideTheCell()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<table:table><table:table-column table:number-columns-repeated=\"2\"/><table:table-row>" +
            "<table:table-cell><text:p>{{#if Show}}</text:p><text:p>multi</text:p><text:p>{{/if}}</text:p></table:table-cell>" +
            "<table:table-cell><text:p>{{#if Show}}inline{{#else}}other{{/if}}</text:p></table:table-cell>" +
            "</table:table-row></table:table>");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Show"] = false });

        // The emptied first cell keeps an (empty) paragraph.
        Assert.Equal(new[] { string.Empty, "other" }, output.GetParagraphTexts());
        XElement firstCell = output.Body.Descendants(_table + "table-cell").First();
        Assert.Single(firstCell.Elements(_text + "p"));
    }

    [Fact]
    public void TableRowConditional_RemovingAllRows_RemovesTable()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddTable(new[] { "{{#if Show}}" }, new[] { "row" }, new[] { "{{/if}}" })
            .AddParagraph("after");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Show"] = false });

        Assert.Empty(output.Body.Descendants(_table + "table"));
        Assert.Equal(new[] { "after" }, output.GetParagraphTexts());
    }

    [Fact]
    public void TableRowConditional_InHeaderRows_RemovesEmptyHeaderGroup()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<table:table><table:table-column/>" +
            "<table:table-header-rows>" +
            "<table:table-row><table:table-cell><text:p>{{#if ShowHeader}}</text:p></table:table-cell></table:table-row>" +
            "<table:table-row><table:table-cell><text:p>Head</text:p></table:table-cell></table:table-row>" +
            "<table:table-row><table:table-cell><text:p>{{/if}}</text:p></table:table-cell></table:table-row>" +
            "</table:table-header-rows>" +
            "<table:table-row><table:table-cell><text:p>Body</text:p></table:table-cell></table:table-row></table:table>");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["ShowHeader"] = false });

        Assert.Equal(new[] { "Body" }, output.GetParagraphTexts());
        Assert.Empty(output.Body.Descendants(_table + "table-header-rows"));
    }

    [Fact]
    public void TableRowConditional_TwoMarkersInOneRow_Fails()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddTable(
            new[] { "{{#if A}}", "{{#if B}}" },
            new[] { "x", "y" },
            new[] { "{{/if}}", "{{/if}}" });

        ProcessingResult result = ProcessExpectingFailure(template, new Dictionary<string, object> { ["A"] = true, ["B"] = true });

        Assert.Contains("must be placed in its own row", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, new[] { "one", "two", "three" })]
    [InlineData(false, new[] { "one", "three" })]
    public void ListItemConditional_KeepsOrRemovesItems(bool show, string[] expected)
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<text:list>" +
            "<text:list-item><text:p>one</text:p></text:list-item>" +
            "<text:list-item><text:p>{{#if Show}}</text:p></text:list-item>" +
            "<text:list-item><text:p>two</text:p></text:list-item>" +
            "<text:list-item><text:p>{{/if}}</text:p></text:list-item>" +
            "<text:list-item><text:p>three</text:p></text:list-item>" +
            "</text:list>");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Show"] = show });

        Assert.Equal(expected, output.GetParagraphTexts());
        Assert.Equal(expected.Length, output.Body.Descendants(_text + "list-item").Count());
    }

    [Fact]
    public void ListItem_InlineConditional_KeepsItem()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<text:list><text:list-item><text:p>{{#if Show}}shown{{#else}}hidden{{/if}}</text:p></text:list-item>" +
            "<text:list-item><text:p>other</text:p></text:list-item></text:list>");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Show"] = false });

        Assert.Equal(new[] { "hidden", "other" }, output.GetParagraphTexts());
    }

    [Fact]
    public void ListItemConditional_RemovingAllItems_RemovesList()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddXml(
                "<text:list><text:list-item><text:p>{{#if Show}}</text:p></text:list-item>" +
                "<text:list-item><text:p>item</text:p></text:list-item>" +
                "<text:list-item><text:p>{{/if}}</text:p></text:list-item></text:list>")
            .AddParagraph("after");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Show"] = false });

        Assert.Empty(output.Body.Descendants(_text + "list"));
        Assert.Equal(new[] { "after" }, output.GetParagraphTexts());
    }

    [Fact]
    public void SingleItemListMarkers_AroundParagraphs_WorkAsBlockMarkers()
    {
        // LibreOffice stores a lone bullet between paragraphs as a list of its own.
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddXml("<text:list><text:list-item><text:p>{{#if Show}}</text:p></text:list-item></text:list>")
            .AddParagraph("content")
            .AddXml("<text:list><text:list-item><text:p>{{/if}}</text:p></text:list-item></text:list>")
            .AddParagraph("after");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Show"] = false });

        Assert.Equal(new[] { "after" }, output.GetParagraphTexts());
        Assert.Empty(output.Body.Descendants(_text + "list"));
    }

    [Fact]
    public void SingleItemList_WithCompleteInlineConditional_IsKept()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddXml("<text:list><text:list-item><text:p>{{#if Show}}yes{{#else}}no{{/if}}</text:p></text:list-item></text:list>");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Show"] = true });

        Assert.Equal(new[] { "yes" }, output.GetParagraphTexts());
        Assert.Single(output.Body.Descendants(_text + "list-item"));
    }

    [Fact]
    public void BlockConditional_InSection_IsProcessed()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<text:section text:name=\"S\"><text:p>{{#if Show}}</text:p><text:p>x</text:p><text:p>{{/if}}</text:p><text:p>y</text:p></text:section>");

        Assert.Equal(new[] { "y" }, Process(template, new Dictionary<string, object> { ["Show"] = false }));
    }

    [Fact]
    public void Conditionals_InHeaderAndFooter_AreProcessed()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("Body")
            .AddHeaderParagraph("{{#if Draft}}")
            .AddHeaderParagraph("DRAFT")
            .AddHeaderParagraph("{{/if}}")
            .AddFooterParagraph("{{#if Draft}}Draft{{#else}}Final{{/if}} version");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Draft"] = false });

        // The emptied header keeps one empty paragraph.
        Assert.Equal(new[] { string.Empty }, output.GetHeaderTexts());
        Assert.Equal(new[] { "Final version" }, output.GetFooterTexts());
    }

    [Fact]
    public void Conditionals_InTextBoxAndFootnote_AreProcessed()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<text:p>Main<draw:frame draw:name=\"F\" text:anchor-type=\"as-char\"><draw:text-box>" +
            "<text:p>{{#if Show}}</text:p><text:p>box</text:p><text:p>{{/if}}</text:p></draw:text-box></draw:frame>" +
            "<text:note text:id=\"n1\" text:note-class=\"footnote\"><text:note-citation>1</text:note-citation><text:note-body>" +
            "<text:p>{{#if Show}}note{{#else}}no note{{/if}}</text:p></text:note-body></text:note></text:p>");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Show"] = false });

        XElement textBox = output.Body.Descendants(OdtDocumentVerifier.Draw + "text-box").Single();
        Assert.Equal(string.Empty, OdtDocumentVerifier.RenderText(Assert.Single(textBox.Elements(_text + "p"))));
        Assert.Contains("no note", output.GetParagraphTexts());
    }

    [Fact]
    public void UnmatchedIf_Fails()
    {
        ProcessingResult result = ProcessExpectingFailure(Paragraphs("{{#if Show}}", "x"), new Dictionary<string, object> { ["Show"] = true });

        Assert.Contains("has no matching '{{/if}}'", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ElseIfAfterElse_Fails()
    {
        ProcessingResult result = ProcessExpectingFailure(
            Paragraphs("{{#if A}}", "a", "{{#else}}", "b", "{{#elseif B}}", "c", "{{/if}}"),
            new Dictionary<string, object> { ["A"] = true, ["B"] = true });

        Assert.Contains("'{{#elseif}}' cannot appear after '{{#else}}'", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void BlockMarkerParagraphs_AreRemovedWithTheirOtherText_AsInWord()
    {
        List<string> texts = Process(
            Paragraphs("{{#if A}} text {{Name}}", "kept", "{{/if}} tail"),
            new Dictionary<string, object> { ["A"] = true, ["Name"] = "N" });

        Assert.Equal(new[] { "kept" }, texts);
    }
}
