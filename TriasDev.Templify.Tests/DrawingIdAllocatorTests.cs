// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Utilities;
using Dw = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using Pic = DocumentFormat.OpenXml.Drawing.Pictures;

namespace TriasDev.Templify.Tests;

public sealed class DrawingIdAllocatorTests
{
    private static WordprocessingDocument CreateDocument(MemoryStream stream, params OpenXmlElement[] content)
    {
        WordprocessingDocument document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document);
        MainDocumentPart main = document.AddMainDocumentPart();
        main.Document = new Document(new Body(content));
        return document;
    }

    [Fact]
    public void EnsureUniqueIds_DuplicateDocPr_RenumbersAndSyncsPictureId()
    {
        using MemoryStream stream = new MemoryStream();
        Drawing first = Helpers.DocumentBuilder.CreateInlineImage("rId1", 3);
        Drawing second = Helpers.DocumentBuilder.CreateInlineImage("rId1", 3);
        foreach (Drawing drawing in new[] { first, second })
        {
            drawing.Descendants<Pic.NonVisualDrawingProperties>().Single().Id = 3U;
        }

        using WordprocessingDocument document = CreateDocument(
            stream,
            new Paragraph(new Run(first)),
            new Paragraph(new Run(second)));

        DrawingIdAllocator.EnsureUniqueIds(document);

        Assert.Equal(3U, first.Descendants<Dw.DocProperties>().Single().Id!.Value);
        Assert.Equal(3U, first.Descendants<Pic.NonVisualDrawingProperties>().Single().Id!.Value);
        Assert.Equal(4U, second.Descendants<Dw.DocProperties>().Single().Id!.Value);
        Assert.Equal(4U, second.Descendants<Pic.NonVisualDrawingProperties>().Single().Id!.Value);
    }

    [Fact]
    public void EnsureUniqueIds_DuplicateOleShape_UpdatesShapeIdReference()
    {
        const string objectXml =
            "<w:r xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" " +
            "xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:o=\"urn:schemas-microsoft-com:office:office\" " +
            "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<w:object><v:shape id=\"_x0000_i1025\" style=\"width:10pt;height:10pt\"/>" +
            "<o:OLEObject Type=\"Embed\" ProgID=\"Package\" ShapeID=\"_x0000_i1025\" DrawAspect=\"Icon\" ObjectID=\"_1\" r:id=\"rId9\"/>" +
            "</w:object></w:r>";

        using MemoryStream stream = new MemoryStream();
        Run first = new Run(objectXml);
        Run second = new Run(objectXml);
        using WordprocessingDocument document = CreateDocument(stream, new Paragraph(first), new Paragraph(second));

        DrawingIdAllocator.EnsureUniqueIds(document);

        DocumentFormat.OpenXml.Vml.Shape shape = second.Descendants<DocumentFormat.OpenXml.Vml.Shape>().Single();
        DocumentFormat.OpenXml.Vml.Office.OleObject ole = second.Descendants<DocumentFormat.OpenXml.Vml.Office.OleObject>().Single();
        Assert.Equal("_x0000_i1025", first.Descendants<DocumentFormat.OpenXml.Vml.Shape>().Single().Id!.Value);
        Assert.NotEqual("_x0000_i1025", shape.Id!.Value);
        Assert.Equal(shape.Id!.Value, ole.ShapeId!.Value);
        Assert.Equal("rId9", ole.Id!.Value);
    }
}
