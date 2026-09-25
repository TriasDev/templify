// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;
using Dw = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using Vml = DocumentFormat.OpenXml.Vml;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Cloned loop content must not duplicate drawing object ids (wp:docPr/@id, VML shape ids),
/// which the OpenXML validator reports and which can trigger Word's repair prompt (issue #178).
/// </summary>
public sealed class DrawingIdUniquenessTests
{
    private const string Namespaces =
        "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" " +
        "xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\" " +
        "xmlns:wps=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\" " +
        "xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" " +
        "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" " +
        "xmlns:o=\"urn:schemas-microsoft-com:office:office\" " +
        "xmlns:v=\"urn:schemas-microsoft-com:vml\"";

    private static Dictionary<string, object> ThreeItems() => new Dictionary<string, object>
    {
        ["Items"] = new List<string> { "a", "b", "c" },
    };

    private static Paragraph P(string text) =>
        new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    private static Paragraph ImageParagraph(string relationshipId, uint id = 1) =>
        new Paragraph(new Run(new Text("{{.}}")), new Run(DocumentBuilder.CreateInlineImage(relationshipId, id)));

    private static DocumentVerifier Process(Action<DocumentBuilder> build, Dictionary<string, object> data)
    {
        DocumentBuilder builder = new DocumentBuilder();
        build(builder);

        MemoryStream output = new MemoryStream();
        ProcessingResult result = new DocumentTemplateProcessor().ProcessTemplate(builder.ToStream(), output, data);
        Assert.True(result.IsSuccess, result.ErrorMessage);

        DocumentVerifier verifier = new DocumentVerifier(output);
        List<string> errors = verifier.GetValidationErrors();
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
        return verifier;
    }

    private static IEnumerable<OpenXmlPartRootElement> Roots(WordprocessingDocument document)
    {
        MainDocumentPart main = document.MainDocumentPart!;
        yield return main.Document!;
        foreach (HeaderPart header in main.HeaderParts)
        {
            yield return header.Header!;
        }

        foreach (FooterPart footer in main.FooterParts)
        {
            yield return footer.Footer!;
        }
    }

    private static List<uint> DocPrIds(WordprocessingDocument document) =>
        Roots(document).SelectMany(r => r.Descendants<Dw.DocProperties>()).Select(d => d.Id!.Value).ToList();

    private static void AssertUnique<T>(List<T> values, int expectedCount)
    {
        Assert.Equal(expectedCount, values.Count);
        Assert.Equal(values.Count, values.Distinct().Count());
    }

    [Fact]
    public void ImageInBodyLoop_ClonesGetUniqueDocPrIds()
    {
        using DocumentVerifier verifier = Process(
            builder =>
            {
                string rel = builder.AddImagePart();
                builder.AddElement(ImageParagraph(rel, 1));
                builder.AddParagraph("{{#foreach Items}}");
                builder.AddElement(ImageParagraph(rel, 2));
                builder.AddParagraph("{{/foreach}}");
            },
            ThreeItems());

        List<uint> ids = DocPrIds(verifier.Document);
        AssertUnique(ids, 4);

        // Ids that were already unique keep their value; the first clone keeps the original id.
        Assert.Equal(1U, ids[0]);
        Assert.Equal(2U, ids[1]);

        // Clones still share the same image relationship.
        Body body = verifier.Document.MainDocumentPart!.Document!.Body!;
        List<string?> embeds = body.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().Select(b => b.Embed?.Value).ToList();
        Assert.Single(embeds.Distinct());
    }

    [Fact]
    public void ImageInTableRowLoop_ClonesGetUniqueDocPrIds()
    {
        using DocumentVerifier verifier = Process(
            builder =>
            {
                string rel = builder.AddImagePart();
                builder.AddElement(new Table(
                    new TableProperties(new TableWidth { Width = "0", Type = TableWidthUnitValues.Auto }),
                    new TableGrid(new GridColumn { Width = "4000" }),
                    new TableRow(new TableCell(P("{{#foreach Items}}"))),
                    new TableRow(new TableCell(ImageParagraph(rel))),
                    new TableRow(new TableCell(P("{{/foreach}}")))));
            },
            ThreeItems());

        AssertUnique(DocPrIds(verifier.Document), 3);
    }

