// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Odt;

/// <summary>
/// Missing-variable validation of OpenDocument templates inside nested scopes: loops in tables, lists, sections,
/// text boxes, notes, indexes and headers; named iteration variables, loop metadata, item-relative collections,
/// JSON and POCO items.
/// </summary>
public sealed class OdtValidationScopeTests
{
    private static ValidationResult Validate(OdtDocumentBuilder template, Dictionary<string, object>? data = null)
    {
        OdtTemplateProcessor processor = new OdtTemplateProcessor(OdtTestHelper.InvariantOptions());
        using MemoryStream stream = template.ToStream();
        return data == null ? processor.ValidateTemplate(stream) : processor.ValidateTemplate(stream, data);
    }

    private static string P(string text) => $"<text:p>{OdtDocumentBuilder.Escape(text)}</text:p>";

    private static string Cell(params string[] paragraphs) =>
        $"<table:table-cell office:value-type=\"string\">{string.Concat(paragraphs.Select(P))}</table:table-cell>";

    private static string Row(params string[] cells) =>
        $"<table:table-row>{string.Concat(cells)}</table:table-row>";

    private static string Table(params string[] rows) =>
        $"<table:table table:name=\"T\"><table:table-column/>{string.Concat(rows)}</table:table>";

    private static Dictionary<string, object> OrdersData() => new Dictionary<string, object>
    {
        ["Company"] = "c",
        ["Orders"] = new List<Dictionary<string, object>>
        {
            new()
            {
                ["Id"] = 1,
                ["Lines"] = new List<Dictionary<string, object>> { new() { ["Sku"] = "a", ["Qty"] = 1 } },
                ["Notes"] = new List<string> { "n" },
            },
            new() { ["Id"] = 2, ["Lines"] = new List<Dictionary<string, object>>() },
        },
    };

    private static void AssertMissing(ValidationResult result, params string[] expected)
    {
        Assert.Equal(expected, result.MissingVariables.Order(StringComparer.Ordinal));
        Assert.All(result.Errors, e => Assert.Equal(ValidationErrorType.MissingVariable, e.Type));
        Assert.Equal(expected.Length, result.Errors.Select(e => e.Message).Distinct().Count());
        Assert.Equal(expected.Length == 0, result.IsValid);
    }

    [Fact]
    public void ParagraphLoopInCell_OfTableRowLoop_UsesTheRowItemScope()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(Table(
            Row(Cell("{{#foreach order in Orders}}")),
            Row(Cell("{{order.Id}} {{order.Nope}}", "{{#foreach order.Lines}}", "{{Sku}} {{Qty}} {{Price}} {{@index}} {{order.Id}}", "{{/foreach}}")),
            Row(Cell("{{/foreach}}"))));

        ValidationResult result = Validate(template, OrdersData());

        AssertMissing(result, "Price", "order.Nope");
        Assert.Equal(
            new[] { "@index", "Orders", "Price", "Qty", "Sku", "order.Id", "order.Lines", "order.Nope" },
            result.AllPlaceholders.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TableRowLoopInsideParagraphLoop_UsesTheOuterItemScope()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("{{#foreach Orders}}")
            .AddXml(Table(Row(Cell("{{#foreach Lines}}")), Row(Cell("{{Sku}} {{Id}} {{Company}} {{Gone}}")), Row(Cell("{{/foreach}}"))))
            .AddParagraph("{{/foreach}}");

        AssertMissing(Validate(template, OrdersData()), "Gone");
    }

    [Fact]
    public void ListItemLoop_WithNestedParagraphLoopInAnItem()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<text:list>"
            + $"<text:list-item>{P("{{#foreach o in Orders}}")}</text:list-item>"
            + $"<text:list-item>{P("{{o.Id}}")}{P("{{#foreach n in o.Notes}}")}{P("{{n}} {{o.Id}} {{n.Length}} {{Missing}}")}{P("{{/foreach}}")}</text:list-item>"
            + $"<text:list-item>{P("{{/foreach}}")}</text:list-item>"
            + "</text:list>");

        ValidationResult result = Validate(template, OrdersData());

        // A string item has no public properties other than Length (and Chars), so n.Length resolves.
        AssertMissing(result, "Missing");
    }

    [Fact]
    public void LoopInSection_WithMetadataAndNamedVariable()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<text:section text:name=\"S\">"
            + P("{{#foreach order in Orders}}")
            + P("{{@number}}/{{@count}} {{@first}} {{@last}} {{order}} {{order.Id}} {{this}} {{.}}")
            + P("{{/foreach}}")
            + "</text:section>");

        ValidationResult result = Validate(template, OrdersData());

        AssertMissing(result);
        Assert.Contains("@count", result.AllPlaceholders);
    }

