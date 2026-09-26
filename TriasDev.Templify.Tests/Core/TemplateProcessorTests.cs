// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.IO.Compression;
using System.Text;
using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Core;

/// <summary>
/// The format-detecting <see cref="TemplateProcessor"/> facade (#138): detection from the package content and
/// delegation to <see cref="DocumentTemplateProcessor"/> or <see cref="OdtTemplateProcessor"/>.
/// </summary>
public sealed class TemplateProcessorTests
{
    private const string WordMainContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";

    private static readonly Dictionary<string, object> _data = new Dictionary<string, object> { ["Name"] = "Alice" };

    // ---------- DetectFormat ----------

    [Fact]
    public void DetectFormat_Docx_ReturnsDocx()
    {
        using MemoryStream stream = new MemoryStream(CreateDocx());

        Assert.Equal(TemplateFormat.Docx, TemplateProcessor.DetectFormat(stream));
    }

    [Theory]
    [InlineData("application/vnd.ms-word.document.macroEnabled.main+xml")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml")]
    [InlineData("application/vnd.ms-word.template.macroEnabledTemplate.main+xml")]
    public void DetectFormat_OtherWordprocessingPackages_ReturnDocx(string mainContentType)
    {
        using MemoryStream stream = new MemoryStream(WithMainContentType(CreateDocx(), mainContentType));

        Assert.Equal(TemplateFormat.Docx, TemplateProcessor.DetectFormat(stream));
    }

    [Fact]
    public void DetectFormat_Odt_ReturnsOdt()
    {
        using MemoryStream stream = new OdtDocumentBuilder().AddParagraph("Hello").ToStream();

        Assert.Equal(TemplateFormat.Odt, TemplateProcessor.DetectFormat(stream));
    }

    [Fact]
    public void DetectFormat_Ott_ReturnsOdt()
    {
        using MemoryStream stream = new OdtDocumentBuilder().AddParagraph("Hello").AsTemplate().ToStream();

        Assert.Equal(TemplateFormat.Odt, TemplateProcessor.DetectFormat(stream));
    }

    [Fact]
    public void DetectFormat_OdtWithoutMimetypeEntry_UsesManifestMediaType()
    {
        using MemoryStream stream = new OdtDocumentBuilder().AddParagraph("Hello").WithoutMimetype().ToStream();

        Assert.Equal(TemplateFormat.Odt, TemplateProcessor.DetectFormat(stream));
    }

    [Theory]
    [InlineData("application/vnd.oasis.opendocument.spreadsheet")]
    [InlineData("application/vnd.oasis.opendocument.presentation")]
    [InlineData("application/vnd.oasis.opendocument.text-master")]
    public void DetectFormat_OtherOpenDocumentTypes_ReturnUnknown(string mediaType)
    {
        using MemoryStream stream = new MemoryStream(CreateZip(("mimetype", mediaType), ("content.xml", "<x/>")));

        Assert.Equal(TemplateFormat.Unknown, TemplateProcessor.DetectFormat(stream));
    }

    [Fact]
    public void DetectFormat_FlatOpenDocument_ReturnsUnknown()
    {
        string fodt = "<?xml version=\"1.0\"?><office:document xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" "
            + "office:mimetype=\"application/vnd.oasis.opendocument.text\"/>";
        using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(fodt));

        Assert.Equal(TemplateFormat.Unknown, TemplateProcessor.DetectFormat(stream));
    }

    [Fact]
    public void DetectFormat_SpreadsheetOpenXmlPackage_ReturnsUnknown()
    {
        string contentTypes = "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
            + "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
            + "</Types>";
        using MemoryStream stream = new MemoryStream(CreateZip(("[Content_Types].xml", contentTypes)));

        Assert.Equal(TemplateFormat.Unknown, TemplateProcessor.DetectFormat(stream));
    }

    [Fact]
    public void DetectFormat_RandomBytes_ReturnsUnknown()
    {
        byte[] bytes = new byte[4096];
        new Random(42).NextBytes(bytes);
        using MemoryStream stream = new MemoryStream(bytes);

        Assert.Equal(TemplateFormat.Unknown, TemplateProcessor.DetectFormat(stream));
    }

    [Fact]
    public void DetectFormat_EmptyStream_ReturnsUnknown()
    {
        using MemoryStream stream = new MemoryStream();

        Assert.Equal(TemplateFormat.Unknown, TemplateProcessor.DetectFormat(stream));
    }

