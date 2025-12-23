// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.Utilities;
using TriasDev.Templify.Visitors;

namespace TriasDev.Templify.Core;

/// <summary>
/// Main entry point for processing Word document templates with placeholder replacement.
/// </summary>
public sealed class DocumentTemplateProcessor
{
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
    /// <param name="outputStream">Stream to write the processed document. Must be writable.</param>
    /// <param name="data">Dictionary containing variable names and their replacement values.</param>
    /// <returns>A <see cref="ProcessingResult"/> indicating success or failure and providing metrics.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public ProcessingResult ProcessTemplate(
        Stream templateStream,
        Stream outputStream,
        Dictionary<string, object> data)
    {
        if (templateStream == null)
        {
            throw new ArgumentNullException(nameof(templateStream));
        }

        if (outputStream == null)
        {
            throw new ArgumentNullException(nameof(outputStream));
        }

        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        try
        {
            // Copy template to output stream (non-destructive processing)
            CopyStream(templateStream, outputStream);
            outputStream.Position = 0;

            // Track missing variables
            HashSet<string> missingVariables = new HashSet<string>();

            // Create placeholder visitor to track replacements
            PlaceholderVisitor placeholderVisitor = new PlaceholderVisitor(_options, missingVariables);

            // Open document for editing
            using (WordprocessingDocument document = WordprocessingDocument.Open(outputStream, isEditable: true))
            {
                if (document.MainDocumentPart == null)
                {
                    return ProcessingResult.Failure("Invalid document: MainDocumentPart is missing.");
                }

                // Create global evaluation context
                GlobalEvaluationContext globalContext = new GlobalEvaluationContext(data);

                // Create visitor instances
                DocumentWalker walker = new DocumentWalker();
                ConditionalVisitor conditionalVisitor = new ConditionalVisitor();

                // Create a temporary composite for initial loop visitor creation
                CompositeVisitor tempComposite = new CompositeVisitor(
                    conditionalVisitor,
                    placeholderVisitor
                );

                // Create loop visitor with temporary composite
                LoopVisitor loopVisitor = new LoopVisitor(walker, tempComposite);

                // Create the final composite that includes all visitors (including loop)
                CompositeVisitor composite = new CompositeVisitor(
                    conditionalVisitor,
                    loopVisitor,
                    placeholderVisitor
                );

                // Update loop visitor to use the final composite as nested visitor
                // This creates a circular reference that allows unlimited nesting depth
                loopVisitor.SetNestedVisitor(composite);

                // Walk the document with the composite visitor
                walker.Walk(document, composite, globalContext);

                // Save changes
                document.MainDocumentPart.Document.Save();
            }

            // Return success with replacement count from placeholder visitor
            return ProcessingResult.Success(
                replacementCount: placeholderVisitor.ReplacementCount,
                missingVariables: missingVariables.OrderBy(v => v).ToList());
        }
        catch (InvalidOperationException)
        {
            // Re-throw InvalidOperationException (e.g., missing variables with ThrowException behavior)
            // These are intentional exceptions that should propagate to the caller
            throw;
        }
        catch (Exception ex)
        {
            return ProcessingResult.Failure($"Processing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes a Word document template, replacing placeholders with values from a JSON string.
    /// </summary>
    /// <param name="templateStream">Stream containing the template .docx file. Must be readable.</param>
    /// <param name="outputStream">Stream to write the processed document. Must be writable.</param>
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
        if (jsonData == null)
        {
            throw new ArgumentNullException(nameof(jsonData));
        }

        try
        {
            // Parse JSON string to dictionary
            Dictionary<string, object> data = JsonDataParser.ParseJsonToDataDictionary(jsonData);

            // Delegate to the existing ProcessTemplate method
            return ProcessTemplate(templateStream, outputStream, data);
        }
        catch (JsonException)
        {
            // Re-throw JSON exceptions as they provide useful error messages
            throw;
        }
        catch (ArgumentException)
        {
            // Re-throw argument exceptions from JSON parsing
            throw;
        }
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
        if (templateStream == null)
        {
            throw new ArgumentNullException(nameof(templateStream));
        }

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
        if (templateStream == null)
        {
            throw new ArgumentNullException(nameof(templateStream));
        }

        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        return ValidateTemplateInternal(templateStream, data);
    }

    /// <summary>
    /// Validates a Word document template for syntax errors and optionally checks for missing variables.
    /// </summary>
    /// <param name="templateStream">Stream containing the template .docx file.</param>
    /// <param name="data">Optional dictionary for checking missing variables. If null, only syntax is validated.</param>
    /// <returns>A validation result with errors, placeholders, and missing variables.</returns>
    private ValidationResult ValidateTemplateInternal(Stream templateStream, Dictionary<string, object>? data)
    {
        TemplateValidator validator = new TemplateValidator(_options);
        return validator.Validate(templateStream, data);
    }

    /// <summary>
    /// Copies the contents of one stream to another.
    /// </summary>
    private static void CopyStream(Stream source, Stream destination)
    {
        if (!source.CanRead)
        {
            throw new ArgumentException("Source stream must be readable.", nameof(source));
        }

        if (!destination.CanWrite)
        {
            throw new ArgumentException("Destination stream must be writable.", nameof(destination));
        }

        // Reset source position if seekable
        if (source.CanSeek)
        {
            source.Position = 0;
        }

        source.CopyTo(destination);
    }
}
