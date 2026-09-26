// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json;
using TriasDev.Templify.OpenDocument;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Core;

/// <summary>
/// Main entry point for processing OpenDocument Text templates (.odt, and .ott templates) created with
/// LibreOffice, Collabora Online or OpenOffice.
/// </summary>
/// <remarks>
/// <para>
/// Uses the same template syntax, <see cref="PlaceholderReplacementOptions"/> and <see cref="ProcessingResult"/>
/// as <see cref="DocumentTemplateProcessor"/>. The output is always an OpenDocument Text document (.odt), also
/// when the template is an OpenDocument template (.ott). Flat OpenDocument (.fodt) is not supported.
/// </para>
/// <para>
/// Currently supported: placeholders (<c>{{Name}}</c>, nested paths, array and dictionary access, format
/// specifiers such as <c>{{Amount:currency}}</c> and <c>{{Date:date:yyyy-MM-dd}}</c>, boolean formatters and
/// expression placeholders such as <c>{{(A and B):yesno}}</c>) in paragraphs, headings, tables, lists,
/// sections, text boxes, footnotes and endnotes, and headers and footers. Placeholders split across
/// formatting spans are found; the replacement takes the formatting of the placeholder's first character.
/// Newlines in values become line breaks (<see cref="PlaceholderReplacementOptions.EnableNewlineSupport"/>).
/// </para>
/// <para>
/// Conditionals (<c>{{#if}}</c>, <c>{{#elseif}}</c>, <c>{{#else}}</c>, <c>{{/if}}</c>, with the full condition syntax)
/// work as block conditionals (markers in their own paragraphs), inline within one paragraph, over table rows (markers
/// in their own rows) and over list items (markers in their own items). Content left empty by a conditional gets an
/// empty paragraph (cells, text boxes, notes, headers and footers); empty list items, lists and tables are removed.
/// </para>
/// <para>
/// Markdown in values is inserted as literal text, and loop markers are not evaluated yet.
/// </para>
/// <para>
/// <see cref="PlaceholderReplacementOptions.UpdateFieldsOnOpen"/> and
/// <see cref="PlaceholderReplacementOptions.DocumentProperties"/> are not applied to OpenDocument output.
/// </para>
/// </remarks>
public sealed class OdtTemplateProcessor
{
    private readonly PlaceholderReplacementOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="OdtTemplateProcessor"/> class.
    /// </summary>
    /// <param name="options">Configuration options for placeholder replacement. If null, default options are used.</param>
    public OdtTemplateProcessor(PlaceholderReplacementOptions? options = null)
    {
        _options = options ?? new PlaceholderReplacementOptions();
    }

