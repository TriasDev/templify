// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using System.Xml.Linq;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Odt;

namespace TriasDev.Templify.Builder.Tests;

public sealed class ConditionalTests
{
    private static readonly XNamespace _w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace _text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace _style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace _fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
    private static readonly XNamespace _office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private static readonly TemplatePath _order = TemplatePath.Root("order");
    private static readonly TemplatePath _a = TemplatePath.Root("a");
    private static readonly TemplatePath _b = TemplatePath.Root("b");
    private static readonly TemplatePath _c = TemplatePath.Root("c");

    // ── The emitter ─────────────────────────────────────────────────────────────

    public static TheoryData<string, Condition> Emitted => new TheoryData<string, Condition>
    {
        { "order.isPaid", _order["isPaid"] },
        { "order.status = \"paid\"", _order["status"].Eq("paid") },
        { "name = \"say \\\"hi\\\" \\\\ ok\"", TemplatePath.Root("name").Eq("say \"hi\" \\ ok") },
        { "total = 1.5", TemplatePath.Root("total").Eq(1.5) },
        { "total != 3", TemplatePath.Root("total").NotEq(3) },
        { "total > -5", TemplatePath.Root("total").Gt(-5) },
        { "total >= 100000000000000000000", TemplatePath.Root("total").Ge(1e20) },
        { "total < 0.25", TemplatePath.Root("total").Lt(0.25) },
        { "total <= 10", TemplatePath.Root("total").Le(10) },
        { "status != \"open\"", TemplatePath.Root("status").NotEq("open") },
        { "role in (\"admin\", \"owner\")", TemplatePath.Root("role").In("admin", "owner") },
        { "tags contains \"x\"", TemplatePath.Root("tags").Contains("x") },
        { "name startswith \"Ac\"", TemplatePath.Root("name").StartsWith("Ac") },
        { "name endswith \"Ltd\"", TemplatePath.Root("name").EndsWith("Ltd") },
        { "note exists", TemplatePath.Root("note").Exists },
        { "note is empty", TemplatePath.Root("note").IsEmpty },
        { "note is not empty", TemplatePath.Root("note").IsNotEmpty },
        { "[in]", TemplatePath.Root("in") },
        { "[Empty] = \"x\"", TemplatePath.Root("Empty").Eq("x") },
        { "order.in", _order["in"] },
        { "not a", !_a },
        { "not (a = \"x\")", !_a.Eq("x") },
        { "not (a and b)", !(_a & _b) },
        { "a or b and c", _a | (_b & _c) },
        { "(a or b) and c", (_a | _b) & _c },
        { "a and b and c", (_a & _b) & _c },
        { "a and (b and c)", _a & (_b & _c) },
        { "a or b or c", (_a | _b) | _c },
        { "not a and b", !_a & _b },
        { "Count > 0 and IsActive", Condition.Raw("Count > 0 and IsActive", TemplatePath.Root("Count"), TemplatePath.Root("IsActive")) },
        { "(Count > 0 and IsActive) or a", Condition.Raw("Count > 0 and IsActive") | _a },
        { "not (Count > 0)", !Condition.Raw("Count > 0") },
    };

    [Theory]
    [MemberData(nameof(Emitted))]
    public void Emitter_WritesTemplifyConditionText(string expected, Condition condition)
    {
        Assert.Equal(expected, condition.ToString());
        ConditionValidationResult validation = new ConditionEvaluator().Validate(condition.ToString());
        Assert.True(validation.IsValid, string.Join("; ", validation.Issues.Select(i => i.Message)));
    }

