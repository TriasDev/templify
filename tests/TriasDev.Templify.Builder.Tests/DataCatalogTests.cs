// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TriasDev.Templify.Builder.Json;
using TriasDev.Templify.Builder.Sample;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>The data catalogue of a C# type, and the import check against it (#274).</summary>
public sealed class DataCatalogTests
{
    public sealed record OrderData(Order Order);

    public sealed record Order(string Number, DateTime Date, decimal? Total, bool IsExpress, Customer Customer, IReadOnlyList<Line> Lines, string[] Tags);

    public sealed record Customer(string Name, Address Address);

    public sealed record Address(string Street, string City);

    public sealed record Line(string Description, int Quantity);

    public sealed record Renamed([property: JsonPropertyName("cust")] Customer Customer, bool IsExpress, [property: JsonIgnore] string Secret);

    public sealed record Node(string Name, IReadOnlyList<Node> Children, Node? Parent);

    public sealed record WithOpen(Dictionary<string, object> Extra, JsonElement Raw, object Anything);

    [Fact]
    public void From_ListsPathsWithKinds()
    {
        DataCatalog catalog = DataCatalog.From<OrderData>();

        Assert.Contains(new DataCatalogEntry("order.number", DataKind.Value, typeof(string)), catalog.Entries);
        Assert.Contains(new DataCatalogEntry("order.total", DataKind.Value, typeof(decimal?)), catalog.Entries);
        Assert.Contains(new DataCatalogEntry("order.customer", DataKind.Object, typeof(Customer)), catalog.Entries);
        Assert.Contains(new DataCatalogEntry("order.lines", DataKind.Collection, typeof(IReadOnlyList<Line>)), catalog.Entries);
        Assert.Contains(new DataCatalogEntry("order.lines[]", DataKind.Object, typeof(Line)), catalog.Entries);
        Assert.Contains(new DataCatalogEntry("order.tags[]", DataKind.Value, typeof(string)), catalog.Entries);
        Assert.True(catalog.Contains("order.lines[].description"));
        Assert.True(catalog.Contains("order.customer.address.city"));
        Assert.False(catalog.Contains("order.customer.address.zip"));
        Assert.False(catalog.Contains("order.number.length"));
    }

    [Fact]
    public void From_HonoursNaming()
    {
        Assert.True(DataCatalog.From<Renamed>().Contains("cust.name"));
        Assert.False(DataCatalog.From<Renamed>().Contains("secret"));
        Assert.True(DataCatalog.From<Renamed>(JsonNamingPolicy.SnakeCaseLower).Contains("is_express"));
        Assert.False(DataCatalog.From<Renamed>(JsonNamingPolicy.SnakeCaseLower).Contains("isExpress"));
    }

    [Fact]
    public void From_RecursiveType_Terminates()
    {
        DataCatalog catalog = DataCatalog.From<Node>();

        Assert.True(catalog.Contains("children[].name"));
        Assert.True(catalog.Contains("parent"));
        Assert.DoesNotContain(catalog.Entries, e => e.Path.Count(c => c == '.') > 16);
    }

    [Fact]
    public void From_OpenShapes_AcceptAnythingBelow()
    {
        DataCatalog catalog = DataCatalog.From<WithOpen>();

        Assert.Contains(catalog.Entries, e => e.Path == "extra" && e.Kind == DataKind.Open);
        Assert.True(catalog.Contains("extra.anything.deep"));
        Assert.True(catalog.Contains("raw.x"));
        Assert.True(catalog.Contains("anything[].y"));
    }

    [Fact]
    public void Import_PathOutsideCatalog_IsTB9203()
    {
        const string json = """
            {
              "templifyDefinition": 1,
              "identity": { "id": "c", "version": 1 },
              "content": [
                { "paragraph": "{{order.nummer}}" },
                { "if": "order.totl > 1", "then": [ { "paragraph": "x" } ] },
                { "forEach": "order.lines", "as": "line", "body": [ "{{line.descr}}{{@number}}" ] }
              ]
            }
            """;

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(() =>
            TemplateJson.Import(json, new TemplateJsonImportOptions { Data = DataCatalog.From<OrderData>() }));

        Assert.All(exception.Errors, e => Assert.Equal("TB9203", e.Code));
        Assert.Equal(
            new[] { "/content/0/paragraph", "/content/1/if", "/content/2/body/0" },
            exception.Errors.Select(e => e.Location));
        Assert.Contains("order.lines[].descr", exception.Errors[2].Message, StringComparison.Ordinal);

        TemplateJson.Import(json.Replace("nummer", "number", StringComparison.Ordinal).Replace("totl", "total", StringComparison.Ordinal).Replace("line.descr}", "line.description}", StringComparison.Ordinal), new TemplateJsonImportOptions { Data = DataCatalog.From<OrderData>() });
    }

    [Fact]
    public void Sample_ContractIsInsideTheCatalogue()
    {
        TemplateBuildOptions options = new TemplateBuildOptions { Format = TemplateFormat.Docx, Language = CultureInfo.GetCultureInfo("en-GB"), Texts = SampleTexts.Catalog };
        GeneratedTemplate template = TemplateBuilder.Build(new OrderConfirmation(BrandTheme.CreateDefault()), options);
        DataCatalog catalog = DataCatalog.From<SampleOrderData>();

        Assert.All(template.Contract.Entries, e => Assert.True(catalog.Contains(e.Path), e.Path));
    }

    public sealed record SampleOrderData(SampleOrder Order);

    public sealed record SampleOrder(
        string Number, DateTime Date, string Status, bool IsExpress, decimal Total, decimal Discount, string Notes,
        SampleCustomer Customer, IReadOnlyList<SampleLine> Lines, IReadOnlyList<SampleCheck> Checks);

    public sealed record SampleCustomer(string Name, string Contact, bool IsNew, SampleAddress Address);

    public sealed record SampleAddress(string Street, string PostalCode, string City, string Country);

    public sealed record SampleLine(string Description, int Quantity, decimal Amount, bool InStock, IReadOnlyList<string> Tags);

    public sealed record SampleCheck(string Title, bool Passed, string? Note);
}
