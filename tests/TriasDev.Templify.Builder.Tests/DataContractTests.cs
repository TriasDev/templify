// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

public sealed class DataContractTests
{
    public static TheoryData<TemplateFormat> Formats => new TheoryData<TemplateFormat>(BuilderHarness.Formats);

    private sealed class Address(TemplatePath address) : ITemplateComponent
    {
        public void Compose(BlockComposer content) => content.Paragraph(x =>
        {
            x.Value(address["street"]);
            x.Text(" ");
            x.Value(address["city"]);
        });
    }

    private static TestDefinition Order() => new TestDefinition(template =>
    {
        TemplatePath order = template.Data["order"];
        template.Content(c =>
        {
            c.Paragraph(order["number"]);
            c.Component(new Address(order["customer.address"]));
            c.If(order["isPaid"] & order["total"].Gt(100), b => b.Paragraph("paid"));
            c.Switch(order["status"], s =>
            {
                s.Case("open", b => b.Paragraph("open"));
                s.Default(b => b.Paragraph("other"));
            });
            c.ForEach(order["lines"], "line", (line, b) =>
            {
                b.If(!line.IsFirst, r => r.Paragraph("---"));
                b.Paragraph(x =>
                {
                    x.Value(line.Number);
                    x.Value(line["description"]);
                    x.Value(order["currency"]);
                });
                b.ForEach(line["tags"], "tag", (tag, tb) => tb.Paragraph(tag.Self));
                b.If(Condition.Raw("line.amount > 10", line["amount"]), r => r.Paragraph("big"));
            });
        });
    });

    [Fact]
    public void Contract_ListsEveryPathInCanonicalForm()
    {
        DataContract contract = BuilderHarness.Build(Order(), TemplateFormat.Docx).Contract;

        Assert.Equal(
            new[]
            {
                new ContractEntry("order.currency", ContractUsage.Value),
                new ContractEntry("order.customer.address.city", ContractUsage.Value),
                new ContractEntry("order.customer.address.street", ContractUsage.Value),
                new ContractEntry("order.isPaid", ContractUsage.Condition),
                new ContractEntry("order.lines", ContractUsage.Collection),
                new ContractEntry("order.lines[].amount", ContractUsage.Condition),
                new ContractEntry("order.lines[].description", ContractUsage.Value),
                new ContractEntry("order.lines[].tags", ContractUsage.Collection),
                new ContractEntry("order.lines[].tags[]", ContractUsage.Value),
                new ContractEntry("order.number", ContractUsage.Value),
                new ContractEntry("order.status", ContractUsage.Condition),
                new ContractEntry("order.total", ContractUsage.Condition),
            },
            contract.Entries);
        Assert.Equal(64, contract.Hash.Length);
    }

    [Fact]
    public void Hash_IsTheSameForEveryFormatAndLanguage()
    {
        string[] hashes = new[] { "en-US", "de-DE", "fr" }
            .SelectMany(language => BuilderHarness.Formats.Select(format =>
                TemplateBuilder.Build(Order(), BuilderHarness.Options(format, language)).Contract.Hash))
            .Distinct()
            .ToArray();

        Assert.Single(hashes);
    }

    [Fact]
    public void Hash_ChangesWithThePaths()
    {
        TestDefinition one = new TestDefinition(t => t.Content(c => c.Paragraph(t.Data["a"])));
        TestDefinition other = new TestDefinition(t => t.Content(c => c.Paragraph(t.Data["b"])));
        TestDefinition asCondition = new TestDefinition(t => t.Content(c => c.If(t.Data["a"], b => b.Paragraph("x"))));

        string[] hashes =
        {
            BuilderHarness.Build(one, TemplateFormat.Docx).Contract.Hash,
            BuilderHarness.Build(other, TemplateFormat.Docx).Contract.Hash,
            BuilderHarness.Build(asCondition, TemplateFormat.Docx).Contract.Hash,
        };

        Assert.Equal(3, hashes.Distinct().Count());
    }

    [Fact]
    public void Witness_HasEveryPathWithOneItemPerCollection()
    {
        IReadOnlyDictionary<string, object?> witness = BuilderHarness.Build(Order(), TemplateFormat.Odt).Contract.CreateWitness();

        Dictionary<string, object?> order = Assert.IsType<Dictionary<string, object?>>(witness["order"]);
        Assert.Equal("x", order["number"]);
        Dictionary<string, object?> line = Assert.IsType<Dictionary<string, object?>>(Assert.Single(Assert.IsType<List<object?>>(order["lines"])));
        Assert.Equal("x", line["description"]);
        Assert.Equal("x", Assert.Single(Assert.IsType<List<object?>>(line["tags"])));
        Assert.Equal("x", ((Dictionary<string, object?>)((Dictionary<string, object?>)order["customer"]!)["address"]!)["city"]);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Witness_FillsTheTemplateWithoutAnyMissingVariable(TemplateFormat format)
    {
        GeneratedTemplate built = BuilderHarness.Build(Order(), format);

        using (Stream stream = built.OpenRead())
        {
            ValidationResult validation = new TemplateProcessor().ValidateTemplate(stream, built.Contract.CreateWitness());
            Assert.Empty(validation.MissingVariables);
            Assert.DoesNotContain(validation.Warnings, w => w.Type == ValidationWarningType.MissingConditionVariable);
        }

        // Fills with MissingVariableBehavior.ThrowException and no warnings (the harness asserts both).
        BuilderHarness.BuildAndFill(Order(), format, built.Contract.CreateWitness());
    }

    [Fact]
    public void Json_RoundTrips()
    {
        DataContract contract = BuilderHarness.Build(Order(), TemplateFormat.Docx).Contract;

        string json = contract.ToJson();
        DataContract read = DataContract.FromJson(json);

        Assert.Equal(contract.Entries, read.Entries);
        Assert.Equal(contract.Hash, read.Hash);
        Assert.Contains("\"usage\": \"collection\"", json, StringComparison.Ordinal);
        Assert.Contains($"\"hash\": \"{contract.Hash}\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("""{ "version": 2, "entries": [] }""")]
    [InlineData("""{ "version": 1 }""")]
    [InlineData("""{ "version": 1, "entries": [ { "path": "a", "usage": "unknown" } ] }""")]
    [InlineData("""{ "version": 1, "entries": [ { "path": "", "usage": "value" } ] }""")]
    [InlineData("""{ "version": 1, "entries": [ { "usage": "value" } ] }""")]
    public void Json_Invalid_Throws(string json)
    {
        Assert.Throws<FormatException>(() => DataContract.FromJson(json));
    }

    [Fact]
    public void EmptyDefinition_HasAnEmptyContractAndWitness()
    {
        DataContract contract = BuilderHarness.Build(new TestDefinition(t => t.Content(c => c.Paragraph("static"))), TemplateFormat.Docx).Contract;

        Assert.Empty(contract.Entries);
        Assert.Empty(contract.CreateWitness());
    }
}
