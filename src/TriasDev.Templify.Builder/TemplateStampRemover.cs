// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.Packaging;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Builder;

/// <summary>
/// Removes the builder's stamp from a document, typically a filled document that goes to an external recipient who
/// should not see internal template ids. Other document properties stay.
/// </summary>
public static class TemplateStampRemover
{
    /// <summary>Removes the stamp properties (names starting with <c>Templify.</c>) from a DOCX or ODT document, in place.</summary>
    /// <param name="document">The document; readable, writable and seekable. It is rewritten from position 0.</param>
    /// <returns><see langword="true"/> when the document had stamp properties and was rewritten.</returns>
    /// <exception cref="ArgumentException">The stream is not readable, writable and seekable.</exception>
    public static bool Remove(Stream document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!document.CanRead || !document.CanWrite || !document.CanSeek)
        {
            throw new ArgumentException("The document stream must be readable, writable and seekable.", nameof(document));
        }

        document.Position = 0;
        using MemoryStream original = new MemoryStream();
        document.CopyTo(original);
        byte[] bytes = original.ToArray();

        TemplateFormat format;
        using (MemoryStream detect = new MemoryStream(bytes, writable: false))
        {
            format = TemplateProcessor.DetectFormat(detect);
        }

        if (TemplateInspector.ReadProperties(bytes, format).Count == 0)
        {
            return false;
        }

        byte[] rewritten = format == TemplateFormat.Docx ? RemoveFromDocx(bytes) : RemoveFromOdt(bytes);
        document.Position = 0;
        document.SetLength(0);
        document.Write(rewritten);
        document.Position = 0;
        return true;
    }

    private static byte[] RemoveFromDocx(byte[] bytes)
    {
        using MemoryStream stream = new MemoryStream();
        stream.Write(bytes);
        using (WordprocessingDocument document = WordprocessingDocument.Open(stream, true))
        {
            CustomFilePropertiesPart part = document.CustomFilePropertiesPart!;
            List<CustomDocumentProperty> stamp = part.Properties!.Elements<CustomDocumentProperty>()
                .Where(p => p.Name?.Value?.StartsWith(TemplateStamp.PropertyPrefix, StringComparison.Ordinal) == true)
                .ToList();
            stamp.ForEach(p => p.Remove());

            if (!part.Properties.Elements<CustomDocumentProperty>().Any())
            {
                document.DeletePart(part);
            }
            else
            {
                part.Properties.Save();
            }
        }

        return stream.ToArray();
    }

    /// <summary>Rewrites the package with the stamp removed from <c>meta.xml</c>; the <c>mimetype</c> entry stays first and stored.</summary>
    private static byte[] RemoveFromOdt(byte[] bytes)
    {
        XNamespace meta = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";
        List<(string Name, byte[] Data, DateTimeOffset Time)> entries = new List<(string, byte[], DateTimeOffset)>();
        using (MemoryStream input = new MemoryStream(bytes, writable: false))
        using (ZipArchive archive = new ZipArchive(input, ZipArchiveMode.Read))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                using Stream entryStream = entry.Open();
                using MemoryStream data = new MemoryStream();
                entryStream.CopyTo(data);
                byte[] content = data.ToArray();

                if (entry.FullName == "meta.xml")
                {
                    XDocument xml = XDocument.Parse(Encoding.UTF8.GetString(content), LoadOptions.PreserveWhitespace);
                    xml.Descendants(meta + "user-defined")
                        .Where(e => ((string?)e.Attribute(meta + "name"))?.StartsWith(TemplateStamp.PropertyPrefix, StringComparison.Ordinal) == true)
                        .ToList()
                        .ForEach(e => e.Remove());
                    using MemoryStream serialized = new MemoryStream();
                    using (XmlWriter writer = XmlWriter.Create(serialized, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
                    {
                        xml.Save(writer);
                    }

                    content = serialized.ToArray();
                }

                entries.Add((entry.FullName, content, entry.LastWriteTime));
            }
        }

        using MemoryStream output = new MemoryStream();
        using (ZipArchive archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, byte[] data, DateTimeOffset time) in entries.OrderBy(e => e.Name == "mimetype" ? 0 : 1))
            {
                ZipArchiveEntry entry = archive.CreateEntry(name, name == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
                if (time.Year >= 1980)
                {
                    entry.LastWriteTime = time;
                }

                using Stream entryStream = entry.Open();
                entryStream.Write(data);
            }
        }

        return output.ToArray();
    }
}
