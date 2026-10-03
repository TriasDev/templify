// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Odt;

/// <summary>
/// Template constructs inside nested containers (text boxes, notes, sections, nested lists, headers and
/// footers) and the uniqueness of names and ids after loop cloning.
/// </summary>
public sealed class OdtContainerTests
{
    private static readonly XNamespace _text = OdtDocumentVerifier.Text;
    private static readonly XNamespace _table = OdtDocumentVerifier.Table;
    private static readonly XNamespace _draw = OdtDocumentVerifier.Draw;

    private static List<Dictionary<string, object>> Items(params string[] names) =>
        names.Select(n => new Dictionary<string, object> { ["Name"] = n }).ToList();

    [Fact]
    public void LoopCloningFramesTablesSectionsAndNotes_MakesNamesUnique()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("{{#foreach Items}}")
            .AddXml(
                "<text:p>{{Name}}<draw:frame draw:name=\"Box\" text:anchor-type=\"as-char\"><draw:text-box><text:p>in box {{Name}}</text:p></draw:text-box></draw:frame>" +
                "<text:note text:id=\"ftn1\" text:note-class=\"footnote\"><text:note-citation>1</text:note-citation><text:note-body><text:p>note {{Name}}</text:p></text:note-body></text:note></text:p>")
            .AddXml("<table:table table:name=\"Prices\"><table:table-column/><table:table-row><table:table-cell><text:p>{{Name}}</text:p></table:table-cell></table:table-row></table:table>")
            .AddXml("<text:section text:name=\"Details\"><text:p xml:id=\"p1\">section {{Name}}</text:p></text:section>")
            .AddParagraph("{{/foreach}}");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Items"] = Items("a", "b", "c") });

        Assert.Equal(new[] { "Box", "Box_2", "Box_3" }, output.Body.Descendants(_draw + "frame").Select(f => (string)f.Attribute(_draw + "name")!));
        Assert.Equal(new[] { "Prices", "Prices_2", "Prices_3" }, output.Body.Descendants(_table + "table").Select(t => (string)t.Attribute(_table + "name")!));
        Assert.Equal(new[] { "Details", "Details_2", "Details_3" }, output.Body.Descendants(_text + "section").Select(s => (string)s.Attribute(_text + "name")!));
        Assert.Equal(new[] { "ftn1", "ftn1_2", "ftn1_3" }, output.Body.Descendants(_text + "note").Select(n => (string)n.Attribute(_text + "id")!));
        Assert.Single(output.Body.Descendants(), e => e.Attribute(XNamespace.Xml + "id") != null);

