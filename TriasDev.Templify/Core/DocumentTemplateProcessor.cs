using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Loops;
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
        List<ValidationError> errors = new List<ValidationError>();
        HashSet<string> allPlaceholders = new HashSet<string>();
        HashSet<string> missingVariables = new HashSet<string>();

        try
        {
            // Reset stream position if seekable
            if (templateStream.CanSeek)
            {
                templateStream.Position = 0;
            }

            // Open document read-only for validation
            using (WordprocessingDocument document = WordprocessingDocument.Open(templateStream, isEditable: false))
            {
                if (document.MainDocumentPart?.Document?.Body == null)
                {
                    errors.Add(ValidationError.Create(
                        ValidationErrorType.InvalidPlaceholderSyntax,
                        "Invalid document: MainDocumentPart or Body is missing."));

                    return ValidationResult.Failure(errors, Array.Empty<string>());
                }

                Body body = document.MainDocumentPart.Document.Body;
                List<OpenXmlElement> elements = body.Elements<OpenXmlElement>().ToList();

                // 1. Validate conditionals and collect variables from conditions
                IReadOnlyList<ConditionalBlock> conditionalBlocks = new List<ConditionalBlock>();
                try
                {
                    conditionalBlocks = ConditionalDetector.DetectConditionalsInElements(elements);

                    // Extract variables from conditional expressions
                    foreach (ConditionalBlock block in conditionalBlocks)
                    {
                        // Extract all identifiers from condition expression
                        string condition = block.ConditionExpression;
                        string[] parts = condition.Split(new[] { ' ', '(', ')', '!', '>', '<', '=', '&', '|' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string part in parts)
                        {
                            // Filter out operator keywords
                            if (part != "and" && part != "or" && part != "not" &&
                                part != "eq" && part != "ne" && part != "lt" && part != "gt" &&
                                part != "lte" && part != "gte" && !string.IsNullOrWhiteSpace(part))
                            {
                                allPlaceholders.Add(part.Trim());
                            }
                        }
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // Parse error message to determine error type
                    ValidationErrorType errorType = ex.Message.Contains("has no matching")
                        ? ValidationErrorType.UnmatchedConditionalStart
                        : ValidationErrorType.InvalidConditionalExpression;

                    errors.Add(ValidationError.Create(errorType, ex.Message));
                }

                // 2. Validate loops and collect collection names
                IReadOnlyList<LoopBlock> loopBlocks = new List<LoopBlock>();
                try
                {
                    loopBlocks = LoopDetector.DetectLoopsInElements(elements);

                    // Extract collection names from loops
                    foreach (LoopBlock block in loopBlocks)
                    {
                        allPlaceholders.Add(block.CollectionName);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // Parse error message to determine error type
                    ValidationErrorType errorType = ex.Message.Contains("has no matching")
                        ? ValidationErrorType.UnmatchedLoopStart
                        : ValidationErrorType.InvalidPlaceholderSyntax;

                    errors.Add(ValidationError.Create(errorType, ex.Message));
                }

                // 3. Validate table row loops and collect collection names
                foreach (Table table in body.Elements<Table>())
                {
                    try
                    {
                        IReadOnlyList<LoopBlock> tableLoops = LoopDetector.DetectTableRowLoops(table);
                        foreach (LoopBlock block in tableLoops)
                        {
                            allPlaceholders.Add(block.CollectionName);
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        errors.Add(ValidationError.Create(
                            ValidationErrorType.UnmatchedLoopStart,
                            ex.Message));
                    }
                }

                // 4. Find all regular placeholders in the document
                PlaceholderFinder placeholderFinder = new PlaceholderFinder();
                string bodyText = body.InnerText;
                IEnumerable<string> foundPlaceholders = placeholderFinder.GetUniqueVariableNames(bodyText);

                foreach (string placeholder in foundPlaceholders)
                {
                    allPlaceholders.Add(placeholder);
                }

                // 5. Check for missing variables if data is provided
                if (data != null)
                {
                    ValueResolver resolver = new ValueResolver();

                    foreach (string placeholder in allPlaceholders)
                    {
                        // Skip loop metadata placeholders (@index, @first, etc.) as they're generated at runtime
                        if (placeholder.StartsWith("@"))
                        {
                            continue;
                        }

                        // Skip current item placeholders (. and this) as they're context-dependent
                        if (placeholder == "." || placeholder == "this")
                        {
                            continue;
                        }

                        // Skip relative path placeholders (e.g., .Name, .Address.City) as they're context-dependent
                        // These can only be resolved during processing when loop context exists
                        if (placeholder.StartsWith("."))
                        {
                            continue;
                        }

                        // Try to resolve the placeholder
                        if (!resolver.TryResolveValue(data, placeholder, out _))
                        {
                            missingVariables.Add(placeholder);

                            // Add a validation error for missing variable
                            errors.Add(ValidationError.Create(
                                ValidationErrorType.MissingVariable,
                                $"Variable '{placeholder}' is referenced in the template but not provided in the data."));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add(ValidationError.Create(
                ValidationErrorType.InvalidPlaceholderSyntax,
                $"Validation failed: {ex.Message}"));
        }

        // Return result
        List<string> allPlaceholdersList = allPlaceholders.OrderBy(p => p).ToList();
        List<string> missingVariablesList = missingVariables.OrderBy(v => v).ToList();

        return errors.Count == 0
            ? ValidationResult.Success(allPlaceholdersList, missingVariablesList)
            : ValidationResult.Failure(errors, allPlaceholdersList, missingVariablesList);
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
