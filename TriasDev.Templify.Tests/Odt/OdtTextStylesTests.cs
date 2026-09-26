// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using TriasDev.Templify.OpenDocument;
using TriasDev.Templify.Tests.Helpers;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Tests.Odt;

/// <summary>
/// <see cref="OdtTextStyles"/>: reuse, naming and placement of the automatic text styles for markdown.
/// </summary>
public sealed class OdtTextStylesTests
{
    private static readonly XNamespace _office = OdtDocumentVerifier.Office;
    private static readonly XNamespace _style = OdtDocumentVerifier.Style;
    private static readonly XNamespace _fo = OdtDocumentVerifier.Fo;

    private const string Bold =
        "fo:font-weight=\"bold\" style:font-weight-asian=\"bold\" style:font-weight-complex=\"bold\"";

    private static XDocument Content(string automaticStyles, string body = "") =>
        XDocument.Parse(
            $"<office:document-content {OdtDocumentBuilder.NamespaceDeclarations}>"
            + (automaticStyles == null ? string.Empty : $"<office:automatic-styles>{automaticStyles}</office:automatic-styles>")
            + $"<office:body><office:text>{body}</office:text></office:body></office:document-content>");

    private static List<XElement> TextStyles(XDocument part) =>
        part.Root!.Element(_office + "automatic-styles")!.Elements(_style + "style")
            .Where(s => (string?)s.Attribute(_style + "family") == "text").ToList();

    [Fact]
    public void PieceWithoutFormatting_OrPartWithoutRoot_HasNoStyle()
    {
        OdtTextStyles styles = new OdtTextStyles();

        Assert.Null(styles.GetStyleName(Content(string.Empty), new ReplacementPiece("plain")));
        Assert.Null(styles.GetStyleName(new XDocument(), new ReplacementPiece("b", IsBold: true)));
    }

    [Fact]
    public void ExistingStyleWithExactlyTheProperties_IsReused()
    {
        // LibreOffice writes bold as these three attributes; its own style is reused, whatever its name.
        XDocument part = Content($"<style:style style:name=\"T7\" style:family=\"text\"><style:text-properties {Bold}/></style:style>");

        string? name = new OdtTextStyles().GetStyleName(part, new ReplacementPiece("b", IsBold: true));

        Assert.Equal("T7", name);
        Assert.Single(TextStyles(part));
    }

    [Theory]
    [InlineData("<style:style style:name=\"T1\" style:family=\"text\" style:parent-style-name=\"Strong\"><style:text-properties " + Bold + "/></style:style>")]
    [InlineData("<style:style style:name=\"T1\" style:family=\"text\"><style:text-properties " + Bold + " fo:color=\"#ff0000\"/></style:style>")]
    [InlineData("<style:style style:name=\"T1\" style:family=\"text\"><style:text-properties fo:font-weight=\"bold\"/></style:style>")]
    [InlineData("<style:style style:name=\"T1\" style:family=\"text\"><style:text-properties " + Bold + "/><style:paragraph-properties/></style:style>")]
    [InlineData("<style:style style:name=\"T1\" style:family=\"text\"><style:text-properties " + Bold + "><style:tab-stops/></style:text-properties></style:style>")]
    [InlineData("<style:style style:name=\"T1\" style:family=\"paragraph\"><style:text-properties " + Bold + "/></style:style>")]
    [InlineData("<style:style style:name=\"\" style:family=\"text\"><style:text-properties " + Bold + "/></style:style>")]
    public void StyleThatIsNotExactlyTheMarkdownFormatting_IsNotReused(string existing)
    {
        XDocument part = Content(existing);

        string? name = new OdtTextStyles().GetStyleName(part, new ReplacementPiece("b", IsBold: true));

        Assert.False(string.IsNullOrEmpty(name));
        XElement created = TextStyles(part).Single(s => (string?)s.Attribute(_style + "name") == name);
        Assert.NotSame(part.Root!.Element(_office + "automatic-styles")!.Elements().First(), created);
        Assert.Equal(new[] { "name", "family" }, created.Attributes().Select(a => a.Name.LocalName));
    }

