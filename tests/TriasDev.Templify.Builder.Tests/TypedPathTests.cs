// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>Data paths from C# types (#274, approach A with types all the way through).</summary>
public sealed class TypedPathTests
{
    public sealed record OrderData(Order Order);

    public sealed record Order(string Number, decimal Total, bool IsExpress, Customer Customer, List<Line> Lines, Dictionary<string, string> Settings);

    public sealed record Customer(string Name, Address Address);

    public sealed record Address(string Street, string City);

    public sealed record Line(string Description, int Quantity);

    public sealed record Renamed([property: JsonPropertyName("cust")] Customer Customer, bool IsExpress);

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private static TemplatePath<OrderData> Root(Action<TemplatePath<OrderData>>? use = null)
    {
        TemplatePath<OrderData>? root = null;
        BuilderHarness.Build(new TestDefinition(t =>
        {
            root = t.DataAs<OrderData>();
            use?.Invoke(root.Value);
            t.Content(c => c.Paragraph("x"));
        }), TemplateFormat.Docx);
        return root!.Value;
    }

    [Fact]
    public void Path_MapsMembersWithCamelCase()
    {
        Assert.Equal("order.customer.name", Root().Path(o => o.Order.Customer.Name).ToString());
    }

    [Fact]
    public void Path_HonoursJsonPropertyNameAndPolicy()
    {
        string? renamed = null;
        string? snake = null;
        BuilderHarness.Build(new TestDefinition(t =>
        {
            renamed = t.DataAs<Renamed>().Path(r => r.Customer.Name).ToString();
            snake = t.DataAs<Renamed>(JsonNamingPolicy.SnakeCaseLower).Path(r => r.IsExpress).ToString();
            t.Content(c => c.Paragraph("x"));
        }), TemplateFormat.Docx);

        Assert.Equal("cust.name", renamed);
        Assert.Equal("is_express", snake);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Path_FillsWithDataInThatShape(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(t =>
        {
            TemplatePath<Renamed> d = t.DataAs<Renamed>(JsonNamingPolicy.SnakeCaseLower);
            t.Content(c => c.Paragraph(x =>
            {
                x.Value(d.Path(r => r.Customer.Name));
                x.Text(" ");
                x.Expression(d.Path(r => r.IsExpress), ValueFormat.YesNo);
            }));
        });

        (_, byte[] filled) = BuilderHarness.BuildAndFill(definition, format, """{ "cust": { "name": "Ada" }, "is_express": true }""");

        Assert.Equal("Ada Yes", Assert.Single(BuilderHarness.ParagraphTexts(format, filled)));
    }

    [Fact]
    public void Path_DictionaryKey_IsASegment()
    {
        Assert.Equal("order.settings.Theme", Root().Path(o => o.Order.Settings["Theme"]).ToString());
    }

    [Theory]
    [InlineData("first")]
    [InlineData("method")]
    [InlineData("captured")]
    [InlineData("index")]
    public void Path_UnsupportedExpressions_Throw(string kind)
    {
        int i = 0;
        TemplatePath<OrderData> d = Root();

        ArgumentException exception = Assert.Throws<ArgumentException>(() => kind switch
        {
            "first" => d.Path(o => o.Order.Lines.First().Description).ToString(),
            "method" => d.Path(o => o.Order.Number.ToUpperInvariant()).ToString(),
            "captured" => d.Path(o => o.Order.Settings[i.ToString(System.Globalization.CultureInfo.InvariantCulture)]).ToString(),
            _ => d.Path(o => o.Order.Lines[i].Description).ToString(),
        });

        Assert.Contains("o => o.Order", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void TypedLoop_ProducesTheSameBytesAsTheStringApi(TemplateFormat format)
    {
        TestDefinition typed = new TestDefinition(t =>
        {
            TemplatePath<OrderData> d = t.DataAs<OrderData>();
            t.Content(c =>
            {
                c.ForEach(d.Path(o => o.Order).Items(o => o.Lines), "line", (line, b) => b.Paragraph(x =>
                {
                    x.Value(line.Number);
                    x.Text(". ");
                    x.Value(line.Path(l => l.Description));
                }));
                c.Table(tb =>
                {
                    tb.Columns(cols => cols.Relative(1));
                    tb.ForEachRow(d.Items(o => o.Order.Lines), "row", (row, rows) => rows.Row(r => r.Cell(row.Path(l => l.Quantity))));
                });
            });
        });
        TestDefinition strings = new TestDefinition(t =>
        {
            TemplatePath order = t.Data["order"];
            t.Content(c =>
            {
                c.ForEach(order["lines"], "line", (line, b) => b.Paragraph(x =>
                {
                    x.Value(line.Number);
                    x.Text(". ");
                    x.Value(line["description"]);
                }));
                c.Table(tb =>
                {
                    tb.Columns(cols => cols.Relative(1));
                    tb.ForEachRow(order["lines"], "row", (row, rows) => rows.Row(r => r.Cell(row["quantity"])));
                });
            });
        });

        Assert.Equal(BuilderHarness.Build(strings, format).ContentHash, BuilderHarness.Build(typed, format).ContentHash);
    }

    private sealed record AddressBlock(TemplatePath<Address> Address) : ITemplateComponent
    {
        public void Compose(BlockComposer content) => content.Paragraph(x =>
        {
            x.Value(Address.Path(a => a.Street));
            x.Text(", ");
            x.Value(Address.Path(a => a.City));
        });
    }

    [Fact]
    public void TypedComponentParameter()
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c =>
            c.Component(new AddressBlock(t.DataAs<OrderData>().Path(o => o.Order.Customer.Address)))));

        GeneratedTemplate template = BuilderHarness.Build(definition, TemplateFormat.Odt);

        Assert.Contains(new ContractEntry("order.customer.address.city", ContractUsage.Value), template.Contract.Entries);
        Assert.Contains(new ContractEntry("order.customer.address.street", ContractUsage.Value), template.Contract.Entries);
    }

