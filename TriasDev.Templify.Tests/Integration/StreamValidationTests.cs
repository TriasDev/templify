// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;
using TriasDev.Templify.Tests.Helpers;

namespace TriasDev.Templify.Tests.Integration;

/// <summary>
/// Unusable template/output streams are reported up front as a failed result with a clear message,
/// before anything is written to the output (#156).
/// </summary>
public sealed class StreamValidationTests
{
    private static readonly Dictionary<string, object> _data = new Dictionary<string, object> { ["Name"] = "Alice" };

    [Fact]
    public void ProcessTemplate_NonWritableOutput_FailsWithClearMessage()
    {
        MemoryStream output = new MemoryStream(new byte[16], writable: false);

        ProcessingResult result = new DocumentTemplateProcessor().ProcessTemplate(CreateTemplate(), output, _data);

        AssertOutputStreamFailure(result);
    }

    [Fact]
    public void ProcessTemplate_NonSeekableOutput_FailsBeforeWriting()
    {
        CapabilityStream output = new CapabilityStream(canRead: true, canWrite: true, canSeek: false);

        ProcessingResult result = new DocumentTemplateProcessor().ProcessTemplate(CreateTemplate(), output, _data);

        AssertOutputStreamFailure(result);
        Assert.Equal(0, output.BytesWritten);
    }

    [Fact]
    public void ProcessTemplate_WriteOnlyOutput_FailsBeforeWriting()
    {
        CapabilityStream output = new CapabilityStream(canRead: false, canWrite: true, canSeek: true);

        ProcessingResult result = new DocumentTemplateProcessor().ProcessTemplate(CreateTemplate(), output, _data);

        AssertOutputStreamFailure(result);
        Assert.Equal(0, output.BytesWritten);
    }

    [Fact]
    public void ProcessTemplate_WriteOnlyFileStream_FailsBeforeWriting()
    {
        string path = Path.Combine(Path.GetTempPath(), "templify-writeonly-" + Guid.NewGuid().ToString("N") + ".docx");
        try
        {
            ProcessingResult result;
            using (FileStream output = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                result = new DocumentTemplateProcessor().ProcessTemplate(CreateTemplate(), output, _data);
            }

            AssertOutputStreamFailure(result);
            Assert.Equal(0, new FileInfo(path).Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ProcessTemplate_NonReadableTemplate_FailsWithClearMessage()
    {
        CapabilityStream template = new CapabilityStream(canRead: false, canWrite: true, canSeek: true);
        MemoryStream output = new MemoryStream();

        ProcessingResult result = new DocumentTemplateProcessor().ProcessTemplate(template, output, _data);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid template stream: the template stream must be readable.", result.ErrorMessage);
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void ProcessTemplate_JsonOverloadNonSeekableOutput_FailsWithClearMessage()
    {
        CapabilityStream output = new CapabilityStream(canRead: true, canWrite: true, canSeek: false);

        ProcessingResult result = new DocumentTemplateProcessor().ProcessTemplate(CreateTemplate(), output, """{ "Name": "Alice" }""");

        AssertOutputStreamFailure(result);
    }

    [Fact]
    public void ProcessTemplate_ReadOnlyDataOverloadNonWritableOutput_FailsWithClearMessage()
    {
        MemoryStream output = new MemoryStream(new byte[16], writable: false);
        IReadOnlyDictionary<string, object?> data = new Dictionary<string, object?> { ["Name"] = "Alice" };

        ProcessingResult result = new DocumentTemplateProcessor().ProcessTemplate(CreateTemplate(), output, data);

        AssertOutputStreamFailure(result);
    }

    [Fact]
    public void ProcessTemplate_NonSeekableTemplate_IsSupported()
    {
        MemoryStream template = CreateTemplate();
        CapabilityStream nonSeekableTemplate = new CapabilityStream(canRead: true, canWrite: false, canSeek: false, template.ToArray());
        MemoryStream output = new MemoryStream();

        ProcessingResult result = new DocumentTemplateProcessor().ProcessTemplate(nonSeekableTemplate, output, _data);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        using DocumentVerifier verifier = new DocumentVerifier(output);
        Assert.Equal("Hello Alice!", verifier.GetParagraphText(0));
    }

    [Fact]
    public void ProcessTemplate_ExistingLongerOutputFile_IsTruncated()
    {
        // An existing file opened without truncation (FileMode.OpenOrCreate) used to keep its trailing bytes after
        // the template copy, so the package could not be opened and processing failed.
        string path = Path.Combine(Path.GetTempPath(), "templify-longer-" + Guid.NewGuid().ToString("N") + ".docx");
        try
        {
            File.WriteAllBytes(path, new byte[200 * 1024]);
            ProcessingResult result;
            using (FileStream output = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                result = new DocumentTemplateProcessor().ProcessTemplate(CreateTemplate(), output, _data);
            }

            Assert.True(result.IsSuccess, result.ErrorMessage);
            byte[] written = File.ReadAllBytes(path);
            Assert.True(written.Length < 200 * 1024);
            using DocumentVerifier verifier = new DocumentVerifier(new MemoryStream(written));
            Assert.Equal("Hello Alice!", verifier.GetParagraphText(0));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void AssertOutputStreamFailure(ProcessingResult result)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Invalid output stream: the output stream must be readable, writable and seekable "
            + "(for example a MemoryStream, or a FileStream opened with FileAccess.ReadWrite).",
            result.ErrorMessage);
    }

    private static MemoryStream CreateTemplate()
    {
        DocumentBuilder builder = new DocumentBuilder();
        builder.AddParagraph("Hello {{Name}}!");
        return builder.ToStream();
    }

    /// <summary>
    /// A stream with configurable capabilities that counts written bytes.
    /// </summary>
    private sealed class CapabilityStream : Stream
    {
        private readonly MemoryStream _inner;
        private readonly bool _canRead;
        private readonly bool _canWrite;
        private readonly bool _canSeek;

        public CapabilityStream(bool canRead, bool canWrite, bool canSeek, byte[]? content = null)
        {
            _canRead = canRead;
            _canWrite = canWrite;
            _canSeek = canSeek;
            _inner = content == null ? new MemoryStream() : new MemoryStream(content);
        }

        public long BytesWritten { get; private set; }

        public override bool CanRead => _canRead;

        public override bool CanWrite => _canWrite;

        public override bool CanSeek => _canSeek;

        public override long Length => _canSeek ? _inner.Length : throw new NotSupportedException();

        public override long Position
        {
            get => _canSeek ? _inner.Position : throw new NotSupportedException();
            set
            {
                if (!_canSeek)
                {
                    throw new NotSupportedException();
                }

                _inner.Position = value;
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _canRead ? _inner.Read(buffer, offset, count) : throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _canSeek ? _inner.Seek(offset, origin) : throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            if (!_canSeek || !_canWrite)
            {
                throw new NotSupportedException();
            }

            _inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!_canWrite)
            {
                throw new NotSupportedException();
            }

            _inner.Write(buffer, offset, count);
            BytesWritten += count;
        }
    }
}
