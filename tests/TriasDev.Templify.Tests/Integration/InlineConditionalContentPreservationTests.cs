// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Inline conditionals (and placeholders) must only rewrite text: hyperlinks, fields, breaks,
/// tabs, drawings, footnote references and bookmarks outside the removed text stay intact, and
/// non-text content inside a removed branch is removed with it (issue #143).
/// </summary>
public sealed class InlineConditionalContentPreservationTests
{
    private static Run TextRun(string text) =>
        new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

    private static Hyperlink Link(string text) => new Hyperlink(TextRun(text)) { Anchor = "target" };

    private static OpenXmlElement[] PageField() => new OpenXmlElement[]
    {
        new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
        new Run(new FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve }),
        new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
        TextRun("1"),
        new Run(new FieldChar { FieldCharType = FieldCharValues.End }),
    };

    private static DocumentVerifier Process(
        Func<DocumentBuilder, Paragraph> buildParagraph,
        Dictionary<string, object> data)
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddElement(buildParagraph(builder));

        MemoryStream output = new MemoryStream();
        ProcessingResult result = new DocumentTemplateProcessor().ProcessTemplate(builder.ToStream(), output, data);
        Assert.True(result.IsSuccess, result.ErrorMessage);

        DocumentVerifier verifier = new DocumentVerifier(output);
        Assert.Empty(verifier.GetValidationErrors());
        return verifier;
    }

    private static DocumentVerifier Process(Paragraph paragraph, Dictionary<string, object> data) =>
        Process(_ => paragraph, data);

    private static Dictionary<string, object> Show(bool show) => new Dictionary<string, object> { ["Show"] = show };

    // ---------- Confirming tests from the audit (#143) ----------

    [Fact]
    public void InlineConditional_WithHyperlink_KeepsOrderAndNoDuplication()
    {
        using DocumentVerifier verifier = Process(
            new Paragraph(
                TextRun("{{#if Show}}See {{/if}}"),
                Link("link"),
                TextRun(" end")),
            Show(true));

        Paragraph paragraph = verifier.GetParagraph(0);
        Assert.Equal("See link end", paragraph.InnerText);
        Hyperlink hyperlink = Assert.Single(paragraph.Elements<Hyperlink>());
        Assert.Equal("link", hyperlink.InnerText);
    }

    [Fact]
    public void InlineConditional_FieldCodeNotTurnedIntoText()
    {
        using DocumentVerifier verifier = Process(
            new Paragraph(new OpenXmlElement[] { TextRun("{{#if Show}}Page {{/if}}") }.Concat(PageField())),
            Show(true));

        Paragraph paragraph = verifier.GetParagraph(0);
        Assert.Equal("Page  PAGE 1", paragraph.InnerText);
        Assert.Equal(3, paragraph.Descendants<FieldChar>().Count());
        Assert.Single(paragraph.Descendants<FieldCode>());
        Assert.DoesNotContain(paragraph.Descendants<Text>(), t => t.Text.Contains("PAGE"));
    }

    [Fact]
    public void InlineConditional_PreservesLineBreakAndDrawingRuns()
    {
        using DocumentVerifier verifier = Process(
            builder => new Paragraph(
                TextRun("{{#if Show}}A{{/if}}"),
                new Run(new Break()),
                TextRun("B"),
                new Run(DocumentBuilder.CreateInlineImage(builder.AddImagePart()))),
            Show(true));

        Paragraph paragraph = verifier.GetParagraph(0);
        Assert.Equal("AB", paragraph.InnerText);
        Assert.Single(paragraph.Descendants<Break>());
        Assert.Single(paragraph.Descendants<Drawing>());
    }

    // ---------- Hyperlinks ----------

    [Theory]
    [InlineData(true, "Go to docs now")]
    [InlineData(false, "Go to  now")]
    public void InlineConditional_MarkersInsideHyperlink_RewritesHyperlinkText(bool show, string expected)
    {
        using DocumentVerifier verifier = Process(
            new Paragraph(TextRun("Go to "), Link("{{#if Show}}docs{{/if}}"), TextRun(" now")),
            Show(show));

        Paragraph paragraph = verifier.GetParagraph(0);
        Assert.Equal(expected, paragraph.InnerText);
        if (show)
        {
            Assert.Equal("docs", Assert.Single(paragraph.Elements<Hyperlink>()).InnerText);
        }
        else
        {
            // The hyperlink lost all of its text, so it is removed rather than left empty.
            Assert.Empty(paragraph.Elements<Hyperlink>());
        }
    }

    [Theory]
    [InlineData(true, "A link B", 1)]
    [InlineData(false, "A  B", 0)]
    public void InlineConditional_BranchContainingHyperlink_KeepsOrRemovesIt(bool show, string expected, int hyperlinks)
    {
        using DocumentVerifier verifier = Process(
            new Paragraph(TextRun("A {{#if Show}}"), Link("link"), TextRun("{{/if}} B")),
            Show(show));

        Paragraph paragraph = verifier.GetParagraph(0);
        Assert.Equal(expected, paragraph.InnerText);
        Assert.Equal(hyperlinks, paragraph.Elements<Hyperlink>().Count());
    }

    [Fact]
    public void InlineConditional_MarkersSplitBetweenRunAndHyperlink_Works()
    {
        using DocumentVerifier verifier = Process(
            new Paragraph(TextRun("{{#if Show}}Read "), Link("this{{/if}}"), TextRun("!")),
            Show(true));

        Paragraph paragraph = verifier.GetParagraph(0);
        Assert.Equal("Read this!", paragraph.InnerText);
        Assert.Equal("this", Assert.Single(paragraph.Elements<Hyperlink>()).InnerText);
    }

    [Fact]
    public void Placeholder_InsideHyperlink_ReplacedAndHyperlinkKept()
    {
        using DocumentVerifier verifier = Process(
            new Paragraph(TextRun("See "), Link("{{Label}}"), TextRun(".")),
            new Dictionary<string, object> { ["Label"] = "the manual" });

        Paragraph paragraph = verifier.GetParagraph(0);
        Assert.Equal("See the manual.", paragraph.InnerText);
        Assert.Equal("the manual", Assert.Single(paragraph.Elements<Hyperlink>()).InnerText);
    }

    [Fact]
    public void Placeholder_SplitBetweenRunAndHyperlink_ReplacedInFirstRun()
    {
        using DocumentVerifier verifier = Process(
            new Paragraph(TextRun("See {{Lab"), Link("el}} here")),
            new Dictionary<string, object> { ["Label"] = "X" });

        Paragraph paragraph = verifier.GetParagraph(0);
        Assert.Equal("See X here", paragraph.InnerText);
        Assert.Equal(" here", Assert.Single(paragraph.Elements<Hyperlink>()).InnerText);
    }

    [Fact]
    public void Placeholder_InHyperlinkWithMarkdown_RunsStayInsideHyperlink()
    {
        using DocumentVerifier verifier = Process(
            new Paragraph(Link("{{Label}}")),
            new Dictionary<string, object> { ["Label"] = "**bold** text" });

        Paragraph paragraph = verifier.GetParagraph(0);
        Hyperlink hyperlink = Assert.Single(paragraph.Elements<Hyperlink>());
        Assert.Equal("bold text", hyperlink.InnerText);
        Assert.Empty(paragraph.Elements<Run>());
        Assert.Contains(hyperlink.Elements<Run>(), r => r.RunProperties?.Bold != null);
    }

    // ---------- Fields ----------

    [Fact]
    public void InlineConditional_FalseBranchContainingField_RemovesWholeField()
    {
        using DocumentVerifier verifier = Process(
            new Paragraph(
                new OpenXmlElement[] { TextRun("A{{#if Show}}Page ") }
                    .Concat(PageField())
                    .Concat(new[] { TextRun("{{/if}}B") })),
            Show(false));

        Paragraph paragraph = verifier.GetParagraph(0);
        Assert.Equal("AB", paragraph.InnerText);
        Assert.Empty(paragraph.Descendants<FieldChar>());
        Assert.Empty(paragraph.Descendants<FieldCode>());
    }

    [Fact]
    public void InlineConditional_TrueBranchContainingField_KeepsFieldIntact()
    {
        using DocumentVerifier verifier = Process(
            new Paragraph(
                new OpenXmlElement[] { TextRun("A{{#if Show}}Page ") }
                    .Concat(PageField())
                    .Concat(new[] { TextRun("{{/if}}B") })),
            Show(true));

        Paragraph paragraph = verifier.GetParagraph(0);
        Assert.Equal("APage  PAGE 1B", paragraph.InnerText);
        Assert.Equal(3, paragraph.Descendants<FieldChar>().Count());
        Assert.Equal(" PAGE ", Assert.Single(paragraph.Descendants<FieldCode>()).Text);
    }

    [Fact]
    public void InlineConditional_FieldOutsideFalseBranch_IsKept()
    {
        using DocumentVerifier verifier = Process(
            new Paragraph(
                new OpenXmlElement[] { TextRun("Page ") }
                    .Concat(PageField())
                    .Concat(new[] { TextRun("{{#if Show}} of many{{/if}}") })),
            Show(false));

        Paragraph paragraph = verifier.GetParagraph(0);
        Assert.Equal(3, paragraph.Descendants<FieldChar>().Count());
        Assert.Single(paragraph.Descendants<FieldCode>());
        Assert.DoesNotContain("many", paragraph.InnerText);
    }

    // ---------- Breaks, tabs, drawings ----------

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void InlineConditional_BreakInsideBranch_FollowsBranch(bool show, int breaks)
    {
        using DocumentVerifier verifier = Process(
            new Paragraph(TextRun("A{{#if Show}}B"), new Run(new Break()), TextRun("C{{/if}}D")),
            Show(show));

        Assert.Equal(breaks, verifier.GetParagraph(0).Descendants<Break>().Count());
    }

    [Theory]
    [InlineData(true, "AimgB", 1)]
    [InlineData(false, "AB", 0)]
    public void InlineConditional_ImageInsideBranch_FollowsBranch(bool show, string expected, int drawings)
    {
        using DocumentVerifier verifier = Process(
            builder => new Paragraph(
                TextRun("A{{#if Show}}img"),
                new Run(DocumentBuilder.CreateInlineImage(builder.AddImagePart())),
                TextRun("{{/if}}B")),
            Show(show));

        Paragraph paragraph = verifier.GetParagraph(0);
        Assert.Equal(expected, paragraph.InnerText);
        Assert.Equal(drawings, paragraph.Descendants<Drawing>().Count());
    }

    [Fact]
    public void InlineConditional_ImageOutsideFalseBranch_IsKept()
    {
        using DocumentVerifier verifier = Process(
            builder => new Paragraph(
                new Run(DocumentBuilder.CreateInlineImage(builder.AddImagePart(), 1)),
                TextRun("{{#if Show}}caption{{/if}}"),
                new Run(DocumentBuilder.CreateInlineImage(builder.AddImagePart(), 2))),
            Show(false));

        Paragraph paragraph = verifier.GetParagraph(0);
        Assert.Equal(string.Empty, paragraph.InnerText);
        Assert.Equal(2, paragraph.Descendants<Drawing>().Count());
    }

    [Fact]
    public void InlineConditional_ImageInSameRunAsMarkers_IsKeptWhenBranchIsTrue()
    {
        using DocumentVerifier verifier = Process(
            builder => new Paragraph(
                new Run(
                    new Text("{{#if Show}}"),
                    DocumentBuilder.CreateInlineImage(builder.AddImagePart()),
                    new Text("{{/if}}"))),
            Show(true));

        Assert.Single(verifier.GetParagraph(0).Descendants<Drawing>());
    }

    // ---------- Footnote references and bookmarks ----------

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void InlineConditional_FootnoteReferenceInsideBranch_FollowsBranch(bool show, int references)
    {
        using DocumentVerifier verifier = Process(
            builder =>
            {
                AddFootnotes(builder.MainPart);
                return new Paragraph(
                    TextRun("Claim{{#if Show}} (source)"),
                    new Run(new FootnoteReference { Id = 1 }),
                    TextRun("{{/if}}."));
            },
            Show(show));

        Assert.Equal(references, verifier.GetParagraph(0).Descendants<FootnoteReference>().Count());
    }

    [Fact]
    public void InlineConditional_FalseBranchContainingBookmark_KeepsBookmark()
    {
        using DocumentVerifier verifier = Process(
            new Paragraph(
                TextRun("A{{#if Show}}x"),
                new BookmarkStart { Id = "0", Name = "target" },
                TextRun("y"),
                new BookmarkEnd { Id = "0" },
                TextRun("z{{/if}}B")),
            Show(false));

        Paragraph paragraph = verifier.GetParagraph(0);
        Assert.Equal("AB", paragraph.InnerText);
        Assert.Single(paragraph.Elements<BookmarkStart>());
        Assert.Single(paragraph.Elements<BookmarkEnd>());
    }

    private static void AddFootnotes(MainDocumentPart mainPart)
    {
        FootnotesPart footnotesPart = mainPart.AddNewPart<FootnotesPart>();
        footnotesPart.Footnotes = new Footnotes(
            new Footnote(new Paragraph(new Run(new SeparatorMark()))) { Type = FootnoteEndnoteValues.Separator, Id = -1 },
            new Footnote(new Paragraph(new Run(new ContinuationSeparatorMark()))) { Type = FootnoteEndnoteValues.ContinuationSeparator, Id = 0 },
            new Footnote(new Paragraph(TextRun("Source."))) { Id = 1 });
    }
}