    /// <summary>
    /// Processes an OpenDocument Text template, replacing placeholders with values from the data dictionary.
    /// </summary>
    /// <param name="templateStream">Stream containing the template .odt or .ott file. Must be readable.</param>
    /// <param name="outputStream">
    /// Stream to write the processed .odt document to. Must be writable. The document is built in memory and
    /// written only when processing succeeds; on failure nothing is written.
    /// </param>
    /// <param name="data">Dictionary containing variable names and their replacement values.</param>
    /// <returns>
    /// A <see cref="ProcessingResult"/> indicating success or failure and providing metrics. Templates that are not
    /// OpenDocument Text packages, encrypted documents, template syntax errors, data errors and unusable streams
    /// are reported as a failed result with <see cref="ProcessingResult.ErrorMessage"/> set.
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
    /// Processes an OpenDocument Text template, replacing placeholders with values from read-only data.
    /// </summary>
    /// <param name="templateStream">Stream containing the template .odt or .ott file. Must be readable.</param>
    /// <param name="outputStream">Stream to write the processed .odt document to. Must be writable.</param>
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
    /// Processes an OpenDocument Text template, replacing placeholders with values from a JSON string.
    /// </summary>
    /// <param name="templateStream">Stream containing the template .odt or .ott file. Must be readable.</param>
    /// <param name="outputStream">Stream to write the processed .odt document to. Must be writable.</param>
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
    /// Processes an OpenDocument Text template held in memory and returns the processed document as a byte array.
    /// </summary>
    /// <param name="template">The template .odt or .ott file content. Not modified.</param>
    /// <param name="data">Dictionary containing variable names and their replacement values.</param>
    /// <param name="output">
    /// When this method returns, the processed .odt file content if processing succeeded; otherwise an empty array.
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
    /// Processes an OpenDocument Text template held in memory, using read-only data, and returns the processed
    /// document as a byte array.
    /// </summary>
    /// <param name="template">The template .odt or .ott file content. Not modified.</param>
    /// <param name="data">
    /// Variable names and their replacement values. Not copied: lookups use the dictionary's own key comparer.
    /// </param>
    /// <param name="output">
    /// When this method returns, the processed .odt file content if processing succeeded; otherwise an empty array.
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
    /// Processes an OpenDocument Text template held in memory, using data from a JSON string, and returns the
    /// processed document as a byte array.
    /// </summary>
    /// <param name="template">The template .odt or .ott file content. Not modified.</param>
    /// <param name="jsonData">JSON string containing variable names and their replacement values. Must be a valid JSON object (not an array).</param>
    /// <param name="output">
    /// When this method returns, the processed .odt file content if processing succeeded; otherwise an empty array.
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
    /// Processes an OpenDocument Text template file and writes the processed document to a file.
    /// </summary>
    /// <param name="templatePath">Path of the template .odt or .ott file. Not modified.</param>
    /// <param name="outputPath">
    /// Path of the output .odt file. Created or overwritten only when processing succeeds; may be the same
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
    /// Processes an OpenDocument Text template file, using read-only data, and writes the processed document to a file.
    /// </summary>
    /// <param name="templatePath">Path of the template .odt or .ott file. Not modified.</param>
    /// <param name="outputPath">
    /// Path of the output .odt file. Created or overwritten only when processing succeeds; may be the same
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
    /// Processes an OpenDocument Text template file, using data from a JSON string, and writes the processed
    /// document to a file.
    /// </summary>
    /// <param name="templatePath">Path of the template .odt or .ott file. Not modified.</param>
    /// <param name="outputPath">
    /// Path of the output .odt file. Created or overwritten only when processing succeeds; may be the same
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
    /// Processes the template into the output stream. Arguments are already null-checked.
    /// </summary>
    private ProcessingResult ProcessTemplateCore(
        Stream templateStream,
        Stream outputStream,
        IReadOnlyDictionary<string, object> data)
    {
        if (!templateStream.CanRead)
        {
            return ProcessingResult.Failure("Invalid template stream: the template stream must be readable.");
        }

        if (!outputStream.CanWrite)
        {
            return ProcessingResult.Failure("Invalid output stream: the output stream must be writable.");
        }

        try
        {
            if (templateStream.CanSeek)
            {
                templateStream.Position = 0;
            }

            OdtPackage package = OdtPackage.Open(templateStream);

            HashSet<string> missingVariables = new HashSet<string>();
            WarningCollector warningCollector = new WarningCollector();
            OdtTemplateEngine engine = new OdtTemplateEngine(_options, missingVariables, warningCollector);

            engine.Process(package, new GlobalEvaluationContext(data));

            package.Save(outputStream);

            return ProcessingResult.Success(
                replacementCount: engine.ReplacementCount,
                missingVariables: missingVariables.OrderBy(v => v).ToList(),
                warnings: warningCollector.GetWarnings());
        }
        catch (MissingVariableException ex)
        {
            // As for Word documents: only missing variables with MissingVariableBehavior.ThrowException
            // propagate, as a plain InvalidOperationException.
            throw ex.ToPublicException();
        }
        catch (InvalidOdtPackageException ex)
        {
            return ProcessingResult.Failure(ex.Message);
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
    /// Processes a template file in memory and writes the output file only when processing succeeds.
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
    /// Views data with nullable values in the shape used internally; nothing is copied.
    /// </summary>
    private static IReadOnlyDictionary<string, object> AsNonNullValues(IReadOnlyDictionary<string, object?> data)
    {
        return data!;
    }
}
