// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// <see cref="DocumentTemplateProcessor.ValidateTemplate(Stream, Dictionary{string, object})"/> scopes the variables
/// inside table row loops like processing does (issue #198).
/// </summary>
public sealed class ValidationTableRowLoopTests
{
    private static ValidationResult Validate(DocumentBuilder builder, Dictionary<string, object> data)
    {
        DocumentTemplateProcessor processor = TemplateTestHarness.CreateInvariantProcessor();
        using MemoryStream template = builder.ToStream();
        return processor.ValidateTemplate(template, data);
    }

    /// <summary>
    /// Validates the template and asserts that it is valid, and that processing resolves every variable too.
    /// </summary>
    private static ValidationResult AssertValidAndProcessable(Func<DocumentBuilder> template, Dictionary<string, object> data)
    {
        using (TemplateTestRun run = TemplateTestHarness.Process(template(), data))
        {
            Assert.True(run.Result.IsSuccess, run.Result.ErrorMessage);
            Assert.Empty(run.Result.MissingVariables);
        }

        ValidationResult result = Validate(template(), data);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Empty(result.MissingVariables);
        return result;
    }

    private static Paragraph P(string text) =>
        new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    private static TableCell Cell(params OpenXmlElement[] content) => new TableCell(content);

    private static TableRow Row(params string[] cellTexts) =>
        new TableRow(cellTexts.Select(t => (OpenXmlElement)Cell(P(t))));

    private static Table Tbl(params TableRow[] rows) => new Table(rows);

    private static Dictionary<string, object> Item(params (string Key, object Value)[] properties) =>
        properties.ToDictionary(p => p.Key, p => p.Value);

    private static Dictionary<string, object> OrdersData() => new Dictionary<string, object>
    {
        ["Company"] = "ACME",
        ["Orders"] = new List<Dictionary<string, object>>
        {
            Item(
                ("Number", "O-1"),
                ("Lines", new List<Dictionary<string, object>> { Item(("Product", "X"), ("Qty", 1)) })),
            Item(
                ("Number", "O-2"),
                ("Lines", new List<Dictionary<string, object>> { Item(("Product", "Y"), ("Qty", 2)) }))
        }
    };

