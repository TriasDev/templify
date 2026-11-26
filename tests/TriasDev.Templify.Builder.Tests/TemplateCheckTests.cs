// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Json;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>Checking stored templates against the current data catalogue (#294).</summary>
public sealed class TemplateCheckTests
{
    public sealed record InvoiceData(Order Order);

    public sealed record Order(string Number, bool IsPaid, IReadOnlyList<Line> Lines, string DueDate, string Reference, Dictionary<string, object> Extra);

    public sealed record Line(string Description, decimal Amount);

    /// <summary>The data after a breaking change: <c>number</c> renamed to <c>invoiceNumber</c>.</summary>
    public sealed record RenamedData(RenamedOrder Order);

    public sealed record RenamedOrder(string InvoiceNumber, bool IsPaid, IReadOnlyList<Line> Lines, string DueDate);

    /// <summary>The component as an older build knew it.</summary>
    public static class Before
    {
        [ComponentVersion(1)]
        public sealed record Terms : ITemplateComponent
        {
            public void Compose(BlockComposer content) => content.Paragraph("Terms v1");
        }
    }

    /// <summary>The same component today: its wording changed, its version went up.</summary>
    public static class Now
    {
        [ComponentVersion(2)]
        public sealed record Terms : ITemplateComponent
        {
            public void Compose(BlockComposer content) => content.Paragraph("Terms v2");
        }
    }

    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private static readonly DataCatalog _catalog = DataCatalog.From<InvoiceData>();

    private static TemplateCheckOptions Options(TemplateIdentity? expected = null) =>
        new TemplateCheckOptions { Data = _catalog, Expected = expected ?? IdentityTests.InvoiceIdentity with { Version = 2 } };

    private static TemplateCheckResult Check(GeneratedTemplate template, TemplateCheckOptions? options = null) =>
        TemplateInspector.Check(template.OpenRead(), options ?? Options());

    private static TemplateCheckResult Check(byte[] template, TemplateCheckOptions? options = null) =>
        TemplateInspector.Check(new MemoryStream(template), options ?? Options());

