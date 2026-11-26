// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>Findings of the final review of the stored-template check (#294).</summary>
public sealed class ReviewFixes294Tests
{
    public sealed record Data(Ord Order);

    public sealed record Ord(string Number, IReadOnlyList<Ln> Lines, Customer Customer, Dictionary<string, object> Extra, List<Dictionary<string, object>> Rows);

    public sealed record Ln(string Description, Product Product);

    public sealed record Product(string Name);

    public sealed record Customer(string Name);

    public class Base
    {
        public int Name { get; set; }
    }

    public sealed class Derived : Base
    {
        public new string Name { get; set; } = string.Empty;
    }

    public sealed record DuplicateData(Derived Item);

    public static class Env<T>
    {
        public sealed record Meta(T Value, string Label);
    }

    public sealed record NestedGenericData(Env<int>.Meta Meta);

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private static readonly DataCatalog _catalog = DataCatalog.From<Data>();

    private static TemplateCheckResult Check(GeneratedTemplate template, DataCatalog? catalog = null) =>
        TemplateInspector.Check(template.OpenRead(), new TemplateCheckOptions { Data = catalog ?? _catalog });

    private static TemplateCheckResult Check(byte[] template) =>
        TemplateInspector.Check(new MemoryStream(template), new TemplateCheckOptions { Data = _catalog });

    private static GeneratedTemplate Build(Action<TemplatePath, BlockComposer> content, TemplateFormat format = TemplateFormat.Docx) =>
        BuilderHarness.Build(new TestDefinition(t => t.Content(c => content(t.Data["order"], c))), format);

    private static IEnumerable<string?> Unknown(TemplateCheckResult result) =>
        result.Problems.Where(p => p.Code == "TB9301").Select(p => p.DataPath).Order(StringComparer.Ordinal);

    [Theory]
    [MemberData(nameof(Formats))]
    public void C1_RemovedNestedFieldOfALoopItem_IsBroken(TemplateFormat format)
    {
        GeneratedTemplate copy = Build((order, c) => c.ForEach(order["lines"], "line", (line, b) => b.Paragraph(line["product"]["removed"])), format);

        TemplateCheckResult result = Check(copy);

        Assert.Equal(TemplateCheckStatus.Broken, result.Status);
        Assert.Equal(["line.product.removed"], Unknown(result));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void C1_RemovedNestedField_ImplicitLoopName_IsBroken(TemplateFormat format)
    {
        byte[] bytes = Build((order, c) => c.ForEach(order["lines"], "line", (line, b) => b.Paragraph(line["product"]["removed"])), format).Content.ToArray();
        byte[] implicitName = BuilderHarness.EditEntry(bytes, BuilderHarness.MainPart(format), xml => xml
            .Replace("foreach line in order.lines", "foreach order.lines", StringComparison.Ordinal)
            .Replace("line.product.removed", "product.removed", StringComparison.Ordinal));

        TemplateCheckResult result = Check(implicitName);

        Assert.Equal(TemplateCheckStatus.Broken, result.Status);
        Assert.Contains(result.Problems, p => p.Code == "TB9301" && p.DataPath == "product.removed");
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void C1_ExistingNestedFieldOfALoopItem_IsOk(TemplateFormat format)
    {
        GeneratedTemplate copy = Build(
            (order, c) => c.ForEach(order["lines"], "line", (line, b) =>
            {
                b.Paragraph(line["product"]["name"]);
                b.If(line["product"]["name"], x => x.Paragraph("named"));
            }),
            format);

        Assert.Equal(TemplateCheckStatus.Ok, Check(copy).Status);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void I1_ClosedTypoInsideALoopOverAnOpenShape_IsBroken(TemplateFormat format)
    {
        GeneratedTemplate copy = Build(
            (order, c) =>
            {
                c.ForEach(order["extra"]["items"], "e", (e, b) => b.Paragraph(x => x.Value(e["name"]).Value(order["customer"]["typo"])));
                c.ForEach(order["rows"], "r", (r, b) => b.Paragraph(x => x.Value(r["anything"]).Value(order["customer"]["other"])));
            },
            format);

        TemplateCheckResult result = Check(copy);

        Assert.Equal(TemplateCheckStatus.Broken, result.Status);
        Assert.Equal(["order.customer.other", "order.customer.typo"], Unknown(result));
    }

    [Fact]
    public void I1_LoopOverAnOpenShape_WithKnownPaths_IsOk()
    {
        GeneratedTemplate copy = Build((order, c) => c.ForEach(order["extra"]["items"], "e", (e, b) =>
        {
            b.Paragraph(x => x.Value(e["name"]).Value(order["customer"]["name"]));
            b.ForEach(e["children"], "child", (child, d) => d.If(child["flag"], y => y.Paragraph(child["label"])));
        }));

        Assert.DoesNotContain(Check(copy).Problems, p => p.Severity == TemplateCheckSeverity.Error);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void I3_IndexedPaths_AreOk(TemplateFormat format)
    {
        GeneratedTemplate copy = Build(
            (order, c) =>
            {
                c.Paragraph(order["lines[1].description"]);
                c.Paragraph(order["lines[2].product.name"]);
                c.Paragraph(order["extra.items[0].name"]);
                c.Paragraph(order["extra[3]"]);
            },
            format);

        Assert.DoesNotContain(Check(copy).Problems, p => p.Severity == TemplateCheckSeverity.Error);
    }

    [Fact]
    public void I3_IndexedPathToARemovedField_IsBroken()
    {
        GeneratedTemplate copy = Build((order, c) => c.Paragraph(order["lines[2].removed"]));

        Assert.Equal(["order.lines[2].removed"], Unknown(Check(copy)));
    }

    [Fact]
    public void I4_CatalogueWithAHiddenProperty_DoesNotThrow()
    {
        GeneratedTemplate copy = BuilderHarness.Build(new TestDefinition(t => t.Content(c => c.Paragraph(t.Data["item"]["name"]))), TemplateFormat.Docx);

        TemplateCheckResult result = Check(copy, DataCatalog.From<DuplicateData>());

        Assert.DoesNotContain(result.Problems, p => p.Severity == TemplateCheckSeverity.Error);
    }

    [Fact]
    public void M1_ToJson_TypeNestedInAGenericType_DoesNotThrow()
    {
        string json = DataCatalog.From<NestedGenericData>().ToJson();

        Assert.Contains("Meta", json, StringComparison.Ordinal);
    }
}
