// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace TriasDev.Templify.OpenDocument;

/// <summary>
/// Thrown when a package is not a usable OpenDocument Text document. The message is user-facing.
/// </summary>
internal sealed class InvalidOdtPackageException : Exception
{
    public InvalidOdtPackageException(string message)
        : base(message)
    {
    }

    public InvalidOdtPackageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// An OpenDocument Text package (.odt or .ott) held in memory: its entries in their original
/// order, with XML parts loaded on demand and written back only when they changed.
/// </summary>
/// <remarks>
/// <para>
/// The written package always starts with the <c>mimetype</c> entry, stored (uncompressed) and
/// without an extra field, as ODF requires. A template (.ott) is written as a document (.odt):
/// its <c>mimetype</c> and the manifest's root media type are rewritten.
/// </para>
/// <para>
/// XML is loaded with white space preserved, DTD processing prohibited and no resolver, and is
/// written as UTF-8 without a byte order mark, without indentation, with line breaks entitized
/// so text content survives unchanged on every platform.
/// </para>
/// </remarks>
internal sealed class OdtPackage
{
    /// <summary>Name of the mimetype entry.</summary>
    public const string MimetypeEntry = "mimetype";

    /// <summary>Name of the content part.</summary>
    public const string ContentEntry = "content.xml";

    /// <summary>Name of the styles part.</summary>
    public const string StylesEntry = "styles.xml";

    /// <summary>Name of the meta part.</summary>
    public const string MetaEntry = "meta.xml";

    /// <summary>Name of the manifest part.</summary>
    public const string ManifestEntry = "META-INF/manifest.xml";

    /// <summary>Name of the document signature part, which processing invalidates.</summary>
    public const string DocumentSignaturesEntry = "META-INF/documentsignatures.xml";

    private static readonly XmlReaderSettings _readerSettings = new XmlReaderSettings
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreWhitespace = false,
        CloseInput = false,
    };

    private readonly List<PackageEntry> _entries;
    private readonly Dictionary<string, LoadedPart> _parts = new Dictionary<string, LoadedPart>(StringComparer.Ordinal);

    private OdtPackage(List<PackageEntry> entries, string mediaType)
    {
        _entries = entries;
        MediaType = mediaType;
    }

    /// <summary>Gets the media type of the source package (document or template).</summary>
    public string MediaType { get; }

    /// <summary>Gets whether the source package is a template (.ott).</summary>
    public bool IsTemplate => MediaType == OdfNames.TextTemplateMediaType;

    /// <summary>
    /// Reads a package from a stream.
    /// </summary>
    /// <exception cref="InvalidOdtPackageException">The stream is not an OpenDocument Text package.</exception>
    public static OdtPackage Open(Stream stream)
    {
        List<PackageEntry> entries = new List<PackageEntry>();

        try
        {
            using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                using Stream entryStream = entry.Open();
                using MemoryStream buffer = new MemoryStream();
                entryStream.CopyTo(buffer);
                entries.Add(new PackageEntry(entry.FullName, buffer.ToArray(), entry.LastWriteTime));
            }
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidOdtPackageException(
                "Invalid document: the template is not an OpenDocument Text package (.odt/.ott). "
                + "Flat OpenDocument (.fodt) is not supported.",
                ex);
        }

        string mediaType = DetermineMediaType(entries);
        OdtPackage package = new OdtPackage(entries, mediaType);
        package.EnsureNotEncrypted();

        if (package.FindEntry(ContentEntry) == null)
        {
            throw new InvalidOdtPackageException("Invalid document: content.xml is missing.");
        }