    [Theory]
    [MemberData(nameof(Formats))]
    public void Check_UnchangedTemplate_IsOk(TemplateFormat format)
    {
        TemplateCheckResult result = Check(BuilderHarness.Build(IdentityTests.Invoice(2), format));

        Assert.Equal(TemplateCheckStatus.Ok, result.Status);
        Assert.Empty(result.Problems);
        Assert.Equal(format, result.Format);
        Assert.Equal("invoice", result.Stamp!.TemplateId);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Check_CopyReadingAnotherCatalogueField_IsOk(TemplateFormat format)
    {
        // The standard template does not read order.reference; a customer's copy may.
        GeneratedTemplate copy = BuilderHarness.Build(
            new TestDefinition(t => t.Content(c => c.Paragraph(t.Data["order"]["reference"])), IdentityTests.InvoiceIdentity with { Version = 2 }),
            format);

        Assert.Equal(TemplateCheckStatus.Ok, Check(copy).Status);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Check_RemovedField_IsBroken_WithPath(TemplateFormat format)
    {
        TemplateCheckResult result = Check(
            BuilderHarness.Build(IdentityTests.Invoice(2), format),
            new TemplateCheckOptions { Data = DataCatalog.From<RenamedData>() });

        Assert.Equal(TemplateCheckStatus.Broken, result.Status);
        TemplateCheckProblem problem = Assert.Single(result.Problems, p => p.Severity == TemplateCheckSeverity.Error);
        Assert.Equal("TB9301", problem.Code);
        Assert.Equal("order.number", problem.DataPath);
        Assert.Contains("order.number", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Check_PathsTheDataNoLongerHas_AreListed(TemplateFormat format)
    {
        TemplateCheckResult result = Check(BuilderHarness.Build(IdentityTests.Invoice(1, withOldFields: true), format));

        Assert.Equal(TemplateCheckStatus.Broken, result.Status);
        // order.archived comes from a MissingConditionVariable warning, read from its message until #275.
        Assert.Equal(
            ["line.discount", "order.archived", "order.fax"],
            result.Problems.Where(p => p.Code == "TB9301").Select(p => p.DataPath).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Check_LoopOverValue_IsBroken()
    {
        GeneratedTemplate copy = BuilderHarness.Build(
            new TestDefinition(t => t.Content(c => c.ForEach(t.Data["order"]["number"], "n", (n, b) => b.Paragraph(n.Self)))),
            TemplateFormat.Docx);

        TemplateCheckResult result = Check(copy);

        Assert.Equal(TemplateCheckStatus.Broken, result.Status);
        TemplateCheckProblem problem = Assert.Single(result.Problems, p => p.Severity == TemplateCheckSeverity.Error);
        Assert.Equal("TB9302", problem.Code);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Check_HandEdits_AreJudgedByTheirPaths(TemplateFormat format)
    {
        GeneratedTemplate template = BuilderHarness.Build(IdentityTests.Invoice(2), format);
        byte[] bytes = template.Content.ToArray();
        string part = BuilderHarness.MainPart(format);

        // A section deleted (the paid note and its markers), a loop variable renamed.
        byte[] cutDown = BuilderHarness.EditEntry(bytes, part, xml => IdentityTests.RemoveParagraphsContaining(xml, format, "{{#if order.isPaid}}", "Paid", "{{/if}}"));
        byte[] renamed = BuilderHarness.EditEntry(bytes, part, xml => xml.Replace("line.", "l.", StringComparison.Ordinal).Replace("foreach line in", "foreach l in", StringComparison.Ordinal));

        Assert.Equal(TemplateCheckStatus.Ok, Check(cutDown).Status);
        Assert.Equal(TemplateCheckStatus.Ok, Check(renamed).Status);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Check_SyntaxError_IsBroken(TemplateFormat format)
    {
        byte[] bytes = BuilderHarness.Build(IdentityTests.Invoice(2), format).Content.ToArray();
        byte[] broken = BuilderHarness.EditEntry(bytes, BuilderHarness.MainPart(format), xml => IdentityTests.RemoveParagraphsContaining(xml, format, "{{/foreach}}"));

        TemplateCheckResult result = Check(broken);

        Assert.Equal(TemplateCheckStatus.Broken, result.Status);
        TemplateCheckProblem problem = Assert.Single(result.Problems, p => p.Code == "TB9300");
        Assert.Equal(TemplateCheckSeverity.Error, problem.Severity);
        Assert.Contains("foreach", problem.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Problems, p => p.Code == "TB9302");
    }

    [Fact]
    public void Check_NotATemplate_IsBroken()
    {
        TemplateCheckResult result = TemplateInspector.Check(new MemoryStream([1, 2, 3, 4]), Options());

        Assert.Equal(TemplateCheckStatus.Broken, result.Status);
        Assert.Equal(TemplateFormat.Unknown, result.Format);
        Assert.Null(result.Stamp);
        Assert.Contains(result.Problems, p => p.Code == "TB9300");
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Check_NoStamp_IsWarning(TemplateFormat format)
    {
        using MemoryStream stripped = new MemoryStream();
        BuilderHarness.Build(IdentityTests.Invoice(2), format).WriteTo(stripped);
        TemplateStampRemover.Remove(stripped);

        TemplateCheckResult result = TemplateInspector.Check(stripped, Options());

        Assert.Equal(TemplateCheckStatus.Warning, result.Status);
        Assert.Equal("TB9303", Assert.Single(result.Problems).Code);
    }

    [Fact]
    public void Check_OtherTemplate_IsWarning()
    {
        TemplateCheckResult result = Check(BuilderHarness.Build(IdentityTests.Invoice(2), TemplateFormat.Odt), Options(new TemplateIdentity("letter", 1)));

        Assert.Equal(TemplateCheckStatus.Warning, result.Status);
        Assert.Equal("TB9304", Assert.Single(result.Problems).Code);
    }

    [Fact]
    public void Check_OlderVersion_IsWarning()
    {
        TemplateCheckResult result = Check(BuilderHarness.Build(IdentityTests.Invoice(1), TemplateFormat.Docx));

        Assert.Equal(TemplateCheckStatus.Warning, result.Status);
        TemplateCheckProblem problem = Assert.Single(result.Problems);
        Assert.Equal("TB9305", problem.Code);
        Assert.Contains("1", problem.Message, StringComparison.Ordinal);
        Assert.Contains("2", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_WithoutExpectedIdentity_DoesNotJudgeTheStamp()
    {
        TemplateCheckResult result = Check(BuilderHarness.Build(IdentityTests.Invoice(1), TemplateFormat.Docx), new TemplateCheckOptions { Data = _catalog });

        Assert.Equal(TemplateCheckStatus.Ok, result.Status);
    }

    [Fact]
    public void Check_OlderComponent_IsWarning()
    {
        // The copy was built with Terms at version 1; the host now registers version 2 under the same type name.
        GeneratedTemplate copy = BuilderHarness.Build(
            new TestDefinition(t => t.Content(c => c.Component(new Before.Terms())), IdentityTests.InvoiceIdentity with { Version = 2 }),
            TemplateFormat.Docx);
        TemplateCheckResult result = Check(copy, Options() with { Components = new ComponentCatalog().Register<Now.Terms>("terms") });

        Assert.Equal(TemplateCheckStatus.Warning, result.Status);
        TemplateCheckProblem problem = Assert.Single(result.Problems);
        Assert.Equal("TB9306", problem.Code);
        Assert.Contains("Terms", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_CurrentComponent_IsOk()
    {
        GeneratedTemplate copy = BuilderHarness.Build(
            new TestDefinition(t => t.Content(c => c.Component(new Now.Terms())), IdentityTests.InvoiceIdentity with { Version = 2 }),
            TemplateFormat.Docx);

        TemplateCheckResult result = Check(copy, Options() with { Components = new ComponentCatalog().Register<Now.Terms>("terms") });

        Assert.Equal(TemplateCheckStatus.Ok, result.Status);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Check_OpenShape_AcceptsAnyPath(TemplateFormat format)
    {
        GeneratedTemplate copy = BuilderHarness.Build(
            new TestDefinition(
                t =>
                {
                    TemplatePath extra = t.Data["order"]["extra"];
                    t.Content(c =>
                    {
                        c.Paragraph(extra["costCentre"]["code"]);
                        c.ForEach(extra["notes"], "note", (note, b) => b.Paragraph(note["text"]));
                    });
                },
                IdentityTests.InvoiceIdentity with { Version = 2 }),
            format);

        Assert.Equal(TemplateCheckStatus.Ok, Check(copy).Status);
    }

    private static string InvoiceJson(int version = 2) =>
        TemplateJson.Export(IdentityTests.Invoice(version), BuilderHarness.Options(TemplateFormat.Docx, "de-DE")).Json;

    private static TemplateJsonImportOptions Import(DataCatalog? data = null) => new TemplateJsonImportOptions { Data = data ?? _catalog };

    [Fact]
    public void CheckDefinition_Current_IsOk()
    {
        TemplateCheckResult result = TemplateInspector.CheckDefinition(InvoiceJson(), Import(), BuilderHarness.Options(TemplateFormat.Docx, "de-DE"), IdentityTests.InvoiceIdentity with { Version = 2 });

        Assert.Equal(TemplateCheckStatus.Ok, result.Status);
        Assert.Empty(result.Problems);
        Assert.Equal(TemplateFormat.Unknown, result.Format);
        Assert.Null(result.Stamp);
    }

    [Fact]
    public void CheckDefinition_RemovedField_IsBroken_AtPointer()
    {
        TemplateCheckResult result = TemplateInspector.CheckDefinition(InvoiceJson(), Import(DataCatalog.From<RenamedData>()));

        Assert.Equal(TemplateCheckStatus.Broken, result.Status);
        TemplateCheckProblem problem = Assert.Single(result.Problems);
        Assert.Equal("TB9203", problem.Code);
        Assert.Equal("order.number", problem.DataPath);
        Assert.StartsWith("/content/", problem.Location, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckDefinition_NotJson_IsBroken()
    {
        TemplateCheckResult result = TemplateInspector.CheckDefinition("{ not json", Import());

        Assert.Equal(TemplateCheckStatus.Broken, result.Status);
        Assert.Equal("TB9200", Assert.Single(result.Problems).Code);
    }

    [Fact]
    public void CheckDefinition_BuildError_IsBroken()
    {
        // Imports, but the build rejects it: a paragraph style the style sheet does not define.
        string json = InvoiceJson().Replace("\"content\": [", "\"content\": [ { \"paragraph\": \"x\", \"style\": \"Missing\" },", StringComparison.Ordinal);
        Assert.NotNull(TemplateJson.Import(json, Import()));

        TemplateCheckResult withoutBuild = TemplateInspector.CheckDefinition(json, Import());
        TemplateCheckResult withBuild = TemplateInspector.CheckDefinition(json, Import(), BuilderHarness.Options(TemplateFormat.Docx, "de-DE"));

        Assert.Equal(TemplateCheckStatus.Ok, withoutBuild.Status);
        Assert.Equal(TemplateCheckStatus.Broken, withBuild.Status);
        Assert.All(withBuild.Problems, p => Assert.Equal(TemplateCheckSeverity.Error, p.Severity));
    }

    [Fact]
    public void CheckDefinition_OlderVersion_IsWarning()
    {
        TemplateCheckResult result = TemplateInspector.CheckDefinition(InvoiceJson(version: 1), Import(), expected: IdentityTests.InvoiceIdentity with { Version = 2 });

        Assert.Equal(TemplateCheckStatus.Warning, result.Status);
        Assert.Equal("TB9305", Assert.Single(result.Problems).Code);
    }

    [Fact]
    public void CheckDefinition_NeedsADataCatalogue()
    {
        Assert.Throws<ArgumentException>(() => TemplateInspector.CheckDefinition(InvoiceJson(), new TemplateJsonImportOptions()));
    }
}
