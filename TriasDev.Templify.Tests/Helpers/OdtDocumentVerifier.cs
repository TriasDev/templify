// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace TriasDev.Templify.Tests.Helpers;

/// <summary>
/// Reads an OpenDocument Text package produced in tests: rendered paragraph text, XML parts and
/// package structure.
/// </summary>
/// <remarks>
/// Rendered text follows the ODF/LibreOffice white-space rules: literal white space (space, tab, CR, LF
/// in text nodes) collapses to one space and is dropped at the start of a paragraph, also across span
/// boundaries; <c>text:s</c> renders its count of spaces, <c>text:tab</c> a tab and
/// <c>text:line-break</c> a newline.
/// </remarks>
public sealed class OdtDocumentVerifier
{
    public static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    public static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    public static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    public static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    public static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    public static readonly XNamespace Fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
    public static readonly XNamespace Manifest = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";

    private readonly byte[] _package;
    private readonly Dictionary<string, byte[]> _entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
    private readonly List<string> _entryNames = new List<string>();

    public OdtDocumentVerifier(byte[] package)
    {
        _package = package;
        using MemoryStream stream = new MemoryStream(package);
        using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            using Stream entryStream = entry.Open();
            using MemoryStream buffer = new MemoryStream();
            entryStream.CopyTo(buffer);
            _entries[entry.FullName] = buffer.ToArray();
            _entryNames.Add(entry.FullName);
        }

        ContentXml = XDocument.Parse(GetEntryString("content.xml"), LoadOptions.PreserveWhitespace);
        StylesXml = _entries.ContainsKey("styles.xml")
            ? XDocument.Parse(GetEntryString("styles.xml"), LoadOptions.PreserveWhitespace)
            : null;
    }

    public OdtDocumentVerifier(MemoryStream stream)
        : this(stream.ToArray())
    {
    }

    public XDocument ContentXml { get; }

    public XDocument? StylesXml { get; }

    public IReadOnlyList<string> EntryNames => _entryNames;

    public XElement Body => ContentXml.Root!.Element(Office + "body")!.Element(Office + "text")!;

    /// <summary>Gets a copy of the package bytes.</summary>
    public byte[] ToBytes() => (byte[])_package.Clone();

    public byte[] GetEntryBytes(string name) => _entries[name];

    public string GetEntryString(string name) => Encoding.UTF8.GetString(_entries[name]);

    public string Mimetype => Encoding.ASCII.GetString(_entries["mimetype"]);

    public string? ManifestRootMediaType =>
        XDocument.Parse(GetEntryString("META-INF/manifest.xml")).Root!
            .Elements(Manifest + "file-entry")
            .FirstOrDefault(e => (string?)e.Attribute(Manifest + "full-path") == "/")
            ?.Attribute(Manifest + "media-type")?.Value;

    /// <summary>Gets the rendered text of all paragraphs and headings of the body, in document order.</summary>
    public List<string> GetParagraphTexts() => GetParagraphs(Body).Select(RenderText).ToList();

    /// <summary>Gets the rendered text of the paragraphs of all headers.</summary>
    public List<string> GetHeaderTexts() => GetMasterPageTexts("header");

    /// <summary>Gets the rendered text of the paragraphs of all footers.</summary>
    public List<string> GetFooterTexts() => GetMasterPageTexts("footer");

    /// <summary>Gets the paragraphs and headings in a container, excluding annotations and tracked deletions.</summary>
    public static IEnumerable<XElement> GetParagraphs(XElement container) =>
        container.Descendants()
            .Where(e => e.Name == Text + "p" || e.Name == Text + "h")
            .Where(e => !e.Ancestors().Any(a => a.Name == Office + "annotation" || a.Name == Text + "tracked-changes"));

    /// <summary>Renders the text of a paragraph as LibreOffice displays it.</summary>
    public static string RenderText(XElement paragraph)
    {
        StringBuilder result = new StringBuilder();
        bool ignoreSpace = true;
        Render(paragraph, result, ref ignoreSpace);
        return result.ToString();
    }

    private static void Render(XElement container, StringBuilder result, ref bool ignoreSpace)
    {
        foreach (XNode node in container.Nodes())
        {
            if (node is XText text)
            {
                foreach (char c in text.Value)
                {
                    if (c is ' ' or '\t' or '\r' or '\n')
                    {
                        if (!ignoreSpace)
                        {
                            result.Append(' ');
                            ignoreSpace = true;
                        }
                    }
                    else
                    {
                        result.Append(c);
                        ignoreSpace = false;
                    }
                }
            }
            else if (node is XElement element)
            {
                if (element.Name == Text + "s")
                {
                    int count = int.TryParse((string?)element.Attribute(Text + "c"), out int c) ? c : 1;
                    result.Append(' ', count);
                    ignoreSpace = false;
                }
                else if (element.Name == Text + "tab")
                {
                    result.Append('\t');
                    ignoreSpace = false;
                }
                else if (element.Name == Text + "line-break")
                {
                    result.Append('\n');
                    ignoreSpace = false;
                }
                else if (element.Name == Text + "span" || element.Name == Text + "a" || element.Name == Text + "meta")
                {
                    Render(element, result, ref ignoreSpace);
                }
            }
        }
    }

    private List<string> GetMasterPageTexts(string kind)
    {
        if (StylesXml == null)
        {
            return new List<string>();
        }

        return StylesXml.Descendants(Style + "master-page")
            .Elements()
            .Where(e => e.Name.LocalName.StartsWith(kind, StringComparison.Ordinal))
            .SelectMany(GetParagraphs)
            .Select(RenderText)
            .ToList();
    }

    /// <summary>
    /// Asserts the package structure required by ODF: the mimetype entry first, stored, without extra
    /// field and data descriptor, holding the .odt media type; the manifest's root entry names the same
    /// media type and every manifest entry exists; all XML parts are well-formed.
    /// </summary>
    public void AssertValidOdtPackage()
    {
        Assert.Equal("mimetype", _entryNames[0]);
        Assert.Equal(0x04034b50u, BitConverter.ToUInt32(_package, 0));
        ushort flags = BitConverter.ToUInt16(_package, 6);
        Assert.Equal(0, flags & 0x08); // no data descriptor
        Assert.Equal(0, BitConverter.ToUInt16(_package, 8)); // stored
        Assert.Equal(8, BitConverter.ToUInt16(_package, 26)); // name length
        Assert.Equal(0, BitConverter.ToUInt16(_package, 28)); // no extra field
        Assert.Equal("mimetype", Encoding.ASCII.GetString(_package, 30, 8));
        Assert.Equal(OdtDocumentBuilder.TextMediaType, Encoding.ASCII.GetString(_package, 38, OdtDocumentBuilder.TextMediaType.Length));
        Assert.Equal(OdtDocumentBuilder.TextMediaType, Mimetype);

        Assert.Equal(OdtDocumentBuilder.TextMediaType, ManifestRootMediaType);
        XDocument manifest = XDocument.Parse(GetEntryString("META-INF/manifest.xml"));
        foreach (XElement entry in manifest.Root!.Elements(Manifest + "file-entry"))
        {
            string path = (string)entry.Attribute(Manifest + "full-path")!;
            if (path != "/")
            {
                Assert.True(_entries.ContainsKey(path), $"Manifest entry '{path}' is missing from the package.");
            }
        }

        foreach (string name in _entryNames.Where(n => n.EndsWith(".xml", StringComparison.Ordinal)))
        {
            XDocument.Parse(GetEntryString(name));
        }
    }
}
