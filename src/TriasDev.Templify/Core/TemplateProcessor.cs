// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Core;

/// <summary>
/// Processes Word (.docx) and OpenDocument Text (.odt, .ott) templates through one entry point. The format is
/// detected from the package content (not from the file name), and processing is delegated to
/// <see cref="DocumentTemplateProcessor"/> or <see cref="OdtTemplateProcessor"/>.
/// </summary>
/// <remarks>
/// <para>
/// Detection reads the ZIP package: an OpenDocument package names its media type in the <c>mimetype</c> entry
/// (<c>application/vnd.oasis.opendocument.text</c>, or <c>…-text-template</c> for .ott), a Word package declares
/// a WordprocessingML main document in <c>[Content_Types].xml</c> (.docx, and also .docm, .dotx and .dotm).
/// See <see cref="DetectFormat(Stream)"/>.
/// </para>
/// <para>
/// Results, warnings, options and exceptions are exactly those of the processor the template is delegated to,
/// including the stream requirements for the output: a Word document is edited in place and needs a readable,
/// writable and seekable output stream; an OpenDocument document only needs a writable one. A
/// <see cref="MemoryStream"/>, or a <see cref="FileStream"/> opened with <see cref="FileAccess.ReadWrite"/>,
/// works for both. The output has the format of the template (an .ott template produces an .odt document).
/// </para>
/// <para>
/// A template stream that is not seekable is first copied into memory, because the format has to be detected
/// before the template is processed. A template in an unsupported format (not a ZIP package, another
/// OpenDocument type such as a spreadsheet, a legacy .doc, or a flat OpenDocument .fodt) is reported as a
/// failed <see cref="ProcessingResult"/>, or as an invalid <see cref="ValidationResult"/>, and nothing is written.
/// </para>
/// <para>
/// Use <see cref="DocumentTemplateProcessor"/> or <see cref="OdtTemplateProcessor"/> directly when the format is
/// known in advance.
/// </para>
/// </remarks>
public sealed class TemplateProcessor
{
    private const string UnreadableTemplateMessage = "Invalid template stream: the template stream must be readable.";

    private readonly DocumentTemplateProcessor _docxProcessor;
    private readonly OdtTemplateProcessor _odtProcessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateProcessor"/> class.
    /// </summary>
    /// <param name="options">
    /// Configuration options for placeholder replacement, used for both formats. If null, default options are used.
    /// </param>
    public TemplateProcessor(PlaceholderReplacementOptions? options = null)
    {
        PlaceholderReplacementOptions effective = options ?? new PlaceholderReplacementOptions();
        _docxProcessor = new DocumentTemplateProcessor(effective);
        _odtProcessor = new OdtTemplateProcessor(effective);
    }

    /// <summary>
    /// Detects the format of a template from its content.
    /// </summary>
    /// <param name="templateStream">
    /// Stream containing the template. Must be readable and seekable. The whole package is inspected, regardless
    /// of the current position, and the position is restored before the method returns.
    /// </param>
    /// <returns>
    /// <see cref="TemplateFormat.Docx"/> for a Word package, <see cref="TemplateFormat.Odt"/> for an OpenDocument
    /// Text document or template, and <see cref="TemplateFormat.Unknown"/> for anything else (including an empty
    /// stream and other OpenDocument types).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when templateStream is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when templateStream is not readable or not seekable. Copy a non-seekable stream into a
    /// <see cref="MemoryStream"/> first.
    /// </exception>
    public static TemplateFormat DetectFormat(Stream templateStream)
    {
        ArgumentNullException.ThrowIfNull(templateStream);
        if (!templateStream.CanRead || !templateStream.CanSeek)
        {
            throw new ArgumentException(
                "The template stream must be readable and seekable to detect its format.", nameof(templateStream));
        }

        long position = templateStream.Position;
        try
        {
            return TemplateFormatDetector.Detect(templateStream, out _);
        }
        finally
        {
            templateStream.Position = position;
        }
    }

