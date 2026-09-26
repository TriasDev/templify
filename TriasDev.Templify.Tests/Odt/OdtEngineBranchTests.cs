// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Odt;

/// <summary>
/// Less common structures walked by the OpenDocument engine: indexes, shapes in the body, row groups, emptied
/// list items, leftover markers, annotations with lists.
/// </summary>
public sealed class OdtEngineBranchTests
{
    private static readonly XNamespace _text = OdtDocumentVerifier.Text;
    private static readonly XNamespace _table = OdtDocumentVerifier.Table;

    private static string P(string text) => $"<text:p>{OdtDocumentBuilder.Escape(text)}</text:p>";

    private static string Row(string text) =>
        $"<table:table-row><table:table-cell office:value-type=\"string\">{P(text)}</table:table-cell></table:table-row>";

    [Fact]
    public void IndexBody_IsProcessed_AndTheIndexSourceIsLeftAlone()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<text:table-of-content text:name=\"TOC\">"
            + "<text:table-of-content-source text:outline-level=\"3\"><text:index-title-template>{{Title}}</text:index-title-template></text:table-of-content-source>"
            + "<text:index-body><text:index-title text:name=\"TOC_Head\">" + P("{{Title}}") + "</text:index-title>"
            + P("{{#foreach Items}}") + P("{{.}}") + P("{{/foreach}}") + "</text:index-body></text:table-of-content>");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            template,
            new Dictionary<string, object> { ["Title"] = "Contents", ["Items"] = new List<string> { "a", "b" } });

        Assert.Equal(new[] { "Contents", "a", "b" }, output.GetParagraphTexts());
        Assert.Equal("{{Title}}", output.Body.Descendants(_text + "index-title-template").Single().Value);
    }

    [Fact]
    public void ShapeWithParagraphsInTheBody_IsProcessedAsAContainer()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddXml("<draw:custom-shape draw:name=\"S\">" + P("{{#if Show}}") + P("{{Shape}}") + P("{{/if}}") + P("{{Always}}") + "</draw:custom-shape>")
            .AddXml("<draw:frame draw:name=\"F\"><draw:text-box>" + P("{{Box}}") + "</draw:text-box></draw:frame>");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            template,
            new Dictionary<string, object> { ["Show"] = false, ["Shape"] = "s", ["Always"] = "yes", ["Box"] = "in box" });

        Assert.Equal(new[] { "yes", "in box" }, output.GetParagraphTexts());
    }

    [Fact]
    public void RowGroupEmptiedByAConditional_IsRemoved_AndTheTableKeepsItsHeader()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<table:table table:name=\"T\"><table:table-column/>"
            + "<table:table-header-rows>" + Row("Header {{H}}") + "</table:table-header-rows>"
            + "<table:table-rows>" + Row("{{#if Show}}") + Row("hidden") + Row("{{/if}}") + "</table:table-rows>"
            + "<table:table-row-group><table:table-rows>" + Row("{{#foreach Items}}") + Row("{{.}}") + Row("{{/foreach}}") + "</table:table-rows></table:table-row-group>"
            + "</table:table>");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(
            template,
            new Dictionary<string, object> { ["H"] = "h", ["Show"] = false, ["Items"] = new List<string> { "x", "y" } });

        XElement table = output.Body.Element(_table + "table")!;
        Assert.Equal(new[] { "Header h", "x", "y" }, output.GetParagraphTexts());
        Assert.Equal(new[] { "table-column", "table-header-rows", "table-row-group" }, table.Elements().Select(e => e.Name.LocalName));
    }

    [Fact]
    public void TableWhoseRowsAreAllInGroups_IsRemovedWhenAllRowsAre()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddXml("<table:table table:name=\"T\"><table:table-column/><table:table-rows>" + Row("{{#if Show}}") + Row("hidden") + Row("{{/if}}")
                    + "</table:table-rows></table:table>")
            .AddParagraph("after");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Show"] = false });

        Assert.Empty(output.Body.Elements(_table + "table"));
        Assert.Equal(new[] { "after" }, output.GetParagraphTexts());
    }

    [Fact]
    public void ListItemEmptiedByAnInItemConditional_IsRemoved_AndAnEmptiedListToo()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddXml("<text:list>"
                    + "<text:list-item>" + P("keep") + "</text:list-item>"
                    + "<text:list-item>" + P("{{#if Show}}") + P("gone") + P("{{/if}}") + "</text:list-item>"
                    + "</text:list>")
            .AddXml("<text:list><text:list-item>" + P("{{#if Show}}") + P("gone too") + P("{{/if}}") + "</text:list-item>"
                    + "<text:list-item>" + P("{{#if Show}}") + P("and this") + P("{{/if}}") + "</text:list-item></text:list>")
            .AddParagraph("end");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Show"] = false });

        XElement list = Assert.Single(output.Body.Elements(_text + "list"));
        Assert.Single(list.Elements(_text + "list-item"));
        Assert.Equal(new[] { "keep", "end" }, output.GetParagraphTexts());
    }

    [Fact]
    public void LeftoverMarkers_AreLeftUnchanged_AsInWordDocuments()
    {
        string[] paragraphs = { "{{Name}} {{/if}}", "{{/foreach}}", "{{Name}} {{#else}}", "{{Name}}" };
        OdtDocumentBuilder odt = new OdtDocumentBuilder();
        DocumentBuilder docx = new DocumentBuilder();
        foreach (string paragraph in paragraphs)
        {
            odt.AddParagraph(paragraph);
            docx.AddParagraph(paragraph);
        }

        Dictionary<string, object> data = new Dictionary<string, object> { ["Name"] = "N" };
        (ProcessingResult result, OdtDocumentVerifier output) = OdtTestHelper.Process(odt, data);
        using MemoryStream docxOutput = new MemoryStream();
        ProcessingResult docxResult = new DocumentTemplateProcessor(OdtTestHelper.InvariantOptions()).ProcessTemplate(docx.ToStream(), docxOutput, data);

        Assert.True(docxResult.IsSuccess, docxResult.ErrorMessage);
        using DocumentVerifier docxVerifier = new DocumentVerifier(docxOutput);
        Assert.Equal(docxVerifier.GetAllParagraphTexts(), output.GetParagraphTexts());
        Assert.Equal(docxResult.ReplacementCount, result.ReplacementCount);
        Assert.Equal("N", output.GetParagraphTexts()[^1]);
    }

    [Fact]
    public void ListInAnAnnotation_IsNotLinkedToLoopCopiesOfAList()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("{{#foreach Items}}")
            .AddXml("<text:list text:style-name=\"L1\"><text:list-item>" + P("{{.}}") + "</text:list-item></text:list>")
            .AddXml("<text:p>note<office:annotation><dc:creator>A</dc:creator><text:list text:style-name=\"L1\"><text:list-item>"
                    + P("comment") + "</text:list-item></text:list></office:annotation></text:p>")
            .AddParagraph("{{/foreach}}");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Items"] = new List<string> { "a", "b" } });

        List<XElement> bodyLists = output.Body.Elements(_text + "list").ToList();
        Assert.Equal(2, bodyLists.Count);
        Assert.NotNull(bodyLists[1].Attribute(_text + "continue-list"));
        Assert.All(
            output.Body.Descendants(OdtDocumentVerifier.Office + "annotation").Descendants(_text + "list"),
            list => Assert.Null(list.Attribute(_text + "continue-list")));
    }
}