    [Fact]
    public void Emitter_RejectsWhatCannotBeWritten()
    {
        Assert.Throws<InvalidOperationException>(() => default(TemplatePath).Eq("x"));
        Assert.Throws<InvalidOperationException>(() => _ = (Condition)default(TemplatePath));
        Assert.Throws<ArgumentException>(() => TemplatePath.Root("1.5").Eq("x"));
        Assert.Throws<ArgumentException>(() => _a.Eq("a }} b"));
        Assert.Throws<ArgumentException>(() => _a.Eq("{{x"));
        Assert.Throws<ArgumentException>(() => _a.Eq("“quoted”"));
        Assert.Throws<ArgumentException>(() => _a.In());
        Assert.Throws<ArgumentOutOfRangeException>(() => _a.Gt(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => _a.Gt(1e30));
        Assert.Throws<ArgumentException>(() => Condition.Raw("Count >"));
        Assert.Throws<ArgumentException>(() => Condition.Raw("a }} b"));
    }

    // ── The engine evaluates what the builder writes ───────────────────────────

    private const string OrderJson = """
    {
      "order": { "status": "paid", "total": 1500, "isVip": false, "tags": ["a", "b"], "note": "",
                 "role": "owner", "name": "Acme Ltd" },
      "in": "keyword"
    }
    """;

    private static readonly (Condition Condition, bool Expected)[] _cases =
    {
        (_order["status"].Eq("paid"), true),
        (_order["status"].Eq("open"), false),
        (_order["total"].Gt(1000), true),
        (_order["total"].Le(1000), false),
        (_order["total"].Eq(1500), true),
        (!_order["isVip"], true),
        (_order["status"].In("paid", "settled"), true),
        (_order["tags"].Contains("b"), true),
        (_order["name"].StartsWith("Acme"), true),
        (_order["name"].EndsWith("GmbH"), false),
        (_order["note"].IsEmpty, true),
        (_order["note"].IsNotEmpty, false),
        (_order["status"].Eq("paid") | (_order["total"].Lt(0) & _order["isVip"]), true),
        ((_order["status"].Eq("open") | _order["total"].Gt(1000)) & !_order["isVip"], true),
        (!_order["status"].Eq("paid"), false),
        (TemplatePath.Root("in"), true),
        (Condition.Raw("order.total > 100 and order.status = \"paid\"", _order["total"], _order["status"]), true),
        (_order["isVip"], false),
    };

    [Theory]
    [MemberData(nameof(Formats))]
    public void Conditions_AreEvaluatedByTheEngineAsWritten(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c =>
        {
            for (int i = 0; i < _cases.Length; i++)
            {
                int n = i;
                c.If(_cases[n].Condition, b => b.Paragraph($"T{n}")).Else(b => b.Paragraph($"F{n}"));
            }
        }));

        (_, byte[] filled) = BuilderHarness.BuildAndFill(definition, format, OrderJson);