    [Fact]
    public void DetectFormat_ZipWithoutMarkers_ReturnsUnknown()
    {
        using MemoryStream stream = new MemoryStream(CreateZip(("readme.txt", "hello"), ("content.xml", "<x/>")));

        Assert.Equal(TemplateFormat.Unknown, TemplateProcessor.DetectFormat(stream));
    }

    [Fact]
    public void DetectFormat_MalformedContentTypes_ReturnsUnknown()
    {
        using MemoryStream stream = new MemoryStream(CreateZip(("[Content_Types].xml", "<Types")));

        Assert.Equal(TemplateFormat.Unknown, TemplateProcessor.DetectFormat(stream));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(7L)]
    public void DetectFormat_RestoresStreamPosition(long position)
    {
        using MemoryStream docx = new MemoryStream(CreateDocx());
        using MemoryStream odt = new OdtDocumentBuilder().AddParagraph("Hello").ToStream();
        docx.Position = position;
        odt.Position = position;

        Assert.Equal(TemplateFormat.Docx, TemplateProcessor.DetectFormat(docx));
        Assert.Equal(TemplateFormat.Odt, TemplateProcessor.DetectFormat(odt));
        Assert.Equal(position, docx.Position);
        Assert.Equal(position, odt.Position);
    }

    [Fact]
    public void DetectFormat_UnknownFormat_RestoresStreamPosition()
    {
        using MemoryStream stream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        stream.Position = 3;

        Assert.Equal(TemplateFormat.Unknown, TemplateProcessor.DetectFormat(stream));
        Assert.Equal(3, stream.Position);
    }

    [Fact]
    public void DetectFormat_NonSeekableStream_Throws()
    {
        using NonSeekableStream stream = new NonSeekableStream(new MemoryStream(CreateDocx()));

        Assert.Throws<ArgumentException>(() => TemplateProcessor.DetectFormat(stream));
    }

