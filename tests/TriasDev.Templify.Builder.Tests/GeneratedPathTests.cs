// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

internal sealed record GeneratedData(GeneratedOrder Order);

internal sealed record GeneratedOrder([property: JsonPropertyName("no")] string Number, IReadOnlyList<GeneratedLine> Lines, GeneratedCustomer CustomerInfo, bool IsExpress);

internal sealed record GeneratedLine(decimal UnitPrice, IReadOnlyList<GeneratedLine> Parts);

internal sealed record GeneratedCustomer(string FirstName);

/// <summary>The generated typed paths (this project runs the generator): the same paths as the lambda API.</summary>
public sealed class GeneratedPathTests
{
    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    [Fact]
    public void GeneratedPaths_AreTheLambdaPaths()
    {
        List<(string Generated, string Lambda)> pairs = new List<(string, string)>();
        BuilderHarness.Build(new TestDefinition(t =>
        {
            TemplatePath<GeneratedData> data = t.DataAs<GeneratedData>();
            TemplatePath<GeneratedData> snake = t.DataAs<GeneratedData>(JsonNamingPolicy.SnakeCaseLower);
            pairs.Add((data.Order.Number.ToString(), data.Path(d => d.Order.Number).ToString()));
            pairs.Add((data.Order.CustomerInfo.FirstName.ToString(), data.Path(d => d.Order.CustomerInfo.FirstName).ToString()));
            pairs.Add((snake.Order.CustomerInfo.FirstName.ToString(), snake.Path(d => d.Order.CustomerInfo.FirstName).ToString()));
            pairs.Add((((TemplatePath)data.Order.Lines).ToString(), ((TemplatePath)data.Items(d => d.Order.Lines)).ToString()));
            t.Content(c => c.Paragraph("x"));
        }), TemplateFormat.Docx);

        Assert.Equal(
            new[] { "order.no", "order.customerInfo.firstName", "order.customer_info.first_name", "order.lines" },
            pairs.Select(p => p.Generated));
        Assert.All(pairs, p => Assert.Equal(p.Lambda, p.Generated));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void GeneratedLoops_BuildTheLambdaTemplate(TemplateFormat format)
    {
        TestDefinition generated = new TestDefinition(t =>
        {
            TemplatePath<GeneratedOrder> order = t.DataAs<GeneratedData>().Order;
            t.Content(c =>
            {
                c.If(order.IsExpress, b => b.Paragraph(order.Number));
                c.ForEach(order.Lines, "line", (line, b) =>
                {
                    b.Paragraph(line.UnitPrice);
                    b.ForEach(line.Parts, "part", (part, inner) => inner.Paragraph(part.UnitPrice));
                });
            });
        });
        TestDefinition lambdas = new TestDefinition(t =>
        {
            TemplatePath<GeneratedOrder> order = t.DataAs<GeneratedData>().Path(d => d.Order);
            t.Content(c =>
            {
                c.If(order.Path(o => o.IsExpress), b => b.Paragraph(order.Path(o => o.Number)));
                c.ForEach(order.Items(o => o.Lines), "line", (line, b) =>
                {
                    b.Paragraph(line.Path(l => l.UnitPrice));
                    b.ForEach(line.Items(l => l.Parts), "part", (part, inner) => inner.Paragraph(part.Path(p => p.UnitPrice)));
                });
            });
        });

        GeneratedTemplate template = BuilderHarness.Build(generated, format);
        BuilderHarness.AssertInvariants(generated, template);
        Assert.Equal(BuilderHarness.Build(lambdas, format).ContentHash, template.ContentHash);
    }
}
