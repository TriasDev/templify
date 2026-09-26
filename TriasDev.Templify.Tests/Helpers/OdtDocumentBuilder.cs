// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Security;
using System.Text;

namespace TriasDev.Templify.Tests.Helpers;

/// <summary>
/// Builds minimal, valid OpenDocument Text packages (.odt, or .ott templates) in tests.
/// </summary>
/// <remarks>
/// Body and header/footer content is given as ODF XML fragments; the helpers below produce the common
/// ones. The fragments may use the prefixes <c>office</c>, <c>text</c>, <c>table</c>, <c>style</c>,
/// <c>fo</c>, <c>draw</c>, <c>svg</c>, <c>xlink</c>, <c>dc</c>, <c>meta</c> and <c>loext</c>. The automatic
/// text styles <c>T1</c> (bold) and <c>T2</c> (italic) and the paragraph style <c>P1</c> are predefined.
/// </remarks>
public sealed class OdtDocumentBuilder
{
    public const string TextMediaType = "application/vnd.oasis.opendocument.text";
    public const string TemplateMediaType = "application/vnd.oasis.opendocument.text-template";

    public const string NamespaceDeclarations =
        "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
        "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
        "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
        "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" " +
        "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
        "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" " +
        "xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" " +
        "xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
        "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" " +
        "xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" " +
        "xmlns:loext=\"urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0\"";

    private readonly StringBuilder _body = new StringBuilder();
    private readonly StringBuilder _header = new StringBuilder();
    private readonly StringBuilder _footer = new StringBuilder();
    private readonly List<(string Name, byte[] Data)> _extraEntries = new List<(string, byte[])>();
    private string _automaticStyles = string.Empty;
    private bool _isTemplate;
    private bool _mimetypeFirst = true;
    private bool _includeMimetype = true;

    /// <summary>Escapes text for use in XML content.</summary>
    public static string Escape(string text) => SecurityElement.Escape(text)!;

    /// <summary>Adds a paragraph with the given (escaped) text.</summary>
    public OdtDocumentBuilder AddParagraph(string text) =>
        AddXml($"<text:p text:style-name=\"P1\">{Escape(text)}</text:p>");

    /// <summary>Adds a heading with the given (escaped) text.</summary>
    public OdtDocumentBuilder AddHeading(string text, int level = 1) =>
        AddXml($"<text:h text:outline-level=\"{level}\">{Escape(text)}</text:h>");

    /// <summary>
    /// Adds a paragraph made of spans; a null style name adds the text directly to the paragraph.
    /// </summary>
    public OdtDocumentBuilder AddParagraphWithSpans(params (string Text, string? StyleName)[] spans)
    {
        StringBuilder xml = new StringBuilder("<text:p>");
        foreach ((string text, string? styleName) in spans)
        {
            xml.Append(styleName == null
                ? Escape(text)
                : $"<text:span text:style-name=\"{styleName}\">{Escape(text)}</text:span>");
        }

        xml.Append("</text:p>");
        return AddXml(xml.ToString());
    }

    /// <summary>Adds a table with one paragraph per cell.</summary>
    public OdtDocumentBuilder AddTable(params string[][] rows)
    {
        int columns = rows.Length == 0 ? 1 : rows.Max(r => r.Length);
        StringBuilder xml = new StringBuilder("<table:table table:name=\"Table1\">");
        xml.Append($"<table:table-column table:number-columns-repeated=\"{columns}\"/>");
        foreach (string[] row in rows)
        {
            xml.Append("<table:table-row>");
            foreach (string cell in row)
            {
                xml.Append($"<table:table-cell office:value-type=\"string\"><text:p>{Escape(cell)}</text:p></table:table-cell>");
            }

            xml.Append("</table:table-row>");
        }

        xml.Append("</table:table>");
        return AddXml(xml.ToString());
    }

    /// <summary>Adds a raw XML fragment to the body.</summary>
    public OdtDocumentBuilder AddXml(string xml)
    {
        _body.Append(xml);
        return this;
    }

    /// <summary>Adds a raw XML fragment to the standard master page's header.</summary>
    public OdtDocumentBuilder AddHeaderXml(string xml)
    {
        _header.Append(xml);
        return this;
    }

    /// <summary>Adds a paragraph to the standard master page's header.</summary>
    public OdtDocumentBuilder AddHeaderParagraph(string text) => AddHeaderXml($"<text:p>{Escape(text)}</text:p>");

    /// <summary>Adds a paragraph to the standard master page's footer.</summary>
    public OdtDocumentBuilder AddFooterParagraph(string text) => AddFooterXml($"<text:p>{Escape(text)}</text:p>");

    /// <summary>Adds a raw XML fragment to the standard master page's footer.</summary>
    public OdtDocumentBuilder AddFooterXml(string xml)
    {
        _footer.Append(xml);
        return this;
    }

    /// <summary>Adds extra automatic styles (raw XML) to content.xml.</summary>
    public OdtDocumentBuilder AddAutomaticStyles(string xml)
    {
        _automaticStyles += xml;
        return this;
    }