    [Fact]
    public void LoopsInTextBoxAndNote_AreValidatedInTheirParagraphScope()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("{{#foreach Orders}}")
            .AddXml(
                "<text:p>Order {{Id}}"
                + "<draw:frame draw:name=\"F\"><draw:text-box>"
                + P("{{#foreach Lines}}") + P("{{Sku}} {{Id}} {{BoxMissing}}") + P("{{/foreach}}")
                + "</draw:text-box></draw:frame>"
                + "<text:note text:id=\"n1\" text:note-class=\"footnote\"><text:note-citation>1</text:note-citation><text:note-body>"
                + P("{{Id}} {{NoteMissing}}")
                + "</text:note-body></text:note></text:p>")
            .AddParagraph("{{/foreach}}")
            .AddXml("<text:p><draw:frame draw:name=\"G\"><draw:text-box>" + P("{{#foreach Absent}}") + P("{{X}}") + P("{{/foreach}}") + "</draw:text-box></draw:frame></text:p>");

        ValidationResult result = Validate(template, OrdersData());

        AssertMissing(result, "Absent", "BoxMissing", "NoteMissing");
        Assert.Contains(result.Errors, e => e.Message == "Collection 'Absent' is referenced in a loop but not provided in the data.");
    }

    [Fact]
    public void ShapeWithParagraphsDirectlyInTheBody_IsValidated()
    {
        // A page-anchored shape can hold paragraphs directly (no text box).
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddXml("<draw:custom-shape draw:name=\"S\">" + P("{{#if A}}") + P("{{Shape}}") + "</draw:custom-shape>");

        ValidationResult syntax = Validate(template);
        ValidationResult withData = Validate(template.AddParagraph("x"), new Dictionary<string, object> { ["A"] = true });

        ValidationError error = Assert.Single(syntax.Errors);
        Assert.Equal("Conditional start marker '{{#if A}}' has no matching '{{/if}}'.", error.Message);
        Assert.Contains("Shape", withData.MissingVariables);
    }

    [Fact]
    public void LoopInHeaderAndFooter_AreScopedToTheirItems()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("Body")
            .AddHeaderXml(P("{{#foreach o in Orders}}") + P("{{o.Id}} {{o.Nope}}") + P("{{/foreach}}"))
            .AddFooterXml(Table(Row(Cell("{{#foreach Orders}}")), Row(Cell("{{Id}} {{FooterNope}}")), Row(Cell("{{/foreach}}"))));

        AssertMissing(Validate(template, OrdersData()), "FooterNope", "o.Nope");
    }

    [Fact]
    public void IndexBody_IsValidated_ButNotTheIndexSource()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<text:table-of-content text:name=\"TOC\">"
            + "<text:table-of-content-source text:outline-level=\"3\"><text:index-title-template>{{SourceOnly}}</text:index-title-template></text:table-of-content-source>"
            + "<text:index-body><text:index-title text:name=\"TOC_Head\">" + P("{{TocTitle}}") + "</text:index-title>"
            + P("{{#foreach Orders}}") + P("{{Id}} {{TocMissing}}") + P("{{/foreach}}")
            + "</text:index-body></text:table-of-content>");

        ValidationResult result = Validate(template, OrdersData());

        AssertMissing(result, "TocMissing", "TocTitle");
        Assert.DoesNotContain("SourceOnly", result.AllPlaceholders);
    }

    [Fact]
    public void RowGroups_AreValidatedLikeRows()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddXml(
            "<table:table table:name=\"T\"><table:table-column/>"
            + "<table:table-header-rows>" + Row(Cell("{{HeaderMissing}}")) + "</table:table-header-rows>"
            + "<table:table-rows>" + Row(Cell("{{#foreach Orders}}")) + Row(Cell("{{Id}} {{RowMissing}}")) + Row(Cell("{{/foreach}}")) + "</table:table-rows>"
            + "<table:table-row-group><table:table-rows>" + Row(Cell("{{#if A}}")) + Row(Cell("x")) + "</table:table-rows></table:table-row-group>"
            + "</table:table>");

        ValidationResult syntax = Validate(template);
        ValidationResult withData = Validate(template, OrdersData());

        ValidationError error = Assert.Single(syntax.Errors);
        Assert.Equal("Table row conditional start marker '{{#if A}}' has no matching '{{/if}}'.", error.Message);
        Assert.Contains("HeaderMissing", withData.MissingVariables);
        Assert.Contains("RowMissing", withData.MissingVariables);
        Assert.DoesNotContain("Id", withData.MissingVariables);
    }

    [Fact]
    public void EmptyContainers_AreValid()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddXml("<text:section text:name=\"Empty\"/>")
            .AddXml("<text:list/>")
            .AddXml("<table:table table:name=\"NoRows\"><table:table-column/></table:table>")
            .AddParagraph("{{Company}}");

        ValidationResult result = Validate(template, OrdersData());

        Assert.True(result.IsValid);
        Assert.Equal(new[] { "Company" }, result.AllPlaceholders);
    }

    [Fact]
    public void ItemRelativeCollection_MissingOnTheItems_IsReportedAsCollection()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("{{#foreach o in Orders}}")
            .AddParagraph("{{#foreach o.Nope}}")
            .AddParagraph("{{X}}")
            .AddParagraph("{{/foreach}}")
            .AddParagraph("{{#foreach Absent}}")
            .AddParagraph("{{Y}}")
            .AddParagraph("{{/foreach}}")
            .AddParagraph("{{/foreach}}");

        ValidationResult result = Validate(template, OrdersData());

        AssertMissing(result, "Absent", "o.Nope");
        Assert.Equal(
            new[]
            {
                "Collection 'Absent' is referenced in a loop but not provided in the data.",
                "Collection 'o.Nope' is referenced in a loop but not provided in the data.",
            },
            result.Errors.Select(e => e.Message).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void NestedCollectionEmptyOnAllItems_AddsEmptyWarning()
    {
        Dictionary<string, object> data = OrdersData();
        ((List<Dictionary<string, object>>)data["Orders"])[0]["Lines"] = new List<Dictionary<string, object>>();

        ValidationResult result = Validate(
            new OdtDocumentBuilder().AddParagraph("{{#foreach Orders}}").AddParagraph("{{#foreach Lines}}").AddParagraph("{{Sku}}")
                .AddParagraph("{{/foreach}}").AddParagraph("{{/foreach}}"),
            data);

        Assert.True(result.IsValid);
        ValidationWarning warning = Assert.Single(result.Warnings);
        Assert.Equal("Collection 'Lines' is empty. Variables inside this loop could not be validated.", warning.Message);
    }

    [Fact]
    public void ConditionalsReferencingItemProperties_AreListed_AndTheirBranchesValidated()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("{{#foreach o in Orders}}")
            .AddParagraph("{{#if o.Id > 1 and o.Lines is not empty}}")
            .AddParagraph("{{o.Id}} {{BranchMissing}}")
            .AddParagraph("{{#elseif o.Flag}}")
            .AddParagraph("{{o.Notes}}")
            .AddParagraph("{{/if}}")
            .AddParagraph("{{/foreach}}");

        ValidationResult result = Validate(template, OrdersData());

        // Condition operands are not reported as missing (an unresolved operand is a string literal); the
        // placeholders of both branches are checked.
        AssertMissing(result, "BranchMissing");
        Assert.Contains("o.Flag", result.AllPlaceholders);
        Assert.Contains("o.Lines", result.AllPlaceholders);
    }

    [Fact]
    public void JsonData_ArraysAndObjects_AreScopes()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Doc"] = JsonDocument.Parse("{\"Title\":\"t\",\"Rows\":[{\"Key\":\"k\",\"Tags\":[\"a\"]},{\"Key\":\"l\",\"Extra\":1}],\"Scalar\":5}").RootElement,
        };
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("{{Doc.Title}} {{Doc.Nope}}")
            .AddParagraph("{{#foreach r in Doc.Rows}}")
            .AddParagraph("{{r.Key}} {{r.Extra}} {{r.Nope}}")
            .AddParagraph("{{#foreach r.Tags}}")
            .AddParagraph("{{.}}")
            .AddParagraph("{{/foreach}}")
            .AddParagraph("{{/foreach}}")
            .AddParagraph("{{#foreach Doc.Scalar}}")
            .AddParagraph("{{Z}}")
            .AddParagraph("{{/foreach}}");

        ValidationResult result = Validate(template, data);

        AssertMissing(result, "Doc.Nope", "r.Nope");

        // A scalar is no collection: its loop body cannot be validated.
        ValidationWarning warning = Assert.Single(result.Warnings);
        Assert.Equal("Collection 'Doc.Scalar' is empty. Variables inside this loop could not be validated.", warning.Message);
    }

    [Fact]
    public void PocoItems_WithNulls_AreScopes()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["People"] = new List<Person?> { new Person("Ada", new List<string> { "x" }), null, new Person("Bob", new List<string>()) },
            ["Text"] = "abc",
        };
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("{{#foreach People}}")
            .AddParagraph("{{Name}} {{Age}}")
            .AddParagraph("{{#foreach Tags}}")
            .AddParagraph("{{.}}")
            .AddParagraph("{{/foreach}}")
            .AddParagraph("{{/foreach}}")
            .AddParagraph("{{#foreach Text}}")
            .AddParagraph("{{Q}}")
            .AddParagraph("{{/foreach}}");

        ValidationResult result = Validate(template, data);

        AssertMissing(result, "Age");
        ValidationWarning warning = Assert.Single(result.Warnings);
        Assert.Equal("Collection 'Text' is empty. Variables inside this loop could not be validated.", warning.Message);
    }

    [Fact]
    public void UnmatchedLoop_WithData_IsReported_AndItsBodyIsCheckedGlobally()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("{{Company}} {{Missing}}")
            .AddParagraph("{{#foreach Orders}}")
            .AddParagraph("{{Id}}");

        ValidationResult result = Validate(template, OrdersData());

        Assert.Equal(
            new[]
            {
                "MissingVariable: Variable 'Id' is referenced in the template but not provided in the data.",
                "MissingVariable: Variable 'Missing' is referenced in the template but not provided in the data.",
                "UnmatchedLoopStart: Loop start marker '{{#foreach Orders}}' has no matching '{{/foreach}}'.",
            },
            result.Errors.Select(e => $"{e.Type}: {e.Message}").Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AnnotationsAndTrackedDeletions_AreNotValidated()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddXml("<text:tracked-changes><text:changed-region text:id=\"c1\"><text:deletion>" + P("{{Deleted}}") + "</text:deletion></text:changed-region></text:tracked-changes>")
            .AddXml("<text:p>{{Company}}<office:annotation><dc:creator>A</dc:creator>" + P("{{Comment}} {{#if X}}") + "</office:annotation></text:p>");

        ValidationResult result = Validate(template, OrdersData());

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Equal(new[] { "Company" }, result.AllPlaceholders);
    }

    [Fact]
    public void IOExceptionWhileReading_IsInvalidDocument()
    {
        using FailingStream stream = new FailingStream(new OdtDocumentBuilder().AddParagraph("{{A}}").ToBytes());

        ValidationResult result = new OdtTemplateProcessor().ValidateTemplate(stream);

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorType.InvalidDocument, error.Type);
        Assert.Equal("Validation failed: disk gone", error.Message);
    }

    public sealed record Person(string Name, List<string> Tags);

    /// <summary>A non-seekable stream (copied into memory first) that fails after the first bytes.</summary>
    private sealed class FailingStream : MemoryStream
    {
        public FailingStream(byte[] data)
            : base(data)
        {
        }

        public override bool CanSeek => false;

        public override int Read(byte[] buffer, int offset, int count) =>
            Position > 64 ? throw new IOException("disk gone") : base.Read(buffer, offset, Math.Min(count, 64));

        public override int Read(Span<byte> buffer) =>
            Position > 64 ? throw new IOException("disk gone") : base.Read(buffer[..Math.Min(buffer.Length, 64)]);
    }
}