    [Fact]
    public void ValidateTemplate_TableRowLoop_ItemPropertiesMetadataAndGlobalsAreResolved()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Company"] = "ACME",
            ["Items"] = new List<Dictionary<string, object>>
            {
                Item(("Title", "A"), ("Address", Item(("City", "Berlin")))),
                Item(("Title", "B"), ("Address", Item(("City", "Paris"))))
            }
        };

        DocumentBuilder Template()
        {
            DocumentBuilder builder = new DocumentBuilder();
            builder.AddElement(Tbl(
                Row("{{#foreach Items}}", ""),
                Row("{{@number}}. {{Title}}", "{{Address.City}} {{Company}} {{@index}} {{@first}} {{@last}} {{@count}} {{.}}"),
                Row("{{/foreach}}", "")));
            return builder;
        }

        ValidationResult result = AssertValidAndProcessable(Template, data);

        Assert.Contains("Items", result.AllPlaceholders);
        Assert.Contains("Title", result.AllPlaceholders);
        Assert.Contains("Address.City", result.AllPlaceholders);
    }

    [Fact]
    public void ValidateTemplate_TableRowLoop_UnknownVariableIsReportedMissing()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddElement(Tbl(
            Row("{{#foreach Items}}"),
            Row("{{Title}} {{Unknown}}"),
            Row("{{/foreach}}")));

        ValidationResult result = Validate(builder, new Dictionary<string, object>
        {
            ["Items"] = new List<Dictionary<string, object>> { Item(("Title", "A")) }
        });

        Assert.False(result.IsValid);
        Assert.Equal(new[] { "Unknown" }, result.MissingVariables);
        Assert.Contains(result.Errors, e => e.Type == ValidationErrorType.MissingVariable && e.Message.Contains("'Unknown'"));
    }

    [Fact]
    public void ValidateTemplate_TableRowLoopWithNamedIterationVariable_KnownAndUnknownProperties()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddElement(Tbl(
            Row("{{#foreach item in Items}}"),
            Row("{{item}} {{item.Title}} {{item.Title.Length}} {{Title}} {{item.Missing}}"),
            Row("{{/foreach}}")));

        ValidationResult result = Validate(builder, new Dictionary<string, object>
        {
            ["Items"] = new List<Dictionary<string, object>> { Item(("Title", "A")), Item(("Title", "B")) }
        });

        Assert.Equal(new[] { "item.Missing" }, result.MissingVariables);
    }

    [Fact]
    public void ValidateTemplate_TableRowLoopWithNamedIterationVariable_IsValidAndProcessable()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<Dictionary<string, object>> { Item(("Title", "A")) }
        };

        DocumentBuilder Template()
        {
            DocumentBuilder builder = new DocumentBuilder();
            builder.AddElement(Tbl(
                Row("{{#foreach item in Items}}"),
                Row("{{item.Title}}"),
                Row("{{/foreach}}")));
            return builder;
        }

        AssertValidAndProcessable(Template, data);
    }

    [Fact]
    public void ValidateTemplate_TableRowLoopInsideBodyLoop_ScopesItemsOfBothLoops()
    {
        DocumentBuilder Template()
        {
            DocumentBuilder builder = new DocumentBuilder();
            builder.AddParagraph("{{#foreach order in Orders}}");
            builder.AddParagraph("{{order.Number}}");
            builder.AddElement(Tbl(
                Row("{{#foreach line in order.Lines}}", ""),
                Row("{{line.Product}} {{Product}}", "{{order.Number}} {{Number}} {{Company}} {{@index}}"),
                Row("{{/foreach}}", "")));
            builder.AddElement(Tbl(
                Row("{{#foreach Lines}}"),
                Row("{{Qty}}"),
                Row("{{/foreach}}")));
            builder.AddParagraph("{{/foreach}}");
            return builder;
        }

        AssertValidAndProcessable(Template, OrdersData());
    }

    [Fact]
    public void ValidateTemplate_TableRowLoopInsideBodyLoop_ReportsMissingVariablesAndCollections()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach order in Orders}}");
        builder.AddElement(Tbl(
            Row("{{#foreach line in order.Lines}}"),
            Row("{{line.Product}} {{line.Price}} {{Nope}}"),
            Row("{{/foreach}}")));
        builder.AddElement(Tbl(
            Row("{{#foreach Lnes}}"),
            Row("{{Qty}}"),
            Row("{{/foreach}}")));
        builder.AddParagraph("{{/foreach}}");

        ValidationResult result = Validate(builder, OrdersData());

        Assert.Equal(
            new[] { "Lnes", "Nope", "line.Price" },
            result.MissingVariables.Order(StringComparer.Ordinal));
        Assert.Contains(result.Errors, e => e.Message.Contains("Collection 'Lnes'"));
    }

    [Fact]
    public void ValidateTemplate_BodyLoopInsideTableRowLoopCell_ScopesItemsOfBothLoops()
    {
        DocumentBuilder Template()
        {
            DocumentBuilder builder = new DocumentBuilder();
            builder.AddElement(Tbl(
                Row("{{#foreach Orders}}"),
                new TableRow(Cell(
                    P("{{Number}}"),
                    P("{{#foreach Lines}}"),
                    P("{{Product}} {{Qty}} {{Number}} {{Company}}"),
                    P("{{/foreach}}"))),
                Row("{{/foreach}}")));
            return builder;
        }

        AssertValidAndProcessable(Template, OrdersData());
    }

    [Fact]
    public void ValidateTemplate_BodyLoopInsideTableRowLoopCell_ReportsMissingVariable()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddElement(Tbl(
            Row("{{#foreach Orders}}"),
            new TableRow(Cell(
                P("{{#foreach Lines}}"),
                P("{{Product}} {{Missing}}"),
                P("{{/foreach}}"))),
            Row("{{/foreach}}")));

        ValidationResult result = Validate(builder, OrdersData());

        Assert.Equal(new[] { "Missing" }, result.MissingVariables);
    }

    [Fact]
    public void ValidateTemplate_NestedTableRowLoops_SameTableAndNestedTable_AreScoped()
    {
        DocumentBuilder Template()
        {
            DocumentBuilder builder = new DocumentBuilder();

            // Nested row loops in the same table
            builder.AddElement(Tbl(
                Row("{{#foreach Orders}}"),
                Row("{{Number}}"),
                Row("{{#foreach Lines}}"),
                Row("{{Product}} {{Number}}"),
                Row("{{/foreach}}"),
                Row("{{/foreach}}")));

            // Row loop in a table nested in a cell of a row loop
            builder.AddElement(Tbl(
                Row("{{#foreach order in Orders}}"),
                new TableRow(Cell(
                    P("{{order.Number}}"),
                    Tbl(
                        Row("{{#foreach line in order.Lines}}"),
                        Row("{{line.Product}} {{order.Number}}"),
                        Row("{{/foreach}}")),
                    P(""))),
                Row("{{/foreach}}")));
            return builder;
        }

        AssertValidAndProcessable(Template, OrdersData());
    }

    [Fact]
    public void ValidateTemplate_NestedTableInTableRowLoop_ReportsMissingVariable()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddElement(Tbl(
            Row("{{#foreach Orders}}"),
            new TableRow(Cell(
                Tbl(
                    Row("{{#foreach Lines}}"),
                    Row("{{Product}} {{Unknown}}"),
                    Row("{{/foreach}}")),
                P(""))),
            Row("{{/foreach}}")));

        ValidationResult result = Validate(builder, OrdersData());

        Assert.Equal(new[] { "Unknown" }, result.MissingVariables);
    }

    [Fact]
    public void ValidateTemplate_ConditionalsInsideTableRowLoop_ReferencingItemProperties_AreValid()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<Dictionary<string, object>>
            {
                Item(("Title", "A"), ("IsActive", true)),
                Item(("Title", "B"), ("IsActive", false))
            }
        };

        DocumentBuilder Template()
        {
            DocumentBuilder builder = new DocumentBuilder();
            builder.AddElement(Tbl(
                Row("{{#foreach Items}}"),
                Row("{{#if IsActive}}"),
                Row("{{Title}}"),
                Row("{{/if}}"),
                Row("{{#if Title = \"A\"}}first {{Title}}{{#else}}other{{/if}}"),
                Row("{{/foreach}}")));
            return builder;
        }

        ValidationResult result = AssertValidAndProcessable(Template, data);

        Assert.Contains("IsActive", result.AllPlaceholders);
    }

    [Fact]
    public void ValidateTemplate_TableRowLoopInHeader_ScopesItemPropertiesAndReportsMissing()
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            ["Items"] = new List<Dictionary<string, object>> { Item(("Title", "A")) }
        };

        DocumentBuilder Template(string cellText)
        {
            DocumentBuilder builder = new DocumentBuilder();
            builder.AddParagraph("Body");
            builder.AddHeader("Header");
            HeaderPart headerPart = builder.MainPart.HeaderParts.Single();
            headerPart.Header!.Append(Tbl(
                Row("{{#foreach Items}}"),
                Row(cellText),
                Row("{{/foreach}}")));
            headerPart.Header.Append(P(""));
            headerPart.Header.Save();
            return builder;
        }

        AssertValidAndProcessable(() => Template("{{Title}} {{@number}}"), data);

        ValidationResult result = Validate(Template("{{Title}} {{Unknown}}"), data);
        Assert.Equal(new[] { "Unknown" }, result.MissingVariables);
    }

    [Fact]
    public void ValidateTemplate_TableRowLoopInFooterOverMissingCollection_ReportsMissingCollection()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Body");
        builder.AddFooter("Footer");
        FooterPart footerPart = builder.MainPart.FooterParts.Single();
        footerPart.Footer!.Append(Tbl(
            Row("{{#foreach Items}}"),
            Row("{{Title}}"),
            Row("{{/foreach}}")));
        footerPart.Footer.Append(P(""));
        footerPart.Footer.Save();

        ValidationResult result = Validate(builder, new Dictionary<string, object> { ["Other"] = 1 });

        Assert.Equal(new[] { "Items" }, result.MissingVariables);
        Assert.Contains(result.Errors, e => e.Message.Contains("Collection 'Items'"));
    }

    [Fact]
    public void ValidateTemplate_UnmatchedTableRowLoopInNestedTable_ReportsSyntaxError()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddElement(Tbl(
            new TableRow(Cell(
                Tbl(
                    Row("{{#foreach Items}}"),
                    Row("{{Title}}")),
                P("")))));

        DocumentTemplateProcessor processor = TemplateTestHarness.CreateInvariantProcessor();
        using MemoryStream template = builder.ToStream();
        ValidationResult result = processor.ValidateTemplate(template);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Type == ValidationErrorType.UnmatchedLoopStart);
    }
}
