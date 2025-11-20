// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Reflection;

namespace TriasDev.Templify.PropertyPaths;

/// <summary>
/// Resolves property paths by navigating through nested objects, collections, and dictionaries.
/// </summary>
internal sealed class PropertyPathResolver
{
    /// <summary>
    /// Resolves a property path starting from the given root object.
    /// </summary>
    /// <param name="root">The root object to start navigation from.</param>
    /// <param name="path">The property path to resolve.</param>
    /// <returns>The resolved value, or null if any segment in the path could not be resolved.</returns>
    public static object? ResolvePath(object? root, PropertyPath path)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        object? current = root;

        foreach (PropertyPathSegment segment in path.Segments)
        {
            if (current == null)
            {
                return null;
            }

            current = ResolveSegment(current, segment);

            if (current == null)
            {
                return null;
            }
        }

        return current;
    }

    /// <summary>
    /// Resolves a single segment of the path.
    /// </summary>
    private static object? ResolveSegment(object current, PropertyPathSegment segment)
    {
        if (segment.IsIndexer)
        {
            return ResolveIndexer(current, segment);
        }
        else
        {
            return ResolveProperty(current, segment);
        }
    }

    /// <summary>
    /// Resolves an indexer segment (e.g., [0] or [Key]).
    /// </summary>
    private static object? ResolveIndexer(object current, PropertyPathSegment segment)
    {
        // Try as numeric index for collections/arrays
        if (segment.Index.HasValue)
        {
            // Check if it's a list
            if (current is IList list)
            {
                int index = segment.Index.Value;
                if (index >= 0 && index < list.Count)
                {
                    return list[index];
                }
                return null;
            }

            // Check if it's an array
            if (current is Array array)
            {
                int index = segment.Index.Value;
                if (index >= 0 && index < array.Length)
                {
                    return array.GetValue(index);
                }
                return null;
            }
        }

        // Try as dictionary key (string key)
        if (current is IDictionary dictionary)
        {
            if (dictionary.Contains(segment.Name))
            {
                return dictionary[segment.Name];
            }
            return null;
        }

        // Try generic dictionary with string key
        Type currentType = current.GetType();
        if (currentType.IsGenericType)
        {
            Type genericDef = currentType.GetGenericTypeDefinition();
            if (genericDef == typeof(Dictionary<,>) || genericDef == typeof(IDictionary<,>))
            {
                Type[] genericArgs = currentType.GetGenericArguments();
                if (genericArgs[0] == typeof(string))
                {
                    // Use reflection to get the indexer
                    PropertyInfo? indexerProp = currentType.GetProperty("Item", new[] { typeof(string) });
                    if (indexerProp != null)
                    {
                        // Check if key exists
                        MethodInfo? containsKey = currentType.GetMethod("ContainsKey");
                        if (containsKey != null)
                        {
                            bool exists = (bool)containsKey.Invoke(current, new object[] { segment.Name })!;
                            if (exists)
                            {
                                return indexerProp.GetValue(current, new object[] { segment.Name });
                            }
                        }
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves a property segment (e.g., Customer or Address).
    /// </summary>
    private static object? ResolveProperty(object current, PropertyPathSegment segment)
    {
        Type currentType = current.GetType();

        // Try as property
        PropertyInfo? property = currentType.GetProperty(segment.Name,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (property != null && property.CanRead)
        {
            return property.GetValue(current);
        }

        // Try as field
        FieldInfo? field = currentType.GetField(segment.Name,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (field != null)
        {
            return field.GetValue(current);
        }

        // Try as dictionary key (for dictionaries accessed with dot notation)
        if (current is IDictionary dictionary && dictionary.Contains(segment.Name))
        {
            return dictionary[segment.Name];
        }

        // Try generic dictionary with string key
        if (currentType.IsGenericType)
        {
            Type genericDef = currentType.GetGenericTypeDefinition();
            if (genericDef == typeof(Dictionary<,>) || genericDef == typeof(IDictionary<,>))
            {
                Type[] genericArgs = currentType.GetGenericArguments();
                if (genericArgs[0] == typeof(string))
                {
                    PropertyInfo? indexerProp = currentType.GetProperty("Item", new[] { typeof(string) });
                    if (indexerProp != null)
                    {
                        MethodInfo? containsKey = currentType.GetMethod("ContainsKey");
                        if (containsKey != null)
                        {
                            bool exists = (bool)containsKey.Invoke(current, new object[] { segment.Name })!;
                            if (exists)
                            {
                                return indexerProp.GetValue(current, new object[] { segment.Name });
                            }
                        }
                    }
                }
            }
        }

        return null;
    }
}
