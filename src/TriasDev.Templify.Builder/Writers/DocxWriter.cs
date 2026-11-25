// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Security;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Builder.Model;

namespace TriasDev.Templify.Builder.Writers;

/// <summary>
/// Writes a <see cref="DocumentModel"/> as a WordprocessingML package (<c>.docx</c>).
/// </summary>
/// <remarks>
/// The OpenXML SDK's typed elements build the XML parts, which keeps the schema's child order. The package itself
/// is written here rather than by the SDK's packaging: that would give every build random relationship ids, a random
/// core-properties part name and the current time on every entry.
/// </remarks>
internal static class DocxWriter
{
    private const string MainContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";
    private const string CorePropertiesContentType = "application/vnd.openxmlformats-package.core-properties+xml";
    private const string OfficeDocumentRelationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument";
    private const string CorePropertiesRelationship = "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties";

    public static byte[] Write(DocumentModel model)
    {
        return DeterministicZip.Create(new[]
        {
            new PackagePart("[Content_Types].xml", Utf8(ContentTypes())),
            new PackagePart("_rels/.rels", Utf8(PackageRelationships())),
            new PackagePart("docProps/core.xml", Utf8(CoreProperties(model))),
            new PackagePart("word/document.xml", Utf8(XmlDeclaration + MainDocument(model).OuterXml)),
        });
    }

    private const string XmlDeclaration = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";

    private static string ContentTypes() =>
        XmlDeclaration
        + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
        + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
        + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
        + $"<Override PartName=\"/word/document.xml\" ContentType=\"{MainContentType}\"/>"
        + $"<Override PartName=\"/docProps/core.xml\" ContentType=\"{CorePropertiesContentType}\"/>"
        + "</Types>";

    private static string PackageRelationships() =>
        XmlDeclaration
        + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
        + $"<Relationship Id=\"rId1\" Type=\"{OfficeDocumentRelationship}\" Target=\"word/document.xml\"/>"
        + $"<Relationship Id=\"rId2\" Type=\"{CorePropertiesRelationship}\" Target=\"docProps/core.xml\"/>"
        + "</Relationships>";

    private static string CoreProperties(DocumentModel model) =>
        XmlDeclaration
        + "<cp:coreProperties"
        + " xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\""
        + " xmlns:dc=\"http://purl.org/dc/elements/1.1/\">"
        + $"<dc:language>{SecurityElement.Escape(model.Language.Name)}</dc:language>"
        + "</cp:coreProperties>";

    private static Document MainDocument(DocumentModel model)
    {
        Body body = new Body();
        foreach (Block block in model.Body)
        {
            body.Append(WriteBlock(block));
        }

        return new Document(body);
    }

    private static OpenXmlElement WriteBlock(Block block) => block switch
    {
        ParagraphBlock paragraph => WriteParagraph(paragraph),
        _ => throw new NotSupportedException($"Block {block.GetType().Name} is not supported by the DOCX writer."),
    };

    private static Paragraph WriteParagraph(ParagraphBlock block)
    {
        Paragraph paragraph = new Paragraph();
        foreach (Inline inline in block.Inlines)
        {
            paragraph.Append(WriteInline(inline));
        }

        return paragraph;
    }

    private static Run WriteInline(Inline inline)
    {
        switch (inline)
        {
            case TextInline text:
                return TextRun(text.Text);

            case ValueInline value:
                // One run per placeholder: the engine then never has to join a placeholder across runs.
                return new Run(new Text(TemplateSyntax.Placeholder(value.Path)) { Space = SpaceProcessingModeValues.Preserve });

            default:
                throw new NotSupportedException($"Inline {inline.GetType().Name} is not supported by the DOCX writer.");
        }
    }

    private static Run TextRun(string text)
    {
        Run run = new Run();
        foreach ((TextPieceKind kind, string piece) in TextPieces.Split(text))
        {
            run.Append(kind switch
            {
                TextPieceKind.Tab => new TabChar(),
                TextPieceKind.LineBreak => new Break(),
                _ => new Text(piece) { Space = SpaceProcessingModeValues.Preserve },
            });
        }

        return run;
    }

    private static byte[] Utf8(string xml) => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(xml);
}
