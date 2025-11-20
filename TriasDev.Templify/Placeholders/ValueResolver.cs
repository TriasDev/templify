// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using TriasDev.Templify.PropertyPaths;

namespace TriasDev.Templify.Placeholders;

/// <summary>
/// Resolves values from a data dictionary, supporting both simple and nested property paths.
/// </summary>
internal sealed class ValueResolver
{

    /// <summary>
    /// Resolves a value from the data dictionary using the specified variable path.
    /// </summary>
    /// <param name="data">The data dictionary containing root-level values.</param>
    /// <param name="variablePath">The variable path (e.g., "Name" or "Customer.Address.City").</param>
    /// <param name="value">The resolved value if found; otherwise, null.</param>
    /// <returns>True if the value was found; otherwise, false.</returns>
    public bool TryResolveValue(
        Dictionary<string, object> data,
        string variablePath,
        out object? value)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (string.IsNullOrWhiteSpace(variablePath))
        {
            throw new ArgumentException("Variable path cannot be null or whitespace.", nameof(variablePath));
        }

        // Fast path: Check for direct dictionary key match (backward compatibility)
        if (data.TryGetValue(variablePath, out value))
        {
            return true;
        }

        // Check if this is a nested path
        if (!variablePath.Contains('.') && !variablePath.Contains('['))
        {
            // Simple path, not found in dictionary
            value = null;
            return false;
        }

        // Parse and resolve nested path
        if (!PropertyPath.TryParse(variablePath, out PropertyPath? path) || path == null)
        {
            // Invalid path format
            value = null;
            return false;
        }

        // Get the root segment name
        string rootSegmentName = path.Segments[0].Name;

        // Try to find the root object in the dictionary
        if (!data.TryGetValue(rootSegmentName, out object? rootObject))
        {
            // Root object not found
            value = null;
            return false;
        }

        // If it's a simple path (single segment), return the root object
        if (path.IsSimple)
        {
            value = rootObject;
            return true;
        }

        // Navigate through the nested path starting from the root object
        // Skip the first segment since we already resolved it
        List<PropertyPathSegment> remainingSegments = path.Segments.Skip(1).ToList();

        if (remainingSegments.Count == 0)
        {
            value = rootObject;
            return true;
        }

        // Build subpath string correctly
        StringBuilder subPathBuilder = new StringBuilder();
        for (int i = 0; i < remainingSegments.Count; i++)
        {
            PropertyPathSegment segment = remainingSegments[i];
            if (segment.IsIndexer)
            {
                subPathBuilder.Append($"[{segment.Name}]");
            }
            else
            {
                if (i > 0 && !remainingSegments[i - 1].IsIndexer)
                {
                    subPathBuilder.Append('.');
                }
                subPathBuilder.Append(segment.Name);
            }
        }

        PropertyPath subPath = PropertyPath.Parse(subPathBuilder.ToString());
        value = PropertyPathResolver.ResolvePath(rootObject, subPath);
        return value != null;
    }

    /// <summary>
    /// Resolves a value from the data dictionary using the specified variable path.
    /// Throws an exception if the value is not found.
    /// </summary>
    /// <param name="data">The data dictionary containing root-level values.</param>
    /// <param name="variablePath">The variable path (e.g., "Name" or "Customer.Address.City").</param>
    /// <returns>The resolved value.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the value cannot be resolved.</exception>
    public object ResolveValue(Dictionary<string, object> data, string variablePath)
    {
        if (TryResolveValue(data, variablePath, out object? value))
        {
            return value!;
        }

        throw new InvalidOperationException($"Could not resolve variable path: {variablePath}");
    }
}
