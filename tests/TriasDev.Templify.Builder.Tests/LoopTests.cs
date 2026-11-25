// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Odt;

namespace TriasDev.Templify.Builder.Tests;

public sealed class LoopTests
{
    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private const string OrderJson = """
    {
      "order": {
        "number": "A-17",
        "lines": [
          { "description": "Pen", "amount": 2.5, "tags": [ { "name": "office" }, { "name": "cheap" } ] },
          { "description": "Desk", "amount": 300, "tags": [ { "name": "furniture" } ] }
        ]
      }
    }
    """;

    private static TestDefinition Order() => new TestDefinition(template =>
    {
        TemplatePath order = template.Data["order"];
        template.Content(c =>
        {
            c.Paragraph(x =>
            {
                x.Text("Order ");
                x.Value(order["number"]);
            });
            c.ForEach(order["lines"], "line", (line, b) =>
            {
                b.If(!line.IsFirst, r => r.Paragraph("---"));
                b.Paragraph(x =>
                {
                    x.Value(line.Number);
                    x.Text(". ");
                    x.Value(line["description"]);
                    x.Text(": ");
                    x.Value(line["amount"], ValueFormat.Number("F2"));
                });
                b.ForEach(line["tags"], "tag", (tag, tb) => tb.Paragraph(x =>
                {
                    x.Text("  #");
                    x.Value(tag["name"]);
                    x.Text(" of ");
                    x.Value(line["description"]);
                    x.Text(" in ");
                    x.Value(order["number"]);
                    x.Text(" (");
                    x.Value(tag.Number);
                    x.Text("/");
                    x.Value(tag.Count);
                    x.Text(")");
                }));
                b.If(line.IsLast, r => r.Paragraph("end of lines"));
            });
        });
    });

    private static readonly string[] _expectedOrder =
    {
        "Order A-17",
        "1. Pen: 2.50",
        "  #office of Pen in A-17 (1/2)",
        "  #cheap of Pen in A-17 (2/2)",
        "---",
        "2. Desk: 300.00",
        "  #furniture of Desk in A-17 (1/1)",
        "end of lines",
    };

    [Theory]
    [MemberData(nameof(Formats))]
    public void NestedLoops_WithOuterAccessAndMetadata_AreFilledByTheEngine(TemplateFormat format)
    {
        (_, byte[] filled) = BuilderHarness.BuildAndFill(Order(), format, OrderJson);

        Assert.Equal(_expectedOrder, BuilderHarness.ParagraphTexts(format, filled));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Loops_AreWrittenAsNamedForeachMarkers(TemplateFormat format)
    {
        List<string> texts = BuilderHarness.ParagraphTexts(format, BuilderHarness.Build(Order(), format).Content.ToArray());

        Assert.Equal(
            new[]
            {
                "Order {{order.number}}",
                "{{#foreach line in order.lines}}",
                "{{#if not @first}}", "---", "{{/if}}",
                "{{@number}}. {{line.description}}: {{line.amount:number:F2}}",
                "{{#foreach tag in line.tags}}",
                "  #{{tag.name}} of {{line.description}} in {{order.number}} ({{@number}}/{{@count}})",
                "{{/foreach}}",
                "{{#if @last}}", "end of lines", "{{/if}}",
                "{{/foreach}}",
            },
            texts);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void LoopInsideConditional_AndConditionalOnItem(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c =>
            c.If(template.Data["showLines"], b => b.ForEach(template.Data["lines"], "line", (line, lb) =>
                lb.If(line["amount"].Gt(10), big => big.Paragraph(line["name"]))
                    .Else(small => small.Paragraph(x =>
                    {
                        x.Text("small: ");
                        x.Value(line["name"]);
                    }))))));
        string json = """{ "showLines": true, "lines": [ { "name": "a", "amount": 5 }, { "name": "b", "amount": 50 } ] }""";

        (_, byte[] filled) = BuilderHarness.BuildAndFill(definition, format, json);

        Assert.Equal(new[] { "small: a", "b" }, BuilderHarness.ParagraphTexts(format, filled));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void LoopOverScalars_UsesTheItemItself(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c =>
            c.ForEach(template.Data["tags"], "tag", (tag, b) => b.Paragraph(x =>
            {
                x.Text("- ");
                x.Value(tag.Self, ValueFormat.Uppercase);
            }))));

        (_, byte[] filled) = BuilderHarness.BuildAndFill(definition, format, """{ "tags": [ "red", "green" ] }""");