    /// <summary>
    /// Processes a Word or OpenDocument Text template, replacing placeholders with values from the data dictionary.
    /// </summary>
    /// <param name="templateStream">
    /// Stream containing the template (.docx or .odt/.ott). Must be readable; a stream that is not seekable is
    /// copied into memory first.
    /// </param>
    /// <param name="outputStream">
    /// Stream to write the processed document to. Readable, writable and seekable for a Word template; writable
    /// for an OpenDocument template. See the remarks on <see cref="TemplateProcessor"/>.
    /// </param>
    /// <param name="data">Dictionary containing variable names and their replacement values.</param>
    /// <returns>
    /// The <see cref="ProcessingResult"/> of the processor for the detected format, or a failed result when the
    /// template stream is not readable or its format is not supported.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown only when a variable is missing and <see cref="MissingVariableBehavior.ThrowException"/> is configured.
    /// </exception>
    public ProcessingResult ProcessTemplate(
        Stream templateStream,
        Stream outputStream,
        Dictionary<string, object> data)
    {
        ArgumentNullException.ThrowIfNull(templateStream);
        ArgumentNullException.ThrowIfNull(outputStream);
        ArgumentNullException.ThrowIfNull(data);

        return ProcessStream(
            templateStream,
            (format, template) => format == TemplateFormat.Odt
                ? _odtProcessor.ProcessTemplate(template, outputStream, data)
                : _docxProcessor.ProcessTemplate(template, outputStream, data));
    }

    /// <summary>
    /// Processes a Word or OpenDocument Text template, replacing placeholders with values from read-only data.
    /// </summary>
    /// <param name="templateStream">
    /// Stream containing the template (.docx or .odt/.ott). Must be readable; a stream that is not seekable is
    /// copied into memory first.
    /// </param>
    /// <param name="outputStream">
    /// Stream to write the processed document to. Readable, writable and seekable for a Word template; writable
    /// for an OpenDocument template.
    /// </param>
    /// <param name="data">
    /// Variable names and their replacement values. Not copied: lookups use the dictionary's own key comparer.
    /// </param>
    /// <returns>
    /// The <see cref="ProcessingResult"/> of the processor for the detected format, or a failed result when the
    /// template stream is not readable or its format is not supported.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown only when a variable is missing and <see cref="MissingVariableBehavior.ThrowException"/> is configured.
    /// </exception>
    public ProcessingResult ProcessTemplate(
        Stream templateStream,
        Stream outputStream,
        IReadOnlyDictionary<string, object?> data)
    {
        ArgumentNullException.ThrowIfNull(templateStream);
        ArgumentNullException.ThrowIfNull(outputStream);
        ArgumentNullException.ThrowIfNull(data);

        return ProcessStream(
            templateStream,
            (format, template) => format == TemplateFormat.Odt
                ? _odtProcessor.ProcessTemplate(template, outputStream, data)
                : _docxProcessor.ProcessTemplate(template, outputStream, data));
    }

    /// <summary>
    /// Processes a Word or OpenDocument Text template, replacing placeholders with values from a JSON string.
    /// </summary>
    /// <param name="templateStream">
    /// Stream containing the template (.docx or .odt/.ott). Must be readable; a stream that is not seekable is
    /// copied into memory first.
    /// </param>
    /// <param name="outputStream">
    /// Stream to write the processed document to. Readable, writable and seekable for a Word template; writable
    /// for an OpenDocument template.
    /// </param>
    /// <param name="jsonData">JSON string containing variable names and their replacement values. Must be a valid JSON object (not an array).</param>
    /// <returns>A <see cref="ProcessingResult"/> indicating success or failure and providing metrics.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when jsonData is empty or whitespace.</exception>
    /// <exception cref="JsonException">Thrown when jsonData is invalid JSON or root is not an object.</exception>
    public ProcessingResult ProcessTemplate(
        Stream templateStream,
        Stream outputStream,
        string jsonData)
    {
        ArgumentNullException.ThrowIfNull(jsonData);

        // JSON parse errors (JsonException, ArgumentException) propagate to the caller, as in the other processors.
        Dictionary<string, object> data = JsonDataParser.ParseJsonToDataDictionary(jsonData);
        return ProcessTemplate(templateStream, outputStream, data);
    }

    /// <summary>
    /// Processes a Word or OpenDocument Text template held in memory and returns the processed document as a
    /// byte array.
    /// </summary>
    /// <param name="template">The template file content (.docx or .odt/.ott). Not modified.</param>
    /// <param name="data">Dictionary containing variable names and their replacement values.</param>
    /// <param name="output">
    /// When this method returns, the processed file content (in the format of the template) if processing
    /// succeeded; otherwise an empty array.
    /// </param>
    /// <returns>
    /// The <see cref="ProcessingResult"/> of the processor for the detected format, or a failed result when the
    /// format is not supported.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown only when a variable is missing and <see cref="MissingVariableBehavior.ThrowException"/> is configured.
    /// </exception>
    public ProcessingResult ProcessTemplate(
        byte[] template,
        Dictionary<string, object> data,
        out byte[] output)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

