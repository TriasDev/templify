// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using TriasDev.Templify.OpenDocument;
using TriasDev.Templify.Tests.Helpers;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Tests.Odt;

/// <summary>
/// Unit tests for <see cref="OdtParagraphTextModel"/> and <see cref="OdtParagraphTextRewriter"/>.
/// </summary>
public sealed class OdtParagraphTextRewriterTests
{
    private static readonly XNamespace _text = OdtDocumentVerifier.Text;

    private static XElement Paragraph(string innerXml) =>
        XElement.Parse($"<text:p {OdtDocumentBuilder.NamespaceDeclarations}>{innerXml}</text:p>", LoadOptions.PreserveWhitespace);

    [Fact]
    public void Model_IncludesSpacesTabsAndLineBreaks_ExcludesFieldsNotesAndFrames()
    {
        XElement paragraph = Paragraph(
            "a<text:s text:c=\"3\"/>b<text:tab/>c<text:line-break/>d<text:span>e<text:a>f</text:a></text:span>" +
            "<text:date>2026</text:date><text:note><text:note-body><text:p>note</text:p></text:note-body></text:note>" +
            "<draw:frame><draw:text-box><text:p>box</text:p></draw:text-box></draw:frame>g");

        OdtParagraphTextModel model = OdtParagraphTextModel.Build(paragraph);

        Assert.Equal("a   b\tc\nde" + "fg", model.Text);
        Assert.Equal(3, model.Anchors.Count);
    }

    [Fact]
    public void Model_MapsControlWhitespaceInTextNodesToSpaces_OneToOne()
    {
        OdtParagraphTextModel model = OdtParagraphTextModel.Build(Paragraph("{{#if\n\tA}}"));

        Assert.Equal("{{#if  A}}", model.Text);
    }

    [Fact]
    public void Replace_PartOfSpacesElement_KeepsRemainingCount()
    {
        XElement paragraph = Paragraph("x<text:s text:c=\"4\"/>y");

        // Remove the 2nd and 3rd of the four spaces (offsets 2 and 3).
        OdtParagraphTextRewriter.Replace(paragraph, 2, 4, ReplacementContent.Empty);

        Assert.Equal("x  y", OdtParagraphTextModel.GetText(paragraph));
        Assert.Equal("x  y", OdtDocumentVerifier.RenderText(paragraph));
    }

    [Fact]
    public void Replace_RangeContainingFieldAndFrame_RemovesThem_KeepsBookmarkAndAnnotation()
    {
        XElement paragraph = Paragraph(
            "keep [<text:date>d</text:date><text:bookmark text:name=\"b\"/><draw:frame/>" +
            "<office:annotation><text:p>c</text:p></office:annotation>remove] keep");
        string text = OdtParagraphTextModel.GetText(paragraph);
        int start = text.IndexOf('[', StringComparison.Ordinal);
        int end = text.IndexOf(']', StringComparison.Ordinal) + 1;

        OdtParagraphTextRewriter.Replace(paragraph, start, end, ReplacementContent.FromText("X"));

        Assert.Equal("keep X keep", OdtParagraphTextModel.GetText(paragraph));
        Assert.Empty(paragraph.Elements(_text + "date"));
        Assert.Empty(paragraph.Elements(OdtDocumentVerifier.Draw + "frame"));
        Assert.Single(paragraph.Elements(_text + "bookmark"));
        Assert.Single(paragraph.Elements(OdtDocumentVerifier.Office + "annotation"));
    }

    [Fact]
    public void Replace_AnchorsAtRangeBoundaries_AreKept()
    {
        XElement paragraph = Paragraph("<text:date>d</text:date>{{A}}<text:page-number>1</text:page-number>");

        OdtParagraphTextRewriter.Replace(paragraph, 0, 5, ReplacementContent.FromText("x"));

        Assert.Single(paragraph.Elements(_text + "date"));
        Assert.Single(paragraph.Elements(_text + "page-number"));
        Assert.Equal("x", OdtParagraphTextModel.GetText(paragraph));
    }

    [Fact]
    public void Replace_RangeSpanningSpansTabsAndLineBreaks_RemovesCoveredContent()
    {
        XElement paragraph = Paragraph("a<text:span text:style-name=\"T1\">b<text:tab/>c</text:span><text:line-break/><text:span>d</text:span>e");

        OdtParagraphTextRewriter.Replace(paragraph, 1, 6, ReplacementContent.FromText("X"));

        Assert.Equal("aXe", OdtParagraphTextModel.GetText(paragraph));
        XElement span = Assert.Single(paragraph.Elements(_text + "span"));
        Assert.Equal("T1", (string?)span.Attribute(_text + "style-name"));
        Assert.Equal("X", span.Value);
    }

    [Fact]
    public void Replace_InvalidRange_ReturnsFalse()
    {
        XElement paragraph = Paragraph("abc");

        Assert.False(OdtParagraphTextRewriter.Replace(paragraph, 2, 2, ReplacementContent.Empty));
        Assert.False(OdtParagraphTextRewriter.Replace(paragraph, -1, 2, ReplacementContent.Empty));
        Assert.False(OdtParagraphTextRewriter.Replace(paragraph, 1, 4, ReplacementContent.Empty));
        Assert.Equal("abc", OdtParagraphTextModel.GetText(paragraph));
    }

    [Fact]
    public void Remove_MultipleRanges_RemovesEach()
    {
        XElement paragraph = Paragraph("{{#if A}}yes{{/if}}");

        OdtParagraphTextRewriter.Remove(paragraph, new[] { (0, 9), (12, 19) });

        Assert.Equal("yes", OdtParagraphTextModel.GetText(paragraph));
    }

    [Theory]
    [InlineData("a b", false, false, "a b")]
    [InlineData(" a", true, false, "<text:s/>a")]
    [InlineData(" a", false, false, " a")]
    [InlineData("a  b", false, false, "a <text:s/>b")]
    [InlineData("a    b", false, false, "a <text:s text:c=\"3\"/>b")]
    [InlineData("a ", false, true, "a<text:s/>")]
    [InlineData("a\tb", false, false, "a<text:tab/>b")]
    [InlineData("a\t b", false, false, "a<text:tab/> b")]
    public void Encode_ProducesRenderExactNodes(string value, bool preceded, bool followed, string expectedXml)
    {
        List<XNode> nodes = OdtParagraphTextRewriter.Encode(ReplacementContent.FromText(value), preceded, followed);

        XElement actual = Paragraph(string.Empty);
        actual.Add(nodes);
        Assert.Equal(Paragraph(expectedXml).ToString(SaveOptions.DisableFormatting), actual.ToString(SaveOptions.DisableFormatting));
    }

    [Fact]
    public void Encode_FormattedPieces_UseStyleLookup()
    {
        ReplacementContent content = ReplacementContent.FromValue("x **b** y", splitNewlines: false, parseMarkdown: true);

        List<XNode> nodes = OdtParagraphTextRewriter.Encode(content, false, false, piece => piece.IsBold ? "T9" : null);

        XElement paragraph = Paragraph(string.Empty);
        paragraph.Add(nodes);
        XElement span = Assert.Single(paragraph.Elements(_text + "span"));
        Assert.Equal("T9", (string?)span.Attribute(_text + "style-name"));
        Assert.Equal("b", span.Value);
        Assert.Equal("x b y", OdtDocumentVerifier.RenderText(paragraph));
    }
}
