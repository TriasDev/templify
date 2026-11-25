// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Xml.Linq;
using TriasDev.Templify.OpenDocument;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Odt;

public sealed class OdtTemplateLocatorTests
{
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

    [Fact]
    public void Odt_BodyParagraph() =>
        Assert.Equal("body, paragraph 2: 'x'", Describe(new OdtDocumentBuilder().AddParagraph("a").AddParagraph("x"), "x"));

    [Fact]
    public void Odt_Heading() =>
        Assert.Equal("body, paragraph 2: 'x'", Describe(new OdtDocumentBuilder().AddParagraph("a").AddHeading("x"), "x"));

    [Fact]
    public void Odt_TableCell() =>
        Assert.Equal("body, table 1, row 2, cell 1, paragraph 1: 'x'",
            Describe(new OdtDocumentBuilder().AddTable(["a", "b"], ["x", "c"]), "x"));

    [Fact]
    public void Odt_ListItem() =>
        Assert.Equal("body, list 1, item 3, paragraph 1: 'x'",
            Describe(new OdtDocumentBuilder().AddXml(
                "<text:list><text:list-item><text:p>a</text:p></text:list-item><text:list-item><text:p>b</text:p></text:list-item>" +
                "<text:list-item><text:p>x</text:p></text:list-item></text:list>"), "x"));

    [Fact]
    public void Odt_Header() =>
        Assert.Equal("header (Standard), paragraph 1: 'h'",
            Describe(new OdtDocumentBuilder().AddParagraph("b").AddHeaderParagraph("h"), "h"));

    [Fact]
    public void Odt_Footer() =>
        Assert.Equal("footer (Standard), paragraph 1: 'f'",
            Describe(new OdtDocumentBuilder().AddParagraph("b").AddFooterParagraph("f"), "f"));

    private static string Describe(OdtDocumentBuilder builder, string paragraphText)
    {
        using ZipArchive zip = new ZipArchive(new MemoryStream(builder.ToBytes()), ZipArchiveMode.Read);
        XElement content = XDocument.Load(zip.GetEntry("content.xml")!.Open()).Root!;
        XElement styles = XDocument.Load(zip.GetEntry("styles.xml")!.Open()).Root!;
        XElement paragraph = content.Descendants().Concat(styles.Descendants())
            .First(e => (e.Name == Text + "p" || e.Name == Text + "h") && e.Value == paragraphText);

        return new OdtTemplateLocator(content, styles).Describe(paragraph);
    }
}
