// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Security;
using System.Text;
using System.Xml.Linq;

namespace TriasDev.Templify.Tools.Tests.Gui;

/// <summary>
/// Writes and reads minimal OpenDocument Text packages (.odt, or .ott templates) for the GUI tests.
/// </summary>
internal static class OdtTestFile
{
    public const string TextMediaType = "application/vnd.oasis.opendocument.text";
    public const string TemplateMediaType = "application/vnd.oasis.opendocument.text-template";

    private static readonly XNamespace _office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace _text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

    /// <summary>Writes a package with one paragraph per text.</summary>
    public static string Create(string path, bool asTemplate, params string[] paragraphs)
    {
        string mediaType = asTemplate ? TemplateMediaType : TextMediaType;
        string body = string.Concat(paragraphs.Select(p => $"<text:p>{SecurityElement.Escape(p)}</text:p>"));
        string content =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.3\">" +
            $"<office:body><office:text>{body}</office:text></office:body></office:document-content>";
        string manifest =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.3\">" +
            $"<manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"{mediaType}\"/>" +
            "<manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/>" +
            "</manifest:manifest>";

        using (FileStream file = File.Create(path))
        using (ZipArchive archive = new(file, ZipArchiveMode.Create))
        {
            Add(archive, "mimetype", mediaType, CompressionLevel.NoCompression);
            Add(archive, "content.xml", content, CompressionLevel.Optimal);
            Add(archive, "META-INF/manifest.xml", manifest, CompressionLevel.Optimal);
        }

        return path;
    }

    /// <summary>Reads the mimetype entry of a package.</summary>
    public static string ReadMimetype(string path)
    {
        using ZipArchive archive = ZipFile.OpenRead(path);
        using StreamReader reader = new(archive.GetEntry("mimetype")!.Open(), Encoding.ASCII);
        return reader.ReadToEnd();
    }

    /// <summary>Reads the text of each body paragraph.</summary>
    public static List<string> ReadParagraphs(string path)
    {
        using ZipArchive archive = ZipFile.OpenRead(path);
        using Stream content = archive.GetEntry("content.xml")!.Open();
        return XDocument.Load(content).Descendants(_office + "text").Single()
            .Elements(_text + "p").Select(p => p.Value).ToList();
    }

    private static void Add(ZipArchive archive, string name, string content, CompressionLevel level)
    {
        using Stream stream = archive.CreateEntry(name, level).Open();
        stream.Write(Encoding.UTF8.GetBytes(content));
    }
}
