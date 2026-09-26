// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.Utilities;
using TriasDev.Templify.Visitors;

namespace TriasDev.Templify.Core;

/// <summary>
/// Main entry point for processing Word document templates with placeholder replacement.
/// </summary>
public sealed class DocumentTemplateProcessor
{
    /// <summary>
    /// Field types that may need updating when document content changes.
    /// Used by Auto mode to detect if UpdateFieldsOnOpen should be set.
    /// </summary>
    private static readonly string[] _dynamicFieldTypes = new[]
    {
        "TOC",       // Table of Contents
        "PAGE",      // Current page number
        "NUMPAGES",  // Total page count
        "PAGEREF",   // Page references (cross-references to bookmarks)
        "DATE",      // Current date
        "TIME",      // Current time
        "FILENAME",  // Document filename
        "REF",       // Cross-references
        "NOTEREF",   // Footnote/endnote references
        "SECTIONPAGES" // Pages in current section
    };

    private readonly PlaceholderReplacementOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentTemplateProcessor"/> class.
    /// </summary>
    /// <param name="options">Configuration options for placeholder replacement. If null, default options are used.</param>
    public DocumentTemplateProcessor(PlaceholderReplacementOptions? options = null)
    {
        _options = options ?? new PlaceholderReplacementOptions();
    }

    /// <summary>
    /// Processes a Word document template, replacing placeholders with values from the data dictionary.
    /// </summary>
    /// <param name="templateStream">Stream containing the template .docx file. Must be readable.</param>
    /// <param name="outputStream">
    /// Stream to write the processed document to. Must be readable, writable and seekable (for example a
    /// <see cref="MemoryStream"/>, or a <see cref="FileStream"/> opened with <see cref="FileAccess.ReadWrite"/>),
    /// because the document is edited in place after the template has been copied into it.
    /// </param>
    /// <param name="data">Dictionary containing variable names and their replacement values.</param>
    /// <returns>
    /// A <see cref="ProcessingResult"/> indicating success or failure and providing metrics. Template syntax
    /// errors (e.g. unmatched markers), data errors (e.g. a loop over a non-collection) and unusable streams
    /// (a template stream that is not readable, an output stream that is not readable, writable and seekable)
    /// are reported as a failed result with <see cref="ProcessingResult.ErrorMessage"/> set. Unusable streams
    /// are detected before anything is written to the output stream.
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

