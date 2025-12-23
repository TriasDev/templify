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

namespace TriasDev.Templify.Core;

/// <summary>
/// Validates Word document templates for syntax errors and missing variables.
/// </summary>
internal sealed class TemplateValidator
{
    private readonly PlaceholderReplacementOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateValidator"/> class.
    /// </summary>
    /// <param name="options">Configuration options for validation. If null, default options are used.</param>
    public TemplateValidator(PlaceholderReplacementOptions? options = null)
    {
        _options = options ?? new PlaceholderReplacementOptions();
    }

    /// <summary>
    /// Validates a Word document template for syntax errors and optionally checks for missing variables.
    /// </summary>
    /// <param name="templateStream">Stream containing the template .docx file.</param>
    /// <param name="data">Optional dictionary for checking missing variables. If null, only syntax is validated.</param>
    /// <returns>A validation result with errors, placeholders, and missing variables.</returns>
    public ValidationResult Validate(Stream templateStream, Dictionary<string, object>? data)
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
                IReadOnlyList<ConditionalBlock> conditionalBlocks = ValidateConditionals(elements, allPlaceholders, errors);

                // 2. Validate loops and collect collection names
                ValidateLoops(elements, allPlaceholders, errors);

                // 3. Validate table row loops and collect collection names
                ValidateTableRowLoops(body, allPlaceholders, errors);

                // 4. Find all regular placeholders in the document
                FindAllPlaceholders(body, allPlaceholders);

