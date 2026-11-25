// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Globalization;
using System.Resources;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder.Tests;

public sealed class LocalisationTests
{
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Texts(params (string Key, string Language, string Text)[] entries) =>
        entries.GroupBy(e => e.Key)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<string, string>)g.ToDictionary(e => e.Language, e => e.Text));

    private static readonly ITextCatalog _invoiceTexts = TextCatalog.FromDictionary(Texts(
        ("title", "en", "Invoice"),
        ("title", "de", "Rechnung"),
        ("customer", "en", "Customer"),
        ("customer", "de", "Kunde"),
        ("paid", "en", "Paid"),
        ("paid", "de", "Bezahlt")));

    private static TestDefinition Invoice() => new TestDefinition(template =>
    {
        var t = template.Texts;
        template.Properties(p => p.Title(t["title"]));
        template.Content(c =>
        {
            c.Paragraph(t["title"]);
            c.Paragraph(x =>
            {
                x.Text(t["customer"] + ": ");
                x.Value(template.Data["customer"]);
            });
            c.If(template.Data["isPaid"], b => b.Paragraph(t["paid"]));
        });
    });

    private static GeneratedTemplate Build(TemplateFormat format, string language, ITextCatalog texts, ITemplateDefinition? definition = null) =>
        TemplateBuilder.Build(definition ?? Invoice(), new TemplateBuildOptions { Format = format, Language = CultureInfo.GetCultureInfo(language), Texts = texts });

    [Theory]
    [InlineData(TemplateFormat.Docx)]
    [InlineData(TemplateFormat.Odt)]
    public void OneDefinition_BuildsEveryLanguage_WithTheSameContract(TemplateFormat format)
    {
        GeneratedTemplate english = Build(format, "en-GB", _invoiceTexts);
        GeneratedTemplate german = Build(format, "de-DE", _invoiceTexts);

        Assert.Equal(new[] { "Invoice", "Customer: {{customer}}", "{{#if isPaid}}", "Paid", "{{/if}}" }, BuilderHarness.ParagraphTexts(format, english.Content.ToArray()));
        Assert.Equal(new[] { "Rechnung", "Kunde: {{customer}}", "{{#if isPaid}}", "Bezahlt", "{{/if}}" }, BuilderHarness.ParagraphTexts(format, german.Content.ToArray()));
        Assert.Equal(english.Contract.Hash, german.Contract.Hash);
        Assert.Equal("de-DE", german.Stamp.Language);

        (_, byte[] filled) = BuilderHarness.BuildAndFill(
            Invoice(), BuilderHarness.Options(format, "de-DE", _invoiceTexts), """{ "customer": "Alice", "isPaid": true }""");
        Assert.Equal(new[] { "Rechnung", "Kunde: Alice", "Bezahlt" }, BuilderHarness.ParagraphTexts(format, filled));
    }

    [Fact]
    public void MissingTranslations_AreReportedTogether_OncePerKey()
    {
        ITextCatalog incomplete = TextCatalog.FromDictionary(Texts(
            ("title", "en", "Invoice"),
            ("title", "de", "Rechnung"),
            ("customer", "en", "Customer")));
        TestDefinition definition = new TestDefinition(template =>
        {
            var t = template.Texts;
            template.Content(c =>
            {
                c.Paragraph(t["title"]);
                c.Paragraph(t["customer"]);
                c.Paragraph(t["customer"]);
                c.Paragraph(t["paid"]);
            });
        });

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(() => Build(TemplateFormat.Odt, "de-DE", incomplete, definition));

        Assert.Equal(new[] { "TB5001", "TB5001" }, exception.Errors.Select(e => e.Code));
        Assert.Contains("'customer'", exception.Errors[0].Message, StringComparison.Ordinal);
        Assert.Contains("de-DE", exception.Errors[0].Message, StringComparison.Ordinal);
        Assert.Contains("'paid'", exception.Errors[1].Message, StringComparison.Ordinal);
        Build(TemplateFormat.Odt, "en-US", TextCatalog.FromDictionary(Texts(("title", "en", "x"), ("customer", "en", "x"), ("paid", "en", "x"))), definition);
    }

    [Theory]
    [InlineData("de-AT", "de", true)]      // specific → neutral
    [InlineData("de", "de-DE", false)]     // a neutral language does not borrow a specific one
    [InlineData("fr-FR", "en", false)]     // never the default language instead of a missing one
    [InlineData("de-CH", "DE-ch", true)]   // language names compare case-insensitively
    public void Dictionary_FallsBackFromSpecificToNeutralOnly(string language, string available, bool found)
    {
        ITextCatalog catalog = TextCatalog.FromDictionary(Texts(("k", available, "text")));

        Assert.Equal(found, catalog.TryGet("k", CultureInfo.GetCultureInfo(language), out string text));
        Assert.Equal(found ? "text" : string.Empty, text);
    }

    [Fact]
    public void Combine_LetsLaterCatalogsOverride()
    {
        ITextCatalog product = TextCatalog.FromDictionary(Texts(("title", "en", "Invoice"), ("footer", "en", "Thank you")));
        ITextCatalog customer = TextCatalog.FromDictionary(Texts(("title", "en", "Bill")));

        ITextCatalog combined = TextCatalog.Combine(product, customer);

        Assert.True(combined.TryGet("title", CultureInfo.GetCultureInfo("en"), out string title));
        Assert.Equal("Bill", title);
        Assert.True(combined.TryGet("footer", CultureInfo.GetCultureInfo("en"), out string footer));
        Assert.Equal("Thank you", footer);
        Assert.False(TextCatalog.Empty.TryGet("title", CultureInfo.GetCultureInfo("en"), out _));
        Assert.Throws<ArgumentException>(() => TextCatalog.Combine(product, null!));
    }

    [Theory]
    [InlineData("de-DE", null, "Rechnung")]
    [InlineData("de", null, "Rechnung")]
    [InlineData("en-US", "en", "Invoice")]      // the neutral resources are English
    [InlineData("en-US", null, null)]           // without a neutral language they never answer
    [InlineData("fr", "en", null)]              // French is missing, not English
    public void ResourceManager_UsesSatellitesAndTheDeclaredNeutralLanguage(string language, string? neutral, string? expected)
    {
        ResourceManager resources = new InMemoryResourceManager(new Dictionary<string, Dictionary<string, string>>
        {
            [string.Empty] = new Dictionary<string, string> { ["title"] = "Invoice" },
            ["de"] = new Dictionary<string, string> { ["title"] = "Rechnung" },
        });
        ITextCatalog catalog = TextCatalog.FromResourceManager(resources, neutral is null ? null : CultureInfo.GetCultureInfo(neutral));

        bool found = catalog.TryGet("title", CultureInfo.GetCultureInfo(language), out string text);

        Assert.Equal(expected != null, found);
        Assert.Equal(expected ?? string.Empty, text);
    }

    [Fact]
    public void TextWithOpenBraces_IsCaughtLikeAnyLiteral()
    {
        ITextCatalog catalog = TextCatalog.FromDictionary(Texts(("title", "en", "Hello {{name}}")));
        TestDefinition definition = new TestDefinition(t => t.Content(c => c.Paragraph(t.Texts["title"])));

        TemplateBuildException exception = Assert.Throws<TemplateBuildException>(() => Build(TemplateFormat.Docx, "en", catalog, definition));

        Assert.Equal("TB1002", Assert.Single(exception.Errors).Code);
    }

    [Fact]
    public void Texts_KnowTheLanguage()
    {
        CultureInfo? seen = null;
        Build(TemplateFormat.Docx, "de-DE", TextCatalog.Empty, new TestDefinition(t => seen = t.Texts.Language));

        Assert.Equal("de-DE", seen!.Name);
    }

    /// <summary>A resource manager over in-memory resource sets, keyed by culture name ("" for the neutral resources).</summary>
    private sealed class InMemoryResourceManager(Dictionary<string, Dictionary<string, string>> sets) : ResourceManager
    {
        protected override ResourceSet? InternalGetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents) =>
            sets.TryGetValue(culture.Name, out Dictionary<string, string>? texts) ? new ResourceSet(new Reader(texts)) : null;

        private sealed class Reader(Dictionary<string, string> texts) : IResourceReader
        {
            public void Close()
            {
            }

            public void Dispose()
            {
            }

            public IDictionaryEnumerator GetEnumerator() => new Hashtable(texts).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
