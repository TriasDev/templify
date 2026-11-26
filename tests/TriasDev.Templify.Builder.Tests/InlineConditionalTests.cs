// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using System.Xml.Linq;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Odt;

namespace TriasDev.Templify.Builder.Tests;

public sealed class InlineConditionalTests
{
    private static readonly XNamespace _w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly CharacterStyleKey _strong = new CharacterStyleKey("Strong", "Strong Text");

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private static TestDefinition Letter() => new TestDefinition(t =>
    {
        TemplatePath a = t.Data["address"];
        TemplatePath order = t.Data["order"];
        t.Styles(StyleSheet.Create(s => s.Character(_strong, c => c.Bold())));
        t.Content(c =>
        {
            // The separator sits inside the guard of the piece it precedes.
            c.Paragraph(x =>
            {
                x.Value(a["street"]);
                x.If(a["postalCode"], y => y.Text(", ").Value(a["postalCode"]));
                x.If(a["city"], y => y.Text(" ").Value(a["city"]));
            });
            c.Paragraph(x =>
            {
                x.Text("Status: ");
                x.If(order["status"].Eq("paid"), y => y.Text(_strong, "paid"))
                 .ElseIf(order["status"].Eq("overdue"), y => y.Text("overdue").If(order["reminded"], z => z.Text(" (reminded)")))
                 .Else(y => y.Text("open"));
            });
            c.Paragraph(x =>
            {
                x.Text("Paid and positive: ");
                x.Expression(order["isPaid"] & order["total"].Gt(0), ValueFormat.YesNo);
                x.Text(" ");
                x.Expression(order["isPaid"], ValueFormat.Checkbox);
            });
            c.Paragraph(x => x.If(order["note"], y => y.Value(order["note"])));
            c.Paragraph("end");
        });
    });

