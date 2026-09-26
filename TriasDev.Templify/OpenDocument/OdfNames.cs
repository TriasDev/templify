// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

namespace TriasDev.Templify.OpenDocument;

/// <summary>
/// OpenDocument namespaces, element and attribute names, and media types.
/// </summary>
internal static class OdfNames
{
    /// <summary>Media type of an OpenDocument Text document (.odt).</summary>
    public const string TextMediaType = "application/vnd.oasis.opendocument.text";

    /// <summary>Media type of an OpenDocument Text template (.ott).</summary>
    public const string TextTemplateMediaType = "application/vnd.oasis.opendocument.text-template";

    public static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    public static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    public static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    public static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    public static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    public static readonly XNamespace Fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
    public static readonly XNamespace Meta = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";
    public static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    public static readonly XNamespace Manifest = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";
    public static readonly XNamespace LibreOfficeExtension = "urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0";

    // office:
    public static readonly XName OfficeBody = Office + "body";
    public static readonly XName OfficeText = Office + "text";
    public static readonly XName OfficeMasterStyles = Office + "master-styles";
    public static readonly XName OfficeAutomaticStyles = Office + "automatic-styles";
    public static readonly XName OfficeAnnotation = Office + "annotation";
    public static readonly XName OfficeAnnotationEnd = Office + "annotation-end";

    // text: blocks
    public static readonly XName Paragraph = Text + "p";
    public static readonly XName Heading = Text + "h";
    public static readonly XName List = Text + "list";
    public static readonly XName ListItem = Text + "list-item";
    public static readonly XName ListHeader = Text + "list-header";
    public static readonly XName Section = Text + "section";
    public static readonly XName NumberedParagraph = Text + "numbered-paragraph";
    public static readonly XName IndexBody = Text + "index-body";
    public static readonly XName IndexTitle = Text + "index-title";
    public static readonly XName Note = Text + "note";
    public static readonly XName NoteBody = Text + "note-body";

    // text: inline
    public static readonly XName Span = Text + "span";
    public static readonly XName Link = Text + "a";
    public static readonly XName TextMeta = Text + "meta";
    public static readonly XName Space = Text + "s";
    public static readonly XName SpaceCount = Text + "c";
    public static readonly XName Tab = Text + "tab";
    public static readonly XName LineBreak = Text + "line-break";
    public static readonly XName SoftPageBreak = Text + "soft-page-break";
    public static readonly XName Number = Text + "number";
    public static readonly XName StyleName = Text + "style-name";

    // table:
    public static readonly XName TableElement = Table + "table";
    public static readonly XName TableRow = Table + "table-row";
    public static readonly XName TableCell = Table + "table-cell";
    public static readonly XName CoveredTableCell = Table + "covered-table-cell";
    public static readonly XName TableHeaderRows = Table + "table-header-rows";
    public static readonly XName TableRows = Table + "table-rows";
    public static readonly XName TableRowGroup = Table + "table-row-group";

    // draw:
    public static readonly XName DrawFrame = Draw + "frame";
    public static readonly XName DrawTextBox = Draw + "text-box";

    // style:
    public static readonly XName MasterPage = Style + "master-page";

    // manifest:
    public static readonly XName ManifestFileEntry = Manifest + "file-entry";
    public static readonly XName ManifestFullPath = Manifest + "full-path";
    public static readonly XName ManifestMediaType = Manifest + "media-type";
    public static readonly XName ManifestEncryptionData = Manifest + "encryption-data";

    /// <summary>
    /// Index elements (table of contents and friends) whose <c>text:index-body</c> holds cached,
    /// generated paragraphs.
    /// </summary>
    public static readonly IReadOnlySet<XName> Indexes = new HashSet<XName>
    {
        Text + "table-of-content",
        Text + "illustration-index",
        Text + "table-index",
        Text + "object-index",
        Text + "user-index",
        Text + "alphabetical-index",
        Text + "bibliography",
    };

    /// <summary>Checks whether an element is a paragraph or a heading.</summary>
    public static bool IsParagraph(XElement element) =>
        element.Name == Paragraph || element.Name == Heading;
}
