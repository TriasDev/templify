// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Tests.Utilities;

public sealed class ParagraphTextRewriterTests
{
    private static Run TextRun(string text, RunProperties? properties = null)
    {
        Run run = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        if (properties != null)
        {
            run.RunProperties = properties;
        }

        return run;
    }

    private static RunProperties Red() => new RunProperties(new Color { Val = "FF0000" });

    private static RunProperties Bold() => new RunProperties(new Bold());

    /// <summary>
    /// Serializes the paragraph content as a compact token list, e.g. "[Hi ]{tab}[x]".
    /// Text runs with a color are prefixed with the color value.
    /// </summary>
    private static string Describe(Paragraph paragraph)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (OpenXmlElement element in paragraph.Descendants())
        {
            switch (element)
            {
                case Text text when text.Parent is Run run:
                    string color = run.RunProperties?.Color?.Val?.Value is string c ? c + ":" : string.Empty;
                    string bold = run.RunProperties?.Bold != null ? "b:" : string.Empty;
                    sb.Append('[').Append(color).Append(bold).Append(text.Text).Append(']');
                    break;
                case TabChar:
                    sb.Append("{tab}");
                    break;
                case Break:
                    sb.Append("{br}");
                    break;
                case Hyperlink:
                    sb.Append("{link}");
                    break;
            }
        }