        return ProcessTemplateCore(templateStream, outputStream, data);
    }

    /// <summary>
    /// Processes a Word document template, replacing placeholders with values from read-only data.
    /// </summary>
    /// <param name="templateStream">Stream containing the template .docx file. Must be readable.</param>
    /// <param name="outputStream">
    /// Stream to write the processed document to. Must be readable, writable and seekable (for example a
    /// <see cref="MemoryStream"/>, or a <see cref="FileStream"/> opened with <see cref="FileAccess.ReadWrite"/>).
    /// </param>
    /// <param name="data">
    /// Variable names and their replacement values. Not copied: lookups use the dictionary's own key comparer.
    /// </param>
    /// <returns>
    /// A <see cref="ProcessingResult"/>; see <see cref="ProcessTemplate(Stream, Stream, Dictionary{string, object})"/>
    /// for which errors are reported as a failed result.
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

        return ProcessTemplateCore(templateStream, outputStream, AsNonNullValues(data));
    }

    /// <summary>
    /// Processes a Word document template, replacing placeholders with values from a JSON string.
    /// </summary>
    /// <param name="templateStream">Stream containing the template .docx file. Must be readable.</param>
    /// <param name="outputStream">
    /// Stream to write the processed document to. Must be readable, writable and seekable (for example a
    /// <see cref="MemoryStream"/>, or a <see cref="FileStream"/> opened with <see cref="FileAccess.ReadWrite"/>).
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

        // JSON parse errors (JsonException, ArgumentException) propagate to the caller unchanged.
        Dictionary<string, object> data = JsonDataParser.ParseJsonToDataDictionary(jsonData);
        return ProcessTemplate(templateStream, outputStream, data);
    }

    /// <summary>
    /// Processes a Word document template held in memory and returns the processed document as a byte array.
    /// </summary>
    /// <param name="template">The template .docx file content. Not modified.</param>
    /// <param name="data">Dictionary containing variable names and their replacement values.</param>
    /// <param name="output">
    /// When this method returns, the processed .docx file content if processing succeeded; otherwise an empty array.
    /// </param>
    /// <returns>
    /// A <see cref="ProcessingResult"/>; see <see cref="ProcessTemplate(Stream, Stream, Dictionary{string, object})"/>
    /// for which errors are reported as a failed result.
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

        return ProcessBytes(template, data, out output);
    }

    /// <summary>
    /// Processes a Word document template held in memory, using read-only data, and returns the processed
    /// document as a byte array.
    /// </summary>
    /// <param name="template">The template .docx file content. Not modified.</param>
    /// <param name="data">
    /// Variable names and their replacement values. Not copied: lookups use the dictionary's own key comparer.
    /// </param>
    /// <param name="output">
    /// When this method returns, the processed .docx file content if processing succeeded; otherwise an empty array.
    /// </param>
    /// <returns>
    /// A <see cref="ProcessingResult"/>; see <see cref="ProcessTemplate(Stream, Stream, Dictionary{string, object})"/>
    /// for which errors are reported as a failed result.
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

        return ProcessBytes(template, AsNonNullValues(data), out output);
    }

    /// <summary>
    /// Processes a Word document template held in memory, using data from a JSON string, and returns the
    /// processed document as a byte array.
    /// </summary>
    /// <param name="template">The template .docx file content. Not modified.</param>
    /// <param name="jsonData">JSON string containing variable names and their replacement values. Must be a valid JSON object (not an array).</param>
    /// <param name="output">
    /// When this method returns, the processed .docx file content if processing succeeded; otherwise an empty array.
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
        return ProcessBytes(template, data, out output);
    }

    /// <summary>
    /// Processes a Word document template file and writes the processed document to a file.
    /// </summary>
    /// <param name="templatePath">Path of the template .docx file. Not modified.</param>
    /// <param name="outputPath">
    /// Path of the output .docx file. Created or overwritten only when processing succeeds; may be the same
    /// path as <paramref name="templatePath"/>.
    /// </param>
    /// <param name="data">Dictionary containing variable names and their replacement values.</param>
    /// <returns>
    /// A <see cref="ProcessingResult"/>; see <see cref="ProcessTemplate(Stream, Stream, Dictionary{string, object})"/>
    /// for which errors are reported as a failed result.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a path is empty or whitespace.</exception>
    /// <exception cref="IOException">
    /// Thrown when the template file cannot be read or the output file cannot be written (for example
    /// <see cref="FileNotFoundException"/> or <see cref="DirectoryNotFoundException"/>), as by
    /// <see cref="File.ReadAllBytes(string)"/> and <see cref="File.WriteAllBytes(string, byte[])"/>.
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

        return ProcessFile(templatePath, outputPath, data);
    }

    /// <summary>
    /// Processes a Word document template file, using read-only data, and writes the processed document to a file.
    /// </summary>
    /// <param name="templatePath">Path of the template .docx file. Not modified.</param>
    /// <param name="outputPath">
    /// Path of the output .docx file. Created or overwritten only when processing succeeds; may be the same
    /// path as <paramref name="templatePath"/>.
    /// </param>
    /// <param name="data">
    /// Variable names and their replacement values. Not copied: lookups use the dictionary's own key comparer.
    /// </param>
    /// <returns>
    /// A <see cref="ProcessingResult"/>; see <see cref="ProcessTemplate(Stream, Stream, Dictionary{string, object})"/>
    /// for which errors are reported as a failed result.
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

        return ProcessFile(templatePath, outputPath, AsNonNullValues(data));
    }

    /// <summary>
    /// Processes a Word document template file, using data from a JSON string, and writes the processed
    /// document to a file.
    /// </summary>
    /// <param name="templatePath">Path of the template .docx file. Not modified.</param>
    /// <param name="outputPath">
    /// Path of the output .docx file. Created or overwritten only when processing succeeds; may be the same
    /// path as <paramref name="templatePath"/>.
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
        return ProcessFile(templatePath, outputPath, data);
    }

    /// <summary>
    /// Validates a Word document template for syntax errors (unmatched tags, invalid placeholders).
    /// Does not check for missing variables since no data is provided.
    /// </summary>
    /// <param name="templateStream">Stream containing the template .docx file. Must be readable.</param>
    /// <returns>A <see cref="ValidationResult"/> containing any errors found and all placeholders.</returns>
    /// <exception cref="ArgumentNullException">Thrown when templateStream is null.</exception>
    public ValidationResult ValidateTemplate(Stream templateStream)
    {
        ArgumentNullException.ThrowIfNull(templateStream);

        return ValidateTemplateInternal(templateStream, data: null);
    }

    /// <summary>
    /// Validates a Word document template for syntax errors and missing variables.
    /// Checks that all placeholders in the template have corresponding values in the data.
    /// </summary>
    /// <param name="templateStream">Stream containing the template .docx file. Must be readable.</param>
    /// <param name="data">Dictionary containing variable names and their values for validation.</param>
    /// <returns>A <see cref="ValidationResult"/> containing any errors found, all placeholders, and missing variables.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public ValidationResult ValidateTemplate(Stream templateStream, Dictionary<string, object> data)
    {
        ArgumentNullException.ThrowIfNull(templateStream);
        ArgumentNullException.ThrowIfNull(data);

        return ValidateTemplateInternal(templateStream, data);
    }

    /// <summary>
    /// Validates a Word document template for syntax errors and missing variables, using read-only data.
    /// Checks that all placeholders in the template have corresponding values in the data.
    /// </summary>
    /// <param name="templateStream">Stream containing the template .docx file. Must be readable.</param>
    /// <param name="data">
    /// Variable names and their values for validation. Not copied: lookups use the dictionary's own key comparer.
    /// </param>
    /// <returns>A <see cref="ValidationResult"/> containing any errors found, all placeholders, and missing variables.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public ValidationResult ValidateTemplate(Stream templateStream, IReadOnlyDictionary<string, object?> data)
    {
        ArgumentNullException.ThrowIfNull(templateStream);
        ArgumentNullException.ThrowIfNull(data);

        return ValidateTemplateInternal(templateStream, AsNonNullValues(data));
    }

    /// <summary>
    /// Validates a Word document template for syntax errors and optionally checks for missing variables.
    /// </summary>
    /// <param name="templateStream">Stream containing the template .docx file.</param>
    /// <param name="data">Optional data for checking missing variables. If null, only syntax is validated.</param>
    /// <returns>A validation result with errors, placeholders, and missing variables.</returns>
    private ValidationResult ValidateTemplateInternal(Stream templateStream, IReadOnlyDictionary<string, object>? data)
    {
        TemplateValidator validator = new TemplateValidator(_options);
        return validator.Validate(templateStream, data);
    }

    /// <summary>
    /// Processes the template into the output stream. Arguments are already null-checked.
    /// </summary>
    private ProcessingResult ProcessTemplateCore(
        Stream templateStream,
        Stream outputStream,
        IReadOnlyDictionary<string, object> data)
    {
        // Unusable streams are checked before anything is written to the output. They stay a failed result
        // (as in earlier versions, where the copy or OpenXML failed later), now with a clear message.
        string? streamError = GetStreamError(templateStream, outputStream);
        if (streamError != null)
        {
            return ProcessingResult.Failure(streamError);
        }

        try
        {
            // Copy template to output stream (non-destructive processing)
            if (templateStream.CanSeek)
            {
                templateStream.Position = 0;
            }

            templateStream.CopyTo(outputStream);
            outputStream.Position = 0;

            // Track missing variables and warnings
            HashSet<string> missingVariables = new HashSet<string>();
            WarningCollector warningCollector = new WarningCollector();

            // Conditional, loop and placeholder visitors, walked by one document walker
            TemplatePipeline pipeline = TemplatePipeline.Create(_options, missingVariables, warningCollector);

            // Open document for editing
            using (WordprocessingDocument document = WordprocessingDocument.Open(outputStream, isEditable: true))
            {
                if (document.MainDocumentPart == null)
                {
                    return ProcessingResult.Failure("Invalid document: MainDocumentPart is missing.");
                }

                // Create global evaluation context
                GlobalEvaluationContext globalContext = new GlobalEvaluationContext(data);

                // Body, headers/footers, footnotes/endnotes
                pipeline.Process(document, globalContext);

                // Loop cloning copies drawing/shape ids; make them unique again (#178)
                DrawingIdAllocator.EnsureUniqueIds(document);

                // Apply UpdateFieldsOnOpen setting based on mode
                bool shouldUpdateFields = _options.UpdateFieldsOnOpen switch
                {
                    UpdateFieldsOnOpenMode.Always => true,
                    UpdateFieldsOnOpenMode.Auto => HasFields(document),
                    _ => false
                };

                if (shouldUpdateFields)
                {
                    ApplyUpdateFieldsOnOpen(document);
                }

                // Apply document properties if configured
                if (_options.DocumentProperties != null)
                {
                    ApplyDocumentProperties(document, _options.DocumentProperties);
                }

                // Save changes
                document.MainDocumentPart.Document!.Save();
            }

            // Return success with replacement count and warnings
            return ProcessingResult.Success(
                replacementCount: pipeline.PlaceholderVisitor.ReplacementCount,
                missingVariables: missingVariables.OrderBy(v => v).ToList(),
                warnings: warningCollector.GetWarnings());
        }
        catch (MissingVariableException ex)
        {
            // Only missing variables with MissingVariableBehavior.ThrowException propagate to the caller.
            // They surface as a plain InvalidOperationException with the same message as before, so existing
            // exact-type checks keep working. Every other error, including template syntax errors and data
            // that does not fit the template, is reported as a failed result.
            throw ex.ToPublicException();
        }
        catch (Exception ex)
        {
            return ProcessingResult.Failure($"Processing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes an in-memory template into a new byte array.
    /// </summary>
    private ProcessingResult ProcessBytes(byte[] template, IReadOnlyDictionary<string, object> data, out byte[] output)
    {
        using MemoryStream templateStream = new MemoryStream(template, writable: false);
        using MemoryStream outputStream = new MemoryStream();

        ProcessingResult result = ProcessTemplateCore(templateStream, outputStream, data);
        output = result.IsSuccess ? outputStream.ToArray() : Array.Empty<byte>();
        return result;
    }

    /// <summary>
    /// Processes a template file in memory and writes the output file only when processing succeeds,
    /// so a failed run never leaves a truncated output file behind.
    /// </summary>
    private ProcessingResult ProcessFile(string templatePath, string outputPath, IReadOnlyDictionary<string, object> data)
    {
        byte[] template = File.ReadAllBytes(templatePath);
        ProcessingResult result = ProcessBytes(template, data, out byte[] output);
        if (result.IsSuccess)
        {
            File.WriteAllBytes(outputPath, output);
        }

        return result;
    }

    private static void ValidatePaths(string templatePath, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templatePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
    }

    /// <summary>
    /// Returns why the streams cannot be used for processing, or null when they can.
    /// </summary>
    private static string? GetStreamError(Stream templateStream, Stream outputStream)
    {
        if (!templateStream.CanRead)
        {
            return "Invalid template stream: the template stream must be readable.";
        }

        if (!outputStream.CanRead || !outputStream.CanWrite || !outputStream.CanSeek)
        {
            return "Invalid output stream: the output stream must be readable, writable and seekable "
                + "(for example a MemoryStream, or a FileStream opened with FileAccess.ReadWrite).";
        }

        return null;
    }

    /// <summary>
    /// Views data with nullable values in the shape used internally. Values are never dereferenced
    /// without a null check, so this only reinterprets the annotation; nothing is copied.
    /// </summary>
    private static IReadOnlyDictionary<string, object> AsNonNullValues(IReadOnlyDictionary<string, object?> data)
    {
        return data!;
    }

    /// <summary>
    /// Checks if the document contains any fields that would benefit from updating on open.
    /// </summary>
    /// <param name="document">The Word document to check.</param>
    /// <returns>True if the document contains fields like TOC, PAGE, NUMPAGES, etc.</returns>
    /// <remarks>
    /// Both complex fields (<c>w:instrText</c> between <c>w:fldChar</c> markers) and simple fields
    /// (<c>w:fldSimple w:instr="..."</c>) are considered, in the body, headers, footers, footnotes and endnotes.
    /// </remarks>
    private static bool HasFields(WordprocessingDocument document)
    {
        MainDocumentPart? mainPart = document.MainDocumentPart;
        if (mainPart?.Document?.Body == null)
        {
            return false;
        }

        // Roots to scan (lazy to short-circuit on first match)
        IEnumerable<OpenXmlElement> roots = new OpenXmlElement[] { mainPart.Document.Body }
            .Concat(mainPart.HeaderParts.Select(hp => hp.Header).OfType<OpenXmlElement>())
            .Concat(mainPart.FooterParts.Select(fp => fp.Footer).OfType<OpenXmlElement>())
            .Concat(new OpenXmlElement?[] { mainPart.FootnotesPart?.Footnotes, mainPart.EndnotesPart?.Endnotes }
                .OfType<OpenXmlElement>());

        return roots
            .SelectMany(root => root.Descendants<FieldCode>().Select(fc => fc.Text)
                .Concat(root.Descendants<SimpleField>().Select(sf => sf.Instruction?.Value)))
            .Any(IsDynamicFieldInstruction);
    }

    /// <summary>
    /// Returns whether a field instruction starts with one of the dynamic field types.
    /// </summary>
    private static bool IsDynamicFieldInstruction(string? instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
        {
            return false;
        }

        // Field instructions start with the field type, followed by
        // spaces, switches (starting with '\'), and parameters.
        // Example: " TOC \o \"1-3\" \h \z \u "
        string text = instruction.TrimStart();

        // Get the first token (up to whitespace or a backslash for switches)
        int endIndex = text.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '\\' });
        string fieldTypeInDoc = endIndex >= 0 ? text[..endIndex] : text;

        foreach (string field in _dynamicFieldTypes)
        {
            if (fieldTypeInDoc.Equals(field, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Configures the document to update all fields (including TOC) when opened in Word.
    /// </summary>
    /// <param name="document">The Word document to configure.</param>
    private static void ApplyUpdateFieldsOnOpen(WordprocessingDocument document)
    {
        if (document.MainDocumentPart == null)
        {
            return;
        }

        // Get or create the document settings part
        DocumentSettingsPart? settingsPart = document.MainDocumentPart.DocumentSettingsPart;
        if (settingsPart == null)
        {
            settingsPart = document.MainDocumentPart.AddNewPart<DocumentSettingsPart>();
            settingsPart.Settings = new Settings();
        }

        if (settingsPart.Settings == null)
        {
            settingsPart.Settings = new Settings();
        }

        Settings settings = settingsPart.Settings;

        // Check if UpdateFieldsOnOpen already exists
        UpdateFieldsOnOpen? existingElement = settings.GetFirstChild<UpdateFieldsOnOpen>();
        if (existingElement != null)
        {
            // Update the existing element
            existingElement.Val = true;
        }
        else
        {
            // Add new UpdateFieldsOnOpen element
            settings.PrependChild(new UpdateFieldsOnOpen { Val = true });
        }

        settingsPart.Settings.Save();
    }

    /// <summary>
    /// Applies configured document metadata properties to the output document.
    /// Only non-null property values are applied; null values preserve the original template value.
    /// </summary>
    /// <param name="document">The Word document to update.</param>
    /// <param name="props">The document properties to apply.</param>
    private static void ApplyDocumentProperties(WordprocessingDocument document, DocumentProperties props)
    {
        var packageProps = document.PackageProperties;

        if (props.Author != null)
        {
            packageProps.Creator = props.Author;
        }

        if (props.Title != null)
        {
            packageProps.Title = props.Title;
        }

        if (props.Subject != null)
        {
            packageProps.Subject = props.Subject;
        }

        if (props.Description != null)
        {
            packageProps.Description = props.Description;
        }

        if (props.Keywords != null)
        {
            packageProps.Keywords = props.Keywords;
        }

        if (props.Category != null)
        {
            packageProps.Category = props.Category;
        }

        if (props.LastModifiedBy != null)
        {
            packageProps.LastModifiedBy = props.LastModifiedBy;
        }
    }
}
