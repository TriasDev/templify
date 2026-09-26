// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using TriasDev.Templify.Loops;
using TriasDev.Templify.Placeholders;
using TriasDev.Templify.PropertyPaths;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Core;

/// <summary>
/// Checks the placeholders of a block container (body, header, footer, note) against the data,
/// scoping the variables inside loops to the loop items.
/// </summary>
/// <remarks>
/// The containers are traversed like <see cref="Visitors.DocumentWalker"/> traverses them for processing:
/// body loops are detected with <see cref="LoopDetector.DetectLoopsInElements"/>, table row loops with
/// <see cref="LoopDetector.DetectTableRowLoops(IReadOnlyList{TableRow})"/> at the level of each table
/// (and of the rows of an enclosing table row loop), and tables, cells, text boxes and block content
/// controls are descended into. A placeholder is therefore validated in the same loop scope in which
/// processing resolves it.
/// </remarks>
internal sealed class ScopedVariableValidator
{
    private readonly IReadOnlyDictionary<string, object> _data;
    private readonly HashSet<string> _allPlaceholders;
    private readonly HashSet<string> _missingVariables;
    private readonly List<ValidationWarning> _warnings;
    private readonly List<ValidationError> _errors;
    private readonly bool _warnOnEmptyLoopCollections;
    private readonly ValueResolver _resolver = new ValueResolver();
    private readonly Stack<LoopScope> _loopStack = new Stack<LoopScope>();

    public ScopedVariableValidator(
        IReadOnlyDictionary<string, object> data,
        HashSet<string> allPlaceholders,
        HashSet<string> missingVariables,
        List<ValidationWarning> warnings,
        List<ValidationError> errors,
        bool warnOnEmptyLoopCollections)
    {
        _data = data;
        _allPlaceholders = allPlaceholders;
        _missingVariables = missingVariables;
        _warnings = warnings;
        _errors = errors;
        _warnOnEmptyLoopCollections = warnOnEmptyLoopCollections;
    }

    /// <summary>
    /// Validates the elements of a block container (or the rows of a table row loop).
    /// </summary>
    public void ValidateElements(IReadOnlyList<OpenXmlElement> elements)
    {
        // The content of a table row loop is a list of rows, which needs row-aware loop detection
        // (as in DocumentWalker.WalkElements).
        List<TableRow>? rows = elements.Count > 0 && elements.All(e => e is TableRow)
            ? elements.Cast<TableRow>().ToList()
            : null;

        IReadOnlyList<LoopBlock> loops = rows != null
            ? DetectTableRowLoops(rows)
            : DetectLoops(elements);

        ValidateLoops(loops);

        HashSet<OpenXmlElement> loopElements = GetLoopElements(loops);
        foreach (OpenXmlElement element in elements)
        {
            if (!loopElements.Contains(element))
            {
                ValidateElement(element);
            }
        }
    }

    private void ValidateElement(OpenXmlElement element)
    {
        switch (element)
        {
            case Paragraph paragraph:
                ValidateText(ParagraphTextModel.GetText(paragraph));

                // Text boxes anchored in the paragraph are separate block containers.
                foreach (TextBoxContent textBox in paragraph.Descendants<TextBoxContent>()
                    .Where(box => box.Ancestors<Paragraph>().FirstOrDefault() == paragraph))
                {
                    ValidateElements(textBox.Elements<OpenXmlElement>().ToList());
                }

                break;

            case Table table:
                ValidateRows(table);
                break;

            case TableRow row:
                ValidateRow(row);
                break;

            case SdtBlock sdtBlock:
                if (sdtBlock.SdtContentBlock != null)
                {
                    ValidateElements(sdtBlock.SdtContentBlock.Elements<OpenXmlElement>().ToList());
                }

                break;

            default:
                ValidateText(element.InnerText);
                break;
        }
    }