        return package;
    }

    /// <summary>
    /// Gets an XML part, loading it on first access; null if the package has no such entry.
    /// </summary>
    /// <exception cref="InvalidOdtPackageException">The part is not well-formed XML.</exception>
    public XDocument? GetXml(string entryName)
    {
        if (_parts.TryGetValue(entryName, out LoadedPart? loaded))
        {
            return loaded.Document;
        }

        PackageEntry? entry = FindEntry(entryName);
        if (entry == null)
        {
            return null;
        }

        XDocument document;
        try
        {
            using MemoryStream input = new MemoryStream(entry.Data, writable: false);
            using XmlReader reader = XmlReader.Create(input, _readerSettings);
            document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException ex)
        {
            throw new InvalidOdtPackageException($"Invalid document: {entryName} is not well-formed XML ({ex.Message}).", ex);
        }

        LoadedPart part = new LoadedPart(document);
        document.Changed += (_, _) => part.IsChanged = true;
        _parts.Add(entryName, part);
        return document;
    }

    /// <summary>
    /// Writes the package as an OpenDocument Text document (.odt).
    /// </summary>
    public void Save(Stream output)
    {
        if (IsTemplate)
        {
            RewriteManifestMediaType(OdfNames.TextMediaType);
        }

        RemoveDocumentSignatures();

        // Build in memory: ZipArchive writes local headers without data descriptors only on a
        // seekable stream, and the output is written only once everything succeeded.
        using MemoryStream buffer = new MemoryStream();
        using (ZipArchive archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            PackageEntry? sourceMimetype = FindEntry(MimetypeEntry);
            ZipArchiveEntry mimetype = archive.CreateEntry(MimetypeEntry, CompressionLevel.NoCompression);
            if (sourceMimetype != null)
            {
                SetLastWriteTime(mimetype, sourceMimetype.LastWriteTime);
            }

            using (Stream stream = mimetype.Open())
            {
                stream.Write(Encoding.ASCII.GetBytes(OdfNames.TextMediaType));
            }

            foreach (PackageEntry entry in _entries)
            {
                if (entry.Name == MimetypeEntry)
                {
                    continue;
                }

                ZipArchiveEntry target = archive.CreateEntry(entry.Name, CompressionLevel.Optimal);
                SetLastWriteTime(target, entry.LastWriteTime);
                using Stream stream = target.Open();
                stream.Write(GetEntryData(entry));
            }
        }

        buffer.Position = 0;
        buffer.CopyTo(output);
    }

    private static void SetLastWriteTime(ZipArchiveEntry entry, DateTimeOffset time)
    {
        // ZIP (DOS) timestamps cover 1980-2107; out-of-range source times keep the default (now).
        if (time.Year is >= 1980 and <= 2107)
        {
            entry.LastWriteTime = time;
        }
    }

    private byte[] GetEntryData(PackageEntry entry)
    {
        if (!_parts.TryGetValue(entry.Name, out LoadedPart? part) || !part.IsChanged)
        {
            return entry.Data;
        }

        return Serialize(part.Document);
    }

    /// <summary>
    /// Serializes an XML part as UTF-8 without BOM, unindented, with line breaks entitized.
    /// </summary>
    internal static byte[] Serialize(XDocument document)
    {
        XmlWriterSettings settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            NewLineHandling = NewLineHandling.Entitize,
            OmitXmlDeclaration = false,
        };

        using MemoryStream stream = new MemoryStream();
        using (XmlWriter writer = XmlWriter.Create(stream, settings))
        {
            document.Save(writer);
        }

        return stream.ToArray();
    }

    private PackageEntry? FindEntry(string name) =>
        _entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));

    private static string DetermineMediaType(List<PackageEntry> entries)
    {
        PackageEntry? mimetype = entries.FirstOrDefault(e => e.Name == MimetypeEntry);
        string? mediaType = mimetype != null ? Encoding.ASCII.GetString(mimetype.Data).Trim() : null;

        if (string.IsNullOrEmpty(mediaType))
        {
            // Some generators omit the mimetype entry; the manifest root entry names the media type too.
            mediaType = ReadManifestRootMediaType(entries);
        }

        if (mediaType != OdfNames.TextMediaType && mediaType != OdfNames.TextTemplateMediaType)
        {
            string found = string.IsNullOrEmpty(mediaType) ? "none" : $"'{mediaType}'";
            throw new InvalidOdtPackageException(
                $"Invalid document: the template is not an OpenDocument Text document (.odt/.ott); media type: {found}.");
        }

        return mediaType;
    }

    private static string? ReadManifestRootMediaType(List<PackageEntry> entries)
    {
        PackageEntry? manifestEntry = entries.FirstOrDefault(e => e.Name == ManifestEntry);
        if (manifestEntry == null)
        {
            return null;
        }

        try
        {
            using MemoryStream input = new MemoryStream(manifestEntry.Data, writable: false);
            using XmlReader reader = XmlReader.Create(input, _readerSettings);
            XDocument manifest = XDocument.Load(reader);
            return FindManifestRoot(manifest)?.Attribute(OdfNames.ManifestMediaType)?.Value;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static XElement? FindManifestRoot(XDocument manifest) =>
        manifest.Root?.Elements(OdfNames.ManifestFileEntry)
            .FirstOrDefault(e => e.Attribute(OdfNames.ManifestFullPath)?.Value == "/");

    private void EnsureNotEncrypted()
    {
        if (FindEntry(ManifestEntry) == null)
        {
            return;
        }

        XDocument manifest = GetXml(ManifestEntry)!;
        if (manifest.Descendants(OdfNames.ManifestEncryptionData).Any())
        {
            throw new InvalidOdtPackageException(
                "Invalid document: password-protected (encrypted) OpenDocument files are not supported.");
        }
    }

    private void RewriteManifestMediaType(string mediaType)
    {
        if (FindEntry(ManifestEntry) == null)
        {
            return;
        }

        XAttribute? attribute = FindManifestRoot(GetXml(ManifestEntry)!)?.Attribute(OdfNames.ManifestMediaType);
        if (attribute != null && attribute.Value != mediaType)
        {
            attribute.Value = mediaType;
        }
    }

    private void RemoveDocumentSignatures()
    {
        if (_entries.RemoveAll(e => e.Name == DocumentSignaturesEntry) == 0 || FindEntry(ManifestEntry) == null)
        {
            return;
        }

        XDocument manifest = GetXml(ManifestEntry)!;
        manifest.Root?.Elements(OdfNames.ManifestFileEntry)
            .Where(e => e.Attribute(OdfNames.ManifestFullPath)?.Value == DocumentSignaturesEntry)
            .Remove();
    }

    private sealed record PackageEntry(string Name, byte[] Data, DateTimeOffset LastWriteTime);

    private sealed class LoadedPart
    {
        public LoadedPart(XDocument document)
        {
            Document = document;
        }

        public XDocument Document { get; }

        public bool IsChanged { get; set; }
    }
}