    [Fact]
    public void TypedConditions()
    {
        Condition? condition = null;
        Root(d => condition = d.Path(o => o.Order.IsExpress) & d.Path(o => o.Order.Total).Gt(1000) | !d.Path(o => o.Order.Customer.Name).IsEmpty);

        Assert.Equal("order.isExpress and order.total > 1000 or not (order.customer.name is empty)", condition!.ToString());
    }

    [Fact]
    public void TypedList_AndCollectionConditions()
    {
        ListStyleKey bullets = new ListStyleKey("Bullets");
        TestDefinition definition = new TestDefinition(t =>
        {
            TemplatePath<OrderData> d = t.DataAs<OrderData>();
            t.Styles(StyleSheet.Create(s => s.List(bullets, l => l.Level(0, "•", 6.Mm(), 4.Mm()))));
            t.Content(c => c.If(d.Items(o => o.Order.Lines).IsNotEmpty, b => b.List(bullets, l =>
                l.ForEach(d.Items(o => o.Order.Lines), "line", (line, items) => items.Item(line.Path(x => x.Description))))));
        });

        (_, byte[] filled) = BuilderHarness.BuildAndFill(definition, TemplateFormat.Docx, """{ "order": { "lines": [ { "description": "A" }, { "description": "B" } ] } }""");

        Assert.Equal(new[] { "A", "B" }, BuilderHarness.ParagraphTexts(TemplateFormat.Docx, filled));
    }

    [Fact]
    public void TypedPaths_ScopeErrorsStillApply()
    {
        TestDefinition definition = new TestDefinition(t =>
        {
            TemplatePath<OrderData> d = t.DataAs<OrderData>();
            t.Content(c =>
            {
                LoopItem<Line>? escaped = null;
                c.ForEach(d.Items(o => o.Order.Lines), "line", (line, b) => escaped = line);
                c.Paragraph(escaped!.Path(l => l.Description));
            });
        });

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(() => BuilderHarness.Build(definition, TemplateFormat.Docx));

        Assert.Equal("TB4001", Assert.Single(exception.Errors).Code);
    }
}