    public static TheoryData<TemplateFormat, string, string[]> Cases => new TheoryData<TemplateFormat, string, string[]>
    {
        {
            TemplateFormat.Docx,
            """{ "address": { "street": "Main St 1", "postalCode": "80331", "city": "Munich" }, "order": { "status": "paid", "reminded": false, "isPaid": true, "total": 10, "note": "fragile" } }""",
            ["Main St 1, 80331 Munich", "Status: paid", "Paid and positive: Yes ☑", "fragile", "end"]
        },
        {
            TemplateFormat.Odt,
            """{ "address": { "street": "Main St 1", "postalCode": "", "city": "Munich" }, "order": { "status": "overdue", "reminded": true, "isPaid": false, "total": 10, "note": "" } }""",
            ["Main St 1 Munich", "Status: overdue (reminded)", "Paid and positive: No ☐", "", "end"]
        },
        {
            TemplateFormat.Docx,
            """{ "address": { "street": "Main St 1", "postalCode": "", "city": "" }, "order": { "status": "draft", "reminded": false, "isPaid": "x", "total": 0, "note": "" } }""",
            ["Main St 1", "Status: open", "Paid and positive: No ☐", "", "end"]
        },
        {
            TemplateFormat.Odt,
            """{ "address": { "street": "Main St 1", "postalCode": "80331", "city": "" }, "order": { "status": "overdue", "reminded": false, "isPaid": true, "total": 5, "note": "x" } }""",
            ["Main St 1, 80331", "Status: overdue", "Paid and positive: Yes ☑", "x", "end"]
        },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void InlineConditionalsAndExpressions_AreResolvedInsideTheParagraph(TemplateFormat format, string json, string[] expected)
    {
        (_, byte[] filled) = BuilderHarness.BuildAndFill(Letter(), format, json);

        Assert.Equal(expected, BuilderHarness.ParagraphTexts(format, filled));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Template_HoldsTheMarkersInsideTheParagraphs(TemplateFormat format)
    {
        List<string> texts = BuilderHarness.ParagraphTexts(format, BuilderHarness.Build(Letter(), format).Content.ToArray());

        Assert.Equal("{{address.street}}{{#if address.postalCode}}, {{address.postalCode}}{{/if}}{{#if address.city}} {{address.city}}{{/if}}", texts[0]);
        Assert.Equal("Status: {{#if order.status = \"paid\"}}paid{{#elseif order.status = \"overdue\"}}overdue{{#if order.reminded}} (reminded){{/if}}{{#else}}open{{/if}}", texts[1]);
        Assert.Equal("Paid and positive: {{(order.isPaid and order.total > 0):yesno}} {{(order.isPaid):checkbox}}", texts[2]);
    }

    [Fact]
    public void StyledTextInABranch_KeepsItsStyle()
    {
        (_, byte[] filled) = BuilderHarness.BuildAndFill(Letter(), TemplateFormat.Docx,
            """{ "address": { "street": "S", "postalCode": "", "city": "" }, "order": { "status": "paid", "reminded": false, "isPaid": true, "total": 1, "note": "" } }""");

        XElement paid = BuilderHarness.ReadXml(filled, "word/document.xml").Descendants(_w + "r").Single(r => r.Value == "paid");
        Assert.Equal("Strong", paid.Element(_w + "rPr")!.Element(_w + "rStyle")!.Attribute(_w + "val")!.Value);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Contract_IncludesConditionAndExpressionPaths(TemplateFormat format)
    {
        Assert.Equal(
            new[]
            {
                "address.city (Value)", "address.city (Condition)", "address.postalCode (Value)", "address.postalCode (Condition)",
                "address.street (Value)", "order.isPaid (Condition)", "order.note (Value)", "order.note (Condition)",
                "order.reminded (Condition)", "order.status (Condition)", "order.total (Condition)",
            },
            BuilderHarness.Build(Letter(), format).Contract.Entries.Select(e => $"{e.Path} ({e.Usage})"));
    }

    [Fact]
    public void Errors_AreReported()
    {
        Assert.Throws<ArgumentException>(() => BuilderHarness.Build(
            new TestDefinition(t => t.Content(c => c.Paragraph(x => x.If(t.Data["a"], y => y.Text("tab\there"))))), TemplateFormat.Docx));

        TemplateBuildException braces = Assert.Throws<TemplateBuildException>(() => BuilderHarness.Build(
            new TestDefinition(t => t.Content(c => c.Paragraph(x => x.If(t.Data["a"], y => y.Text("{{oops"))))), TemplateFormat.Docx));
        Assert.Equal("TB1002", Assert.Single(braces.Errors).Code);

        InlineIfChain? kept = null;
        BuilderHarness.Build(new TestDefinition(t => t.Content(c => c.Paragraph(x => kept = x.If(t.Data["a"], y => y.Text("a"))))), TemplateFormat.Docx);
        Assert.Throws<InvalidOperationException>(() => kept!.Else(y => y.Text("late")));

        Assert.Throws<InvalidOperationException>(() => BuilderHarness.Build(new TestDefinition(t => t.Content(c => c.Paragraph(x =>
        {
            InlineIfChain chain = x.If(t.Data["a"], y => y.Text("a"));
            chain.Else(y => y.Text("b"));
            chain.ElseIf(t.Data["c"], y => y.Text("c"));
        }))), TemplateFormat.Docx));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    [Trait("Category", "LibreOffice")]
    public void LibreOffice_ShowsTheResolvedText(TemplateFormat format)
    {
        LibreOfficeRunner.RequireExecutable();
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Letter(), format,
            """{ "address": { "street": "Main St 1", "postalCode": "80331", "city": "Munich" }, "order": { "status": "overdue", "reminded": true, "isPaid": true, "total": 10, "note": "" } }""");

        string text = Encoding.UTF8.GetString(LibreOfficeRunner.Convert(filled, template.FileExtension.TrimStart('.'), "txt:Text (encoded):UTF8", "txt"));

        Assert.Contains("Main St 1, 80331 Munich", text, StringComparison.Ordinal);
        Assert.Contains("Status: overdue (reminded)", text, StringComparison.Ordinal);
        Assert.Contains("Paid and positive: Yes ☑", text, StringComparison.Ordinal);
    }
}