                // 5. Check for missing variables if data is provided
                if (data != null)
                {
                    ValidateMissingVariables(elements, conditionalBlocks, data, allPlaceholders, missingVariables, warnings, errors, _options.WarnOnEmptyLoopCollections);
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
    /// Validates conditional blocks and collects variables from condition expressions.
    /// </summary>
    private static IReadOnlyList<ConditionalBlock> ValidateConditionals(
        List<OpenXmlElement> elements,
        HashSet<string> allPlaceholders,
        List<ValidationError> errors)
    {
        IReadOnlyList<ConditionalBlock> conditionalBlocks = new List<ConditionalBlock>();
        try
        {
            conditionalBlocks = ConditionalDetector.DetectConditionalsInElements(elements);

            // Extract variables from conditional expressions
            foreach (ConditionalBlock block in conditionalBlocks)
            {
                ExtractConditionVariables(block.ConditionExpression, allPlaceholders);
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

        return conditionalBlocks;
    }

    /// <summary>
    /// Validates loop blocks and collects collection names.
    /// </summary>
    private static void ValidateLoops(
        List<OpenXmlElement> elements,
        HashSet<string> allPlaceholders,
        List<ValidationError> errors)
    {
        try
        {
            IReadOnlyList<LoopBlock> loopBlocks = LoopDetector.DetectLoopsInElements(elements);

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
    }

    /// <summary>
    /// Validates table row loops and collects collection names.
    /// </summary>
    private static void ValidateTableRowLoops(
        Body body,
        HashSet<string> allPlaceholders,
        List<ValidationError> errors)
    {
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
    }

    /// <summary>
    /// Finds all regular placeholders in the document body.
    /// </summary>
    private static void FindAllPlaceholders(Body body, HashSet<string> allPlaceholders)
    {
        PlaceholderFinder placeholderFinder = new PlaceholderFinder();
        string bodyText = body.InnerText;
        IEnumerable<string> foundPlaceholders = placeholderFinder.GetUniqueVariableNames(bodyText);

        foreach (string placeholder in foundPlaceholders)
        {
            allPlaceholders.Add(placeholder);
        }
    }

    /// <summary>
    /// Validates missing variables using recursive loop scope analysis.
    /// </summary>
    private static void ValidateMissingVariables(
        List<OpenXmlElement> elements,
        IReadOnlyList<ConditionalBlock> conditionalBlocks,
        Dictionary<string, object> data,
        HashSet<string> allPlaceholders,
        HashSet<string> missingVariables,
        List<ValidationWarning> warnings,
        List<ValidationError> errors,
        bool warnOnEmptyLoopCollections)
    {
        ValueResolver resolver = new ValueResolver();
        Stack<(string CollectionName, HashSet<string> Properties)> loopStack =
            new Stack<(string CollectionName, HashSet<string> Properties)>();

        // Clear and re-populate placeholders with proper scoping
        allPlaceholders.Clear();

        // Re-add conditional variables (they were already extracted in step 1)
        foreach (ConditionalBlock block in conditionalBlocks)
        {
            ExtractConditionVariables(block.ConditionExpression, allPlaceholders, excludeLiterals: true);
        }

        // Validate placeholders with proper loop scoping
        ValidatePlaceholdersInScope(elements, loopStack, data, allPlaceholders, missingVariables, warnings, errors, resolver, warnOnEmptyLoopCollections);
    }

    /// <summary>
    /// Extracts variable names from a conditional expression.
    /// </summary>
    private static void ExtractConditionVariables(string condition, HashSet<string> placeholders, bool excludeLiterals = false)
    {
        string[] parts = condition.Split(new[] { ' ', '(', ')', '!', '>', '<', '=', '&', '|' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            // Filter out operator keywords
            if (part != "and" && part != "or" && part != "not" &&
                part != "eq" && part != "ne" && part != "lt" && part != "gt" &&
                part != "lte" && part != "gte" && !string.IsNullOrWhiteSpace(part))
            {
                if (excludeLiterals)
                {
                    // Only add if it's not a string literal (enclosed in quotes)
                    string trimmed = part.Trim().Trim('"', '\'');
                    if (trimmed == part.Trim())
                    {
                        placeholders.Add(trimmed);
                    }
                }
                else
                {
                    placeholders.Add(part.Trim());
                }
            }
        }
    }

    /// <summary>
    /// Recursively validates placeholders within a scope, handling loops and their nested content.
    /// </summary>
    private static void ValidatePlaceholdersInScope(
        IReadOnlyList<OpenXmlElement> elements,
        Stack<(string CollectionName, HashSet<string> Properties)> loopStack,
        Dictionary<string, object> data,
        HashSet<string> allPlaceholders,
        HashSet<string> missingVariables,
        List<ValidationWarning> warnings,
        List<ValidationError> errors,
        ValueResolver resolver,
        bool warnOnEmptyLoopCollections)
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
                // Empty collection - optionally add warning, skip inner validation
                if (warnOnEmptyLoopCollections)
                {
                    warnings.Add(ValidationWarning.Create(
                        ValidationWarningType.EmptyLoopCollection,
                        $"Collection '{loop.CollectionName}' is empty. Variables inside this loop could not be validated."));
                }

                continue;
            }

            // Recurse into loop content with aggregated properties as scope
            loopStack.Push((loop.CollectionName, aggregatedProperties));
            ValidatePlaceholdersInScope(loop.ContentElements, loopStack, data, allPlaceholders, missingVariables, warnings, errors, resolver, warnOnEmptyLoopCollections);
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
                            if (warnOnEmptyLoopCollections)
                            {
                                warnings.Add(ValidationWarning.Create(
                                    ValidationWarningType.EmptyLoopCollection,
                                    $"Collection '{loop.CollectionName}' is empty. Variables inside this loop could not be validated."));
                            }

                            continue;
                        }

                        loopStack.Push((loop.CollectionName, aggregatedProperties));
                        ValidatePlaceholdersInScope(loop.ContentElements, loopStack, data, allPlaceholders, missingVariables, warnings, errors, resolver, warnOnEmptyLoopCollections);
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
    private static object? ResolveCollectionFromScope(
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
    private static bool CanResolveInScope(
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
            // Note: During static validation, we can only verify the root property exists in the loop scope.
            // We cannot deeply validate the nested path (e.g., that Address actually has a City property)
            // because we only have property names from the collection, not actual runtime values.
            // This is an acceptable limitation - runtime processing will catch any invalid nested paths.
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
}
