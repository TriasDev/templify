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
/// An OpenDocument Text package (.odt or .ott): its entries in their original order, with the XML parts
/// that processing reads held in memory and written back only when they changed.
/// </summary>
/// <remarks>
/// <para>
/// Only the <c>mimetype</c> entry and the XML parts processing reads (<c>content.xml</c>, <c>styles.xml</c>,
/// <c>meta.xml</c> and the manifest) are inflated into memory, each up to a maximum size. All other entries
/// (pictures, embedded objects, settings) are streamed from the source package into the output when it is
/// saved, so memory use follows the compressed package size, not its inflated size.
/// </para>
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

    /// <summary>
    /// Default upper bound for the inflated size of each XML part that is loaded (256 MB). Real parts are far
    /// smaller; the bound stops a crafted, highly compressed part from exhausting memory.
    /// </summary>
    public const long DefaultMaxXmlPartBytes = 256L * 1024 * 1024;

    /// <summary>Upper bound for the number of entries in a package (the classic ZIP limit).</summary>
    public const int MaxEntryCount = 65535;

    /// <summary>Upper bound for the <c>mimetype</c> entry; the media type is a short ASCII string.</summary>
    private const long MaxMimetypeBytes = 1024;

    private static readonly HashSet<string> _loadedParts = new HashSet<string>(StringComparer.Ordinal)
    {
        ContentEntry,
        StylesEntry,
        MetaEntry,
        ManifestEntry,
    };

    private static readonly XmlReaderSettings _readerSettings = new XmlReaderSettings
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreWhitespace = false,
        CloseInput = false,
    };

    private readonly Stream _source;
    private readonly List<PackageEntry> _entries;
    private readonly Dictionary<string, LoadedPart> _parts = new Dictionary<string, LoadedPart>(StringComparer.Ordinal);

    private OdtPackage(Stream source, List<PackageEntry> entries, string mediaType)
    {
        _source = source;
        _entries = entries;
        MediaType = mediaType;
    }

    /// <summary>Gets the media type of the source package (document or template).</summary>
    public string MediaType { get; }

    /// <summary>Gets whether the source package is a template (.ott).</summary>
    public bool IsTemplate => MediaType == OdfNames.TextTemplateMediaType;

    /// <summary>
    /// Reads a package from a stream. A seekable stream is read from position 0 and read again by
    /// <see cref="Save"/>, so it must stay open and unchanged until then; a non-seekable stream is buffered
    /// (compressed, as it is) in memory.
    /// </summary>
    /// <param name="stream">The package.</param>
    /// <param name="maxXmlPartBytes">Upper bound for the inflated size of each loaded XML part.</param>
    /// <exception cref="InvalidOdtPackageException">The stream is not a usable OpenDocument Text package.</exception>
    public static OdtPackage Open(Stream stream, long maxXmlPartBytes = DefaultMaxXmlPartBytes)
    {
        Stream source = stream;
        if (!stream.CanSeek)
        {
            MemoryStream buffer = new MemoryStream();
            stream.CopyTo(buffer);
            source = buffer;
        }

        List<PackageEntry> entries = new List<PackageEntry>();
        byte[]? mimetypeData = null;

        try
        {
            source.Position = 0;
            using ZipArchive archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count > MaxEntryCount)
            {
                throw new InvalidOdtPackageException(
                    $"Invalid document: the package has {archive.Entries.Count} entries, more than the maximum "
                    + $"supported number of {MaxEntryCount}.");
            }

            for (int index = 0; index < archive.Entries.Count; index++)
            {
                ZipArchiveEntry entry = archive.Entries[index];
                byte[]? data = null;
                if (entry.FullName == MimetypeEntry)
                {
                    mimetypeData ??= ReadBounded(entry, MaxMimetypeBytes);
                }
                else if (_loadedParts.Contains(entry.FullName))
                {
                    data = ReadBounded(entry, maxXmlPartBytes);
                }

                entries.Add(new PackageEntry(entry.FullName, index, data, entry.LastWriteTime));
            }
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidOdtPackageException(
                "Invalid document: the template is not an OpenDocument Text package (.odt/.ott). "
                + "Flat OpenDocument (.fodt) is not supported.",
                ex);
        }

        string mediaType = DetermineMediaType(mimetypeData, entries);
        OdtPackage package = new OdtPackage(source, entries, mediaType);
        package.EnsureNotEncrypted();

        if (package.FindEntry(ContentEntry) == null)
        {
            throw new InvalidOdtPackageException("Invalid document: content.xml is missing.");
        }

        return package;
    }

    /// <summary>
    /// Gets an XML part, parsing it on first access; null if the package has no such entry.
    /// Only <c>content.xml</c>, <c>styles.xml</c>, <c>meta.xml</c>, the manifest and added parts are XML parts.
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

        if (entry.Data == null)
        {
            throw new InvalidOperationException($"The package entry {entryName} is not loaded as an XML part.");
        }

        XDocument document;
        try
        {
            document = LoadXml(entry.Data);
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
    /// Adds a new XML part (listed in the manifest with media type <c>text/xml</c>) and returns it for editing.
    /// </summary>
    public XDocument AddXml(string entryName, XDocument document)
    {
        _entries.Add(new PackageEntry(entryName, SourceIndex: -1, Array.Empty<byte>(), DateTimeOffset.Now));
        LoadedPart part = new LoadedPart(document) { IsChanged = true };
        document.Changed += (_, _) => part.IsChanged = true;
        _parts[entryName] = part;

        XElement? manifestRoot = FindEntry(ManifestEntry) != null ? GetXml(ManifestEntry)!.Root : null;
        bool listed = manifestRoot?.Elements(OdfNames.ManifestFileEntry)
            .Any(e => e.Attribute(OdfNames.ManifestFullPath)?.Value == entryName) ?? true;
        if (listed)
        {
            return document;
        }

        manifestRoot!.Add(new XElement(
            OdfNames.ManifestFileEntry,
            new XAttribute(OdfNames.ManifestFullPath, entryName),
            new XAttribute(OdfNames.ManifestMediaType, "text/xml")));

        return document;
    }

    /// <summary>
    /// Writes the package as an OpenDocument Text document (.odt) at the output's current position. A seekable
    /// output is truncated after the package, so no bytes of longer earlier content remain.
    /// </summary>
    public void Save(Stream output)
    {
        if (IsTemplate)
        {
            RewriteManifestMediaType(OdfNames.TextMediaType);
        }

        RemoveDocumentSignatures();

        // Build the package in memory: ZipArchive writes local headers without data descriptors only on a
        // seekable stream, and the output is written only once everything succeeded. Entries that are not
        // loaded are streamed from the source archive, so the buffer holds compressed data only.
        using MemoryStream buffer = new MemoryStream();
        _source.Position = 0;
        using (ZipArchive source = new ZipArchive(_source, ZipArchiveMode.Read, leaveOpen: true))
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
                byte[]? data = GetEntryData(entry);
                if (data != null)
                {
                    stream.Write(data);
                }
                else
                {
                    using Stream sourceStream = source.Entries[entry.SourceIndex].Open();
                    sourceStream.CopyTo(stream);
                }
            }
        }

        buffer.Position = 0;
        buffer.CopyTo(output);
        TruncateAfterPosition(output);
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

    /// <summary>
    /// Cuts off what follows the written package in a seekable output, e.g. the rest of a longer file opened with
    /// <see cref="File.OpenWrite(string)"/>.
    /// </summary>
    private static void TruncateAfterPosition(Stream output)
    {
        if (!output.CanSeek || output.Length <= output.Position)
        {
            return;
        }

        try
        {
            output.SetLength(output.Position);
        }
        catch (NotSupportedException)
        {
            // A seekable stream with a fixed length (e.g. a MemoryStream over a byte array): nothing to cut off with.
        }
    }

    private static void SetLastWriteTime(ZipArchiveEntry entry, DateTimeOffset time)
    {
        // ZIP (DOS) timestamps cover 1980-2107; out-of-range source times keep the default (now).
        if (time.Year is >= 1980 and <= 2107)
        {
            entry.LastWriteTime = time;
        }
    }

    /// <summary>
    /// Inflates an entry into memory; fails when its declared or actual size exceeds <paramref name="maxBytes"/>
    /// (the declared size in the ZIP header can be wrong, so the inflated bytes are counted as well).
    /// </summary>
    private static byte[] ReadBounded(ZipArchiveEntry entry, long maxBytes)
    {
        if (entry.Length > maxBytes)
        {
            throw CreateTooLargeException(entry.FullName, maxBytes);
        }

        using Stream input = entry.Open();
        using MemoryStream buffer = new MemoryStream((int)Math.Min(entry.Length, 1024 * 1024));
        byte[] chunk = new byte[81920];
        int read;
        while ((read = input.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                throw CreateTooLargeException(entry.FullName, maxBytes);
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static InvalidOdtPackageException CreateTooLargeException(string entryName, long maxBytes)
    {
        string limit = maxBytes >= 1024 * 1024 ? $"{maxBytes / (1024 * 1024)} MB" : $"{maxBytes} bytes";
        return new InvalidOdtPackageException(
            $"Invalid document: {entryName} exceeds the maximum supported size ({limit} uncompressed).");
    }

    private static XDocument LoadXml(byte[] data)
    {
        using MemoryStream input = new MemoryStream(data, writable: false);
        using XmlReader reader = XmlReader.Create(input, _readerSettings);
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    /// <summary>
    /// The bytes to write for an entry: the serialized part if it changed, else its loaded bytes; null for an
    /// entry that is copied from the source package.
    /// </summary>
    private byte[]? GetEntryData(PackageEntry entry)
    {
        if (!_parts.TryGetValue(entry.Name, out LoadedPart? part) || !part.IsChanged)
        {
            return entry.Data;
        }

        return Serialize(part.Document);
    }

    private PackageEntry? FindEntry(string name) =>
        _entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));

    private static string DetermineMediaType(byte[]? mimetypeData, List<PackageEntry> entries)
    {
        string? mediaType = mimetypeData != null ? Encoding.ASCII.GetString(mimetypeData).Trim() : null;

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
        byte[]? manifestData = entries.FirstOrDefault(e => e.Name == ManifestEntry)?.Data;
        if (manifestData == null)
        {
            return null;
        }

        try
        {
            return FindManifestRoot(LoadXml(manifestData))?.Attribute(OdfNames.ManifestMediaType)?.Value;
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

    /// <summary>
    /// An entry of the package. <paramref name="Data"/> holds the bytes of a loaded or added part; any other entry
    /// is copied from the source archive entry at <paramref name="SourceIndex"/> (-1 for added parts).
    /// </summary>
    private sealed record PackageEntry(string Name, int SourceIndex, byte[]? Data, DateTimeOffset LastWriteTime);

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
