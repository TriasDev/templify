// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Json;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>Indexed path segments, <c>items[0]</c> (#290).</summary>
public sealed class IndexedPathTests
{
    private const string Data = """
        {
          "order": {
            "items": [ { "name": "first", "ok": false, "parts": [ { "name": "p0" } ] }, { "name": "second", "ok": true, "parts": [ { "name": "q0" }, { "name": "q1" } ] } ],
            "lines": [ { "parts": [ { "name": "a" }, { "name": "b" } ] } ]
          }
        }
        """;

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private sealed record Part(string Name);

    private sealed record Item(string Name, bool Ok, List<Part> Parts);

    private sealed record Order(List<Item> Items, Item[] Array);

    private sealed record Root(Order Order);

    [Theory]
    [MemberData(nameof(Formats))]
    public void StringPaths_ReadTheItemAtTheIndex(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(t =>
        {
            TemplatePath order = t.Data["order"];
            t.Content(c =>
            {
                c.Paragraph(x =>
                {
                    x.Text("1: ");
                    x.Value(order["items[1].name"]);
                    x.Text(", 0: ");
                    x.Value(order["items[0]"]["name"]);
                });
                c.If(order["items[1].ok"], b => b.Paragraph("second ok"));
                c.ForEach(order["lines"], "line", (line, b) => b.Paragraph(x => x.Value(line["parts[1].name"])));
            });
        });

        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(definition, format, Data);

        Assert.Equal(new[] { "1: second, 0: first", "second ok", "b" }, BuilderHarness.ParagraphTexts(format, filled));
        Assert.Contains(new ContractEntry("order.items[1].name", ContractUsage.Value), template.Contract.Entries);
        Assert.Contains(new ContractEntry("order.lines[].parts[1].name", ContractUsage.Value), template.Contract.Entries);
    }

    [Fact]
    public void TypedPaths_MapAConstantIndex()
    {
        List<string> paths = new List<string>();
        BuilderHarness.Build(new TestDefinition(t =>
        {
            TemplatePath<Root> root = t.DataAs<Root>();
            paths.Add(root.Path(r => r.Order.Items[1].Name).ToString());
            paths.Add(root.Path(r => r.Order.Array[2].Parts[0].Name).ToString());
            t.Content(c => c.Paragraph("x"));
        }), TemplateFormat.Docx);

        Assert.Equal(new[] { "order.items[1].name", "order.array[2].parts[0].name" }, paths);
    }

