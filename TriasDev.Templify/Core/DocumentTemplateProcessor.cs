// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;
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
        List<ValidationWarning> warnings = new List<ValidationWarning>();
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
                // Use recursive validation to properly handle loop-scoped variables
                if (data != null)
                {
                    ValueResolver resolver = new ValueResolver();
                    Stack<(string CollectionName, HashSet<string> Properties)> loopStack =
                        new Stack<(string CollectionName, HashSet<string> Properties)>();

                    // Clear and re-populate placeholders with proper scoping
                    allPlaceholders.Clear();

                    // Re-add conditional variables (they were already extracted in step 1)
                    foreach (ConditionalBlock block in conditionalBlocks)
                    {
                        string condition = block.ConditionExpression;
                        string[] parts = condition.Split(new[] { ' ', '(', ')', '!', '>', '<', '=', '&', '|' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string part in parts)
                        {
                            if (part != "and" && part != "or" && part != "not" &&
                                part != "eq" && part != "ne" && part != "lt" && part != "gt" &&
                                part != "lte" && part != "gte" && !string.IsNullOrWhiteSpace(part))
                            {
                                // Only add if it's not a string literal (enclosed in quotes)
                                string trimmed = part.Trim().Trim('"', '\'');
                                if (trimmed == part.Trim())
                                {
                                    allPlaceholders.Add(trimmed);
                                }
                            }
                        }
                    }

                    // Validate placeholders with proper loop scoping
                    ValidatePlaceholdersInScope(elements, loopStack, data, allPlaceholders, missingVariables, warnings, errors, resolver);
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
        List<ValidationWarning> warningsList = warnings.OrderBy(w => w.Message).ToList();

        return errors.Count == 0
            ? ValidationResult.Success(allPlaceholdersList, missingVariablesList, warningsList)
            : ValidationResult.Failure(errors, allPlaceholdersList, missingVariablesList, warningsList);
    }

    /// <summary>
    /// Recursively validates placeholders within a scope, handling loops and their nested content.
    /// </summary>
    private void ValidatePlaceholdersInScope(
        IReadOnlyList<OpenXmlElement> elements,
        Stack<(string CollectionName, HashSet<string> Properties)> loopStack,
        Dictionary<string, object> data,
        HashSet<string> allPlaceholders,
        HashSet<string> missingVariables,
        List<ValidationWarning> warnings,
        List<ValidationError> errors,
        ValueResolver resolver)
    {
        // 1. Detect loops in these elements
        IReadOnlyList<LoopBlock> loopBlocks;
        try
        {
            loopBlocks = LoopDetector.DetectLoopsInElements(elements.ToList());
        }
        catch (InvalidOperationException)
        {
            // Loop detection errors are already captured in the main validation
            loopBlocks = Array.Empty<LoopBlock>();
        }

        // 2. Process each loop recursively
        foreach (LoopBlock loop in loopBlocks)
        {
            allPlaceholders.Add(loop.CollectionName);

            // Resolve collection from current scope (check loop scopes first, then global)
            object? collection = ResolveCollectionFromScope(loop.CollectionName, loopStack, data, resolver);

            if (collection == null)
            {
                // Collection not found - it will be flagged as missing in the placeholder check
                continue;
            }

            // Aggregate properties from ALL items in collection
            HashSet<string> aggregatedProperties = AggregatePropertiesFromCollection(collection);

            if (aggregatedProperties.Count == 0)
            {
                // Empty collection - add warning, skip inner validation
                warnings.Add(ValidationWarning.Create(
                    ValidationWarningType.EmptyLoopCollection,
                    $"Collection '{loop.CollectionName}' is empty. Variables inside this loop could not be validated."));
                continue;
            }

            // Recurse into loop content with aggregated properties as scope
            loopStack.Push((loop.CollectionName, aggregatedProperties));
            ValidatePlaceholdersInScope(loop.ContentElements, loopStack, data, allPlaceholders, missingVariables, warnings, errors, resolver);
            loopStack.Pop();
        }

        // 3. Also handle table row loops within table elements
        foreach (OpenXmlElement element in elements)
        {
            if (element is Table table)
            {
                try
                {
                    IReadOnlyList<LoopBlock> tableLoops = LoopDetector.DetectTableRowLoops(table);
                    foreach (LoopBlock loop in tableLoops)
                    {
                        allPlaceholders.Add(loop.CollectionName);

                        object? collection = ResolveCollectionFromScope(loop.CollectionName, loopStack, data, resolver);

                        if (collection == null)
                        {
                            continue;
                        }

                        HashSet<string> aggregatedProperties = AggregatePropertiesFromCollection(collection);

                        if (aggregatedProperties.Count == 0)
                        {
                            warnings.Add(ValidationWarning.Create(
                                ValidationWarningType.EmptyLoopCollection,
                                $"Collection '{loop.CollectionName}' is empty. Variables inside this loop could not be validated."));
                            continue;
                        }

                        loopStack.Push((loop.CollectionName, aggregatedProperties));
                        ValidatePlaceholdersInScope(loop.ContentElements, loopStack, data, allPlaceholders, missingVariables, warnings, errors, resolver);
                        loopStack.Pop();
                    }
                }
                catch (InvalidOperationException)
                {
                    // Table loop detection errors are captured elsewhere
                }
            }
        }

        // 4. Find placeholders in current scope (exclude nested loop content)
        HashSet<string> placeholders = FindPlaceholdersInElements(elements, loopBlocks);

        // 5. Validate each placeholder against current scope
        foreach (string placeholder in placeholders)
        {
            allPlaceholders.Add(placeholder);

            // Skip special placeholders
            if (placeholder.StartsWith("@") || placeholder == "." || placeholder == "this" || placeholder.StartsWith("."))
            {
                continue;
            }

            if (!CanResolveInScope(placeholder, loopStack, data, resolver))
            {
                missingVariables.Add(placeholder);
                errors.Add(ValidationError.Create(
                    ValidationErrorType.MissingVariable,
                    $"Variable '{placeholder}' is referenced in the template but not provided in the data."));
            }
        }
    }

    /// <summary>
    /// Resolves a collection from the current scope (loop scopes or global).
    /// </summary>
    private object? ResolveCollectionFromScope(
        string collectionName,
        Stack<(string CollectionName, HashSet<string> Properties)> loopStack,
        Dictionary<string, object> data,
        ValueResolver resolver)
    {
        // First, check if the collection name is a property in any loop scope
        foreach ((string _, HashSet<string> properties) in loopStack)
        {
            if (properties.Contains(collectionName))
            {
                // The collection is a property of a loop item - we can't resolve the actual value
                // during static validation, so we return null to skip validation
                return null;
            }
        }

        // Try to resolve from global data
        if (resolver.TryResolveValue(data, collectionName, out object? value))
        {
            return value;
        }

        return null;
    }

    /// <summary>
    /// Aggregates all property names from all items in a collection.
    /// </summary>
    private static HashSet<string> AggregatePropertiesFromCollection(object collection)
    {
        HashSet<string> properties = new HashSet<string>();

        if (collection is not IEnumerable enumerable)
        {
            return properties;
        }

        foreach (object? item in enumerable)
        {
            if (item == null)
            {
                continue;
            }

            if (item is IDictionary<string, object> dict)
            {
                foreach (string key in dict.Keys)
                {
                    properties.Add(key);
                }
            }
            else if (item is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty prop in jsonElement.EnumerateObject())
                {
                    properties.Add(prop.Name);
                }
            }
            else
            {
                // POCO - get public properties
                foreach (System.Reflection.PropertyInfo prop in item.GetType().GetProperties())
                {
                    properties.Add(prop.Name);
                }
            }
        }

        return properties;
    }

    /// <summary>
    /// Checks if a placeholder can be resolved in the current scope.
    /// </summary>
    private bool CanResolveInScope(
        string placeholder,
        Stack<(string CollectionName, HashSet<string> Properties)> loopStack,
        Dictionary<string, object> data,
        ValueResolver resolver)
    {
        // Try loop scopes (innermost first - stack iteration goes from top to bottom)
        foreach ((string _, HashSet<string> properties) in loopStack)
        {
            // Direct property match
            if (properties.Contains(placeholder))
            {
                return true;
            }

            // Nested property (e.g., "Address.City" - check if "Address" exists)
            string rootProperty = placeholder.Split('.')[0];
            if (rootProperty != placeholder && properties.Contains(rootProperty))
            {
                return true;
            }
        }

        // Try global scope
        return resolver.TryResolveValue(data, placeholder, out _);
    }

    /// <summary>
    /// Finds all placeholders in the given elements, excluding content inside nested loops.
    /// </summary>
    private static HashSet<string> FindPlaceholdersInElements(
        IReadOnlyList<OpenXmlElement> elements,
        IReadOnlyList<LoopBlock> nestedLoops)
    {
        HashSet<string> placeholders = new HashSet<string>();
        PlaceholderFinder finder = new PlaceholderFinder();

        // Build a set of elements that are inside loops (to exclude)
        HashSet<OpenXmlElement> loopElements = new HashSet<OpenXmlElement>();
        foreach (LoopBlock loop in nestedLoops)
        {
            loopElements.Add(loop.StartMarker);
            loopElements.Add(loop.EndMarker);
            foreach (OpenXmlElement content in loop.ContentElements)
            {
                loopElements.Add(content);
            }
        }

        // Find placeholders in elements that are not inside loops
        foreach (OpenXmlElement element in elements)
        {
            if (loopElements.Contains(element))
            {
                continue;
            }

            string text = element.InnerText;
            IEnumerable<string> found = finder.GetUniqueVariableNames(text);
            foreach (string placeholder in found)
            {
                placeholders.Add(placeholder);
            }
        }

        return placeholders;
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
