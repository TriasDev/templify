// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using TriasDev.Templify.Core;
using TriasDev.Templify.OpenDocument;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Odt;

/// <summary>
/// Less common package shapes and limits of <see cref="OdtPackage"/>: no manifest, no mimetype, unlisted parts,
/// fixed-size outputs, entry count limit.
/// </summary>
public sealed class OdtPackageBranchTests
{
    private const string Content =
        "<office:document-content " + OdtDocumentBuilder.NamespaceDeclarations + "><office:body><office:text>"
        + "<text:p>Hello {{Name}}</text:p></office:text></office:body></office:document-content>";

    private static readonly Dictionary<string, object> _data = new Dictionary<string, object> { ["Name"] = "World" };

    private static byte[] CreateZip(params (string Name, string Content)[] entries)
    {
        using MemoryStream stream = new MemoryStream();
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in entries)
            {
                using Stream entryStream = archive.CreateEntry(name, name == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Optimal).Open();
                entryStream.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        return stream.ToArray();
    }

    private static Dictionary<string, string> ReadEntries(byte[] package)
    {
        using ZipArchive archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        return archive.Entries.ToDictionary(e => e.FullName, e =>
        {
            using StreamReader reader = new StreamReader(e.Open());
            return reader.ReadToEnd();
        });
    }

    [Fact]
    public void TemplateWithoutManifest_IsProcessed_AndStaysWithoutManifest()
    {
        byte[] template = CreateZip(("mimetype", OdtDocumentBuilder.TemplateMediaType), ("content.xml", Content), ("Thumbnails/thumbnail.png", "png"));

        ProcessingResult result = new OdtTemplateProcessor().ProcessTemplate(template, _data, out byte[] output);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Dictionary<string, string> entries = ReadEntries(output);
        Assert.Equal(new[] { "mimetype", "content.xml" }, entries.Keys);
        Assert.Equal(OdtDocumentBuilder.TextMediaType, entries["mimetype"]);
        Assert.Contains("Hello World", entries["content.xml"], StringComparison.Ordinal);
    }

    [Fact]
    public void NoMimetypeAndMalformedManifest_IsRejected_WithMediaTypeNone()
    {
        byte[] template = CreateZip(("content.xml", Content), ("META-INF/manifest.xml", "<manifest:manifest"));

        ProcessingResult result = new OdtTemplateProcessor().ProcessTemplate(template, _data, out byte[] output);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Invalid document: the template is not an OpenDocument Text document (.odt/.ott); media type: none.",
            result.ErrorMessage);
        Assert.Empty(output);
    }

    [Fact]
    public void AddedPart_IsListedInTheManifest_OnlyWhenTheManifestDoesNotListItYet()
    {
        byte[] withManifest = new OdtDocumentBuilder().AddParagraph("x").ToBytes();
        byte[] withoutManifest = CreateZip(("mimetype", OdtDocumentBuilder.TextMediaType), ("content.xml", Content));

        Dictionary<string, string> listed = SaveWithAddedParts(withManifest, "custom.xml", "meta.xml");
        Dictionary<string, string> unlisted = SaveWithAddedParts(withoutManifest, "custom.xml");

        XDocument manifest = XDocument.Parse(listed["META-INF/manifest.xml"]);
        List<string> paths = manifest.Root!.Elements(OdtDocumentVerifier.Manifest + "file-entry")
            .Select(e => (string)e.Attribute(OdtDocumentVerifier.Manifest + "full-path")!).ToList();
        Assert.Single(paths, p => p == "custom.xml");
        Assert.Single(paths, p => p == "meta.xml");
        Assert.Equal("<root />", XDocument.Parse(listed["custom.xml"]).Root!.ToString());
        Assert.DoesNotContain("META-INF/manifest.xml", unlisted.Keys);
        Assert.Contains("custom.xml", unlisted.Keys);
    }

    private static Dictionary<string, string> SaveWithAddedParts(byte[] template, params string[] names)
    {
        using MemoryStream source = new MemoryStream(template);
        OdtPackage package = OdtPackage.Open(source);
        foreach (string name in names.Where(n => package.GetXml(n) == null))
        {
            package.AddXml(name, new XDocument(new XElement("root")));
        }

        using MemoryStream output = new MemoryStream();
        package.Save(output);
        return ReadEntries(output.ToArray());
    }

    [Fact]
    public void GetXml_OfAnEntryThatIsNotAnXmlPart_Throws()
    {
        byte[] template = new OdtDocumentBuilder().AddParagraph("x").AddEntry("Pictures/a.xml", Encoding.UTF8.GetBytes("<a/>")).ToBytes();
        OdtPackage package = OdtPackage.Open(new MemoryStream(template));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => package.GetXml("Pictures/a.xml"));

        Assert.Equal("The package entry Pictures/a.xml is not loaded as an XML part.", exception.Message);
        Assert.Null(package.GetXml("missing.xml"));
        Assert.Same(package.GetXml(OdtPackage.ContentEntry), package.GetXml(OdtPackage.ContentEntry));
    }

    [Fact]
    public void SeekableOutputThatCannotChangeItsLength_IsWrittenWithoutTruncation()
    {
        // The rest of a longer seekable output is cut off where possible. A stream that cannot change its length
        // does not fail the processing; the package is written from the start and the stream keeps its length.
        using FixedLengthStream output = new FixedLengthStream(new byte[1024 * 1024]);

        ProcessingResult result = new OdtTemplateProcessor().ProcessTemplate(
            new MemoryStream(new OdtDocumentBuilder().AddParagraph("Hello {{Name}}").ToBytes()),
            output,
            _data);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(1024 * 1024, output.Length);
        Assert.Equal("Hello World", new OdtDocumentVerifier(output.ToArray()[..(int)output.Position]).GetParagraphTexts()[0]);
    }

    private sealed class FixedLengthStream : MemoryStream
    {
        public FixedLengthStream(byte[] buffer)
            : base(buffer, writable: true)
        {
        }

        public override void SetLength(long value) => throw new NotSupportedException();
    }

    [Fact]
    public void PackageWithMoreThanTheMaximumNumberOfEntries_IsRejected()
    {
        using MemoryStream stream = new MemoryStream();
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("mimetype", CompressionLevel.NoCompression).Open().Dispose();
            for (int i = 0; i < OdtPackage.MaxEntryCount; i++)
            {
                archive.CreateEntry("e" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), CompressionLevel.NoCompression);
            }
        }

        ProcessingResult result = new OdtTemplateProcessor().ProcessTemplate(stream.ToArray(), _data, out _);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            $"Invalid document: the package has {OdtPackage.MaxEntryCount + 1} entries, more than the maximum supported number of {OdtPackage.MaxEntryCount}.",
            result.ErrorMessage);
    }

    [Fact]
    public void XmlPartLimit_InMegabytes_IsNamedInTheMessage()
    {
        byte[] template = new OdtDocumentBuilder().AddParagraph(new string('x', 2 * 1024 * 1024)).ToBytes();

        InvalidOdtPackageException exception = Assert.Throws<InvalidOdtPackageException>(
            () => OdtPackage.Open(new MemoryStream(template), maxXmlPartBytes: 1024 * 1024));

        Assert.Equal("Invalid document: content.xml exceeds the maximum supported size (1 MB uncompressed).", exception.Message);
    }
}