    [Fact]
    public void TypedPaths_RejectAComputedIndex()
    {
        int index = 1;
        TemplatePath<Root> root = default;
        ArgumentException exception = Assert.Throws<ArgumentException>(() => root.Path(r => r.Order.Items[index].Name));
        Assert.Contains("constant", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("items[1000]")]
    [InlineData("items[x]")]
    [InlineData("items[0][1]")]
    [InlineData("items[-1]")]
    [InlineData("[0]")]
    public void InvalidIndexes_AreRejected(string path)
    {
        Assert.Throws<ArgumentException>(() => TemplatePath.Root(path));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Json_TextAndConditionsUseIndexes(TemplateFormat format)
    {
        ITemplateDefinition imported = TemplateJson.Import("""
            {
              "templifyDefinition": 1,
              "identity": { "id": "indexed", "version": 1 },
              "content": [
                "{{order.items[1].name}} / {{order.items[0].parts[0].name}}",
                { "if": "order.items[1].ok and not order.items[0].ok", "then": [ "conditions read indexes" ] }
              ]
            }
            """);

        (_, byte[] filled) = BuilderHarness.BuildAndFill(imported, format, Data);

        Assert.Equal(new[] { "second / p0", "conditions read indexes" }, BuilderHarness.ParagraphTexts(format, filled));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Witness_SatisfiesLoopsAndIndexesOnOneCollection(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(t =>
        {
            TemplatePath order = t.Data["order"];
            t.Content(c =>
            {
                c.ForEach(order["items"], "item", (item, b) => b.Paragraph(x => x.Value(item["name"])));
                c.Paragraph(x => x.Value(order["items[2].parts[1].name"]));
            });
        });
        GeneratedTemplate built = BuilderHarness.Build(definition, format);

        using (Stream stream = built.OpenRead())
        {
            ValidationResult validation = new TemplateProcessor().ValidateTemplate(stream, built.Contract.CreateWitness());
            Assert.Empty(validation.MissingVariables);
        }

        BuilderHarness.BuildAndFill(definition, format, built.Contract.CreateWitness());
    }

    [Fact]
    public void LoopOverAnIndexedPath_IsRejected()
    {
        // Templify's loop markers take a path of letters, digits, '_' and '.': {{#foreach l in groups[1].lines}} would stay text.
        Assert.Throws<ArgumentException>(() => BuilderHarness.Build(new TestDefinition(t => t.Content(c =>
            c.ForEach(t.Data["groups[1].lines"], "l", (l, b) => b.Paragraph(l["name"])))), TemplateFormat.Docx));
        Assert.Throws<ArgumentException>(() => BuilderHarness.Build(new TestDefinition(t => t.Content(c =>
            c.ForEach(t.DataAs<Root>().Items(r => r.Order.Items[0].Parts), "p", (p, b) => b.Paragraph(p.Path(x => x.Name))))), TemplateFormat.Docx));
        Assert.Throws<ArgumentException>(() => BuilderHarness.Build(new TestDefinition(t => t.Content(c => c.Table(tb =>
        {
            tb.Columns(cols => cols.Relative(1));
            tb.ForEachRow(t.Data["matrix[1]"], "cell", (cell, rows) => rows.Row(r => r.Cell(cell.Self)));
        }))), TemplateFormat.Docx));

        ITemplateDefinition imported = TemplateJson.Import("""
            { "templifyDefinition": 1, "identity": { "id": "c", "version": 1 }, "content": [ { "forEach": "order.items[0].parts", "as": "p", "body": [ "{{p.name}}" ] } ] }
            """);
        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => BuilderHarness.Build(imported, TemplateFormat.Docx)).Errors);
        Assert.Equal("TB9201", error.Code);
        Assert.Equal("/content/0", error.Location);
    }

    [Fact]
    public void NonAsciiDigits_AreNotAnIndex()
    {
        Assert.Throws<ArgumentException>(() => TemplatePath.Root("items[\u0663].name"));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void IndexInsideNestedLoops(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c =>
            c.ForEach(t.Data["order.groups"], "g", (g, outer) =>
                outer.ForEach(g["lines"], "line", (line, inner) => inner.Paragraph(x =>
                {
                    x.Value(line["parts[1].name"]);
                    x.Text("/");
                    x.Value(g["lines[0].parts[0].name"]);
                })))));

        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(definition, format, """
            { "order": { "groups": [ { "lines": [ { "parts": [ { "name": "a0" }, { "name": "a1" } ] }, { "parts": [ { "name": "b0" }, { "name": "b1" } ] } ] } ] } }
            """);

        Assert.Equal(new[] { "a1/a0", "b1/a0" }, BuilderHarness.ParagraphTexts(format, filled));
        Assert.Contains(new ContractEntry("order.groups[].lines[].parts[1].name", ContractUsage.Value), template.Contract.Entries);
        Assert.Contains(new ContractEntry("order.groups[].lines[0].parts[0].name", ContractUsage.Value), template.Contract.Entries);
        BuilderHarness.BuildAndFill(definition, format, template.Contract.CreateWitness());
    }

    [Fact]
    public void Catalogue_AcceptsIndexedPaths()
    {
        TemplateJsonImportOptions options = new TemplateJsonImportOptions { Data = DataCatalog.From<Root>() };

        TemplateJson.Import("""
            { "templifyDefinition": 1, "identity": { "id": "c", "version": 1 }, "content": [ "{{order.items[0].name}}", "{{order.array[1].parts[0].name}}" ] }
            """, options);
        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(() => TemplateJson.Import("""
            { "templifyDefinition": 1, "identity": { "id": "c", "version": 1 }, "content": [ "{{order.items[0].nmae}}" ] }
            """, options));
        Assert.Equal("TB9203", Assert.Single(exception.Errors).Code);
    }
}