    [Fact]
    public void DrawingMlTextBoxInLoop_ClonesGetUniqueIds()
    {
        const string textBox =
            "<w:r " + Namespaces + ">" +
            "<mc:AlternateContent><mc:Choice Requires=\"wps\"><w:drawing>" +
            "<wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\">" +
            "<wp:extent cx=\"914400\" cy=\"457200\"/><wp:docPr id=\"5\" name=\"Text Box 5\"/>" +
            "<a:graphic><a:graphicData uri=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\">" +
            "<wps:wsp><wps:cNvSpPr txBox=\"1\"/>" +
            "<wps:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"914400\" cy=\"457200\"/></a:xfrm>" +
            "<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></wps:spPr>" +
            "<wps:txbx><w:txbxContent><w:p><w:r><w:t>{{.}}</w:t></w:r></w:p></w:txbxContent></wps:txbx><wps:bodyPr/></wps:wsp>" +
            "</a:graphicData></a:graphic></wp:inline></w:drawing></mc:Choice>" +
            "<mc:Fallback><w:pict><v:shape id=\"Text Box 5\" o:spid=\"_x0000_s1026\" style=\"width:72pt;height:36pt\">" +
            "<v:textbox><w:txbxContent><w:p><w:r><w:t>{{.}}</w:t></w:r></w:p></w:txbxContent></v:textbox></v:shape></w:pict></mc:Fallback>" +
            "</mc:AlternateContent></w:r>";

        using DocumentVerifier verifier = Process(
            builder =>
            {
                builder.AddParagraph("{{#foreach Items}}");
                builder.AddElement(new Paragraph(new Run(textBox)));
                builder.AddParagraph("{{/foreach}}");
            },
            ThreeItems());

        AssertUnique(DocPrIds(verifier.Document), 3);
        Body body = verifier.Document.MainDocumentPart!.Document!.Body!;
        List<Vml.Shape> shapes = body.Descendants<Vml.Shape>().ToList();
        AssertUnique(shapes.Select(s => s.Id!.Value).ToList(), 3);
        AssertUnique(shapes.Select(s => s.GetAttribute("spid", "urn:schemas-microsoft-com:office:office").Value).ToList(), 3);
        Assert.Equal(new[] { "a", "b", "c" }, body.Descendants<Vml.Shape>().Select(s => s.InnerText));
    }

    [Fact]
    public void VmlTextBoxInLoop_ClonesGetUniqueShapeIds()
    {
        using DocumentVerifier verifier = Process(
            builder =>
            {
                builder.AddParagraph("{{#foreach Items}}");
                builder.AddElement(new Paragraph(new Run(new Picture(
                    new Vml.Shape(new Vml.TextBox(new TextBoxContent(P("{{.}}"))))
                    {
                        Id = "tb1",
                        Style = "width:72pt;height:36pt",
                    }))));
                builder.AddParagraph("{{/foreach}}");
            },
            ThreeItems());

        List<string> ids = verifier.Document.MainDocumentPart!.Document!.Body!
            .Descendants<Vml.Shape>().Select(s => s.Id!.Value!).ToList();
        AssertUnique(ids, 3);
        Assert.Equal("tb1", ids[0]);
    }

    [Fact]
    public void ImageInHeaderLoop_ClonesGetIdsUniqueAcrossDocument()
    {
        using DocumentVerifier verifier = Process(
            builder =>
            {
                string bodyRel = builder.AddImagePart();
                builder.AddElement(ImageParagraph(bodyRel, 1));
                builder.AddHeaderWithParagraphs(HeaderFooterValues.Default, "{{#foreach Items}}", "IMAGE", "{{/foreach}}");

                HeaderPart headerPart = builder.MainPart.HeaderParts.Single();
                ImagePart imagePart = headerPart.AddImagePart(ImagePartType.Png);
                using (MemoryStream png = new MemoryStream(Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==")))
                {
                    imagePart.FeedData(png);
                }

                Paragraph placeholder = headerPart.Header!.Elements<Paragraph>().Single(p => p.InnerText == "IMAGE");
                placeholder.InsertAfterSelf(ImageParagraph(headerPart.GetIdOfPart(imagePart), 1));
                placeholder.Remove();
                headerPart.Header.Save();
            },
            ThreeItems());

        AssertUnique(DocPrIds(verifier.Document), 4);
    }
}
