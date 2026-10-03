// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Odt;

/// <summary>
/// Loops (<c>{{#foreach}}</c> … <c>{{/foreach}}</c>) in OpenDocument Text documents.
/// </summary>
public sealed class OdtLoopTests
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

    private static List<Dictionary<string, object>> Items(params string[] names) =>
        names.Select(n => new Dictionary<string, object> { ["Name"] = n }).ToList();

    [Fact]
    public void BodyLoop_RepeatsContentPerItem()
    {
        List<string> texts = Process(
            Paragraphs("Before", "{{#foreach Items}}", "Item {{Name}}", "{{/foreach}}", "After"),
            new Dictionary<string, object> { ["Items"] = Items("a", "b", "c") });

        Assert.Equal(new[] { "Before", "Item a", "Item b", "Item c", "After" }, texts);
    }

    [Fact]
    public void NamedIterationVariable_AndParentScope_AreResolved()
    {
        List<string> texts = Process(
            Paragraphs("{{#foreach item in Items}}", "{{item.Name}} of {{Owner}}", "{{/foreach}}"),
            new Dictionary<string, object> { ["Items"] = Items("a", "b"), ["Owner"] = "Bob" });

        Assert.Equal(new[] { "a of Bob", "b of Bob" }, texts);
    }

    [Fact]
    public void LoopMetadata_IsAvailable()
    {
        List<string> texts = Process(
            Paragraphs("{{#foreach Items}}", "{{@index}}/{{@number}}/{{@count}} {{Name}} first={{@first}} last={{@last}}", "{{/foreach}}"),
            new Dictionary<string, object> { ["Items"] = Items("a", "b") });

        Assert.Equal(new[] { "0/1/2 a first=True last=False", "1/2/2 b first=False last=True" }, texts);
    }

    [Fact]
    public void PrimitiveCollection_CurrentItem_IsResolved()
    {
        List<string> texts = Process(
            Paragraphs("{{#foreach Tags}}", "- {{.}} / {{this}}", "{{/foreach}}"),
            new Dictionary<string, object> { ["Tags"] = new List<string> { "x", "y" } });

        Assert.Equal(new[] { "- x / x", "- y / y" }, texts);
    }

    [Fact]
    public void NestedLoops_AccessOuterVariable()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Categories"] = new List<Dictionary<string, object>>
            {
                new() { ["Name"] = "Fruit", ["Products"] = Items("Apple", "Pear") },
                new() { ["Name"] = "Veg", ["Products"] = Items("Leek") },
            },
        };

        List<string> texts = Process(
            Paragraphs(
                "{{#foreach category in Categories}}",
                "# {{category.Name}}",
                "{{#foreach product in category.Products}}",
                "{{category.Name}}: {{product.Name}} ({{@number}})",
                "{{/foreach}}",
                "{{/foreach}}"),
            data);

        Assert.Equal(new[] { "# Fruit", "Fruit: Apple (1)", "Fruit: Pear (2)", "# Veg", "Veg: Leek (1)" }, texts);
    }

    [Fact]
    public void ConditionalInsideLoop_UsesItemContext()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<Dictionary<string, object>>
            {
                new() { ["Name"] = "a", ["Active"] = true },
                new() { ["Name"] = "b", ["Active"] = false },
            },
        };

        List<string> texts = Process(
            Paragraphs("{{#foreach Items}}", "{{#if Active}}", "{{Name}} active", "{{#else}}", "{{Name}} inactive", "{{/if}}",
                "{{Name}}: {{#if @last}}last{{#else}}more{{/if}}", "{{/foreach}}"),
            data);

        Assert.Equal(new[] { "a active", "a: more", "b inactive", "b: last" }, texts);
    }

    [Fact]
    public void LoopInsideConditional_IsExpandedOnlyWhenTrue()
    {
        OdtDocumentBuilder template = Paragraphs("{{#if Show}}", "{{#foreach Items}}", "{{Name}}", "{{/foreach}}", "{{/if}}", "end");

        Assert.Equal(new[] { "a", "b", "end" }, Process(template, new Dictionary<string, object> { ["Show"] = true, ["Items"] = Items("a", "b") }));
        Assert.Equal(new[] { "end" }, Process(template, new Dictionary<string, object> { ["Show"] = false, ["Items"] = Items("a", "b") }));
    }

    [Fact]
    public void EmptyCollection_RemovesLoopWithoutWarning()
    {
        (ProcessingResult result, OdtDocumentVerifier output) = OdtTestHelper.Process(
            Paragraphs("{{#foreach Items}}", "{{Name}}", "{{/foreach}}", "end"),
            new Dictionary<string, object> { ["Items"] = new List<object>() });

        Assert.Equal(new[] { "end" }, output.GetParagraphTexts());
        Assert.False(result.HasWarnings);
    }

    [Fact]
    public void MissingCollection_RemovesLoopWithWarning()
    {
        (ProcessingResult result, OdtDocumentVerifier output) = OdtTestHelper.Process(
            Paragraphs("{{#foreach Items}}", "{{Name}}", "{{/foreach}}", "end"),
            new Dictionary<string, object>());

        Assert.Equal(new[] { "end" }, output.GetParagraphTexts());
        Assert.Contains(result.Warnings, w => w.Type == ProcessingWarningType.MissingLoopCollection && w.VariableName == "Items");
    }

    [Fact]
    public void NullCollection_RemovesLoopWithWarning()
    {
        (ProcessingResult result, OdtDocumentVerifier output) = OdtTestHelper.Process(
            Paragraphs("{{#foreach Items}}", "{{Name}}", "{{/foreach}}", "end"),
            new Dictionary<string, object> { ["Items"] = null! });

        Assert.Equal(new[] { "end" }, output.GetParagraphTexts());
        Assert.Contains(result.Warnings, w => w.Type == ProcessingWarningType.NullLoopCollection);
    }

    [Fact]
    public void NullItems_RenderEmpty()
    {
        List<string> texts = Process(
            Paragraphs("{{#foreach item in Items}}", "[{{item.Name}}]", "{{/foreach}}"),
            new Dictionary<string, object> { ["Items"] = new List<object?> { new Dictionary<string, object> { ["Name"] = "a" }, null } });

        Assert.Equal(new[] { "[a]", "[]" }, texts);
    }

    [Fact]
    public void NonCollection_Fails()
    {
        ProcessingResult result = new OdtTemplateProcessor(OdtTestHelper.InvariantOptions()).ProcessTemplate(
            Paragraphs("{{#foreach Items}}", "x", "{{/foreach}}").ToBytes(),
            new Dictionary<string, object> { ["Items"] = "not a list" },
            out _);

        Assert.False(result.IsSuccess);
        Assert.Contains("is not a collection", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{{#foreach Items}}", "has no matching '{{/foreach}}'")]
    [InlineData("{{#foreach in in Items}}", "reserved keyword")]
    [InlineData("{{#foreach @x in Items}}", "cannot start with '@'")]
    public void InvalidLoopSyntax_Fails(string startMarker, string message)
    {
        OdtDocumentBuilder template = startMarker == "{{#foreach Items}}"
            ? Paragraphs(startMarker, "x")
            : Paragraphs(startMarker, "x", "{{/foreach}}");

        ProcessingResult result = new OdtTemplateProcessor(OdtTestHelper.InvariantOptions())
            .ProcessTemplate(template.ToBytes(), new Dictionary<string, object> { ["Items"] = Items("a") }, out _);

        Assert.False(result.IsSuccess);
        Assert.Contains(message, result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void LoopValues_AreNotReprocessedAsTemplateSyntax()
    {
        List<string> texts = Process(
            Paragraphs("{{#foreach Items}}", "{{Name}}", "{{/foreach}}"),
            new Dictionary<string, object> { ["Items"] = Items("{{Secret}}", "{{#if X}}"), ["Secret"] = "leak" });

        Assert.Equal(new[] { "{{Secret}}", "{{#if X}}" }, texts);
    }

    [Fact]
    public void Loop_WithTableAndFormattedContent_ClonesStructure()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("{{#foreach Items}}")
            .AddXml("<text:p>Name: <text:span text:style-name=\"T1\">{{Name}}</text:span></text:p>")
            .AddTable(new[] { "Cell {{Name}}" })
            .AddParagraph("{{/foreach}}");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Items"] = Items("a", "b") });

        Assert.Equal(new[] { "Name: a", "Cell a", "Name: b", "Cell b" }, output.GetParagraphTexts());
        Assert.Equal(2, output.Body.Elements(_table + "table").Count());
        Assert.All(output.Body.Descendants(_text + "span"), s => Assert.Equal("T1", (string?)s.Attribute(_text + "style-name")));
    }

    [Fact]
    public void TableRowLoop_RepeatsRows()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddTable(
            new[] { "Name", "No." },
            new[] { "{{#foreach Items}}", string.Empty },
            new[] { "{{Name}}", "{{@number}}" },
            new[] { "{{/foreach}}", string.Empty },
            new[] { "Total", "{{Items.Count}}" });

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Items"] = Items("a", "b", "c") });

        Assert.Equal(new[] { "Name", "No.", "a", "1", "b", "2", "c", "3", "Total", "3" }, output.GetParagraphTexts());
        Assert.Equal(5, output.Body.Descendants(_table + "table-row").Count());
    }

    [Fact]
    public void TableRowLoop_WithRowConditionalInside_UsesItemContext()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<Dictionary<string, object>>
            {
                new() { ["Name"] = "a", ["Note"] = "n1" },
                new() { ["Name"] = "b", ["Note"] = string.Empty },
            },
        };

        OdtDocumentBuilder template = new OdtDocumentBuilder().AddTable(
            new[] { "{{#foreach Items}}" },
            new[] { "{{Name}}" },
            new[] { "{{#if Note}}" },
            new[] { "Note: {{Note}}" },
            new[] { "{{/if}}" },
            new[] { "{{/foreach}}" });

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, data);

        Assert.Equal(new[] { "a", "Note: n1", "b" }, output.GetParagraphTexts());
    }

    [Fact]
    public void TableRowLoop_Nested_Works()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Groups"] = new List<Dictionary<string, object>>
            {
                new() { ["Name"] = "G1", ["Items"] = Items("a", "b") },
                new() { ["Name"] = "G2", ["Items"] = Items("c") },
            },
        };

        OdtDocumentBuilder template = new OdtDocumentBuilder().AddTable(
            new[] { "{{#foreach group in Groups}}" },
            new[] { "{{group.Name}}" },
            new[] { "{{#foreach item in group.Items}}" },
            new[] { "{{group.Name}}-{{item.Name}}" },
            new[] { "{{/foreach}}" },
            new[] { "{{/foreach}}" });

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, data);

        Assert.Equal(new[] { "G1", "G1-a", "G1-b", "G2", "G2-c" }, output.GetParagraphTexts());
    }

    [Fact]
    public void TableRowLoop_EmptyCollection_RemovesTableWhenNoRowsRemain()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddTable(new[] { "{{#foreach Items}}" }, new[] { "{{Name}}" }, new[] { "{{/foreach}}" })
            .AddParagraph("after");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Items"] = new List<object>() });

        Assert.Empty(output.Body.Descendants(_table + "table"));
        Assert.Equal(new[] { "after" }, output.GetParagraphTexts());
    }

    [Fact]
    public void TableRowLoop_RowsWithCoveredCellsAndRepeatedRows_AreClonedAsIs()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<table:table><table:table-column table:number-columns-repeated=\"2\"/>" +
            "<table:table-row><table:table-cell table:number-columns-spanned=\"2\"><text:p>{{#foreach Items}}</text:p></table:table-cell><table:covered-table-cell/></table:table-row>" +
            "<table:table-row><table:table-cell table:number-columns-spanned=\"2\"><text:p>{{Name}}</text:p></table:table-cell><table:covered-table-cell/></table:table-row>" +
            "<table:table-row table:number-rows-repeated=\"2\"><table:table-cell/><table:table-cell/></table:table-row>" +
            "<table:table-row><table:table-cell table:number-columns-spanned=\"2\"><text:p>{{/foreach}}</text:p></table:table-cell><table:covered-table-cell/></table:table-row>" +
            "</table:table>");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Items"] = Items("a", "b") });

        Assert.Equal(new[] { "a", "b" }, output.GetParagraphTexts());
        List<XElement> rows = output.Body.Descendants(_table + "table-row").ToList();
        Assert.Equal(4, rows.Count);
        Assert.Equal(2, rows.Count(r => (string?)r.Attribute(_table + "number-rows-repeated") == "2"));
        Assert.All(rows.Where(r => r.Elements(_table + "covered-table-cell").Any()), r => Assert.Equal(2, r.Elements().Count()));
    }

    [Fact]
    public void CellLevelLoop_RepeatsParagraphsInsideCell()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<table:table><table:table-column/><table:table-row><table:table-cell>" +
            "<text:p>{{#foreach Items}}</text:p><text:p>- {{Name}}</text:p><text:p>{{/foreach}}</text:p>" +
            "</table:table-cell></table:table-row></table:table>");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Items"] = Items("a", "b") });

        Assert.Equal(new[] { "- a", "- b" }, output.GetParagraphTexts());
        Assert.Single(output.Body.Descendants(_table + "table-row"));
    }

    [Fact]
    public void ListItemLoop_RepeatsItems()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<text:list text:style-name=\"L1\">" +
            "<text:list-item><text:p>Intro</text:p></text:list-item>" +
            "<text:list-item><text:p>{{#foreach Items}}</text:p></text:list-item>" +
            "<text:list-item><text:p>{{@number}}. {{Name}}</text:p></text:list-item>" +
            "<text:list-item><text:p>{{/foreach}}</text:p></text:list-item>" +
            "</text:list>");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Items"] = Items("a", "b") });

        Assert.Equal(new[] { "Intro", "1. a", "2. b" }, output.GetParagraphTexts());
        Assert.Equal(3, output.Body.Descendants(_text + "list-item").Count());
        Assert.Single(output.Body.Elements(_text + "list"));
    }

    [Fact]
    public void ListItemLoop_WithNestedListItemConditional_Works()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<Dictionary<string, object>>
            {
                new() { ["Name"] = "a", ["Show"] = true },
                new() { ["Name"] = "b", ["Show"] = false },
            },
        };

        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<text:list>" +
            "<text:list-item><text:p>{{#foreach Items}}</text:p></text:list-item>" +
            "<text:list-item><text:p>{{Name}}</text:p></text:list-item>" +
            "<text:list-item><text:p>{{#if Show}}</text:p></text:list-item>" +
            "<text:list-item><text:p>shown {{Name}}</text:p></text:list-item>" +
            "<text:list-item><text:p>{{/if}}</text:p></text:list-item>" +
            "<text:list-item><text:p>{{/foreach}}</text:p></text:list-item>" +
            "</text:list>");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, data);

        Assert.Equal(new[] { "a", "shown a", "b" }, output.GetParagraphTexts());
    }

    [Fact]
    public void ListItemLoop_EmptyCollection_RemovesList()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddXml(
                "<text:list><text:list-item><text:p>{{#foreach Items}}</text:p></text:list-item>" +
                "<text:list-item><text:p>{{Name}}</text:p></text:list-item>" +
                "<text:list-item><text:p>{{/foreach}}</text:p></text:list-item></text:list>")
            .AddParagraph("after");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Items"] = new List<object>() });

        Assert.Empty(output.Body.Descendants(_text + "list"));
        Assert.Equal(new[] { "after" }, output.GetParagraphTexts());
    }

    [Fact]
    public void BodyLoop_OverListParagraphs_RepeatsSingleItemLists()
    {
        // A loop whose content is a one-item list repeats the bullet per item.
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("{{#foreach Items}}")
            .AddXml("<text:list><text:list-item><text:p>{{Name}}</text:p></text:list-item></text:list>")
            .AddParagraph("{{/foreach}}");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Items"] = Items("a", "b") });

        Assert.Equal(new[] { "a", "b" }, output.GetParagraphTexts());
        Assert.Equal(2, output.Body.Elements(_text + "list").Count());
    }

    [Fact]
    public void Loops_InHeaderAndFooter_AreProcessed()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("Body")
            .AddHeaderParagraph("{{#foreach Items}}")
            .AddHeaderParagraph("H {{Name}}")
            .AddHeaderParagraph("{{/foreach}}");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, new Dictionary<string, object> { ["Items"] = Items("a", "b") });

        Assert.Equal(new[] { "H a", "H b" }, output.GetHeaderTexts());
    }

    [Fact]
    public void SameParagraphLoop_IsRemoved_AsInWord()
    {
        List<string> texts = Process(
            Paragraphs("{{#foreach Items}}{{Name}}{{/foreach}}", "end"),
            new Dictionary<string, object> { ["Items"] = Items("a") });

        Assert.Equal(new[] { "end" }, texts);
    }

    private const string NumberedList = "<text:list xml:id=\"list1\" text:style-name=\"L1\"><text:list-item><text:p>{{Name}}</text:p></text:list-item></text:list>";

    private static readonly XName _xmlId = XNamespace.Xml + "id";

    private static List<XElement> ProcessLists(OdtDocumentBuilder template, Dictionary<string, object> data) =>
        OdtTestHelper.Process(template, data).Output.Body.Descendants(_text + "list").ToList();

    [Fact]
    public void ParagraphLoopOverList_LaterClonesContinueTheFirstClone()
    {
        List<XElement> lists = ProcessLists(
            Paragraphs("{{#foreach Items}}").AddXml(NumberedList).AddParagraph("{{/foreach}}"),
            new Dictionary<string, object> { ["Items"] = Items("a", "b", "c") });

        Assert.Equal(3, lists.Count);
        Assert.Equal("list1", (string?)lists[0].Attribute(_xmlId));
        Assert.Null(lists[0].Attribute(_text + "continue-list"));
        Assert.All(lists.Skip(1), l =>
        {
            Assert.Equal("list1", (string?)l.Attribute(_text + "continue-list"));
            Assert.Null(l.Attribute(_xmlId));
        });
    }

    [Fact]
    public void ClonedListWithoutId_GetsAnUnusedId()
    {
        List<XElement> lists = ProcessLists(
            Paragraphs("{{#foreach Items}}")
                .AddXml("<text:list text:continue-numbering=\"true\"><text:list-item><text:p>{{Name}}</text:p></text:list-item></text:list>")
                .AddParagraph("{{/foreach}}")
                .AddXml("<text:list xml:id=\"templify-list1\"><text:list-item><text:p>other</text:p></text:list-item></text:list>"),
            new Dictionary<string, object> { ["Items"] = Items("a", "b") });

        Assert.Equal("templify-list2", (string?)lists[0].Attribute(_xmlId));
        Assert.Equal("true", (string?)lists[0].Attribute(_text + "continue-numbering"));
        Assert.Equal("templify-list2", (string?)lists[1].Attribute(_text + "continue-list"));
        Assert.Null(lists[1].Attribute(_text + "continue-numbering"));
        Assert.Null(lists[2].Attribute(_text + "continue-list"));
        Assert.Equal("templify-list1", (string?)lists[2].Attribute(_xmlId));
    }

    [Fact]
    public void ListsOutsideTheLoopAndSingleIterations_AreNotLinked()
    {
        List<XElement> lists = ProcessLists(
            new OdtDocumentBuilder()
                .AddXml("<text:list><text:list-item><text:p>before</text:p></text:list-item></text:list>")
                .AddParagraph("{{#foreach Items}}").AddXml(NumberedList).AddParagraph("{{/foreach}}")
                .AddParagraph("{{#foreach One}}").AddXml(NumberedList.Replace("list1", "list2", StringComparison.Ordinal)).AddParagraph("{{/foreach}}")
                .AddXml("<text:list><text:list-item><text:p>after</text:p></text:list-item></text:list>"),
            new Dictionary<string, object> { ["Items"] = Items("a", "b"), ["One"] = Items("x") });

        Assert.Equal(5, lists.Count);
        Assert.Equal(new string?[] { null, null, "list1", null, null }, lists.Select(l => (string?)l.Attribute(_text + "continue-list")));
        Assert.Equal("list2", (string?)lists[3].Attribute(_xmlId));
    }

    [Fact]
    public void ListRemovedByConditionalInFirstIteration_NextCloneStartsTheNumbering()
    {
        List<XElement> lists = ProcessLists(
            Paragraphs("{{#foreach Items}}", "{{#if Name != \"a\"}}").AddXml(NumberedList).AddParagraph("{{/if}}").AddParagraph("{{/foreach}}"),
            new Dictionary<string, object> { ["Items"] = Items("a", "b", "c") });

        Assert.Equal(2, lists.Count);
        string? id = (string?)lists[0].Attribute(_xmlId);
        Assert.NotNull(id);
        Assert.Null(lists[0].Attribute(_text + "continue-list"));
        Assert.Equal(id, (string?)lists[1].Attribute(_text + "continue-list"));
    }

    [Fact]
    public void NestedLoops_AllCopiesOfTheListContinueTheFirstCopy()
    {
        // As in Word, where every copy of a numbered paragraph keeps its numbering instance.
        List<Dictionary<string, object>> groups = new List<Dictionary<string, object>>
        {
            new() { ["Items"] = Items("a", "b") },
            new() { ["Items"] = Items("c", "d") },
        };

        List<XElement> lists = ProcessLists(
            Paragraphs("{{#foreach Groups}}", "{{#foreach Items}}").AddXml(NumberedList).AddParagraph("{{/foreach}}").AddParagraph("{{/foreach}}"),
            new Dictionary<string, object> { ["Groups"] = groups });

        Assert.Equal(4, lists.Count);
        Assert.Equal("list1", (string?)lists[0].Attribute(_xmlId));
        Assert.All(lists.Skip(1), l => Assert.Equal("list1", (string?)l.Attribute(_text + "continue-list")));
        Assert.Single(lists, l => l.Attribute(_xmlId) != null);
    }

    [Fact]
    public void TableRowLoopOverListInCell_LaterRowsContinueTheFirstRow()
    {
        List<XElement> lists = ProcessLists(
            new OdtDocumentBuilder().AddXml(
                "<table:table table:name=\"T\"><table:table-column/>" +
                "<table:table-row><table:table-cell><text:p>{{#foreach Items}}</text:p></table:table-cell></table:table-row>" +
                "<table:table-row><table:table-cell>" + NumberedList + "</table:table-cell></table:table-row>" +
                "<table:table-row><table:table-cell><text:p>{{/foreach}}</text:p></table:table-cell></table:table-row>" +
                "</table:table>"),
            new Dictionary<string, object> { ["Items"] = Items("a", "b") });

        Assert.Equal(2, lists.Count);
        Assert.Equal("list1", (string?)lists[0].Attribute(_xmlId));
        Assert.Equal("list1", (string?)lists[1].Attribute(_text + "continue-list"));
    }

    [Fact]
    public void NestedSubLists_AreNotLinkedSeparately()
    {
        List<XElement> lists = ProcessLists(
            Paragraphs("{{#foreach Items}}")
                .AddXml("<text:list xml:id=\"outer\"><text:list-item><text:p>{{Name}}</text:p><text:list><text:list-item><text:p>sub</text:p></text:list-item></text:list></text:list-item></text:list>")
                .AddParagraph("{{/foreach}}"),
            new Dictionary<string, object> { ["Items"] = Items("a", "b") });

        List<XElement> outer = lists.Where(l => l.Parent!.Name != _text + "list-item").ToList();
        List<XElement> nested = lists.Except(outer).ToList();
        Assert.Equal("outer", (string?)outer[1].Attribute(_text + "continue-list"));
        Assert.All(nested, l => Assert.Null(l.Attribute(_text + "continue-list")));
    }

    [Fact]
    public void ListItemLoopOverItemsWithSubLists_DoesNotLinkTheSubLists()
    {
        List<XElement> lists = ProcessLists(
            new OdtDocumentBuilder().AddXml(
                "<text:list><text:list-item><text:p>{{#foreach Items}}</text:p></text:list-item>" +
                "<text:list-item><text:p>{{Name}}</text:p><text:list><text:list-item><text:p>sub</text:p></text:list-item></text:list></text:list-item>" +
                "<text:list-item><text:p>{{/foreach}}</text:p></text:list-item></text:list>"),
            new Dictionary<string, object> { ["Items"] = Items("a", "b") });

        Assert.Equal(3, lists.Count);
        Assert.All(lists, l => Assert.Null(l.Attribute(_text + "continue-list")));
    }
}