        Assert.Equal(new[] { "- RED", "- GREEN" }, BuilderHarness.ParagraphTexts(format, filled));
    }

    [Theory]
    [InlineData("in")]
    [InlineData("IN")]
    [InlineData("this")]
    [InlineData("and")]
    [InlineData("empty")]
    [InlineData("1x")]
    [InlineData("a-b")]
    [InlineData("a.b")]
    [InlineData("@index")]
    [InlineData("")]
    public void ItemName_Invalid_ThrowsWhereWritten(string itemName)
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c =>
            c.ForEach(template.Data["items"], itemName, (_, b) => b.Paragraph("x"))));

        Assert.Throws<ArgumentException>(() => TemplateBuilder.Build(definition, BuilderHarness.Options(TemplateFormat.Docx)));
    }

    [Fact]
    public void ItemName_UsedByAnEnclosingLoop_Throws()
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c =>
            c.ForEach(template.Data["groups"], "item", (group, b) =>
                b.ForEach(group["items"], "item", (_, ib) => ib.Paragraph("x")))));

        ArgumentException exception = Assert.Throws<ArgumentException>(() => TemplateBuilder.Build(definition, BuilderHarness.Options(TemplateFormat.Odt)));
        Assert.Contains("enclosing loop", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Collection_RootOrMetadata_Throws()
    {
        TestDefinition fromRoot = new TestDefinition(template => template.Content(c => c.ForEach(template.Data, "x", (_, b) => b.Paragraph("x"))));
        TestDefinition fromMetadata = new TestDefinition(template => template.Content(c =>
            c.ForEach(template.Data["items"], "item", (item, b) => b.ForEach(item.Index, "x", (_, ib) => ib.Paragraph("x")))));

        Assert.Throws<ArgumentException>(() => TemplateBuilder.Build(fromRoot, BuilderHarness.Options(TemplateFormat.Docx)));
        Assert.Throws<ArgumentException>(() => TemplateBuilder.Build(fromMetadata, BuilderHarness.Options(TemplateFormat.Docx)));
    }

    [Fact]
    public void Metadata_HasNoProperties()
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c =>
            c.ForEach(template.Data["items"], "item", (item, b) => b.Paragraph(item.Index["x"]))));

        Assert.Throws<InvalidOperationException>(() => TemplateBuilder.Build(definition, BuilderHarness.Options(TemplateFormat.Docx)));
    }

    [Fact]
    public void ScopeErrors_AreReportedTogether()
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c =>
        {
            LoopItem? escaped = null;
            c.ForEach(template.Data["lines"], "line", (line, b) =>
            {
                escaped = line;
                b.ForEach(line["tags"], "tag", (tag, tb) =>
                {
                    tb.Paragraph(x => x.Value(line.Number));          // outer metadata in the inner loop
                    tb.If(line.IsFirst, f => f.Paragraph("first"));   // the same, in a condition
                    tb.Paragraph(tag["name"]);                        // fine
                });
            });
            c.Paragraph(escaped!["description"]);                     // the variable outside its loop
            c.ForEach(template.Data["orders"], "order", (order, b) =>
                b.Paragraph(template.Data["order"]["number"]));       // a root path hidden by the variable
        }));

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(
            () => TemplateBuilder.Build(definition, BuilderHarness.Options(TemplateFormat.Docx)));

        Assert.Equal(new[] { "TB4002", "TB4002", "TB4001", "TB4003" }, exception.Errors.Select(e => e.Code));
        Assert.Contains("innermost loop", exception.Errors[0].Message, StringComparison.Ordinal);
        Assert.Contains("'line.description'", exception.Errors[2].Message, StringComparison.Ordinal);
    }

    // ── Components ──────────────────────────────────────────────────────────────

    private sealed class AddressBlock(TemplatePath address) : ITemplateComponent
    {
        public void Compose(BlockComposer content)
        {
            content.Paragraph(x =>
            {
                x.Value(address["street"]);
                x.Text(", ");
                x.Value(address["city"]);
            });
            content.If(address["country"].NotEq("DE"), b => b.Paragraph(address["country"]));
        }
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Component_IsUsedAtTheRootAndInsideALoop(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c =>
        {
            c.Paragraph("Customer");
            c.Component(new AddressBlock(template.Data["customer"]["address"]));
            c.ForEach(template.Data["branches"], "branch", (branch, b) =>
            {
                b.Paragraph(branch["name"]);
                b.Component(new AddressBlock(branch["address"]));
            });
        }));
        string json = """
        {
          "customer": { "address": { "street": "Main St 1", "city": "Berlin", "country": "DE" } },
          "branches": [
            { "name": "North", "address": { "street": "Harbour 2", "city": "Hamburg", "country": "DE" } },
            { "name": "Abroad", "address": { "street": "Rue 3", "city": "Paris", "country": "FR" } }
          ]
        }
        """;

        (_, byte[] filled) = BuilderHarness.BuildAndFill(definition, format, json);

        Assert.Equal(
            new[] { "Customer", "Main St 1, Berlin", "North", "Harbour 2, Hamburg", "Abroad", "Rue 3, Paris", "FR" },
            BuilderHarness.ParagraphTexts(format, filled));
    }

    [Fact]
    public void Component_KeepsTheLoopScopeOfItsCaller()
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c =>
        {
            LoopItem? escaped = null;
            c.ForEach(template.Data["branches"], "branch", (branch, b) => escaped = branch);
            c.Component(new AddressBlock(escaped!["address"]));
        }));

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(
            () => TemplateBuilder.Build(definition, BuilderHarness.Options(TemplateFormat.Odt)));

        Assert.All(exception.Errors, e => Assert.Equal("TB4001", e.Code));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    [Trait("Category", "LibreOffice")]
    public void LibreOffice_RendersNestedLoops(TemplateFormat format)
    {
        LibreOfficeRunner.RequireExecutable();
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Order(), format, OrderJson);

        string text = Encoding.UTF8.GetString(LibreOfficeRunner.Convert(filled, template.FileExtension.TrimStart('.'), "txt:Text (encoded):UTF8", "txt"));

        Assert.Equal(_expectedOrder, text.TrimStart('﻿').Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n'));
    }
}