        switch (Detect(template, out string? odfMediaType))
        {
            case TemplateFormat.Docx:
                return _docxProcessor.ProcessTemplate(template, data, out output);
            case TemplateFormat.Odt:
                return _odtProcessor.ProcessTemplate(template, data, out output);
            default:
                output = Array.Empty<byte>();
                return UnsupportedFormat(odfMediaType);
        }
    }

    /// <summary>
    /// Processes a Word or OpenDocument Text template held in memory, using read-only data, and returns the
    /// processed document as a byte array.
    /// </summary>
    /// <param name="template">The template file content (.docx or .odt/.ott). Not modified.</param>
    /// <param name="data">
    /// Variable names and their replacement values. Not copied: lookups use the dictionary's own key comparer.
    /// </param>
    /// <param name="output">
    /// When this method returns, the processed file content (in the format of the template) if processing
    /// succeeded; otherwise an empty array.
    /// </param>
    /// <returns>
    /// The <see cref="ProcessingResult"/> of the processor for the detected format, or a failed result when the
    /// format is not supported.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown only when a variable is missing and <see cref="MissingVariableBehavior.ThrowException"/> is configured.
    /// </exception>
    public ProcessingResult ProcessTemplate(
        byte[] template,
        IReadOnlyDictionary<string, object?> data,
        out byte[] output)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

        switch (Detect(template, out string? odfMediaType))
        {
            case TemplateFormat.Docx:
                return _docxProcessor.ProcessTemplate(template, data, out output);
            case TemplateFormat.Odt:
                return _odtProcessor.ProcessTemplate(template, data, out output);
            default:
                output = Array.Empty<byte>();
                return UnsupportedFormat(odfMediaType);
        }
    }

    /// <summary>
    /// Processes a Word or OpenDocument Text template held in memory, using data from a JSON string, and returns
    /// the processed document as a byte array.
    /// </summary>
    /// <param name="template">The template file content (.docx or .odt/.ott). Not modified.</param>
    /// <param name="jsonData">JSON string containing variable names and their replacement values. Must be a valid JSON object (not an array).</param>
    /// <param name="output">
    /// When this method returns, the processed file content (in the format of the template) if processing
    /// succeeded; otherwise an empty array.
    /// </param>
    /// <returns>A <see cref="ProcessingResult"/> indicating success or failure and providing metrics.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when jsonData is empty or whitespace.</exception>
    /// <exception cref="JsonException">Thrown when jsonData is invalid JSON or root is not an object.</exception>
    public ProcessingResult ProcessTemplate(
        byte[] template,
        string jsonData,
        out byte[] output)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(jsonData);

        Dictionary<string, object> data = JsonDataParser.ParseJsonToDataDictionary(jsonData);
        return ProcessTemplate(template, data, out output);
    }

    /// <summary>
    /// Processes a Word or OpenDocument Text template file and writes the processed document to a file.
    /// </summary>
    /// <param name="templatePath">Path of the template file (.docx or .odt/.ott). Not modified.</param>
    /// <param name="outputPath">
    /// Path of the output file. Created or overwritten only when processing succeeds; may be the same path as
    /// <paramref name="templatePath"/>. The output has the format of the template (.odt for an .ott template),
    /// whatever the extension of this path.
    /// </param>
    /// <param name="data">Dictionary containing variable names and their replacement values.</param>
    /// <returns>
    /// The <see cref="ProcessingResult"/> of the processor for the detected format, or a failed result when the
    /// format is not supported.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a path is empty or whitespace.</exception>
    /// <exception cref="IOException">
    /// Thrown when the template file cannot be read or the output file cannot be written (for example
    /// <see cref="FileNotFoundException"/> or <see cref="DirectoryNotFoundException"/>).
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to a file is denied.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown only when a variable is missing and <see cref="MissingVariableBehavior.ThrowException"/> is configured.
    /// </exception>
    public ProcessingResult ProcessTemplateFile(
        string templatePath,
        string outputPath,
        Dictionary<string, object> data)
    {
        ValidatePaths(templatePath, outputPath);
        ArgumentNullException.ThrowIfNull(data);

        return ProcessFile(
            templatePath,
            format => format == TemplateFormat.Odt
                ? _odtProcessor.ProcessTemplateFile(templatePath, outputPath, data)
                : _docxProcessor.ProcessTemplateFile(templatePath, outputPath, data));
    }

    /// <summary>
    /// Processes a Word or OpenDocument Text template file, using read-only data, and writes the processed
    /// document to a file.
    /// </summary>
    /// <param name="templatePath">Path of the template file (.docx or .odt/.ott). Not modified.</param>
    /// <param name="outputPath">
    /// Path of the output file. Created or overwritten only when processing succeeds; may be the same path as
    /// <paramref name="templatePath"/>. The output has the format of the template.
    /// </param>
    /// <param name="data">
    /// Variable names and their replacement values. Not copied: lookups use the dictionary's own key comparer.
    /// </param>
    /// <returns>
    /// The <see cref="ProcessingResult"/> of the processor for the detected format, or a failed result when the
    /// format is not supported.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a path is empty or whitespace.</exception>
    /// <exception cref="IOException">
    /// Thrown when the template file cannot be read or the output file cannot be written (for example
    /// <see cref="FileNotFoundException"/> or <see cref="DirectoryNotFoundException"/>).
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to a file is denied.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown only when a variable is missing and <see cref="MissingVariableBehavior.ThrowException"/> is configured.
    /// </exception>
    public ProcessingResult ProcessTemplateFile(
        string templatePath,
        string outputPath,
        IReadOnlyDictionary<string, object?> data)
    {
        ValidatePaths(templatePath, outputPath);
        ArgumentNullException.ThrowIfNull(data);

        return ProcessFile(
            templatePath,
            format => format == TemplateFormat.Odt
                ? _odtProcessor.ProcessTemplateFile(templatePath, outputPath, data)
                : _docxProcessor.ProcessTemplateFile(templatePath, outputPath, data));
    }

    /// <summary>
    /// Processes a Word or OpenDocument Text template file, using data from a JSON string, and writes the
    /// processed document to a file.
    /// </summary>
    /// <param name="templatePath">Path of the template file (.docx or .odt/.ott). Not modified.</param>
    /// <param name="outputPath">
    /// Path of the output file. Created or overwritten only when processing succeeds; may be the same path as
    /// <paramref name="templatePath"/>. The output has the format of the template.
    /// </param>
    /// <param name="jsonData">JSON string containing variable names and their replacement values. Must be a valid JSON object (not an array).</param>
    /// <returns>A <see cref="ProcessingResult"/> indicating success or failure and providing metrics.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a path or jsonData is empty or whitespace.</exception>
    /// <exception cref="JsonException">Thrown when jsonData is invalid JSON or root is not an object.</exception>
    /// <exception cref="IOException">
    /// Thrown when the template file cannot be read or the output file cannot be written (for example
    /// <see cref="FileNotFoundException"/> or <see cref="DirectoryNotFoundException"/>).
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to a file is denied.</exception>
    public ProcessingResult ProcessTemplateFile(
        string templatePath,
        string outputPath,
        string jsonData)
    {
        ValidatePaths(templatePath, outputPath);
        ArgumentNullException.ThrowIfNull(jsonData);

        Dictionary<string, object> data = JsonDataParser.ParseJsonToDataDictionary(jsonData);
        return ProcessTemplateFile(templatePath, outputPath, data);
    }

    /// <summary>
    /// Validates a Word or OpenDocument Text template for syntax errors (unmatched markers, invalid conditions,
    /// invalid iteration variables). Does not check for missing variables since no data is provided.
    /// </summary>
    /// <param name="templateStream">
    /// Stream containing the template (.docx or .odt/.ott). Must be readable; a stream that is not seekable is
    /// copied into memory first.
    /// </param>
    /// <returns>
    /// The <see cref="ValidationResult"/> of the processor for the detected format. A template stream that is not
    /// readable, or a template in an unsupported format, is reported as an error.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when templateStream is null.</exception>
    public ValidationResult ValidateTemplate(Stream templateStream)
    {
        ArgumentNullException.ThrowIfNull(templateStream);

        return ValidateStream(
            templateStream,
            (format, template) => format == TemplateFormat.Odt
                ? _odtProcessor.ValidateTemplate(template)
                : _docxProcessor.ValidateTemplate(template));
    }

    /// <summary>
    /// Validates a Word or OpenDocument Text template for syntax errors and missing variables.
    /// </summary>
    /// <param name="templateStream">
    /// Stream containing the template (.docx or .odt/.ott). Must be readable; a stream that is not seekable is
    /// copied into memory first.
    /// </param>
    /// <param name="data">Dictionary containing variable names and their values for validation.</param>
    /// <returns>
    /// The <see cref="ValidationResult"/> of the processor for the detected format. A template stream that is not
    /// readable, or a template in an unsupported format, is reported as an error.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public ValidationResult ValidateTemplate(Stream templateStream, Dictionary<string, object> data)
    {
        ArgumentNullException.ThrowIfNull(templateStream);
        ArgumentNullException.ThrowIfNull(data);

        return ValidateStream(
            templateStream,
            (format, template) => format == TemplateFormat.Odt
                ? _odtProcessor.ValidateTemplate(template, data)
                : _docxProcessor.ValidateTemplate(template, data));
    }

    /// <summary>
    /// Validates a Word or OpenDocument Text template for syntax errors and missing variables, using read-only data.
    /// </summary>
    /// <param name="templateStream">
    /// Stream containing the template (.docx or .odt/.ott). Must be readable; a stream that is not seekable is
    /// copied into memory first.
    /// </param>
    /// <param name="data">
    /// Variable names and their values for validation. Not copied: lookups use the dictionary's own key comparer.
    /// </param>
    /// <returns>
    /// The <see cref="ValidationResult"/> of the processor for the detected format. A template stream that is not
    /// readable, or a template in an unsupported format, is reported as an error.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public ValidationResult ValidateTemplate(Stream templateStream, IReadOnlyDictionary<string, object?> data)
    {
        ArgumentNullException.ThrowIfNull(templateStream);
        ArgumentNullException.ThrowIfNull(data);

        return ValidateStream(
            templateStream,
            (format, template) => format == TemplateFormat.Odt
                ? _odtProcessor.ValidateTemplate(template, data)
                : _docxProcessor.ValidateTemplate(template, data));
    }

    /// <summary>
    /// Detects the format of a template stream (buffering a non-seekable one) and runs the matching processor.
    /// </summary>
    private static ProcessingResult ProcessStream(
        Stream templateStream,
        Func<TemplateFormat, Stream, ProcessingResult> process)
    {
        if (!templateStream.CanRead)
        {
            return ProcessingResult.Failure(UnreadableTemplateMessage);
        }

        MemoryStream? buffer = BufferIfNotSeekable(templateStream);
        try
        {
            Stream template = buffer ?? templateStream;
            TemplateFormat format = DetectAndRestore(template, out string? odfMediaType);
            return format == TemplateFormat.Unknown
                ? UnsupportedFormat(odfMediaType)
                : process(format, template);
        }
        finally
        {
            buffer?.Dispose();
        }
    }

    /// <summary>
    /// Detects the format of a template stream (buffering a non-seekable one) and runs the matching validator.
    /// </summary>
    private static ValidationResult ValidateStream(
        Stream templateStream,
        Func<TemplateFormat, Stream, ValidationResult> validate)
    {
        if (!templateStream.CanRead)
        {
            return InvalidTemplate(UnreadableTemplateMessage);
        }

        MemoryStream? buffer = BufferIfNotSeekable(templateStream);
        try
        {
            Stream template = buffer ?? templateStream;
            TemplateFormat format = DetectAndRestore(template, out string? odfMediaType);
            return format == TemplateFormat.Unknown
                ? InvalidTemplate(TemplateFormatDetector.GetUnsupportedFormatMessage(odfMediaType))
                : validate(format, template);
        }
        finally
        {
            buffer?.Dispose();
        }
    }

    /// <summary>
    /// Detects the format of a template file and runs the matching processor, which reads the file itself.
    /// </summary>
    private static ProcessingResult ProcessFile(string templatePath, Func<TemplateFormat, ProcessingResult> process)
    {
        TemplateFormat format;
        string? odfMediaType;
        using (FileStream stream = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            format = TemplateFormatDetector.Detect(stream, out odfMediaType);
        }

        return format == TemplateFormat.Unknown ? UnsupportedFormat(odfMediaType) : process(format);
    }

    private static TemplateFormat Detect(byte[] template, out string? odfMediaType)
    {
        using MemoryStream stream = new MemoryStream(template, writable: false);
        return TemplateFormatDetector.Detect(stream, out odfMediaType);
    }

    private static TemplateFormat DetectAndRestore(Stream template, out string? odfMediaType)
    {
        long position = template.Position;
        try
        {
            return TemplateFormatDetector.Detect(template, out odfMediaType);
        }
        finally
        {
            template.Position = position;
        }
    }

    private static MemoryStream? BufferIfNotSeekable(Stream templateStream)
    {
        if (templateStream.CanSeek)
        {
            return null;
        }

        MemoryStream buffer = new MemoryStream();
        templateStream.CopyTo(buffer);
        buffer.Position = 0;
        return buffer;
    }

    private static ProcessingResult UnsupportedFormat(string? odfMediaType) =>
        ProcessingResult.Failure(TemplateFormatDetector.GetUnsupportedFormatMessage(odfMediaType));

    private static ValidationResult InvalidTemplate(string message) =>
        ValidationResult.Failure(
            new[] { ValidationError.Create(ValidationErrorType.InvalidDocument, message) },
            Array.Empty<string>());

    private static void ValidatePaths(string templatePath, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templatePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
    }
}
