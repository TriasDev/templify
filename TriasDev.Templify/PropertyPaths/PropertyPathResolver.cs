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
    /// Tries to resolve a property path starting from the given root object.
    /// Distinguishes between "path exists with null value" and "path doesn't exist".
    /// </summary>
    /// <param name="root">The root object to start navigation from. If null, returns true with null value.</param>
    /// <param name="path">The property path to resolve.</param>
    /// <param name="value">The resolved value if the path exists; otherwise, null.</param>
    /// <returns>True if the path exists (even if the value is null); false if the path doesn't exist.</returns>
    /// <remarks>
    /// <para>
    /// When a null value is encountered mid-path (e.g., resolving "Address.City" when Address is null),
    /// this method returns true with a null value. This is intentional: the path is considered valid
    /// because all segments up to the null value were successfully resolved; traversal simply cannot
    /// continue beyond the null. The method does not inspect type metadata for the remaining segments
    /// once a null value is encountered.
    /// </para>
    /// <para>
    /// This behavior allows template validation to correctly distinguish between:
    /// <list type="bullet">
    /// <item><description>"street2": null - Variable exists with null value (no warning)</description></item>
    /// <item><description>"street2" not in data - Variable is missing (warning)</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public static bool TryResolvePath(object? root, PropertyPath path, out object? value)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        value = null;
        object? current = root;

        foreach (PropertyPathSegment segment in path.Segments)
        {
            if (current == null)
            {
                // Previous segment was null - path ends here
                // This is valid: we found the path, value is null
                return true;
            }

            if (!TryResolveSegment(current, segment, out current))
            {
                // Segment not found - path doesn't exist
                return false;
            }
        }

        value = current;
        return true;
    }

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
    /// Tries to resolve a single segment of the path.
    /// Distinguishes between "found with null value" and "not found".
    /// </summary>
    /// <param name="current">The current object to resolve against.</param>
    /// <param name="segment">The segment to resolve.</param>
    /// <param name="value">The resolved value if found; otherwise, null.</param>
    /// <returns>True if the segment was found (even if value is null); false if not found.</returns>
    private static bool TryResolveSegment(object current, PropertyPathSegment segment, out object? value)
    {
        if (segment.IsIndexer)
        {
            return TryResolveIndexer(current, segment, out value);
        }
        else
        {
            return TryResolveProperty(current, segment, out value);
        }
    }

    /// <summary>
    /// Resolves a single segment of the path.
    /// </summary>
    private static object? ResolveSegment(object current, PropertyPathSegment segment)
    {
        TryResolveSegment(current, segment, out object? value);
        return value;
    }

    /// <summary>
    /// Tries to resolve an indexer segment (e.g., [0] or [Key]).
    /// </summary>
    private static bool TryResolveIndexer(object current, PropertyPathSegment segment, out object? value)
    {
        value = null;

        // Try as numeric index for collections/arrays
        if (segment.Index.HasValue)
        {
            // Check if it's a list
            if (current is IList list)
            {
                int index = segment.Index.Value;
                if (index >= 0 && index < list.Count)
                {
                    value = list[index];
                    return true;
                }
                return false;
            }

            // Check if it's an array
            if (current is Array array)
            {
                int index = segment.Index.Value;
                if (index >= 0 && index < array.Length)
                {
                    value = array.GetValue(index);
                    return true;
                }
                return false;
            }
        }

        // Try as dictionary key (string key)
        if (current is IDictionary dictionary)
        {
            if (dictionary.Contains(segment.Name))
            {
                value = dictionary[segment.Name];
                return true;
            }
            return false;
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
                                value = indexerProp.GetValue(current, new object[] { segment.Name });
                                return true;
                            }
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Tries to resolve a property segment (e.g., Customer or Address).
    /// </summary>
    private static bool TryResolveProperty(object current, PropertyPathSegment segment, out object? value)
    {
        value = null;
        Type currentType = current.GetType();

        // Try as property
        PropertyInfo? property = currentType.GetProperty(segment.Name,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (property != null && property.CanRead)
        {
            value = property.GetValue(current);
            return true;
        }

        // Try as field
        FieldInfo? field = currentType.GetField(segment.Name,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (field != null)
        {
            value = field.GetValue(current);
            return true;
        }

        // Try as dictionary key (for dictionaries accessed with dot notation)
        if (current is IDictionary dictionary)
        {
            if (dictionary.Contains(segment.Name))
            {
                value = dictionary[segment.Name];
                return true;
            }
            return false;
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
                                value = indexerProp.GetValue(current, new object[] { segment.Name });
                                return true;
                            }
                        }
                    }
                }
            }
        }

        return false;
    }

}
