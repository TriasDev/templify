// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Text;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Odt;

/// <summary>
/// Package-level behavior of <see cref="OdtTemplateProcessor"/>: mimetype, manifest, entries, stream contracts.
/// </summary>
public sealed class OdtPackageTests
{
    private static readonly Dictionary<string, object> _data = new Dictionary<string, object> { ["Name"] = "World" };

    [Fact]
    public void Process_Odt_WritesMimetypeFirstAndStored()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(new OdtDocumentBuilder().AddParagraph("Hello {{Name}}"), _data);

        // AssertValidOdtPackage (called by the helper) checks the local header bytes; check the entry list too.
        Assert.Equal("mimetype", output.EntryNames[0]);
        Assert.Equal("application/vnd.oasis.opendocument.text", output.Mimetype);
    }

    [Fact]
    public void Process_OttTemplate_ProducesOdtMimetypeAndManifest()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AsTemplate().AddParagraph("Hello {{Name}}");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, _data);

        Assert.Equal(OdtDocumentBuilder.TextMediaType, output.Mimetype);
        Assert.Equal(OdtDocumentBuilder.TextMediaType, output.ManifestRootMediaType);
        Assert.Equal("Hello World", output.GetParagraphTexts()[0]);
    }

    [Fact]
    public void Process_MimetypeNotFirstInTemplate_WritesItFirst()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().WithMimetypeLast().AddParagraph("{{Name}}");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, _data);

        Assert.Equal("mimetype", output.EntryNames[0]);
        Assert.Single(output.EntryNames, n => n == "mimetype");
    }

    [Fact]
    public void Process_MimetypeMissing_UsesManifestAndAddsMimetype()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().WithoutMimetype().AddParagraph("{{Name}}");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, _data);

        Assert.Equal("World", output.GetParagraphTexts()[0]);
    }

    [Fact]
    public void Process_KeepsOtherEntriesInOrderAndUnchanged()
    {
        byte[] picture = { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4, 5 };
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("{{Name}}")
            .AddEntry("Pictures/image.png", picture)
            .AddEntry("settings.xml", Encoding.UTF8.GetBytes("<office:document-settings xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"/>"));

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, _data);

        Assert.Equal(
            new[] { "mimetype", "content.xml", "styles.xml", "meta.xml", "Pictures/image.png", "settings.xml", "META-INF/manifest.xml" },
            output.EntryNames);
        Assert.Equal(picture, output.GetEntryBytes("Pictures/image.png"));
    }

    [Fact]
    public void Process_UnchangedStylesPart_IsCopiedByteForByte()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder().AddParagraph("{{Name}}");

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, _data);

        Assert.Equal(template.BuildStylesXml(), output.GetEntryString("styles.xml"));
    }

    [Fact]
    public void Process_WritesContentAsUtf8WithoutBom()
    {
        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(new OdtDocumentBuilder().AddParagraph("{{Name}} äöü"), _data);

        byte[] content = output.GetEntryBytes("content.xml");
        Assert.NotEqual(0xEF, content[0]);
        Assert.StartsWith("<?xml", Encoding.UTF8.GetString(content), StringComparison.Ordinal);
        Assert.Equal("World äöü", output.GetParagraphTexts()[0]);
    }

    [Fact]
    public void Process_DocumentSignature_IsRemovedWithManifestEntry()
    {
        OdtDocumentBuilder template = new OdtDocumentBuilder()
            .AddParagraph("{{Name}}")
            .AddEntry("META-INF/documentsignatures.xml", Encoding.UTF8.GetBytes("<signatures/>"));

        (_, OdtDocumentVerifier output) = OdtTestHelper.Process(template, _data);

        Assert.DoesNotContain("META-INF/documentsignatures.xml", output.EntryNames);
        Assert.DoesNotContain("documentsignatures", output.GetEntryString("META-INF/manifest.xml"), StringComparison.Ordinal);
    }

    [Fact]
    public void Process_NotAZip_ReturnsFailure()
    {
        OdtTemplateProcessor processor = new OdtTemplateProcessor();

        ProcessingResult result = processor.ProcessTemplate(Encoding.UTF8.GetBytes("<office:document/>"), _data, out byte[] output);

        Assert.False(result.IsSuccess);
        Assert.Contains("not an OpenDocument Text package", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(output);
    }

    [Fact]
    public void Process_DocxPackage_ReturnsFailure()
    {
        DocumentBuilder docx = new DocumentBuilder();
        docx.AddParagraph("{{Name}}");
        byte[] template = docx.ToStream().ToArray();
        docx.Dispose();

        ProcessingResult result = new OdtTemplateProcessor().ProcessTemplate(template, _data, out _);

        Assert.False(result.IsSuccess);
        Assert.Contains("not an OpenDocument Text document", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_SpreadsheetMediaType_ReturnsFailure()
    {
        byte[] template = CreateZip(("mimetype", "application/vnd.oasis.opendocument.spreadsheet"), ("content.xml", "<x/>"));

        ProcessingResult result = new OdtTemplateProcessor().ProcessTemplate(template, _data, out _);

        Assert.False(result.IsSuccess);
        Assert.Contains("application/vnd.oasis.opendocument.spreadsheet", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_EncryptedPackage_ReturnsFailure()
    {
        byte[] template = CreateZip(
            ("mimetype", OdtDocumentBuilder.TextMediaType),
            ("content.xml", "encrypted bytes"),
            ("META-INF/manifest.xml",
                "<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\">" +
                "<manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.text\"/>" +
                "<manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\">" +
                "<manifest:encryption-data manifest:checksum-type=\"SHA1/1K\" manifest:checksum=\"x\"/></manifest:file-entry>" +
                "</manifest:manifest>"));

        ProcessingResult result = new OdtTemplateProcessor().ProcessTemplate(template, _data, out _);

        Assert.False(result.IsSuccess);
        Assert.Contains("encrypted", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_MalformedContent_ReturnsFailure()
    {
        byte[] template = CreateZip(("mimetype", OdtDocumentBuilder.TextMediaType), ("content.xml", "<office:document-content"));

        ProcessingResult result = new OdtTemplateProcessor().ProcessTemplate(template, _data, out _);

        Assert.False(result.IsSuccess);
        Assert.Contains("content.xml is not well-formed XML", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_ContentWithDtd_ReturnsFailure()
    {
        byte[] template = CreateZip(
            ("mimetype", OdtDocumentBuilder.TextMediaType),
            ("content.xml", "<?xml version=\"1.0\"?><!DOCTYPE x [<!ENTITY e SYSTEM \"file:///etc/passwd\">]><x>&e;</x>"));

        ProcessingResult result = new OdtTemplateProcessor().ProcessTemplate(template, _data, out _);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Process_ContentWithoutTextBody_ReturnsFailure()
    {
        byte[] template = CreateZip(
            ("mimetype", OdtDocumentBuilder.TextMediaType),
            ("content.xml", "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"><office:body/></office:document-content>"));

        ProcessingResult result = new OdtTemplateProcessor().ProcessTemplate(template, _data, out _);

        Assert.False(result.IsSuccess);
        Assert.Contains("office:text", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_StreamOverload_WritesToOutputStream()
    {
        using MemoryStream template = new OdtDocumentBuilder().AddParagraph("Hello {{Name}}").ToStream();
        template.Position = template.Length; // the processor rewinds seekable template streams
        using MemoryStream output = new MemoryStream();

        ProcessingResult result = new OdtTemplateProcessor().ProcessTemplate(template, output, _data);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(1, result.ReplacementCount);
        OdtDocumentVerifier verifier = new OdtDocumentVerifier(output);
        verifier.AssertValidOdtPackage();
        Assert.Equal("Hello World", verifier.GetParagraphTexts()[0]);
    }

    [Fact]
    public void Process_NonSeekableStreams_Work()
    {
        byte[] template = new OdtDocumentBuilder().AddParagraph("Hello {{Name}}").ToBytes();
        using NonSeekableStream input = new NonSeekableStream(new MemoryStream(template));
        using MemoryStream buffer = new MemoryStream();
        using NonSeekableStream output = new NonSeekableStream(buffer);

        ProcessingResult result = new OdtTemplateProcessor().ProcessTemplate(input, output, _data);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        OdtDocumentVerifier verifier = new OdtDocumentVerifier(buffer.ToArray());
        verifier.AssertValidOdtPackage();
        Assert.Equal("Hello World", verifier.GetParagraphTexts()[0]);
    }

    [Fact]
    public void Process_ReadOnlyOutputStream_ReturnsFailureWithoutWriting()
    {
        using MemoryStream template = new OdtDocumentBuilder().AddParagraph("{{Name}}").ToStream();
        using MemoryStream output = new MemoryStream(new byte[10], writable: false);

        ProcessingResult result = new OdtTemplateProcessor().ProcessTemplate(template, output, _data);

        Assert.False(result.IsSuccess);
        Assert.Contains("writable", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_FailedProcessing_WritesNothingToOutputStream()
    {
        using MemoryStream template = new MemoryStream(Encoding.UTF8.GetBytes("not a zip"));
        using MemoryStream output = new MemoryStream();

        ProcessingResult result = new OdtTemplateProcessor().ProcessTemplate(template, output, _data);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void Process_NullArguments_Throw()
    {
        OdtTemplateProcessor processor = new OdtTemplateProcessor();
        using MemoryStream stream = new MemoryStream();

        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplate(null!, stream, _data));
        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplate(stream, null!, _data));
        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplate(stream, stream, (Dictionary<string, object>)null!));
        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplate((byte[])null!, _data, out _));
        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplate(Array.Empty<byte>(), (string)null!, out _));
        Assert.Throws<ArgumentException>(() => processor.ProcessTemplateFile(" ", "out.odt", _data));
    }

    [Fact]
    public void Process_JsonOverloads_ReplacePlaceholders()
    {
        byte[] template = new OdtDocumentBuilder().AddParagraph("{{Customer.Name}} ({{Count}})").ToBytes();
        const string json = "{\"Customer\": {\"Name\": \"Alice\"}, \"Count\": 3}";
        OdtTemplateProcessor processor = new OdtTemplateProcessor(OdtTestHelper.InvariantOptions());

        ProcessingResult bytesResult = processor.ProcessTemplate(template, json, out byte[] output);
        using MemoryStream streamOutput = new MemoryStream();
        ProcessingResult streamResult = processor.ProcessTemplate(new MemoryStream(template), streamOutput, json);

        Assert.True(bytesResult.IsSuccess, bytesResult.ErrorMessage);
        Assert.True(streamResult.IsSuccess, streamResult.ErrorMessage);
        Assert.Equal("Alice (3)", new OdtDocumentVerifier(output).GetParagraphTexts()[0]);
        Assert.Equal("Alice (3)", new OdtDocumentVerifier(streamOutput).GetParagraphTexts()[0]);
    }

    [Fact]
    public void Process_ReadOnlyDictionaryOverloads_ReplacePlaceholders()
    {
        byte[] template = new OdtDocumentBuilder().AddParagraph("{{Name}}|{{Empty}}").ToBytes();
        IReadOnlyDictionary<string, object?> data = new Dictionary<string, object?> { ["Name"] = "Bob", ["Empty"] = null };
        OdtTemplateProcessor processor = new OdtTemplateProcessor(OdtTestHelper.InvariantOptions());

        ProcessingResult result = processor.ProcessTemplate(template, data, out byte[] output);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal("Bob|", new OdtDocumentVerifier(output).GetParagraphTexts()[0]);
    }

    [Fact]
    public void ProcessTemplateFile_WritesOutputFile_OnlyOnSuccess()
    {
        string directory = Path.Combine(Path.GetTempPath(), "templify-odt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string templatePath = Path.Combine(directory, "template.ott");
            string outputPath = Path.Combine(directory, "output.odt");
            File.WriteAllBytes(templatePath, new OdtDocumentBuilder().AsTemplate().AddParagraph("Hi {{Name}}").ToBytes());
            OdtTemplateProcessor processor = new OdtTemplateProcessor(OdtTestHelper.InvariantOptions());

            ProcessingResult result = processor.ProcessTemplateFile(templatePath, outputPath, _data);
            ProcessingResult readOnlyResult = processor.ProcessTemplateFile(
                templatePath, outputPath, (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["Name"] = "RO" });
            string jsonOutputPath = Path.Combine(directory, "json.odt");
            ProcessingResult jsonResult = processor.ProcessTemplateFile(templatePath, jsonOutputPath, "{\"Name\":\"Json\"}");

            string failedOutputPath = Path.Combine(directory, "failed.odt");
            File.WriteAllText(Path.Combine(directory, "bad.odt"), "bad");
            ProcessingResult failed = processor.ProcessTemplateFile(Path.Combine(directory, "bad.odt"), failedOutputPath, _data);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.True(readOnlyResult.IsSuccess, readOnlyResult.ErrorMessage);
            Assert.True(jsonResult.IsSuccess, jsonResult.ErrorMessage);
            OdtDocumentVerifier verifier = new OdtDocumentVerifier(File.ReadAllBytes(outputPath));
            verifier.AssertValidOdtPackage();
            Assert.Equal("Hi RO", verifier.GetParagraphTexts()[0]);
            Assert.Equal("Hi Json", new OdtDocumentVerifier(File.ReadAllBytes(jsonOutputPath)).GetParagraphTexts()[0]);
            Assert.False(failed.IsSuccess);
            Assert.False(File.Exists(failedOutputPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Process_LargeUnprocessedEntry_IsStreamedWithoutInflatingIntoMemory()
    {
        // A 64 MB entry that processing never reads (a picture) compresses to about 64 KB. It used to be inflated
        // into memory and buffered twice (more than 3x its inflated size was allocated).
        byte[] template = CreateTemplateWithLargeEntry(out int entrySize);

        long before = GC.GetAllocatedBytesForCurrentThread();
        ProcessingResult result = new OdtTemplateProcessor().ProcessTemplate(template, _data, out byte[] output);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.True(allocated < entrySize / 4, $"Allocated {allocated:N0} bytes for a {entrySize:N0}-byte entry.");
        AssertLargeEntryCopied(output, entrySize);
    }

    [Fact]
    public void Process_NonSeekableTemplateWithLargeEntry_BuffersOnlyCompressedData()
    {
        byte[] template = CreateTemplateWithLargeEntry(out int entrySize);
        using NonSeekableStream input = new NonSeekableStream(new MemoryStream(template));
        using MemoryStream output = new MemoryStream();

        long before = GC.GetAllocatedBytesForCurrentThread();
        ProcessingResult result = new OdtTemplateProcessor().ProcessTemplate(input, output, _data);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.True(allocated < entrySize / 4, $"Allocated {allocated:N0} bytes for a {entrySize:N0}-byte entry.");
        AssertLargeEntryCopied(output.ToArray(), entrySize);
    }

    [Fact]
    public void Open_XmlPartLargerThanLimit_ThrowsInvalidPackage()
    {
        byte[] template = new OdtDocumentBuilder().AddParagraph(new string('x', 4096)).ToBytes();

        TriasDev.Templify.OpenDocument.InvalidOdtPackageException exception =
            Assert.Throws<TriasDev.Templify.OpenDocument.InvalidOdtPackageException>(
                () => TriasDev.Templify.OpenDocument.OdtPackage.Open(new MemoryStream(template), maxXmlPartBytes: 2048));

        Assert.Equal(
            "Invalid document: content.xml exceeds the maximum supported size (2048 bytes uncompressed).",
            exception.Message);
    }

    [Fact]
    public void Process_ExistingLongerOutputFile_IsTruncated()
    {
        // File.OpenWrite does not truncate: without cutting off, the rest of a longer earlier file stayed behind.
        string path = Path.Combine(Path.GetTempPath(), "templify-odt-openwrite-" + Guid.NewGuid().ToString("N") + ".odt");
        try
        {
            File.WriteAllBytes(path, new byte[200 * 1024]);
            byte[] template = new OdtDocumentBuilder().AddParagraph("Hello {{Name}}").ToBytes();
            ProcessingResult result;
            using (FileStream output = File.OpenWrite(path))
            {
                result = new OdtTemplateProcessor().ProcessTemplate(new MemoryStream(template), output, _data);
            }

            Assert.True(result.IsSuccess, result.ErrorMessage);
            byte[] written = File.ReadAllBytes(path);
            Assert.True(written.Length < 200 * 1024);
            OdtDocumentVerifier verifier = new OdtDocumentVerifier(written);
            verifier.AssertValidOdtPackage();
            Assert.Equal("Hello World", verifier.GetParagraphTexts()[0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Process_SeekableOutputWithPrefix_KeepsPrefixAndCutsOffTheRest()
    {
        byte[] template = new OdtDocumentBuilder().AddParagraph("Hello {{Name}}").ToBytes();
        using MemoryStream output = new MemoryStream();
        output.Write(new byte[] { 1, 2, 3 });
        output.Write(new byte[100 * 1024]);
        output.Position = 3;

        ProcessingResult result = new OdtTemplateProcessor().ProcessTemplate(new MemoryStream(template), output, _data);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(output.Position, output.Length);
        byte[] written = output.ToArray();
        Assert.Equal(new byte[] { 1, 2, 3 }, written[..3]);
        Assert.Equal("Hello World", new OdtDocumentVerifier(written[3..]).GetParagraphTexts()[0]);
    }

    private static byte[] CreateTemplateWithLargeEntry(out int entrySize)
    {
        entrySize = 64 * 1024 * 1024;
        return new OdtDocumentBuilder()
            .AddParagraph("{{Name}}")
            .AddEntry("Pictures/big.bin", new byte[entrySize])
            .ToBytes();
    }

    private static void AssertLargeEntryCopied(byte[] output, int entrySize)
    {
        using ZipArchive archive = new ZipArchive(new MemoryStream(output), ZipArchiveMode.Read);
        Assert.Equal(entrySize, archive.GetEntry("Pictures/big.bin")!.Length);
        Assert.True(output.Length < entrySize / 100, $"Output of {output.Length:N0} bytes is not compressed.");
        Assert.Equal("World", ReadParagraphText(archive));
    }

    private static string ReadParagraphText(ZipArchive archive)
    {
        using Stream content = archive.GetEntry("content.xml")!.Open();
        System.Xml.Linq.XDocument document = System.Xml.Linq.XDocument.Load(content);
        return document.Descendants(OdtDocumentVerifier.Text + "p").Single().Value;
    }

    private static byte[] CreateZip(params (string Name, string Content)[] entries)
    {
        using MemoryStream stream = new MemoryStream();
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(name, name == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
                using Stream entryStream = entry.Open();
                entryStream.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        return stream.ToArray();
    }

    private sealed class NonSeekableStream : Stream
    {
        private readonly Stream _inner;

        public NonSeekableStream(Stream inner)
        {
            _inner = inner;
        }

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => _inner.CanWrite;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    }
}