    [Fact]
    public void NewName_SkipsEveryStyleNameOfThePart_AndReservedNames()
    {
        // Names are unique across families and kinds: a list style or page layout named T2 blocks T2.
        XDocument part = Content(
            "<style:style style:name=\"T1\" style:family=\"text\"><style:text-properties fo:color=\"#000000\"/></style:style>"
            + "<text:list-style style:name=\"T2\"/>"
            + "<style:page-layout style:name=\"T3\"/>");
        OdtTextStyles styles = new OdtTextStyles();
        styles.ReserveNames(new[] { "T4", "T6" });

        string? bold = styles.GetStyleName(part, new ReplacementPiece("b", IsBold: true));
        string? italic = styles.GetStyleName(part, new ReplacementPiece("i", IsItalic: true));

        Assert.Equal("T5", bold);
        Assert.Equal("T7", italic);
    }

    [Fact]
    public void CombinedFormatting_IsOneStyle_WithAllProperties()
    {
        XDocument part = Content(string.Empty);

        string? name = new OdtTextStyles().GetStyleName(part, new ReplacementPiece("x", IsBold: true, IsItalic: true, IsStrikethrough: true));

        XElement properties = TextStyles(part).Single(s => (string?)s.Attribute(_style + "name") == name).Element(_style + "text-properties")!;
        Assert.Equal(
            new[]
            {
                "fo:font-weight=bold", "style:font-weight-asian=bold", "style:font-weight-complex=bold",
                "fo:font-style=italic", "style:font-style-asian=italic", "style:font-style-complex=italic",
                "style:text-line-through-style=solid", "style:text-line-through-type=single",
            },
            properties.Attributes().Select(a => $"{properties.GetPrefixOfNamespace(a.Name.Namespace)}:{a.Name.LocalName}={a.Value}"));
    }

    [Fact]
    public void SameFormatting_IsCachedPerPart_AndPartsGetTheirOwnStyles()
    {
        XDocument content = Content(string.Empty);
        XDocument styles = Content(string.Empty);
        OdtTextStyles textStyles = new OdtTextStyles();

        string? first = textStyles.GetStyleName(content, new ReplacementPiece("a", IsBold: true));
        string? second = textStyles.GetStyleName(content, new ReplacementPiece("b", IsBold: true));
        string? italic = textStyles.GetStyleName(content, new ReplacementPiece("c", IsItalic: true));
        string? otherPart = textStyles.GetStyleName(styles, new ReplacementPiece("d", IsBold: true));

        Assert.Equal("T1", first);
        Assert.Equal(first, second);
        Assert.Equal("T2", italic);
        Assert.Equal("T1", otherPart);
        Assert.Equal(2, TextStyles(content).Count);
        Assert.Single(TextStyles(styles));
    }

    [Fact]
    public void MissingAutomaticStyles_AreCreatedInSchemaOrder()
    {
        XDocument content = XDocument.Parse(
            $"<office:document-content {OdtDocumentBuilder.NamespaceDeclarations}><office:font-face-decls/><office:body><office:text/></office:body></office:document-content>");
        XDocument styles = XDocument.Parse(
            $"<office:document-styles {OdtDocumentBuilder.NamespaceDeclarations}><office:styles/><office:master-styles/></office:document-styles>");
        XDocument bare = XDocument.Parse($"<office:document-meta {OdtDocumentBuilder.NamespaceDeclarations}/>");
        OdtTextStyles textStyles = new OdtTextStyles();

        textStyles.GetStyleName(content, new ReplacementPiece("a", IsBold: true));
        textStyles.GetStyleName(styles, new ReplacementPiece("a", IsBold: true));
        textStyles.GetStyleName(bare, new ReplacementPiece("a", IsBold: true));

        Assert.Equal(new[] { "font-face-decls", "automatic-styles", "body" }, content.Root!.Elements().Select(e => e.Name.LocalName));
        Assert.Equal(new[] { "styles", "automatic-styles", "master-styles" }, styles.Root!.Elements().Select(e => e.Name.LocalName));
        Assert.Equal(new[] { "automatic-styles" }, bare.Root!.Elements().Select(e => e.Name.LocalName));
        Assert.Equal("bold", (string?)TextStyles(bare).Single().Element(_style + "text-properties")!.Attribute(_fo + "font-weight"));
    }
}
