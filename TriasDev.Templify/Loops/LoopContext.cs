// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;
using TriasDev.Templify.PropertyPaths;

namespace TriasDev.Templify.Loops;

/// <summary>
/// Represents the execution context for a loop iteration.
/// Provides access to current item, index, and metadata.
/// </summary>
internal sealed class LoopContext
{
    /// <summary>
    /// Gets the current item being processed in the loop.
    /// </summary>
    public object CurrentItem { get; }

    /// <summary>
    /// Gets the zero-based index of the current item.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the total count of items in the collection.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets the name of the collection being iterated.
    /// </summary>
    public string CollectionName { get; }

    /// <summary>
    /// Gets the name of the iteration variable, or null if using implicit syntax.
    /// For {{#foreach item in Items}}, this is "item".
    /// For {{#foreach Items}}, this is null (implicit).
    /// </summary>
    public string? IterationVariableName { get; }

    /// <summary>
    /// Gets the parent loop context (for nested loops).
    /// </summary>
    public LoopContext? Parent { get; }

    /// <summary>
    /// Cached prefix for iteration variable property access (e.g., "item.").
    /// Null if using implicit syntax.
    /// </summary>
    private readonly string? _iterationVariablePrefix;

    /// <summary>
    /// Gets whether this is the first item in the collection.
    /// </summary>
    public bool IsFirst => Index == 0;

    /// <summary>
    /// Gets whether this is the last item in the collection.
    /// </summary>
    public bool IsLast => Index == Count - 1;

    public LoopContext(
        object currentItem,
        int index,
        int count,
        string collectionName,
        string? iterationVariableName = null,
        LoopContext? parent = null)
    {
        CurrentItem = currentItem ?? throw new ArgumentNullException(nameof(currentItem));
        Index = index;
        Count = count;
        CollectionName = collectionName ?? throw new ArgumentNullException(nameof(collectionName));
        IterationVariableName = iterationVariableName;
        _iterationVariablePrefix = iterationVariableName != null ? iterationVariableName + "." : null;
        Parent = parent;
    }

    /// <summary>
    /// Creates loop contexts for all items in a collection.
    /// </summary>
    public static IReadOnlyList<LoopContext> CreateContexts(
        IEnumerable collection,
        string collectionName,
        string? iterationVariableName = null,
        LoopContext? parent = null)
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }

        List<object> items = new List<object>();
        foreach (object item in collection)
        {
            items.Add(item);
        }

        List<LoopContext> contexts = new List<LoopContext>(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            contexts.Add(new LoopContext(items[i], i, items.Count, collectionName, iterationVariableName, parent));
        }

        return contexts;
    }

    /// <summary>
    /// Tries to resolve a variable in this context or parent contexts.
    /// Supports direct property access ({{Name}}), named iteration variable access ({{item.Name}}),
    /// and metadata ({{@index}}).
    /// </summary>
    /// <remarks>
    /// <para>Variable resolution follows this precedence order (first match wins):</para>
    /// <list type="number">
    /// <item><description>Loop metadata (@index, @first, @last, @count)</description></item>
    /// <item><description>Named iteration variable direct reference (e.g., {{item}} when using "item in Items")</description></item>
    /// <item><description>Named iteration variable property access (e.g., {{item.Name}})</description></item>
    /// <item><description>Current item property (implicit syntax, e.g., {{Name}})</description></item>
    /// <item><description>Parent loop context (recursive, for nested loop variable access)</description></item>
    /// </list>
    /// <para>
    /// This means local scope always takes precedence over parent scope. If the current item
    /// has a property with the same name as a parent loop's iteration variable, the current
    /// item's property will be resolved first.
    /// </para>
    /// </remarks>
    public bool TryResolveVariable(string variableName, out object? value)
    {
        // Check for loop metadata variables
        if (variableName.StartsWith("@"))
        {
            return TryResolveMetadata(variableName, out value);
        }

        // Check if accessing via named iteration variable (e.g., "item" or "item.Name")
        if (IterationVariableName != null)
        {
            // Direct reference to iteration variable (e.g., {{item}})
            if (variableName == IterationVariableName)
            {
                value = CurrentItem;
                return true;
            }

            // Property access via iteration variable (e.g., {{item.Name}})
            if (variableName.StartsWith(_iterationVariablePrefix!, StringComparison.Ordinal))
            {
                string propertyPath = variableName.Substring(_iterationVariablePrefix!.Length);
                return TryResolveFromCurrentItem(propertyPath, out value);
            }
        }

        // Try to resolve from current item (implicit syntax - backward compatible)
        if (TryResolveFromCurrentItem(variableName, out value))
        {
            return true;
        }

        // Try parent context (for nested loop variable access like {{category.Name}} from inner loop)
        if (Parent != null && Parent.TryResolveVariable(variableName, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    private bool TryResolveMetadata(string metadataName, out object? value)
    {
        switch (metadataName.ToLowerInvariant())
        {
            case "@index":
                value = Index;
                return true;
            case "@first":
                value = IsFirst;
                return true;
            case "@last":
                value = IsLast;
                return true;
            case "@count":
                value = Count;
                return true;
            default:
                value = null;
                return false;
        }
    }

    private bool TryResolveFromCurrentItem(string variableName, out object? value)
    {
        // Special case: "." or "this" refers to the current item itself
        // Useful for collections of primitive values (strings, numbers, etc.)
        if (variableName == "." || variableName == "this")
        {
            value = CurrentItem;
            return true;
        }

        // Use PropertyPathResolver for nested property access
        PropertyPath path;
        if (!PropertyPath.TryParse(variableName, out PropertyPath? parsedPath) || parsedPath == null)
        {
            value = null;
            return false;
        }

        path = parsedPath;
        value = PropertyPathResolver.ResolvePath(CurrentItem, path);
        return value != null;
    }
}
