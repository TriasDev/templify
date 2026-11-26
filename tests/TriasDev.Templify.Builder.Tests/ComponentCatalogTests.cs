// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Builder.Json;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

/// <summary>Registered components export as references (#274, hybrid).</summary>
public sealed class ComponentCatalogTests
{
    private static readonly ParagraphStyleKey _strong = new ParagraphStyleKey("Strong", "Strong Body");

    public sealed record Address(string Street, string City);

    public sealed record AddressBlock(TemplatePath Address) : ITemplateComponent
    {
        public void Compose(BlockComposer content) => content.Paragraph(x =>
        {
            x.Value(Address["street"]);
            x.Text(", ");
            x.Value(Address["city"]);
        });
    }

    public sealed record AddressBlockV2(TemplatePath Address) : ITemplateComponent
    {
        public void Compose(BlockComposer content) => content.Paragraph(x =>
        {
            x.Text("City: ");
            x.Value(Address["city"]);
        });
    }

    public enum Tone
    {
        Plain,
        LoudVoice,
    }

    public sealed record Badge(string Label, Tone Tone, int Repeat, decimal Threshold, ParagraphStyleKey Style, Length Gap, Color? Ink, IReadOnlyList<string> Tags, TemplatePath<Address> Where) : ITemplateComponent
    {
        public void Compose(BlockComposer content)
        {
            for (int i = 0; i < Repeat; i++)
            {
                content.Paragraph(Style, x =>
                {
                    x.Text(Label + " " + Tone + " " + Threshold.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + string.Join("/", Tags) + " " + Gap + " " + Ink + " ");
                    x.Value(Where.Path(a => a.City));
                });
            }
        }
    }

    public sealed class ThemeBlock(BrandTheme theme) : ITemplateComponent
    {
        public BrandTheme Theme { get; } = theme;

        public void Compose(BlockComposer content) => content.Paragraph("x");
    }

    public sealed record BrandTheme(string Name);

    private static ComponentCatalog Catalog() => new ComponentCatalog().Register<AddressBlock>("address").Register<Badge>("badge");

    private static TestDefinition Definition() => new TestDefinition(t =>
    {
        t.Styles(StyleSheet.Create(s => s.Paragraph(_strong, p => p.Bold())));
        t.Content(c =>
        {
            c.Component(new AddressBlock(t.Data["order"]["customer"]["address"]));
            c.ForEach(t.Data["branches"], "branch", (branch, b) =>
                b.Component(new AddressBlock(branch["address"])));
            c.Component(new Badge("Hi", Tone.LoudVoice, 2, 0.5m, _strong, 3.Mm(), Color.FromHex("1F4E79"), new[] { "a", "b" }, t.DataAs<Root>().Path(r => r.Office)));
        });
    });

    public sealed record Root(Address Office);

    [Fact]
    public void Register_RejectsUnserializableParameters()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new ComponentCatalog().Register<ThemeBlock>("theme"));

        Assert.Contains("theme", exception.Message, StringComparison.Ordinal);
        Assert.Contains("BrandTheme", exception.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => new ComponentCatalog().Register<AddressBlock>("Address-Block"));
        Assert.Throws<ArgumentException>(() => new ComponentCatalog().Register<AddressBlock>("address").Register<AddressBlockV2>("address"));
    }

    [Fact]
    public void Export_RegisteredComponent_IsAReference()
    {
        string json = TemplateJson.Export(Definition(), BuilderHarness.Options(TemplateFormat.Docx), Catalog()).Json;

        Assert.Contains("\"component\": \"address\"", json, StringComparison.Ordinal);
        Assert.Contains("\"address\": \"order.customer.address\"", json, StringComparison.Ordinal);
        Assert.Contains("\"address\": \"branch.address\"", json, StringComparison.Ordinal);
        Assert.Contains("\"tone\": \"loudVoice\"", json, StringComparison.Ordinal);
        Assert.Contains("\"gap\": \"3mm\"", json, StringComparison.Ordinal);
        Assert.Contains("\"where\": \"office\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"section\"", json, StringComparison.Ordinal);

        string unregistered = TemplateJson.Export(Definition(), BuilderHarness.Options(TemplateFormat.Docx)).Json;
        Assert.Contains("\"section\": \"AddressBlock\"", unregistered, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TemplateFormat.Docx)]
    [InlineData(TemplateFormat.Odt)]
    public void RoundTrip_WithCatalog_IsByteIdentical(TemplateFormat format)
    {
        TemplateBuildOptions options = BuilderHarness.Options(format);
        TemplateJsonDocument exported = TemplateJson.Export(Definition(), options, Catalog());

        ITemplateDefinition imported = TemplateJson.Import(exported.Json, new TemplateJsonImportOptions { Components = Catalog() });

        Assert.Equal(TemplateBuilder.Build(Definition(), options).ContentHash, TemplateBuilder.Build(imported, options).ContentHash);
        Assert.Equal(exported.Json, TemplateJson.Export(imported, options, Catalog()).Json);
    }

    [Fact]
    public void Import_ComponentReference_UsesCurrentComponentCode()
    {
        string json = TemplateJson.Export(Definition(), BuilderHarness.Options(TemplateFormat.Docx), Catalog()).Json;
        ComponentCatalog updated = new ComponentCatalog().Register<AddressBlockV2>("address").Register<Badge>("badge");

        ITemplateDefinition imported = TemplateJson.Import(json, new TemplateJsonImportOptions { Components = updated });
        (_, byte[] filled) = BuilderHarness.BuildAndFill(
            imported,
            TemplateFormat.Docx,
            """{ "order": { "customer": { "address": { "street": "S", "city": "Munich" } } }, "branches": [], "office": { "city": "Berlin" } }""");

        Assert.Equal("City: Munich", BuilderHarness.ParagraphTexts(TemplateFormat.Docx, filled)[0]);
    }

    [Fact]
    public void Import_UnknownComponent_IsTB9200()
    {
        string json = TemplateJson.Export(Definition(), BuilderHarness.Options(TemplateFormat.Docx), Catalog()).Json;

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(() => TemplateJson.Import(json, new TemplateJsonImportOptions { Components = new ComponentCatalog() }));

        Assert.All(exception.Errors, e => Assert.Equal("TB9200", e.Code));
        Assert.Contains(exception.Errors, e => e.Location == "/content/0" && e.Message.Contains("'address'", StringComparison.Ordinal));
    }
}
