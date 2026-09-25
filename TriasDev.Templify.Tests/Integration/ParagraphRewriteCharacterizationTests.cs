// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Characterization tests for paragraph text rewriting paths that were previously untested:
/// inline conditional markers split across runs, tabs inside inline conditionals, and
/// placeholders split across runs whose values contain newlines and/or markdown.
/// They protect the extraction of the shared paragraph text rewriter (#155).
/// </summary>
public sealed class ParagraphRewriteCharacterizationTests
{
    private static Run TextRun(string text, RunProperties? properties = null)
    {
        Run run = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        if (properties != null)
        {
            run.RunProperties = (RunProperties)properties.CloneNode(true);
        }

        return run;
    }

    private static Run TabRun() => new Run(new TabChar());

    private static MemoryStream Process(Paragraph paragraph, Dictionary<string, object> data)
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddElement(paragraph);

        MemoryStream output = new MemoryStream();
        ProcessingResult result = new DocumentTemplateProcessor().ProcessTemplate(builder.ToStream(), output, data);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        return output;
    }

    private static Run RunContaining(Paragraph paragraph, string text) =>
        paragraph.Descendants<Run>().First(r => r.InnerText.Contains(text));

    [Fact]
    public void InlineConditional_MarkersSplitAcrossRuns_ConditionTrue_KeepsContentAndFormatting()
    {
        RunProperties bold = DocumentBuilder.CreateFormatting(bold: true);
        RunProperties red = DocumentBuilder.CreateFormatting(color: "FF0000");
        Paragraph paragraph = new Paragraph(
            TextRun("Hi "),
            TextRun("{{#i", bold),
            TextRun("f Show}}Bo", red),
            TextRun("ld{{/i"),
            TextRun("f}} end"));

        using DocumentVerifier verifier = new DocumentVerifier(
            Process(paragraph, new Dictionary<string, object> { ["Show"] = true }));

        Paragraph output = verifier.GetParagraph(0);
        Assert.Equal("Hi Bold end", output.InnerText);
        Assert.Equal("FF0000", RunContaining(output, "Bo").RunProperties?.Color?.Val?.Value);
        Assert.Null(RunContaining(output, "ld").RunProperties?.Color);
        Assert.DoesNotContain(output.Descendants<Run>(), r => r.RunProperties?.Bold != null);
    }

    [Fact]
    public void InlineConditional_MarkersSplitAcrossRuns_ConditionFalse_RemovesBranch()
    {
        RunProperties bold = DocumentBuilder.CreateFormatting(bold: true);
        RunProperties red = DocumentBuilder.CreateFormatting(color: "FF0000");
        Paragraph paragraph = new Paragraph(
            TextRun("Hi "),
            TextRun("{{#i", bold),
            TextRun("f Show}}Bo", red),
            TextRun("ld{{/i"),
            TextRun("f}} end"));

        using DocumentVerifier verifier = new DocumentVerifier(
            Process(paragraph, new Dictionary<string, object> { ["Show"] = false }));

        Assert.Equal("Hi  end", verifier.GetParagraphText(0));
    }

    [Fact]
    public void InlineConditional_WithTabsInsideBranch_ConditionTrue_KeepsTabs()
    {
        Paragraph paragraph = new Paragraph(
            TextRun("A"),
            TabRun(),
            TextRun("{{#if Show}}B"),
            TabRun(),
            TextRun("C{{/if}}"),
            TextRun("D"));

        using DocumentVerifier verifier = new DocumentVerifier(
            Process(paragraph, new Dictionary<string, object> { ["Show"] = true }));

        Paragraph output = verifier.GetParagraph(0);
        Assert.Equal("ABCD", output.InnerText);
        Assert.Equal(2, output.Descendants<TabChar>().Count());
    }

    [Fact]
    public void InlineConditional_WithTabInsideBranch_ConditionFalse_DropsBranchTab()
    {
        Paragraph paragraph = new Paragraph(
            TextRun("A"),
            TabRun(),
            TextRun("{{#if Show}}B"),
            TabRun(),
            TextRun("C{{/if}}"),
            TextRun("D"));

        using DocumentVerifier verifier = new DocumentVerifier(
            Process(paragraph, new Dictionary<string, object> { ["Show"] = false }));

        Paragraph output = verifier.GetParagraph(0);
        Assert.Equal("AD", output.InnerText);
        Assert.Single(output.Descendants<TabChar>());
    }

    [Fact]
    public void InlineConditional_WithTabsAroundElseBranch_KeepsBothTabs()
    {
        Paragraph paragraph = new Paragraph(
            TextRun("A"),
            TabRun(),
            TextRun("{{#if Show}}B{{#else}}X{{/if}}"),
            TabRun(),
            TextRun("D"));

        using DocumentVerifier verifier = new DocumentVerifier(
            Process(paragraph, new Dictionary<string, object> { ["Show"] = false }));

        Paragraph output = verifier.GetParagraph(0);
        Assert.Equal("AXD", output.InnerText);
        Assert.Equal(2, output.Descendants<TabChar>().Count());
    }

    [Fact]
    public void Placeholder_SplitAcrossRuns_WithNewlines_InsertsBreakAndKeepsFormatting()
    {
        RunProperties red = DocumentBuilder.CreateFormatting(color: "FF0000");
        RunProperties bold = DocumentBuilder.CreateFormatting(bold: true);
        Paragraph paragraph = new Paragraph(
            TextRun("Hi {{Na", red),
            TextRun("me}}!", bold));

        using DocumentVerifier verifier = new DocumentVerifier(
            Process(paragraph, new Dictionary<string, object> { ["Name"] = "A\nB" }));

        Paragraph output = verifier.GetParagraph(0);
        Assert.Equal("Hi AB!", output.InnerText);
        Assert.Single(output.Descendants<Break>());

        List<OpenXmlElement> content = output.Descendants<Run>()
            .SelectMany(r => r.ChildElements.Where(c => c is Text or Break))
            .ToList();
        Assert.Equal(new[] { "Hi ", "A", "<br>", "B", "!" },
            content.Select(c => c is Break ? "<br>" : ((Text)c).Text).Where(s => s.Length > 0));

        Assert.Equal("FF0000", RunContaining(output, "A").RunProperties?.Color?.Val?.Value);
        Assert.Equal("FF0000", RunContaining(output, "B").RunProperties?.Color?.Val?.Value);
        Run exclamation = RunContaining(output, "!");
        Assert.NotNull(exclamation.RunProperties?.Bold);
        Assert.Null(exclamation.RunProperties?.Color);
    }

    [Fact]
    public void Placeholder_SplitAcrossRuns_WithMarkdownAndNewlines_AppliesBoth()
    {
        RunProperties red = DocumentBuilder.CreateFormatting(color: "FF0000");
        RunProperties bold = DocumentBuilder.CreateFormatting(bold: true);
        Paragraph paragraph = new Paragraph(
            TextRun("Hi {{Na", red),
            TextRun("me}}!", bold));

        using DocumentVerifier verifier = new DocumentVerifier(
            Process(paragraph, new Dictionary<string, object> { ["Name"] = "**A**\nB *c*" }));

        Paragraph output = verifier.GetParagraph(0);
        Assert.Equal("Hi AB c!", output.InnerText);
        Assert.Single(output.Descendants<Break>());

        Run a = RunContaining(output, "A");
        Assert.NotNull(a.RunProperties?.Bold);
        Assert.Equal("FF0000", a.RunProperties?.Color?.Val?.Value);

        Run c = RunContaining(output, "c");
        Assert.NotNull(c.RunProperties?.Italic);
        Assert.Null(c.RunProperties?.Bold);
        Assert.Equal("FF0000", c.RunProperties?.Color?.Val?.Value);

        Run b = RunContaining(output, "B ");
        Assert.Null(b.RunProperties?.Bold);
        Assert.Equal("FF0000", b.RunProperties?.Color?.Val?.Value);

        Assert.Empty(verifier.GetValidationErrors());
    }

    [Fact]
    public void Placeholder_SplitAcrossThreeRuns_UsesFirstRunFormattingAndKeepsSuffixFormatting()
    {
        RunProperties red = DocumentBuilder.CreateFormatting(color: "FF0000");
        RunProperties bold = DocumentBuilder.CreateFormatting(bold: true);
        Paragraph paragraph = new Paragraph(
            TextRun("Hi {{", red),
            TextRun("Na"),
            TextRun("me}}!", bold));

        using DocumentVerifier verifier = new DocumentVerifier(
            Process(paragraph, new Dictionary<string, object> { ["Name"] = "Al" }));

        Paragraph output = verifier.GetParagraph(0);
        Assert.Equal("Hi Al!", output.InnerText);
        Assert.Equal("FF0000", RunContaining(output, "Al").RunProperties?.Color?.Val?.Value);
        Assert.NotNull(RunContaining(output, "!").RunProperties?.Bold);
    }
}