    /// <summary>
    /// Validates the rows of a row container: a table, or the content of a row-level content control.
    /// </summary>
    private void ValidateRows(OpenXmlCompositeElement container)
    {
        List<TableRow> rows = container.Elements<TableRow>().ToList();
        IReadOnlyList<LoopBlock> loops = DetectTableRowLoops(rows);

        ValidateLoops(loops);

        HashSet<OpenXmlElement> loopElements = GetLoopElements(loops);
        foreach (TableRow row in rows)
        {
            if (!loopElements.Contains(row))
            {
                ValidateRow(row);
            }
        }

        foreach (SdtRow rowContentControl in container.Elements<SdtRow>())
        {
            if (rowContentControl.SdtContentRow != null)
            {
                ValidateRows(rowContentControl.SdtContentRow);
            }
        }
    }

    private void ValidateRow(TableRow row)
    {
        foreach (TableCell cell in TemplateElementText.GetRowCells(row))
        {
            ValidateElements(cell.Elements<OpenXmlElement>().ToList());
        }

        // Paragraphs directly in a row (malformed, but processed by the walker)
        foreach (Paragraph paragraph in row.Elements<Paragraph>())
        {
            ValidateText(ParagraphTextModel.GetText(paragraph));
        }
    }

    private void ValidateLoops(IReadOnlyList<LoopBlock> loops)
    {
        foreach (LoopBlock loop in loops)
        {
            ValidateLoop(loop);
        }
    }

    /// <summary>
    /// Validates a loop's collection and its content in the scope of the loop items.
    /// </summary>
    private void ValidateLoop(LoopBlock loop)
    {
        _allPlaceholders.Add(loop.CollectionName);

        if (!TryResolveCollectionItems(loop.CollectionName, out List<object?> items))
        {
            ReportMissing(
                loop.CollectionName,
                $"Collection '{loop.CollectionName}' is referenced in a loop but not provided in the data.");
            return;
        }

        HashSet<string> properties = AggregateProperties(items);
        if (properties.Count == 0)
        {
            // Empty collection - optionally add warning, skip inner validation
            if (_warnOnEmptyLoopCollections)
            {
                _warnings.Add(ValidationWarning.Create(
                    ValidationWarningType.EmptyLoopCollection,
                    $"Collection '{loop.CollectionName}' is empty. Variables inside this loop could not be validated."));
            }

            return;
        }

        _loopStack.Push(new LoopScope(loop.IterationVariableName, properties, items));
        ValidateElements(loop.ContentElements);
        _loopStack.Pop();
    }

    private void ValidateText(string text)
    {
        foreach (string placeholder in PlaceholderScanner.GetUniqueVariableNames(text))
        {
            _allPlaceholders.Add(placeholder);

            // Skip special placeholders (loop metadata, current item)
            if (placeholder.StartsWith('@') || placeholder.StartsWith('.') || placeholder == "this")
            {
                continue;
            }

            if (!CanResolveInScope(placeholder))
            {
                ReportMissing(
                    placeholder,
                    $"Variable '{placeholder}' is referenced in the template but not provided in the data.");
            }
        }
    }

    private void ReportMissing(string variable, string message)
    {
        _missingVariables.Add(variable);
        _errors.Add(ValidationError.Create(ValidationErrorType.MissingVariable, message));
    }

    /// <summary>
    /// Resolves the items of a loop collection: from the items of an enclosing loop
    /// (e.g. <c>Lines</c> or <c>order.Lines</c>), otherwise from the global data.
    /// </summary>
    private bool TryResolveCollectionItems(string collectionName, out List<object?> items)
    {
        items = new List<object?>();

        // Innermost loop scope first
        foreach (LoopScope scope in _loopStack)
        {
            string? relativePath = GetPathRelativeToScope(collectionName, scope);
            if (relativePath == null)
            {
                continue;
            }

            if (PropertyPath.TryParse(relativePath, out PropertyPath? path) && path != null)
            {
                foreach (object? item in scope.Items)
                {
                    if (PropertyPathResolver.TryResolvePath(item, path, out object? value))
                    {
                        AddItems(value, items);
                    }
                }
            }

            return true;
        }

        if (!_resolver.TryResolveValue(_data, collectionName, out object? collection) || collection == null)
        {
            return false;
        }

        AddItems(collection, items);
        return true;
    }

