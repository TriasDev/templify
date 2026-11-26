// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json;
using TriasDev.Templify.Builder.Json;
using TriasDev.Templify.Builder.Sample;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>Findings of the phase 3 branch review (#274), each pinned by a test.</summary>
public sealed class Phase3ReviewTests
{
    private static string Definition(string content) => $$"""
        {
          "templifyDefinition": 1,
          "identity": { "id": "review", "version": 1 },
          "content": {{content}}
        }
        """;

    // ── C1: deep conditions must not overflow the stack ───────────────────────

    [Fact]
    public void C1_DeeplyNestedCondition_IsADiagnostic()
    {
        string condition = new string('(', 20_000) + "a" + new string(')', 20_000);
        string json = Definition($$"""[ { "if": "{{condition}}", "then": [ { "paragraph": "x" } ] } ]""");

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(() => TemplateJson.Import(json));

        BuildDiagnostic error = Assert.Single(exception.Errors);
        Assert.Equal("/content/0/if", error.Location);
        Assert.Contains("nested", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void C1_ManyNots_IsADiagnostic()
    {
        string condition = string.Concat(Enumerable.Repeat("not ", 200_000)) + "a";
        string json = Definition($$"""[ { "if": "{{condition}}", "then": [ { "paragraph": "x" } ] } ]""");

        Assert.Equal("/content/0/if", Assert.Single(Assert.Throws<TemplateBuildException>(() => TemplateJson.Import(json)).Errors).Location);
    }

    [Fact]
    public void C1_DeeplyNestedRawCondition_IsADiagnostic()
    {
        string condition = new string('(', 50_000) + "a" + new string(')', 50_000);
        string json = Definition($$"""[ { "if": { "raw": "{{condition}}", "uses": [ "a" ] }, "then": [ { "paragraph": "x" } ] } ]""");

        Assert.Equal("/content/0/if", Assert.Single(Assert.Throws<TemplateBuildException>(() => TemplateJson.Import(json)).Errors).Location);
    }

    // ── C2: an imported definition can be built concurrently ──────────────────

    [Fact]
    public void C2_ImportedDefinition_BuildsConcurrently()
    {
        TemplateBuildOptions options = new TemplateBuildOptions { Format = TemplateFormat.Docx, Language = CultureInfo.GetCultureInfo("en-GB"), Texts = SampleTexts.Catalog };
        OrderConfirmation definition = new OrderConfirmation(BrandTheme.CreateDefault());
        TemplateJsonDocument exported = TemplateJson.Export(definition, options, SampleRunner.Components);
        ITemplateDefinition imported = TemplateJson.Import(exported.Json, new TemplateJsonImportOptions { Assets = exported.Assets, Components = SampleRunner.Components, AllowRawXml = true });
        string expected = TemplateBuilder.Build(definition, options).ContentHash;

        string[] hashes = new string[200];
        Parallel.For(0, hashes.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i => hashes[i] = TemplateBuilder.Build(imported, options).ContentHash);

        Assert.All(hashes, h => Assert.Equal(expected, h));
    }

    // ── I1: every problem is a TemplateBuildException ─────────────────────────

    public sealed record Validating(string Label) : ITemplateComponent
    {
        public string Label { get; } = Label.Length > 0 ? Label : throw new ArgumentException("A label is required.", nameof(Label));

        public void Compose(BlockComposer content) => content.Paragraph(Label);
    }

    public sealed record Crashing(string Label) : ITemplateComponent
    {
        public string Label { get; } = Label == "boom" ? throw new InvalidOperationException("boom") : Label;

        public void Compose(BlockComposer content) => content.Paragraph(Label);
    }

    [Fact]
    public void I1_HeaderRowAfterBodyRow_IsTB9201()
    {
        string json = Definition("""
            [ { "columns": [ 1 ], "table": [ { "row": [ "a" ] }, { "header": true, "row": [ "h" ] } ] } ]
            """);

        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => BuilderHarness.Build(TemplateJson.Import(json), TemplateFormat.Docx)).Errors);

        Assert.Equal("TB9201", error.Code);
        Assert.Equal("/content/0/table/1", error.Location);
    }

    [Theory]
    [InlineData("")]
    [InlineData("boom")]
    public void I1_ComponentConstructorThrowing_IsTB9201(string label)
    {
        ComponentCatalog catalog = new ComponentCatalog().Register<Validating>("validating").Register<Crashing>("crashing");
        string name = label.Length == 0 ? "validating" : "crashing";
        string json = Definition($$"""[ { "component": "{{name}}", "params": { "label": "{{label}}" } } ]""");

        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => BuilderHarness.Build(TemplateJson.Import(json, new TemplateJsonImportOptions { Components = catalog }), TemplateFormat.Docx)).Errors);

        Assert.Equal("TB9201", error.Code);
        Assert.Equal("/content/0", error.Location);
    }

    [Theory]
    [InlineData("\"templifyDefinition\": 1.5", "/templifyDefinition")]
    [InlineData("\"identity\": \"x\"", "/identity")]
    [InlineData("\"identity\": { \"id\": \"x\", \"version\": 1.5 }", "/identity")]
    public void I1_MalformedHeader_IsTB9200(string header, string location)
    {
        string json = Definition("[]").Replace("\"templifyDefinition\": 1", "\"templifyDefinition\": 1, \"__\": 0", StringComparison.Ordinal);
        json = header.StartsWith("\"templifyDefinition\"", StringComparison.Ordinal)
            ? json.Replace("\"templifyDefinition\": 1, \"__\": 0", header, StringComparison.Ordinal)
            : json.Replace("\"identity\": { \"id\": \"review\", \"version\": 1 }", header, StringComparison.Ordinal).Replace(", \"__\": 0", string.Empty, StringComparison.Ordinal);

        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => TemplateJson.Import(json)).Errors);

        Assert.Equal("TB9200", error.Code);
        Assert.Equal(location, error.Location);
    }

    // ── I2: typed component parameters keep the data's naming ─────────────────

    public sealed record SnakeData(SnakeCustomer Customer);

    public sealed record SnakeCustomer(SnakeAddress HomeAddress);

    public sealed record SnakeAddress(string PostalCode);

    public sealed record SnakeBlock(TemplatePath<SnakeAddress> Address) : ITemplateComponent
    {
        public void Compose(BlockComposer content) => content.Paragraph(Address.Path(a => a.PostalCode));
    }

    [Theory]
    [InlineData(TemplateFormat.Docx)]
    [InlineData(TemplateFormat.Odt)]
    public void I2_TypedComponentParameters_RoundTripWithTheCatalogsNaming(TemplateFormat format)
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c =>
            c.Component(new SnakeBlock(t.DataAs<SnakeData>(JsonNamingPolicy.SnakeCaseLower).Path(d => d.Customer.HomeAddress)))));
        ComponentCatalog catalog = new ComponentCatalog { DataNaming = JsonNamingPolicy.SnakeCaseLower }.Register<SnakeBlock>("snake");
        TemplateBuildOptions options = BuilderHarness.Options(format);

        string json = TemplateJson.Export(definition, options, catalog).Json;
        GeneratedTemplate imported = TemplateBuilder.Build(TemplateJson.Import(json, new TemplateJsonImportOptions { Components = catalog }), options);

        Assert.Equal(TemplateBuilder.Build(definition, options).ContentHash, imported.ContentHash);
        Assert.Contains(new ContractEntry("customer.home_address.postal_code", ContractUsage.Value), imported.Contract.Entries);
    }

    [Fact]
    public void I2_ParameterNamingDifferentFromTheCatalog_IsAnExportError()
    {
        TestDefinition definition = new TestDefinition(t => t.Content(c =>
            c.Component(new SnakeBlock(t.DataAs<SnakeData>(JsonNamingPolicy.SnakeCaseLower).Path(d => d.Customer.HomeAddress)))));

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            TemplateJson.Export(definition, BuilderHarness.Options(TemplateFormat.Docx), new ComponentCatalog().Register<SnakeBlock>("snake")));

        Assert.Contains("DataNaming", exception.Message, StringComparison.Ordinal);
    }

    // ── I3: computed members are not data paths ───────────────────────────────

    public sealed record Computed(decimal? Discount, List<string> Lines, string Name, DateTime When, Dictionary<string, string> Map);

    [Theory]
    [InlineData("count")]
    [InlineData("length")]
    [InlineData("year")]
    [InlineData("dotted")]
    public void I3_ComputedMembersAndDottedKeys_Throw(string kind)
    {
        TemplatePath<Computed> d = default(TemplateComposerProbe).Root;

        Assert.Throws<ArgumentException>(() => kind switch
        {
            "count" => d.Path(o => o.Lines.Count).ToString(),
            "length" => d.Path(o => o.Name.Length).ToString(),
            "year" => d.Path(o => o.When.Year).ToString(),
            _ => d.Path(o => o.Map["a.b"]).ToString(),
        });
    }

    [Fact]
    public void I3_NullableValue_IsTheValueItself()
    {
        TemplatePath<Computed> d = default(TemplateComposerProbe).Root;

        Assert.Equal("discount", d.Path(o => o.Discount!.Value).ToString());
    }

    /// <summary>A typed root without building a template.</summary>
    private readonly struct TemplateComposerProbe
    {
        public TemplatePath<Computed> Root
        {
            get
            {
                TemplatePath<Computed>? root = null;
                BuilderHarness.Build(new TestDefinition(t =>
                {
                    root = t.DataAs<Computed>();
                    t.Content(c => c.Paragraph("x"));
                }), TemplateFormat.Docx);
                return root!.Value;
            }
        }
    }

    // ── I4: with a data catalogue, the kind of a path must fit ────────────────

    public sealed record KindData(string Title, IReadOnlyList<string> Tags, KindAddress Address);

    public sealed record KindAddress(string City);

    public sealed record KindBlock(TemplatePath<KindAddress> Where) : ITemplateComponent
    {
        public void Compose(BlockComposer content) => content.Paragraph(Where.Path(a => a.City));
    }

    [Fact]
    public void I4_CatalogueChecksThatTheKindFits()
    {
        ComponentCatalog catalog = new ComponentCatalog().Register<KindBlock>("where");
        string json = Definition("""
            [
              { "forEach": "title", "as": "t", "body": [ { "paragraph": "x" } ] },
              { "paragraph": "{{address}}" },
              { "component": "where", "params": { "where": "title" } },
              { "forEach": "tags", "as": "tag", "body": [ { "paragraph": "{{tag}}" } ] },
              { "component": "where", "params": { "where": "address" } }
            ]
            """);

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(() =>
            TemplateJson.Import(json, new TemplateJsonImportOptions { Components = catalog, Data = DataCatalog.From<KindData>() }));

        Assert.All(exception.Errors, e => Assert.Equal("TB9203", e.Code));
        Assert.Equal(new[] { "/content/0/forEach", "/content/1/paragraph", "/content/2/params" }, exception.Errors.Select(e => e.Location));
    }

    // ── Re-graded minor: flags must be true ───────────────────────────────────

    [Theory]
    [InlineData("""{ "pageBreak": false }""", "/content/0/pageBreak")]
    [InlineData("""{ "paragraph": [ { "tabRight": false } ] }""", "/content/0/paragraph/0/tabRight")]
    public void Flags_MustBeTrue(string block, string location)
    {
        BuildDiagnostic error = Assert.Single(Assert.Throws<TemplateBuildException>(() => TemplateJson.Import(Definition("[" + block + "]"))).Errors);

        Assert.Equal("TB9200", error.Code);
        Assert.Equal(location, error.Location);
    }
}
