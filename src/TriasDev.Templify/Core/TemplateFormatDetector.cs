// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using TriasDev.Templify.OpenDocument;

namespace TriasDev.Templify.Core;

/// <summary>
/// Detects the format of a template package from its content, not from a file name.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>OpenDocument: the <c>mimetype</c> entry (with a fallback to the root entry of
/// <c>META-INF/manifest.xml</c>, as <see cref="OdtPackage"/> does) names the text or text-template media type.</item>
/// <item>Word: <c>[Content_Types].xml</c> declares a WordprocessingML main document part
/// (document, template, macro-enabled document or macro-enabled template).</item>
/// </list>
/// Only the ZIP central directory and these small entries are read.
/// </remarks>
internal static class TemplateFormatDetector
{
    private const string ContentTypesEntry = "[Content_Types].xml";

    /// <summary>Upper bound for the entries read during detection; real ones are far smaller.</summary>
    private const int MaxEntryBytes = 1024 * 1024;

    private static readonly XNamespace _contentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly XNamespace _manifest = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";

    private static readonly HashSet<string> _wordMainContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml",
        "application/vnd.ms-word.document.macroEnabled.main+xml",
        "application/vnd.ms-word.template.macroEnabledTemplate.main+xml",
    };

    private static readonly XmlReaderSettings _readerSettings = new XmlReaderSettings
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        CloseInput = false,
    };

    /// <summary>
    /// Detects the format of the package in a readable, seekable stream. The stream is read from its start;
    /// its position is not restored (callers do that).
    /// </summary>
    /// <param name="stream">The package.</param>
    /// <param name="odfMediaType">The OpenDocument media type found, if any (also when it is not a text type).</param>
    public static TemplateFormat Detect(Stream stream, out string? odfMediaType)
    {
        odfMediaType = null;

        try
        {
            stream.Position = 0;
            using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

            odfMediaType = ReadOdfMediaType(archive);
            if (odfMediaType != null)
            {
                // Media types are case-insensitive (RFC 2045), as in OdtPackage.
                return string.Equals(odfMediaType, OdfNames.TextMediaType, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(odfMediaType, OdfNames.TextTemplateMediaType, StringComparison.OrdinalIgnoreCase)
                    ? TemplateFormat.Odt
                    : TemplateFormat.Unknown;
            }

            return IsWordprocessingPackage(archive) ? TemplateFormat.Docx : TemplateFormat.Unknown;
        }
        catch (InvalidDataException)
        {
            // Not a ZIP archive (empty, random bytes, legacy .doc, flat .fodt) or a corrupt entry.
            return TemplateFormat.Unknown;
        }
    }

    /// <summary>
    /// The message for a template whose format is not supported.
    /// </summary>
    public static string GetUnsupportedFormatMessage(string? odfMediaType)
    {
        string message = "Unsupported template format: the template is neither a Word document (.docx) nor an "
            + "OpenDocument Text document (.odt/.ott).";

        return odfMediaType != null
            ? $"{message} Found the OpenDocument media type '{odfMediaType}'."
            : $"{message} Legacy Word (.doc) and flat OpenDocument (.fodt) files are not supported.";
    }

    private static string? ReadOdfMediaType(ZipArchive archive)
    {
        ZipArchiveEntry? mimetype = archive.GetEntry(OdtPackage.MimetypeEntry);
        if (mimetype != null)
        {
            byte[]? data = ReadEntry(mimetype);
            string mediaType = data != null ? Encoding.ASCII.GetString(data).Trim() : string.Empty;
            if (mediaType.Length > 0)
            {
                return mediaType;
            }
        }

        ZipArchiveEntry? manifest = archive.GetEntry(OdtPackage.ManifestEntry);
        XDocument? document = manifest != null ? LoadXml(manifest) : null;
        return document?.Root?
            .Elements(_manifest + "file-entry")
            .FirstOrDefault(e => e.Attribute(_manifest + "full-path")?.Value == "/")?
            .Attribute(_manifest + "media-type")?.Value;
    }

    private static bool IsWordprocessingPackage(ZipArchive archive)
    {
        ZipArchiveEntry? entry = archive.GetEntry(ContentTypesEntry);
        XDocument? contentTypes = entry != null ? LoadXml(entry) : null;
        if (contentTypes?.Root == null)
        {
            return false;
        }

        return contentTypes.Root.Elements()
            .Where(e => e.Name == _contentTypes + "Override" || e.Name == _contentTypes + "Default")
            .Select(e => e.Attribute("ContentType")?.Value)
            .Any(type => type != null && _wordMainContentTypes.Contains(type));
    }

    private static XDocument? LoadXml(ZipArchiveEntry entry)
    {
        byte[]? data = ReadEntry(entry);
        if (data == null)
        {
            return null;
        }

        try
        {
            using MemoryStream input = new MemoryStream(data, writable: false);
            using XmlReader reader = XmlReader.Create(input, _readerSettings);
            return XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    /// <summary>Reads an entry, or returns null when it is larger than detection needs.</summary>
    private static byte[]? ReadEntry(ZipArchiveEntry entry)
    {
        if (entry.Length > MaxEntryBytes)
        {
            return null;
        }

        using Stream entryStream = entry.Open();
        using MemoryStream buffer = new MemoryStream();
        entryStream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