    [Fact]
    public void DetectFormat_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TemplateProcessor.DetectFormat(null!));
    }

    // ---------- Delegation ----------

    [Fact]
    public void ProcessTemplate_Docx_MatchesDocumentTemplateProcessor()
    {
        byte[] template = CreateDocx();

        ProcessingResult direct = new DocumentTemplateProcessor(Options()).ProcessTemplate(template, _data, out byte[] expected);
        ProcessingResult facade = new TemplateProcessor(Options()).ProcessTemplate(template, _data, out byte[] actual);

        AssertSameResult(direct, facade);
        Assert.Equal(TemplateFormat.Docx, DetectBytes(actual));
        Assert.Equal(DocxTexts(expected), DocxTexts(actual));
        Assert.Equal(new[] { "Hello Alice!" }, DocxTexts(actual));
    }

    [Fact]
    public void ProcessTemplate_Odt_MatchesOdtTemplateProcessor()
    {
        byte[] template = new OdtDocumentBuilder().AddParagraph("Hello {{Name}}!").ToBytes();

        ProcessingResult direct = new OdtTemplateProcessor(Options()).ProcessTemplate(template, _data, out byte[] expected);
        ProcessingResult facade = new TemplateProcessor(Options()).ProcessTemplate(template, _data, out byte[] actual);

        AssertSameResult(direct, facade);
        Assert.Equal(expected, actual);
        Assert.Equal(new[] { "Hello Alice!" }, new OdtDocumentVerifier(actual).GetParagraphTexts());
    }

    [Fact]
    public void ProcessTemplate_Ott_ProducesOdt()
    {
        byte[] template = new OdtDocumentBuilder().AddParagraph("Hello {{Name}}!").AsTemplate().ToBytes();

        ProcessingResult result = new TemplateProcessor(Options()).ProcessTemplate(template, _data, out byte[] output);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        OdtDocumentVerifier verifier = new OdtDocumentVerifier(output);
        verifier.AssertValidOdtPackage();
        Assert.Equal("application/vnd.oasis.opendocument.text", verifier.Mimetype);
    }

    [Fact]
    public void ProcessTemplate_Streams_DelegateByFormat()
    {
        TemplateProcessor processor = new TemplateProcessor(Options());

        using MemoryStream docxOutput = new MemoryStream();
        using MemoryStream docxTemplate = new MemoryStream(CreateDocx());
        ProcessingResult docxResult = processor.ProcessTemplate(docxTemplate, docxOutput, _data);

        using MemoryStream odtOutput = new MemoryStream();
        using MemoryStream odtTemplate = new OdtDocumentBuilder().AddParagraph("Hello {{Name}}!").ToStream();
        ProcessingResult odtResult = processor.ProcessTemplate(odtTemplate, odtOutput, _data);

        Assert.True(docxResult.IsSuccess, docxResult.ErrorMessage);
        Assert.True(odtResult.IsSuccess, odtResult.ErrorMessage);
        Assert.Equal(new[] { "Hello Alice!" }, DocxTexts(docxOutput.ToArray()));
        Assert.Equal(new[] { "Hello Alice!" }, new OdtDocumentVerifier(odtOutput.ToArray()).GetParagraphTexts());
    }

    [Fact]
    public void ProcessTemplate_ReadOnlyDictionaryAndJson_Delegate()
    {
        TemplateProcessor processor = new TemplateProcessor(Options());
        IReadOnlyDictionary<string, object?> readOnly = new Dictionary<string, object?> { ["Name"] = "Alice" };
        byte[] odt = new OdtDocumentBuilder().AddParagraph("Hello {{Name}}!").ToBytes();
        byte[] docx = CreateDocx();

        ProcessingResult odtReadOnly = processor.ProcessTemplate(odt, readOnly, out byte[] odtOut1);
        ProcessingResult odtJson = processor.ProcessTemplate(odt, "{\"Name\":\"Alice\"}", out byte[] odtOut2);
        ProcessingResult docxReadOnly = processor.ProcessTemplate(docx, readOnly, out byte[] docxOut1);
        ProcessingResult docxJson = processor.ProcessTemplate(docx, "{\"Name\":\"Alice\"}", out byte[] docxOut2);

        Assert.All(new[] { odtReadOnly, odtJson, docxReadOnly, docxJson }, r => Assert.True(r.IsSuccess, r.ErrorMessage));
        Assert.Equal("Hello Alice!", new OdtDocumentVerifier(odtOut1).GetParagraphTexts()[0]);
        Assert.Equal("Hello Alice!", new OdtDocumentVerifier(odtOut2).GetParagraphTexts()[0]);
        Assert.Equal("Hello Alice!", DocxTexts(docxOut1)[0]);
        Assert.Equal("Hello Alice!", DocxTexts(docxOut2)[0]);

        using MemoryStream jsonOutput = new MemoryStream();
        using MemoryStream jsonTemplate = new MemoryStream(odt);
        Assert.True(processor.ProcessTemplate(jsonTemplate, jsonOutput, "{\"Name\":\"Alice\"}").IsSuccess);
        using MemoryStream readOnlyOutput = new MemoryStream();
        using MemoryStream readOnlyTemplate = new MemoryStream(docx);
        Assert.True(processor.ProcessTemplate(readOnlyTemplate, readOnlyOutput, readOnly).IsSuccess);
    }

    [Fact]
    public void ProcessTemplate_NonSeekableTemplate_IsBuffered()
    {
        TemplateProcessor processor = new TemplateProcessor(Options());

        using NonSeekableStream odtTemplate = new NonSeekableStream(new OdtDocumentBuilder().AddParagraph("Hello {{Name}}!").ToStream());
        using MemoryStream odtOutput = new MemoryStream();
        ProcessingResult odtResult = processor.ProcessTemplate(odtTemplate, odtOutput, _data);

        using NonSeekableStream docxTemplate = new NonSeekableStream(new MemoryStream(CreateDocx()));
        using MemoryStream docxOutput = new MemoryStream();
        ProcessingResult docxResult = processor.ProcessTemplate(docxTemplate, docxOutput, _data);

        Assert.True(odtResult.IsSuccess, odtResult.ErrorMessage);
        Assert.True(docxResult.IsSuccess, docxResult.ErrorMessage);
        Assert.Equal("Hello Alice!", new OdtDocumentVerifier(odtOutput.ToArray()).GetParagraphTexts()[0]);
        Assert.Equal("Hello Alice!", DocxTexts(docxOutput.ToArray())[0]);
    }

    [Fact]
    public void ProcessTemplate_ResultsAndWarnings_AreThoseOfTheDelegate()
    {
        byte[] template = new OdtDocumentBuilder().AddParagraph("{{Name}} {{Missing}}").ToBytes();

        ProcessingResult direct = new OdtTemplateProcessor(Options()).ProcessTemplate(template, _data, out _);
        ProcessingResult facade = new TemplateProcessor(Options()).ProcessTemplate(template, _data, out _);

        AssertSameResult(direct, facade);
        Assert.Equal(new[] { "Missing" }, facade.MissingVariables);
    }

    [Fact]
    public void ProcessTemplate_TemplateSyntaxError_IsTheDelegatesFailure()
    {
        byte[] template = new OdtDocumentBuilder().AddParagraph("{{#if Name}}").AddParagraph("x").ToBytes();

        ProcessingResult direct = new OdtTemplateProcessor(Options()).ProcessTemplate(template, _data, out _);
        ProcessingResult facade = new TemplateProcessor(Options()).ProcessTemplate(template, _data, out byte[] output);

        Assert.False(facade.IsSuccess);
        Assert.Equal(direct.ErrorMessage, facade.ErrorMessage);
        Assert.Empty(output);
    }

    [Fact]
    public void ProcessTemplate_MissingVariableWithThrow_Throws()
    {
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.ThrowException,
        };
        byte[] template = new OdtDocumentBuilder().AddParagraph("{{Missing}}").ToBytes();

        Assert.Throws<InvalidOperationException>(
            () => new TemplateProcessor(options).ProcessTemplate(template, _data, out _));
    }

    [Fact]
    public void ProcessTemplate_OptionsAreUsedForBothFormats()
    {
        PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
        {
            MissingVariableBehavior = MissingVariableBehavior.ReplaceWithEmpty,
        };
        TemplateProcessor processor = new TemplateProcessor(options);

        processor.ProcessTemplate(new OdtDocumentBuilder().AddParagraph("[{{Missing}}]").ToBytes(), _data, out byte[] odt);
        processor.ProcessTemplate(CreateDocx("[{{Missing}}]"), _data, out byte[] docx);

        Assert.Equal("[]", new OdtDocumentVerifier(odt).GetParagraphTexts()[0]);
        Assert.Equal("[]", DocxTexts(docx)[0]);
    }

    // ---------- Unsupported formats ----------

    [Fact]
    public void ProcessTemplate_UnknownFormat_FailsWithClearMessageAndWritesNothing()
    {
        using MemoryStream template = new MemoryStream(Encoding.UTF8.GetBytes("not a document"));
        using MemoryStream output = new MemoryStream();

        ProcessingResult result = new TemplateProcessor().ProcessTemplate(template, output, _data);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("Unsupported template format", result.ErrorMessage);
        Assert.Contains(".docx", result.ErrorMessage);
        Assert.Contains(".odt", result.ErrorMessage);
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void ProcessTemplate_OtherOpenDocumentType_NamesMediaType()
    {
        byte[] template = CreateZip(("mimetype", "application/vnd.oasis.opendocument.spreadsheet"), ("content.xml", "<x/>"));

        ProcessingResult result = new TemplateProcessor().ProcessTemplate(template, _data, out byte[] output);

        Assert.False(result.IsSuccess);
        Assert.Contains("application/vnd.oasis.opendocument.spreadsheet", result.ErrorMessage);
        Assert.Empty(output);
    }

    [Fact]
    public void ProcessTemplate_EmptyTemplate_Fails()
    {
        ProcessingResult result = new TemplateProcessor().ProcessTemplate(Array.Empty<byte>(), _data, out byte[] output);

        Assert.False(result.IsSuccess);
        Assert.StartsWith("Unsupported template format", result.ErrorMessage);
        Assert.Empty(output);
    }

    [Fact]
    public void ProcessTemplate_UnreadableTemplate_Fails()
    {
        using MemoryStream inner = new MemoryStream(CreateDocx());
        using WriteOnlyStream template = new WriteOnlyStream(inner);
        using MemoryStream output = new MemoryStream();

        ProcessingResult result = new TemplateProcessor().ProcessTemplate(template, output, _data);

        Assert.False(result.IsSuccess);
        Assert.Contains("must be readable", result.ErrorMessage);
    }

    [Fact]
    public void ProcessTemplate_NullArguments_Throw()
    {
        TemplateProcessor processor = new TemplateProcessor();
        using MemoryStream stream = new MemoryStream();

        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplate(null!, stream, _data));
        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplate(stream, null!, _data));
        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplate(stream, stream, (Dictionary<string, object>)null!));
        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplate((byte[])null!, _data, out _));
        Assert.Throws<ArgumentNullException>(() => processor.ProcessTemplate(Array.Empty<byte>(), (string)null!, out _));
        Assert.Throws<ArgumentException>(() => processor.ProcessTemplateFile(" ", "out.odt", _data));
        Assert.Throws<ArgumentNullException>(() => processor.ValidateTemplate(null!));
    }

    [Fact]
    public void ProcessTemplate_InvalidJson_ThrowsBeforeDetection()
    {
        Assert.ThrowsAny<System.Text.Json.JsonException>(
            () => new TemplateProcessor().ProcessTemplate(Array.Empty<byte>(), "{not json", out _));
    }

    // ---------- Files ----------

    [Fact]
    public void ProcessTemplateFile_DelegatesByContentNotExtension()
    {
        string directory = Directory.CreateTempSubdirectory("templify-facade-").FullName;
        try
        {
            // An OpenDocument template saved with a misleading extension is still detected by content.
            string odtTemplate = Path.Combine(directory, "template.docx");
            string docxTemplate = Path.Combine(directory, "template.odt");
            File.WriteAllBytes(odtTemplate, new OdtDocumentBuilder().AddParagraph("Hello {{Name}}!").ToBytes());
            File.WriteAllBytes(docxTemplate, CreateDocx());
            TemplateProcessor processor = new TemplateProcessor(Options());

            string odtOutput = Path.Combine(directory, "out1");
            string docxOutput = Path.Combine(directory, "out2");
            string jsonOutput = Path.Combine(directory, "out3");
            string readOnlyOutput = Path.Combine(directory, "out4");
            Assert.True(processor.ProcessTemplateFile(odtTemplate, odtOutput, _data).IsSuccess);
            Assert.True(processor.ProcessTemplateFile(docxTemplate, docxOutput, _data).IsSuccess);
            Assert.True(processor.ProcessTemplateFile(odtTemplate, jsonOutput, "{\"Name\":\"Alice\"}").IsSuccess);
            Assert.True(processor.ProcessTemplateFile(
                docxTemplate, readOnlyOutput, (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["Name"] = "Alice" }).IsSuccess);

            Assert.Equal("Hello Alice!", new OdtDocumentVerifier(File.ReadAllBytes(odtOutput)).GetParagraphTexts()[0]);
            Assert.Equal("Hello Alice!", DocxTexts(File.ReadAllBytes(docxOutput))[0]);
            Assert.Equal("Hello Alice!", new OdtDocumentVerifier(File.ReadAllBytes(jsonOutput)).GetParagraphTexts()[0]);
            Assert.Equal("Hello Alice!", DocxTexts(File.ReadAllBytes(readOnlyOutput))[0]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ProcessTemplateFile_UnknownFormat_DoesNotCreateOutput()
    {
        string directory = Directory.CreateTempSubdirectory("templify-facade-").FullName;
        try
        {
            string template = Path.Combine(directory, "template.odt");
            string output = Path.Combine(directory, "out.odt");
            File.WriteAllText(template, "plain text");

            ProcessingResult result = new TemplateProcessor().ProcessTemplateFile(template, output, _data);

            Assert.False(result.IsSuccess);
            Assert.StartsWith("Unsupported template format", result.ErrorMessage);
            Assert.False(File.Exists(output));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ProcessTemplateFile_MissingTemplate_Throws()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"templify-missing-{Guid.NewGuid():N}.odt");

        Assert.Throws<FileNotFoundException>(() => new TemplateProcessor().ProcessTemplateFile(missing, missing + ".out", _data));
    }

    // ---------- Validation ----------

    [Fact]
    public void ValidateTemplate_DelegatesByFormat()
    {
        TemplateProcessor processor = new TemplateProcessor(Options());
        byte[] odt = new OdtDocumentBuilder().AddParagraph("{{#if Flag}}").AddParagraph("{{Name}}").ToBytes();
        byte[] docx = CreateDocx("{{Name}} {{Other}}");

        using MemoryStream odtStream = new MemoryStream(odt);
        ValidationResult odtFacade = processor.ValidateTemplate(odtStream);
        using MemoryStream odtDirectStream = new MemoryStream(odt);
        ValidationResult odtDirect = new OdtTemplateProcessor(Options()).ValidateTemplate(odtDirectStream);

        using MemoryStream docxStream = new MemoryStream(docx);
        ValidationResult docxFacade = processor.ValidateTemplate(docxStream, _data);
        using MemoryStream docxDirectStream = new MemoryStream(docx);
        ValidationResult docxDirect = new DocumentTemplateProcessor(Options()).ValidateTemplate(docxDirectStream, _data);

        Assert.False(odtFacade.IsValid);
        Assert.Equal(odtDirect.Errors.Select(e => e.Message), odtFacade.Errors.Select(e => e.Message));
        Assert.Equal(odtDirect.AllPlaceholders, odtFacade.AllPlaceholders);
        Assert.Equal(docxDirect.MissingVariables, docxFacade.MissingVariables);
        Assert.Equal(new[] { "Other" }, docxFacade.MissingVariables);
    }

    [Fact]
    public void ValidateTemplate_ReadOnlyDictionaryAndNonSeekableStream()
    {
        IReadOnlyDictionary<string, object?> data = new Dictionary<string, object?> { ["Name"] = "Alice" };
        using NonSeekableStream template = new NonSeekableStream(new OdtDocumentBuilder().AddParagraph("{{Name}} {{Other}}").ToStream());

        ValidationResult result = new TemplateProcessor().ValidateTemplate(template, data);

        Assert.Equal(new[] { "Other" }, result.MissingVariables);
        Assert.Equal(new[] { "Name", "Other" }, result.AllPlaceholders);
    }

    [Fact]
    public void ValidateTemplate_UnknownFormat_IsInvalid()
    {
        using MemoryStream template = new MemoryStream(CreateZip(("readme.txt", "hello")));

        ValidationResult result = new TemplateProcessor().ValidateTemplate(template);

        Assert.False(result.IsValid);
        ValidationError error = Assert.Single(result.Errors);
        Assert.StartsWith("Unsupported template format", error.Message);
    }

    [Fact]
    public void ValidateTemplate_UnreadableStream_IsInvalid()
    {
        using MemoryStream inner = new MemoryStream(CreateDocx());
        using WriteOnlyStream template = new WriteOnlyStream(inner);

        ValidationResult result = new TemplateProcessor().ValidateTemplate(template, _data);

        Assert.False(result.IsValid);
        Assert.Contains("must be readable", Assert.Single(result.Errors).Message);
    }

    // ---------- Helpers ----------

    private static PlaceholderReplacementOptions Options() =>
        new PlaceholderReplacementOptions { Culture = CultureInfo.InvariantCulture };

    private static byte[] CreateDocx(string text = "Hello {{Name}}!")
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph(text);
        return builder.ToStream().ToArray();
    }

    private static TemplateFormat DetectBytes(byte[] bytes)
    {
        using MemoryStream stream = new MemoryStream(bytes);
        return TemplateProcessor.DetectFormat(stream);
    }

    private static List<string> DocxTexts(byte[] docx)
    {
        using DocumentVerifier verifier = new DocumentVerifier(new MemoryStream(docx));
        return verifier.GetAllParagraphTexts();
    }

    private static void AssertSameResult(ProcessingResult expected, ProcessingResult actual)
    {
        Assert.Equal(expected.IsSuccess, actual.IsSuccess);
        Assert.Equal(expected.ErrorMessage, actual.ErrorMessage);
        Assert.Equal(expected.ReplacementCount, actual.ReplacementCount);
        Assert.Equal(expected.MissingVariables, actual.MissingVariables);
        Assert.Equal(expected.Warnings.Select(w => w.Message), actual.Warnings.Select(w => w.Message));
    }

    private static byte[] WithMainContentType(byte[] docx, string contentType)
    {
        using MemoryStream stream = new MemoryStream();
        stream.Write(docx);
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            ZipArchiveEntry entry = archive.GetEntry("[Content_Types].xml")!;
            string xml;
            using (StreamReader reader = new StreamReader(entry.Open()))
            {
                xml = reader.ReadToEnd();
            }

            Assert.Contains(WordMainContentType, xml);
            entry.Delete();
            ZipArchiveEntry replacement = archive.CreateEntry("[Content_Types].xml");
            using StreamWriter writer = new StreamWriter(replacement.Open());
            writer.Write(xml.Replace(WordMainContentType, contentType));
        }

        return stream.ToArray();
    }

    private static byte[] CreateZip(params (string Name, string Content)[] entries)
    {
        using MemoryStream stream = new MemoryStream();
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
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

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class WriteOnlyStream : Stream
    {
        private readonly Stream _inner;

        public WriteOnlyStream(Stream inner)
        {
            _inner = inner;
        }

        public override bool CanRead => false;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        public override void SetLength(long value) => _inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    }
}