        Assert.Equal(_cases.Select((c, i) => (c.Expected ? "T" : "F") + i), BuilderHarness.ParagraphTexts(format, filled));
    }

    private static TestDefinition Chain() => new TestDefinition(template =>
    {
        TemplatePath status = template.Data["order"]["status"];
        template.Content(c =>
        {
            c.Paragraph("before");
            c.If(status.Eq("open"), b => b.Paragraph("open"))
                .ElseIf(status.Eq("paid"), b =>
                {
                    b.Paragraph("paid");
                    b.If(template.Data["order"]["total"].Gt(1000), inner => inner.Paragraph("large"));
                })
                .ElseIf(status.Eq("overdue"), b => b.Paragraph("overdue"))
                .Else(b => b.Paragraph("other"));
            c.Paragraph("after");
        });
    });

    [Theory]
    [InlineData(TemplateFormat.Docx, "open", 5, "before|open|after")]
    [InlineData(TemplateFormat.Docx, "paid", 5000, "before|paid|large|after")]
    [InlineData(TemplateFormat.Docx, "paid", 5, "before|paid|after")]
    [InlineData(TemplateFormat.Docx, "overdue", 5, "before|overdue|after")]
    [InlineData(TemplateFormat.Docx, "draft", 5, "before|other|after")]
    [InlineData(TemplateFormat.Odt, "open", 5, "before|open|after")]
    [InlineData(TemplateFormat.Odt, "paid", 5000, "before|paid|large|after")]
    [InlineData(TemplateFormat.Odt, "paid", 5, "before|paid|after")]
    [InlineData(TemplateFormat.Odt, "overdue", 5, "before|overdue|after")]
    [InlineData(TemplateFormat.Odt, "draft", 5, "before|other|after")]
    public void ElseIfChain_AndNesting_ChooseOneBranch(TemplateFormat format, string status, int total, string expected)
    {
        (_, byte[] filled) = BuilderHarness.BuildAndFill(Chain(), format, $$"""{ "order": { "status": "{{status}}", "total": {{total}} } }""");

        Assert.Equal(expected.Split('|'), BuilderHarness.ParagraphTexts(format, filled));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Markers_AreParagraphsOfTheirOwnInTheMarkerStyle(TemplateFormat format)
    {
        byte[] template = BuilderHarness.Build(Chain(), format).Content.ToArray();

        string[] expected =
        {
            "before", "{{#if order.status = \"open\"}}", "open", "{{#elseif order.status = \"paid\"}}", "paid",
            "{{#if order.total > 1000}}", "large", "{{/if}}", "{{#elseif order.status = \"overdue\"}}", "overdue",
            "{{#else}}", "other", "{{/if}}", "after",
        };
        Assert.Equal(expected, BuilderHarness.ParagraphTexts(format, template));

        if (format == TemplateFormat.Docx)
        {
            XElement body = BuilderHarness.ReadXml(template, "word/document.xml").Root!.Element(_w + "body")!;
            List<XElement> markers = body.Elements(_w + "p").Where(p => p.Value.StartsWith("{{#", StringComparison.Ordinal) || p.Value.StartsWith("{{/", StringComparison.Ordinal)).ToList();
            Assert.Equal(7, markers.Count);
            Assert.All(markers, m =>
            {
                Assert.Equal("TemplifyMarker", m.Element(_w + "pPr")!.Element(_w + "pStyle")!.Attribute(_w + "val")!.Value);
                Assert.Single(m.Elements(_w + "r"));
            });
            XElement style = BuilderHarness.ReadXml(template, "word/styles.xml").Root!.Elements(_w + "style")
                .Single(s => (string?)s.Attribute(_w + "styleId") == "TemplifyMarker");
            Assert.Equal("Template Marker", style.Element(_w + "name")!.Attribute(_w + "val")!.Value);
            Assert.Equal("16", style.Element(_w + "rPr")!.Element(_w + "sz")!.Attribute(_w + "val")!.Value);
        }
        else
        {
            List<XElement> markers = BuilderHarness.ReadXml(template, "content.xml").Descendants(_text + "p")
                .Where(p => (string?)p.Attribute(_text + "style-name") == "TemplifyMarker").ToList();
            Assert.Equal(7, markers.Count);
            XElement style = BuilderHarness.ReadXml(template, "styles.xml").Descendants(_style + "style")
                .Single(s => (string?)s.Attribute(_style + "name") == "TemplifyMarker");
            Assert.Equal("Template Marker", style.Attribute(_style + "display-name")!.Value);
            Assert.Equal("8pt", style.Element(_style + "text-properties")!.Attribute(_fo + "font-size")!.Value);
        }
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void MarkerStyle_CanBeRedefinedByTheStyleSheet(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(template =>
        {
            template.Styles(StyleSheet.Create(s => s.Paragraph(BuilderStyles.TemplateMarker, p => p.Size(6.Pt()).Color(Color.FromHex("FF0000")))));
            template.Content(c => c.If(template.Data["x"], b => b.Paragraph("x")));
        });

        byte[] template = BuilderHarness.Build(definition, format).Content.ToArray();

        if (format == TemplateFormat.Docx)
        {
            List<XElement> styles = BuilderHarness.ReadXml(template, "word/styles.xml").Root!.Elements(_w + "style")
                .Where(s => (string?)s.Attribute(_w + "styleId") == "TemplifyMarker").ToList();
            Assert.Equal("12", Assert.Single(styles).Element(_w + "rPr")!.Element(_w + "sz")!.Attribute(_w + "val")!.Value);
        }
        else
        {
            List<XElement> styles = BuilderHarness.ReadXml(template, "styles.xml").Descendants(_style + "style")
                .Where(s => (string?)s.Attribute(_style + "name") == "TemplifyMarker").ToList();
            Assert.Equal("6pt", Assert.Single(styles).Element(_style + "text-properties")!.Attribute(_fo + "font-size")!.Value);
        }
    }

    [Fact]
    public void WithoutConditionals_NoMarkerStyleIsWritten()
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c => c.Paragraph("x")));

        XDocument styles = BuilderHarness.ReadXml(BuilderHarness.Build(definition, TemplateFormat.Docx).Content.ToArray(), "word/styles.xml");

        Assert.DoesNotContain(styles.Root!.Elements(_w + "style"), s => (string?)s.Attribute(_w + "styleId") == "TemplifyMarker");
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void PageBreak_InsideAFalseConditional_IsRemoved(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c =>
        {
            c.Paragraph("one");
            c.If(template.Data["breakPage"], b => b.PageBreak());
            c.Paragraph("two");
        }));

        (_, byte[] kept) = BuilderHarness.BuildAndFill(definition, format, """{ "breakPage": true }""");
        (_, byte[] removed) = BuilderHarness.BuildAndFill(definition, format, """{ "breakPage": false }""");

        Assert.Equal(new[] { "one", string.Empty, "two" }, BuilderHarness.ParagraphTexts(format, kept));
        Assert.Equal(new[] { "one", "two" }, BuilderHarness.ParagraphTexts(format, removed));
    }

    // ── Switch ──────────────────────────────────────────────────────────────────

    private static TestDefinition StatusSwitch() => new TestDefinition(template => template.Content(c =>
        c.Switch(template.Data["status"], s =>
        {
            s.Case("paid", b => b.Paragraph("Paid"));
            s.Case(new[] { "settled", "closed" }, b => b.Paragraph("Settled"));
            s.Default(b => b.Paragraph("Open"));
        })));

    [Theory]
    [InlineData(TemplateFormat.Docx, "paid", "Paid")]
    [InlineData(TemplateFormat.Docx, "closed", "Settled")]
    [InlineData(TemplateFormat.Docx, "settled", "Settled")]
    [InlineData(TemplateFormat.Docx, "unknown-later-value", "Open")]
    [InlineData(TemplateFormat.Odt, "paid", "Paid")]
    [InlineData(TemplateFormat.Odt, "closed", "Settled")]
    [InlineData(TemplateFormat.Odt, "unknown-later-value", "Open")]
    public void Switch_ChoosesTheCaseOrTheDefault(TemplateFormat format, string status, string expected)
    {
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(StatusSwitch(), format, $$"""{ "status": "{{status}}" }""");

        Assert.Equal(new[] { expected }, BuilderHarness.ParagraphTexts(format, filled));
        Assert.Equal(
            new[] { "{{#if status = \"paid\"}}", "Paid", "{{#elseif status in (\"settled\", \"closed\")}}", "Settled", "{{#else}}", "Open", "{{/if}}" },
            BuilderHarness.ParagraphTexts(format, template.Content.ToArray()));
    }

    [Fact]
    public void Switch_WithoutCases_WritesTheDefaultUnconditionally()
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c =>
            c.Switch(template.Data["status"], s => s.Default(b => b.Paragraph("Always")))));

        Assert.Equal(new[] { "Always" }, BuilderHarness.ParagraphTexts(TemplateFormat.Docx, BuilderHarness.Build(definition, TemplateFormat.Docx).Content.ToArray()));
    }

    [Fact]
    public void Switch_WithoutDefault_OrWithDuplicateCases_IsAnError()
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c =>
        {
            c.Switch(template.Data["status"], s => s.Case("paid", b => b.Paragraph("x")));
            c.Switch(template.Data["kind"], s =>
            {
                s.Case("a", b => b.Paragraph("a"));
                s.Case(new[] { "b", "a" }, b => b.Paragraph("b"));
                s.Default(b => b.Paragraph("d"));
            });
        }));

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(
            () => TemplateBuilder.Build(definition, BuilderHarness.Options(TemplateFormat.Odt)));

        Assert.Equal(new[] { "TB3001", "TB3002" }, exception.Errors.Select(e => e.Code));
        Assert.Contains("'status'", exception.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Switch_DefaultTwice_Throws()
    {
        TestDefinition definition = new TestDefinition(template => template.Content(c =>
            c.Switch(template.Data["status"], s =>
            {
                s.Default(b => b.Paragraph("a"));
                s.Default(b => b.Paragraph("b"));
            })));

        Assert.Throws<InvalidOperationException>(() => TemplateBuilder.Build(definition, BuilderHarness.Options(TemplateFormat.Docx)));
    }

    [Fact]
    public void IfChain_MisuseThrows()
    {
        IfChain? kept = null;
        TestDefinition afterElse = new TestDefinition(template => template.Content(c =>
        {
            IfChain chain = c.If(template.Data["a"], b => b.Paragraph("a"));
            chain.Else(b => b.Paragraph("b"));
            chain.ElseIf(template.Data["c"], b => b.Paragraph("c"));
        }));
        TestDefinition outside = new TestDefinition(template => template.Content(c => kept = c.If(template.Data["a"], b => b.Paragraph("a"))));

        Assert.Throws<InvalidOperationException>(() => TemplateBuilder.Build(afterElse, BuilderHarness.Options(TemplateFormat.Docx)));
        TemplateBuilder.Build(outside, BuilderHarness.Options(TemplateFormat.Docx));
        Assert.Throws<InvalidOperationException>(() => kept!.Else(b => b.Paragraph("late")));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    [Trait("Category", "LibreOffice")]
    public void LibreOffice_RendersTheChosenBranchesOnly(TemplateFormat format)
    {
        LibreOfficeRunner.RequireExecutable();
        (GeneratedTemplate template, byte[] filled) = BuilderHarness.BuildAndFill(Chain(), format, """{ "order": { "status": "paid", "total": 5000 } }""");
        string extension = template.FileExtension.TrimStart('.');

        string text = Encoding.UTF8.GetString(LibreOfficeRunner.Convert(filled, extension, "txt:Text (encoded):UTF8", "txt")).TrimStart('﻿');
        Assert.Equal(new[] { "before", "paid", "large", "after" }, text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n'));

        string templateText = Encoding.UTF8.GetString(LibreOfficeRunner.Convert(template.Content.ToArray(), extension, "txt:Text (encoded):UTF8", "txt"));
        Assert.Contains("{{#elseif order.status = \"overdue\"}}", templateText, StringComparison.Ordinal);
    }
}
