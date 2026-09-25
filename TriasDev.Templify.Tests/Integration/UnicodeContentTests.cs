// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Non-ASCII content: unicode, right-to-left and emoji values, and non-ASCII variable names.
/// </summary>
public sealed class UnicodeContentTests
{
    [Theory]
    [InlineData("Zoë Ångström-Ñúñez")]
    [InlineData("Ελληνικά και Кириллица")]
    [InlineData("مرحبا بالعالم")]                 // Arabic (RTL)
    [InlineData("שלום עולם")]                     // Hebrew (RTL)
    [InlineData("日本語のテキスト")]
    [InlineData("Party 🎉 👩‍💻 🇩🇪")]              // surrogate pairs, ZWJ sequence, flag
    [InlineData("é composed vs é")]     // combining mark vs precomposed
    public void ProcessTemplate_UnicodeValue_IsInsertedUnchanged(string value)
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Value: {{Value}}.");

        using TemplateTestRun run = TemplateTestHarness.Process(builder, new Dictionary<string, object> { ["Value"] = value });

        Assert.Equal($"Value: {value}.", run.Verifier.GetParagraphText(0));
        Assert.Empty(run.Verifier.GetValidationErrors());
    }

    [Fact]
    public void ProcessTemplate_EmojiValueInSplitPlaceholder_IsInsertedUnchanged()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraphWithRuns(("🎉 {{Val", null), ("ue}} 🎉", DocumentBuilder.CreateFormatting(bold: true)));

        using TemplateTestRun run = TemplateTestHarness.Process(builder, new Dictionary<string, object> { ["Value"] = "👩‍💻" });

        Assert.Equal("🎉 👩‍💻 🎉", run.Verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_RtlValueInLoop_KeepsOrderOfItems()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#foreach Names}}");
        builder.AddParagraph("{{@number}}: {{.}}");
        builder.AddParagraph("{{/foreach}}");

        using TemplateTestRun run = TemplateTestHarness.Process(
            builder,
            new Dictionary<string, object> { ["Names"] = new List<string> { "أحمد", "فاطمة" } });

        Assert.Equal(new[] { "1: أحمد", "2: فاطمة" }, run.Verifier.GetAllParagraphTexts());
    }

    [Fact]
    public void ProcessTemplate_UnicodeValueWithMarkdown_IsFormatted()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{Message}}");

        using TemplateTestRun run = TemplateTestHarness.Process(
            builder,
            new Dictionary<string, object> { ["Message"] = "Grüße **Ωmega** 🎉" });

        Assert.Equal("Grüße Ωmega 🎉", run.Verifier.GetParagraphText(0));
        Assert.Contains(run.Verifier.GetRuns(0), r => r.InnerText == "Ωmega" && r.RunProperties?.Bold != null);
    }

    [Theory]
    [InlineData("Größe")]
    [InlineData("Имя")]
    [InlineData("名前")]
    [InlineData("prénom")]
    public void ProcessTemplate_NonAsciiVariableName_IsResolved(string name)
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph($"[{{{{{name}}}}}]");

        using TemplateTestRun run = TemplateTestHarness.Process(builder, new Dictionary<string, object> { [name] = "ok" });

        Assert.Equal("[ok]", run.Verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_NonAsciiNestedPathAndCondition_AreResolved()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("{{#if Kunde.Größe > 3}}");
        builder.AddParagraph("{{Kunde.Straße}}");
        builder.AddParagraph("{{/if}}");

        using TemplateTestRun run = TemplateTestHarness.Process(
            builder,
            new Dictionary<string, object>
            {
                ["Kunde"] = new Dictionary<string, object> { ["Größe"] = 5, ["Straße"] = "Hauptstraße 1" }
            });

        Assert.Equal(new[] { "Hauptstraße 1" }, run.Verifier.GetAllParagraphTexts());
    }
}