        List<string> texts = output.GetParagraphTexts();
        Assert.Contains("in box b", texts);
        Assert.Contains("note c", texts);
        Assert.Contains("section a", texts);
    }

    [Fact]
    public void UniqueNames_AvoidExistingNames()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddXml("<text:p><draw:frame draw:name=\"F_2\"/></text:p>")
            .AddParagraph("{{#foreach Items}}")
            .AddXml("<text:p><draw:frame draw:name=\"F\"/></text:p>")
            .AddParagraph("{{/foreach}}");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Items"] = Items("a", "b") });

        Assert.Equal(new[] { "F_2", "F", "F_3" }, output.Body.Descendants(_draw + "frame").Select(f => (string)f.Attribute(_draw + "name")!));
    }

    [Fact]
    public void FramesInHeaderAndBody_ShareOneNameSpace()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddXml("<text:p><draw:frame draw:name=\"Logo\"/></text:p>")
            .AddHeaderXml("<text:p><draw:frame draw:name=\"Logo\"/></text:p>");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object>());

        Assert.Equal("Logo", (string?)output.Body.Descendants(_draw + "frame").Single().Attribute(_draw + "name"));
        Assert.Equal("Logo_2", (string?)output.StylesXml!.Descendants(_draw + "frame").Single().Attribute(_draw + "name"));
    }

    [Fact]
    public void LoopInsideTextBox_IsExpanded()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<text:p><draw:frame draw:name=\"F\" text:anchor-type=\"paragraph\"><draw:text-box>" +
            "<text:p>{{#foreach Items}}</text:p><text:p>- {{Name}}</text:p><text:p>{{/foreach}}</text:p>" +
            "</draw:text-box></draw:frame></text:p>");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Items"] = Items("a", "b") });

        XElement textBox = output.Body.Descendants(_draw + "text-box").Single();
        Assert.Equal(new[] { "- a", "- b" }, textBox.Elements(_text + "p").Select(OdtDocumentVerifier.RenderText));
    }

    [Fact]
    public void LoopInsideFootnote_IsExpanded()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<text:p>Text<text:note text:id=\"n\" text:note-class=\"endnote\"><text:note-citation>i</text:note-citation><text:note-body>" +
            "<text:p>{{#foreach Items}}</text:p><text:p>src {{Name}}</text:p><text:p>{{/foreach}}</text:p>" +
            "</text:note-body></text:note></text:p>");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Items"] = Items("a", "b") });

        XElement noteBody = output.Body.Descendants(_text + "note-body").Single();
        Assert.Equal(new[] { "src a", "src b" }, noteBody.Elements(_text + "p").Select(OdtDocumentVerifier.RenderText));
    }

    [Fact]
    public void EmptyLoopInsideTextBox_LeavesEmptyParagraph()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<text:p><draw:frame draw:name=\"F\"><draw:text-box>" +
            "<text:p>{{#foreach Items}}</text:p><text:p>{{Name}}</text:p><text:p>{{/foreach}}</text:p>" +
            "</draw:text-box></draw:frame></text:p>");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Items"] = new List<object>() });

        XElement textBox = output.Body.Descendants(_draw + "text-box").Single();
        Assert.Single(textBox.Elements(_text + "p"));
    }

    [Fact]
    public void LoopInsideSection_AndSectionInsideLoop_AreProcessed()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddXml("<text:section text:name=\"S\"><text:p>{{#foreach Items}}</text:p><text:p>s {{Name}}</text:p><text:p>{{/foreach}}</text:p></text:section>");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Items"] = Items("a", "b") });

        Assert.Equal(new[] { "s a", "s b" }, output.GetParagraphTexts());
    }

    [Fact]
    public void NestedListItemLoopInsideListItem_IsExpanded()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Groups"] = new List<Dictionary<string, object>>
            {
                new() { ["Name"] = "G1", ["Items"] = Items("a", "b") },
                new() { ["Name"] = "G2", ["Items"] = Items("c") },
            },
        };

        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<text:list>" +
            "<text:list-item><text:p>{{#foreach group in Groups}}</text:p></text:list-item>" +
            "<text:list-item><text:p>{{group.Name}}</text:p><text:list>" +
            "<text:list-item><text:p>{{#foreach item in group.Items}}</text:p></text:list-item>" +
            "<text:list-item><text:p>{{group.Name}}.{{item.Name}}</text:p></text:list-item>" +
            "<text:list-item><text:p>{{/foreach}}</text:p></text:list-item>" +
            "</text:list></text:list-item>" +
            "<text:list-item><text:p>{{/foreach}}</text:p></text:list-item>" +
            "</text:list>");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, data);

        Assert.Equal(new[] { "G1", "G1.a", "G1.b", "G2", "G2.c" }, output.GetParagraphTexts());
        Assert.Equal(2, output.Body.Element(_text + "list")!.Elements(_text + "list-item").Count());
    }

    [Fact]
    public void TableRowLoopInFooter_IsExpanded()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("Body")
            .AddFooterXml(
                "<table:table table:name=\"FooterTable\"><table:table-column/>" +
                "<table:table-row><table:table-cell><text:p>{{#foreach Items}}</text:p></table:table-cell></table:table-row>" +
                "<table:table-row><table:table-cell><text:p>f {{Name}}</text:p></table:table-cell></table:table-row>" +
                "<table:table-row><table:table-cell><text:p>{{/foreach}}</text:p></table:table-cell></table:table-row>" +
                "</table:table>");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Items"] = Items("a", "b") });

        Assert.Equal(new[] { "f a", "f b" }, output.GetFooterTexts());
    }

    [Fact]
    public void ConditionalRemovingWholeHeaderContent_LeavesEmptyParagraph()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("Body")
            .AddHeaderXml("<text:p>{{#if Show}}</text:p><table:table><table:table-column/><table:table-row><table:table-cell><text:p>x</text:p></table:table-cell></table:table-row></table:table><text:p>{{/if}}</text:p>");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Show"] = false });

        XElement header = output.StylesXml!.Descendants(OdtDocumentVerifier.Style + "header").Single();
        Assert.Single(header.Elements());
        Assert.Equal(_text + "p", header.Elements().Single().Name);
    }

    [Fact]
    public void HeaderAndFooterRegions_AreProcessed()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("Body {{Name}}")
            .AddHeaderXml(
                "<style:region-left><text:p>L {{Name}}</text:p></style:region-left>" +
                "<style:region-center><text:p>{{#if Show}}</text:p><text:p>hidden</text:p><text:p>{{/if}}</text:p></style:region-center>" +
                "<style:region-right><text:p>{{#foreach Items}}</text:p><text:p>R {{Name}}</text:p><text:p>{{/foreach}}</text:p></style:region-right>")
            .AddFooterXml("<style:region-center><text:p>F {{Name}}</text:p></style:region-center>");

        (ProcessingResult result, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object>
        {
            ["Name"] = "X",
            ["Show"] = false,
            ["Items"] = Items("a", "b"),
        });

        Assert.Equal(new[] { "L X", string.Empty, "R a", "R b" }, output.GetHeaderTexts());
        Assert.Equal(new[] { "F X" }, output.GetFooterTexts());
        Assert.Equal(5, result.ReplacementCount);
    }
}
