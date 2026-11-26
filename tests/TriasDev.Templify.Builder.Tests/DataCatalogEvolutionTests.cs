// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>The data catalogue as JSON and as witness data for checking stored templates (#294).</summary>
public sealed class DataCatalogEvolutionTests
{
    public sealed record OrderData(Order Order);

    public sealed record Order(string Number, bool IsExpress, Customer Customer, IReadOnlyList<Line> Lines, Dictionary<string, object> Extra);

    public sealed record Customer(string Name, string Contact);

    public sealed record Line(string Description, decimal Amount);

    [Fact]
    public void ToJson_ListsEveryPath_WithKindAndType()
    {
        DataCatalog catalog = DataCatalog.From<OrderData>();

        JsonNode root = JsonNode.Parse(catalog.ToJson())!;

        Assert.Equal(1, root["version"]!.GetValue<int>());
        JsonArray paths = root["paths"]!.AsArray();
        Assert.Equal(catalog.Entries.Count, paths.Count);
        Assert.Equal(catalog.Entries.Select(e => e.Path), paths.Select(p => p!["path"]!.GetValue<string>()));
        JsonNode lines = paths.Single(p => p!["path"]!.GetValue<string>() == "order.lines")!;
        Assert.Equal("collection", lines["kind"]!.GetValue<string>());
        JsonNode amount = paths.Single(p => p!["path"]!.GetValue<string>() == "order.lines[].amount")!;
        Assert.Equal("value", amount["kind"]!.GetValue<string>());
        Assert.Equal("decimal", amount["type"]!.GetValue<string>());
        Assert.Equal("open", paths.Single(p => p!["path"]!.GetValue<string>() == "order.extra")!["kind"]!.GetValue<string>());
        Assert.Contains("\n", catalog.ToJson(), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Witness_ValidatesTemplateReadingAnyCataloguePath(TemplateFormat format)
    {
        GeneratedTemplate template = BuilderHarness.Build(
            new TestDefinition(t =>
            {
                TemplatePath order = t.Data["order"];
                t.Content(c =>
                {
                    c.Paragraph(order["customer"]["name"]);
                    c.If(order["isExpress"], b => b.Paragraph("Express"));
                    c.ForEach(order["lines"], "line", (line, b) => b.Paragraph(line["description"]));
                });
            }),
            format);

        ValidationResult result = DataCatalog.From<OrderData>().ValidateTemplate(template.Content.ToArray());

        Assert.True(result.IsValid);
        Assert.Empty(result.MissingVariables);
        Assert.DoesNotContain(result.Warnings, w => w.Type == ValidationWarningType.MissingConditionVariable);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Witness_AcceptsAnyPathBelowAnOpenShape(TemplateFormat format)
    {
        GeneratedTemplate template = BuilderHarness.Build(
            new TestDefinition(t =>
            {
                TemplatePath order = t.Data["order"];
                t.Content(c =>
                {
                    c.Paragraph(order["extra"]["anything"]["deep"]);
                    c.If(order["extra"]["flag"], b => b.Paragraph("Flag"));
                    c.ForEach(order["extra"]["items"], "item", (item, b) => b.Paragraph(item["name"]));
                });
            }),
            format);

        ValidationResult result = DataCatalog.From<OrderData>().ValidateTemplate(template.Content.ToArray());

        Assert.True(result.IsValid);
        Assert.Empty(result.MissingVariables);
        Assert.DoesNotContain(result.Warnings, w => w.Type == ValidationWarningType.MissingConditionVariable);
    }

    [Fact]
    public void Witness_LacksPathsTheCatalogueDoesNotHave()
    {
        GeneratedTemplate template = BuilderHarness.Build(
            new TestDefinition(t => t.Content(c => c.Paragraph(t.Data["order"]["customer"]["fax"]))),
            TemplateFormat.Docx);

        ValidationResult result = DataCatalog.From<OrderData>().ValidateTemplate(template.Content.ToArray());

        Assert.Equal(["order.customer.fax"], result.MissingVariables);
    }

    [Fact]
    public void Witness_OpenShapeNames_DoNotReachClosedTypes()
    {
        GeneratedTemplate template = BuilderHarness.Build(
            new TestDefinition(t =>
            {
                TemplatePath order = t.Data["order"];
                t.Content(c =>
                {
                    c.Paragraph(order["extra"]["fax"]);
                    c.Paragraph(order["customer"]["fax"]);
                    c.ForEach(order["lines"], "line", (line, b) => b.Paragraph(line["fax"]));
                    c.If(order["customer"]["vip"], b => b.Paragraph("VIP"));
                });
            }),
            TemplateFormat.Docx);

        ValidationResult result = DataCatalog.From<OrderData>().ValidateTemplate(template.Content.ToArray());

        Assert.Equal(["line.fax", "order.customer.fax"], result.MissingVariables.Order(StringComparer.Ordinal));
        Assert.Contains(DataCatalog.MissingNames(result), n => n == "order.customer.vip");
    }

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);
}