    /// <summary>Adds an extra package entry (listed in the manifest).</summary>
    public OdtDocumentBuilder AddEntry(string name, byte[] data)
    {
        _extraEntries.Add((name, data));
        return this;
    }

    /// <summary>Builds an OpenDocument template (.ott) instead of a document.</summary>
    public OdtDocumentBuilder AsTemplate()
    {
        _isTemplate = true;
        return this;
    }

    /// <summary>Writes the mimetype entry last (a malformed but readable package).</summary>
    public OdtDocumentBuilder WithMimetypeLast()
    {
        _mimetypeFirst = false;
        return this;
    }

    /// <summary>Omits the mimetype entry (the manifest still names the media type).</summary>
    public OdtDocumentBuilder WithoutMimetype()
    {
        _includeMimetype = false;
        return this;
    }

    /// <summary>Gets the content.xml that <see cref="ToBytes"/> writes.</summary>
    public string BuildContentXml() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        $"<office:document-content {NamespaceDeclarations} office:version=\"1.3\">" +
        "<office:automatic-styles>" +
        "<style:style style:name=\"P1\" style:family=\"paragraph\" style:parent-style-name=\"Standard\"/>" +
        "<style:style style:name=\"T1\" style:family=\"text\"><style:text-properties fo:font-weight=\"bold\"/></style:style>" +
        "<style:style style:name=\"T2\" style:family=\"text\"><style:text-properties fo:font-style=\"italic\"/></style:style>" +
        _automaticStyles +
        "</office:automatic-styles>" +
        "<office:body><office:text>" +
        "<text:sequence-decls><text:sequence-decl text:display-outline-level=\"0\" text:name=\"Table\"/></text:sequence-decls>" +
        _body +
        "</office:text></office:body></office:document-content>";

    /// <summary>Gets the styles.xml that <see cref="ToBytes"/> writes.</summary>
    public string BuildStylesXml()
    {
        string header = _header.Length > 0 ? $"<style:header>{_header}</style:header>" : string.Empty;
        string footer = _footer.Length > 0 ? $"<style:footer>{_footer}</style:footer>" : string.Empty;
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
               $"<office:document-styles {NamespaceDeclarations} office:version=\"1.3\">" +
               "<office:styles><style:style style:name=\"Standard\" style:family=\"paragraph\" style:class=\"text\"/></office:styles>" +
               "<office:automatic-styles><style:page-layout style:name=\"pm1\"/></office:automatic-styles>" +
               "<office:master-styles>" +
               $"<style:master-page style:name=\"Standard\" style:page-layout-name=\"pm1\">{header}{footer}</style:master-page>" +
               "</office:master-styles></office:document-styles>";
    }

    /// <summary>Builds the package.</summary>
    public byte[] ToBytes()
    {
        string mediaType = _isTemplate ? TemplateMediaType : TextMediaType;
        string meta = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                      $"<office:document-meta {NamespaceDeclarations} office:version=\"1.3\">" +
                      "<office:meta><meta:generator>Templify.Tests</meta:generator><dc:title>Test</dc:title></office:meta>" +
                      "</office:document-meta>";

        StringBuilder manifest = new StringBuilder(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.3\">" +
            $"<manifest:file-entry manifest:full-path=\"/\" manifest:version=\"1.3\" manifest:media-type=\"{mediaType}\"/>" +
            "<manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/>" +
            "<manifest:file-entry manifest:full-path=\"styles.xml\" manifest:media-type=\"text/xml\"/>" +
            "<manifest:file-entry manifest:full-path=\"meta.xml\" manifest:media-type=\"text/xml\"/>");
        foreach ((string name, _) in _extraEntries)
        {
            manifest.Append($"<manifest:file-entry manifest:full-path=\"{name}\" manifest:media-type=\"\"/>");
        }

        manifest.Append("</manifest:manifest>");

        using MemoryStream stream = new MemoryStream();
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            if (_includeMimetype && _mimetypeFirst)
            {
                AddEntry(archive, "mimetype", Encoding.ASCII.GetBytes(mediaType), CompressionLevel.NoCompression);
            }

            AddEntry(archive, "content.xml", Encoding.UTF8.GetBytes(BuildContentXml()));
            AddEntry(archive, "styles.xml", Encoding.UTF8.GetBytes(BuildStylesXml()));
            AddEntry(archive, "meta.xml", Encoding.UTF8.GetBytes(meta));
            foreach ((string name, byte[] data) in _extraEntries)
            {
                AddEntry(archive, name, data);
            }

            AddEntry(archive, "META-INF/manifest.xml", Encoding.UTF8.GetBytes(manifest.ToString()));

            if (_includeMimetype && !_mimetypeFirst)
            {
                AddEntry(archive, "mimetype", Encoding.ASCII.GetBytes(mediaType), CompressionLevel.Optimal);
            }
        }

        return stream.ToArray();
    }

    /// <summary>Builds the package as a readable stream positioned at the start.</summary>
    public MemoryStream ToStream() => new MemoryStream(ToBytes());

    private static void AddEntry(ZipArchive archive, string name, byte[] data, CompressionLevel level = CompressionLevel.Optimal)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, level);
        using Stream stream = entry.Open();
        stream.Write(data);
    }
}