    /// <summary>
    /// Gets the path of <paramref name="name"/> relative to a loop item of <paramref name="scope"/>,
    /// or <see langword="null"/> if the name does not refer to the loop item.
    /// </summary>
    private static string? GetPathRelativeToScope(string name, LoopScope scope)
    {
        if (scope.IterationVariableName != null)
        {
            string prefix = scope.IterationVariableName + ".";
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                string propertyPath = name.Substring(prefix.Length);
                return scope.Properties.Contains(GetRootSegment(propertyPath)) ? propertyPath : null;
            }
        }

        return scope.Properties.Contains(GetRootSegment(name)) ? name : null;
    }

    private static string GetRootSegment(string path)
    {
        int end = path.IndexOfAny(new[] { '.', '[' });
        return end > 0 ? path.Substring(0, end) : path;
    }

    private static void AddItems(object? collection, List<object?> items)
    {
        if (collection is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
        {
            foreach (JsonElement item in jsonArray.EnumerateArray())
            {
                items.Add(item);
            }

            return;
        }

        // Strings implement IEnumerable<char>, so exclude them explicitly
        if (collection is string || collection is not IEnumerable enumerable)
        {
            return;
        }

        foreach (object? item in enumerable)
        {
            items.Add(item);
        }
    }

    /// <summary>
    /// Aggregates all property names from all items of a collection.
    /// </summary>
    private static HashSet<string> AggregateProperties(List<object?> items)
    {
        HashSet<string> properties = new HashSet<string>();

        // Cache property info by type to avoid repeated reflection
        Dictionary<Type, System.Reflection.PropertyInfo[]> typePropertyCache = new Dictionary<Type, System.Reflection.PropertyInfo[]>();

        foreach (object? item in items)
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
                // POCO - get public properties (with caching by type)
                Type itemType = item.GetType();
                if (!typePropertyCache.TryGetValue(itemType, out System.Reflection.PropertyInfo[]? cachedProperties))
                {
                    cachedProperties = itemType.GetProperties();
                    typePropertyCache[itemType] = cachedProperties;
                }

                foreach (System.Reflection.PropertyInfo prop in cachedProperties)
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
    private bool CanResolveInScope(string placeholder)
    {
        // Try loop scopes (innermost first - stack iteration goes from top to bottom)
        foreach (LoopScope scope in _loopStack)
        {
            // Direct reference to the named iteration variable (e.g., {{item}})
            if (placeholder == scope.IterationVariableName)
            {
                return true;
            }

            // Property access via the named iteration variable (e.g., {{item.Name}}) or implicit
            // property access (e.g., {{Name}}, {{Address.City}}).
            // Note: During static validation, only the root property is verified against the loop items;
            // runtime processing reports invalid nested paths.
            if (GetPathRelativeToScope(placeholder, scope) != null)
            {
                return true;
            }
        }

        // Try global scope
        return _resolver.TryResolveValue(_data, placeholder, out _);
    }

    private static IReadOnlyList<LoopBlock> DetectLoops(IReadOnlyList<OpenXmlElement> elements)
    {
        try
        {
            return LoopDetector.DetectLoopsInElements(elements.ToList());
        }
        catch (TemplateSyntaxException)
        {
            // Loop syntax errors are reported by the syntax validation
            return Array.Empty<LoopBlock>();
        }
    }

    private static IReadOnlyList<LoopBlock> DetectTableRowLoops(IReadOnlyList<TableRow> rows)
    {
        try
        {
            return LoopDetector.DetectTableRowLoops(rows);
        }
        catch (TemplateSyntaxException)
        {
            // Loop syntax errors are reported by the syntax validation
            return Array.Empty<LoopBlock>();
        }
    }

    private static HashSet<OpenXmlElement> GetLoopElements(IReadOnlyList<LoopBlock> loops)
    {
        HashSet<OpenXmlElement> loopElements = new HashSet<OpenXmlElement>();
        foreach (LoopBlock loop in loops)
        {
            loopElements.Add(loop.StartMarker);
            loopElements.Add(loop.EndMarker);
            loopElements.UnionWith(loop.ContentElements);
        }

        return loopElements;
    }

    /// <summary>
    /// A loop scope: the iteration variable name, the properties of the loop items and the items.
    /// </summary>
    private sealed record LoopScope(string? IterationVariableName, HashSet<string> Properties, List<object?> Items);
}