        return sb.ToString();
    }

    [Fact]
    public void Model_ConcatenatesOwnRunText_IncludingHyperlinks()
    {
        Paragraph paragraph = new Paragraph(
            new ParagraphProperties(),
            TextRun("A"),
            new Hyperlink(TextRun("B")) { Anchor = "x" },
            new BookmarkStart { Id = "1", Name = "bm" },
            TextRun("C"));

        ParagraphTextModel model = ParagraphTextModel.Build(paragraph);

        Assert.Equal("ABC", model.Text);
        Assert.Equal(3, model.Segments.Count);
        ParagraphContentAnchor bookmark = Assert.Single(model.Anchors);
        Assert.IsType<BookmarkStart>(bookmark.Element);
        Assert.Equal(2, bookmark.Offset);
    }

    [Fact]
    public void Model_ExcludesFieldInstructionsAndNestedParagraphs()
    {
        DocumentFormat.OpenXml.Vml.TextBox textBox = new DocumentFormat.OpenXml.Vml.TextBox(
            new TextBoxContent(new Paragraph(TextRun("inside"))));
        Paragraph paragraph = new Paragraph(
            TextRun("A"),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
            new Run(new FieldCode(" PAGE ")),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
            TextRun("1"),
            new Run(new FieldChar { FieldCharType = FieldCharValues.End }),
            new Run(new Picture(new DocumentFormat.OpenXml.Vml.Shape(textBox))),
            TextRun("B"));

        ParagraphTextModel model = ParagraphTextModel.Build(paragraph);

        Assert.Equal("A1B", model.Text);
    }

    [Fact]
    public void Replace_WithinSingleRun_KeepsRunFormatting()
    {
        Paragraph paragraph = new Paragraph(TextRun("Hi {{Name}}!", Red()));

        bool replaced = ParagraphTextRewriter.Replace(paragraph, 3, 11, ReplacementContent.FromText("Alice"));

        Assert.True(replaced);
        Assert.Equal("[FF0000:Hi Alice!]", Describe(paragraph));
    }

    [Fact]
    public void Replace_AcrossRuns_UsesFirstRunFormattingAndKeepsSuffixRun()
    {
        Paragraph paragraph = new Paragraph(TextRun("Hi {{Na", Red()), TextRun("me}}!", Bold()));

        ParagraphTextRewriter.Replace(paragraph, 3, 11, ReplacementContent.FromText("Alice"));

        Assert.Equal("[FF0000:Hi Alice][b:!]", Describe(paragraph));
    }

    [Fact]
    public void Replace_WithLineBreaks_SplitsRunAndKeepsTrailingContentInOrder()
    {
        Paragraph paragraph = new Paragraph(
            new Run(
                new RunProperties(new Color { Val = "FF0000" }),
                new Text("x{{V}}y") { Space = SpaceProcessingModeValues.Preserve },
                new TabChar(),
                new Text("z")));

        ParagraphTextRewriter.Replace(paragraph, 1, 6, ReplacementContent.FromValue("a\nb", splitNewlines: true, parseMarkdown: false));

        Assert.Equal("[FF0000:x][FF0000:a]{br}[FF0000:b][FF0000:y]{tab}[FF0000:z]", Describe(paragraph));
    }

    [Fact]
    public void Replace_WithMarkdown_AppliesFormattingOnTopOfBase()
    {
        Paragraph paragraph = new Paragraph(TextRun("{{V}}", Red()));

        ParagraphTextRewriter.Replace(paragraph, 0, 5, ReplacementContent.FromValue("**a** b", splitNewlines: true, parseMarkdown: true));

        Assert.Equal("[FF0000:b:a][FF0000: b]", Describe(paragraph));
    }

    [Fact]
    public void Replace_WithEmpty_RemovesEmptiedRuns()
    {
        Paragraph paragraph = new Paragraph(TextRun("A"), TextRun("{{V}}"), TextRun("B"));

        ParagraphTextRewriter.Replace(paragraph, 1, 6, ReplacementContent.Empty);

        Assert.Equal("[A][B]", Describe(paragraph));
        Assert.Equal(2, paragraph.Elements<Run>().Count());
    }

    [Fact]
    public void Replace_InsideHyperlink_KeepsHyperlink()
    {
        Paragraph paragraph = new Paragraph(
            TextRun("See "),
            new Hyperlink(TextRun("{{Label}}")) { Anchor = "x" },
            TextRun(" end"));

        ParagraphTextRewriter.Replace(paragraph, 4, 13, ReplacementContent.FromText("docs"));

        Assert.Equal("[See ]{link}[docs][ end]", Describe(paragraph));
    }

    [Fact]
    public void Replace_KeepsTabsAtRangeBoundaries_AndRemovesTabsInside()
    {
        Paragraph paragraph = new Paragraph(
            TextRun("A"),
            new Run(new TabChar()),
            TextRun("{{"),
            new Run(new TabChar()),
            TextRun("V}}"),
            new Run(new TabChar()),
            TextRun("B"));

        ParagraphTextRewriter.Replace(paragraph, 1, 6, ReplacementContent.FromText("x"));

        Assert.Equal("[A]{tab}[x]{tab}[B]", Describe(paragraph));
    }

    [Fact]
    public void Remove_FieldCrossingRangeBoundary_KeepsFieldCharacters()
    {
        Paragraph paragraph = new Paragraph(
            TextRun("ab"),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
            new Run(new FieldCode(" PAGE ")),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
            TextRun("12"),
            new Run(new FieldChar { FieldCharType = FieldCharValues.End }),
            TextRun("cd"));

        // Removes "b1": starts before the field and ends inside its result.
        ParagraphTextRewriter.Remove(paragraph, new[] { (1, 3) });

        Assert.Equal(3, paragraph.Descendants<FieldChar>().Count());
        Assert.Single(paragraph.Descendants<FieldCode>());
        Assert.Equal("a2cd", ParagraphTextModel.GetText(paragraph));
    }

    [Fact]
    public void Remove_NestedFieldsInsideRange_RemovesAllFieldParts()
    {
        Paragraph paragraph = new Paragraph(
            TextRun("a["),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
            new Run(new FieldCode(" IF ")),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
            new Run(new FieldCode(" PAGE ")),
            new Run(new FieldChar { FieldCharType = FieldCharValues.End }),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
            TextRun("x"),
            new Run(new FieldChar { FieldCharType = FieldCharValues.End }),
            TextRun("]b"));

        ParagraphTextRewriter.Remove(paragraph, new[] { (1, 4) });

        Assert.Empty(paragraph.Descendants<FieldChar>());
        Assert.Empty(paragraph.Descendants<FieldCode>());
        Assert.Equal("[a][b]", Describe(paragraph));
    }

    [Fact]
    public void Replace_InvalidRange_ReturnsFalse()
    {
        Paragraph paragraph = new Paragraph(TextRun("abc"));

        Assert.False(ParagraphTextRewriter.Replace(paragraph, 2, 10, ReplacementContent.Empty));
        Assert.False(ParagraphTextRewriter.Replace(paragraph, 2, 2, ReplacementContent.Empty));
        Assert.Equal("[abc]", Describe(paragraph));
    }

    [Fact]
    public void Remove_MultipleRanges_AppliesAll()
    {
        Paragraph paragraph = new Paragraph(TextRun("a[x]b"), TextRun("[y]c", Red()));

        ParagraphTextRewriter.Remove(paragraph, new[] { (1, 4), (5, 8) });

        Assert.Equal("[ab][FF0000:c]", Describe(paragraph));
    }
}
