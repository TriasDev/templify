// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.PropertyPaths;

/// <summary>
/// Resolves property paths by navigating through nested objects, collections, and dictionaries.
/// </summary>
/// <remarks>
/// <para>
/// Dictionary-like objects (<see cref="IDictionary{TKey,TValue}"/>, <see cref="IReadOnlyDictionary{TKey,TValue}"/>,
/// <see cref="System.Dynamic.ExpandoObject"/> and non-generic <see cref="IDictionary"/>) are looked up by key first,
/// using the dictionary's own key comparer. Only when no such key exists does resolution fall back to the
/// dictionary's .NET members, so a key named <c>Count</c>, <c>Keys</c> or <c>Values</c> is never shadowed,
/// while <c>{{Dict.Count}}</c> on a dictionary without a <c>Count</c> key still returns the entry count.
/// </para>
/// <para>
/// Indexer keys are converted to the dictionary's key type using the invariant culture
/// (e.g. <c>Map[1]</c> on a <c>Dictionary&lt;int, string&gt;</c>).
/// </para>
/// <para>
/// <see cref="JsonElement"/> values (e.g. from <c>JsonSerializer.Deserialize&lt;Dictionary&lt;string, object&gt;&gt;</c>)
/// are navigated by property name and array index; primitives are converted to .NET values, and
/// resolved objects/arrays are converted to dictionaries/lists.
/// </para>
/// </remarks>
internal sealed class PropertyPathResolver
{
    private delegate bool DictionaryLookup(object dictionary, object key, out object? value);

    private sealed class DictionaryAccessor
    {
        public DictionaryAccessor(Type keyType, DictionaryLookup lookup)
        {
            KeyType = keyType;
            Lookup = lookup;
        }

        public Type KeyType { get; }

        public DictionaryLookup Lookup { get; }
    }

    private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

    private static readonly MethodInfo _lookupDictionaryMethod = typeof(PropertyPathResolver)
        .GetMethod(nameof(LookupDictionary), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo _lookupReadOnlyDictionaryMethod = typeof(PropertyPathResolver)
        .GetMethod(nameof(LookupReadOnlyDictionary), BindingFlags.NonPublic | BindingFlags.Static)!;

    // Per-type cache of generic dictionary accessors; a null value means "not a generic dictionary".
    private static readonly ConcurrentDictionary<Type, DictionaryAccessor?> _dictionaryAccessors = new();

    // Per-(type, member name) cache of readable properties/fields; a null value means "no such member".
    private static readonly ConcurrentDictionary<(Type Type, string Name), MemberInfo?> _members = new();

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

        // JSON objects/arrays reached by navigation are materialized as dictionaries/lists
        // so that loops, conditionals and value conversion treat them like parsed JSON data.
        if (current is JsonElement { ValueKind: JsonValueKind.Object or JsonValueKind.Array } json)
        {
            current = JsonDataParser.ConvertJsonElementToObject(json);
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
        TryResolvePath(root, path, out object? value);
        return value;
    }

    /// <summary>
    /// Tries to resolve a single segment of the path.
    /// Distinguishes between "found with null value" and "not found".
    /// </summary>
    private static bool TryResolveSegment(object current, PropertyPathSegment segment, out object? value)
    {
        if (current is JsonElement json)
        {
            return TryResolveJsonElement(json, segment, out value);
        }

        if (segment.IsIndexer)
        {
            if (segment.Index.HasValue && current is IList list)
            {
                int index = segment.Index.Value;
                if (index >= 0 && index < list.Count)
                {
                    value = list[index];
                    return true;
                }

                value = null;
                return false;
            }

            return TryGetDictionaryValue(current, segment.Name, out value);
        }

        // An existing dictionary key takes precedence over the dictionary's own members (Count, Keys, ...).
        if (TryGetDictionaryValue(current, segment.Name, out value))
        {
            return true;
        }

        return TryGetMemberValue(current, segment.Name, out value);
    }

    /// <summary>
    /// Tries to read a public instance property or field (case-insensitive).
    /// </summary>
    private static bool TryGetMemberValue(object current, string name, out object? value)
    {
        MemberInfo? member = _members.GetOrAdd((current.GetType(), name), static key => FindMember(key.Type, key.Name));

        switch (member)
        {
            case PropertyInfo property:
                value = property.GetValue(current);
                return true;
            case FieldInfo field:
                value = field.GetValue(current);
                return true;
            default:
                value = null;
                return false;
        }
    }

    private static MemberInfo? FindMember(Type type, string name)
    {
        PropertyInfo? property = type.GetProperty(name, MemberFlags);
        if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
        {
            return property;
        }

        return type.GetField(name, MemberFlags);
    }

    /// <summary>
    /// Tries to look up <paramref name="key"/> in a dictionary-like object.
    /// Returns false when the object is not a dictionary or the key does not exist.
    /// </summary>
    private static bool TryGetDictionaryValue(object current, string key, out object? value)
    {
        DictionaryAccessor? accessor = _dictionaryAccessors.GetOrAdd(current.GetType(), CreateAccessor);
        if (accessor != null)
        {
            if (TryConvertKey(key, accessor.KeyType, out object? typedKey))
            {
                return accessor.Lookup(current, typedKey!, out value);
            }

            value = null;
            return false;
        }

        // Non-generic dictionaries (e.g. Hashtable)
        if (current is IDictionary dictionary && dictionary.Contains(key))
        {
            value = dictionary[key];
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Builds an accessor for the generic dictionary interface implemented by <paramref name="type"/>, if any.
    /// Prefers string-keyed interfaces, and <see cref="IDictionary{TKey,TValue}"/> over
    /// <see cref="IReadOnlyDictionary{TKey,TValue}"/>.
    /// </summary>
    private static DictionaryAccessor? CreateAccessor(Type type)
    {
        Type? dictionaryInterface = null;
        Type? readOnlyInterface = null;

        IEnumerable<Type> interfaces = type.IsInterface
            ? type.GetInterfaces().Prepend(type)
            : type.GetInterfaces();

        foreach (Type candidate in interfaces)
        {
            if (!candidate.IsGenericType)
            {
                continue;
            }

            Type definition = candidate.GetGenericTypeDefinition();
            if (definition == typeof(IDictionary<,>))
            {
                dictionaryInterface = PreferStringKey(dictionaryInterface, candidate);
            }
            else if (definition == typeof(IReadOnlyDictionary<,>))
            {
                readOnlyInterface = PreferStringKey(readOnlyInterface, candidate);
            }
        }

        Type? chosen;
        MethodInfo lookupMethod;
        if (dictionaryInterface != null
            && (readOnlyInterface == null || IsStringKeyed(dictionaryInterface) || !IsStringKeyed(readOnlyInterface)))
        {
            chosen = dictionaryInterface;
            lookupMethod = _lookupDictionaryMethod;
        }
        else
        {
            chosen = readOnlyInterface;
            lookupMethod = _lookupReadOnlyDictionaryMethod;
        }

        if (chosen == null)
        {
            return null;
        }

        Type[] arguments = chosen.GetGenericArguments();
        DictionaryLookup lookup = (DictionaryLookup)lookupMethod
            .MakeGenericMethod(arguments)
            .CreateDelegate(typeof(DictionaryLookup));

        return new DictionaryAccessor(arguments[0], lookup);
    }

    private static Type PreferStringKey(Type? current, Type candidate)
    {
        return current == null || (!IsStringKeyed(current) && IsStringKeyed(candidate)) ? candidate : current;
    }

    private static bool IsStringKeyed(Type dictionaryInterface)
    {
        return dictionaryInterface.GetGenericArguments()[0] == typeof(string);
    }

    private static bool LookupDictionary<TKey, TValue>(object dictionary, object key, out object? value)
    {
        if (((IDictionary<TKey, TValue>)dictionary).TryGetValue((TKey)key, out TValue? result))
        {
            value = result;
            return true;
        }

        value = null;
        return false;
    }

    private static bool LookupReadOnlyDictionary<TKey, TValue>(object dictionary, object key, out object? value)
    {
        if (((IReadOnlyDictionary<TKey, TValue>)dictionary).TryGetValue((TKey)key, out TValue? result))
        {
            value = result;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Converts a path key to the dictionary's key type using the invariant culture.
    /// </summary>
    private static bool TryConvertKey(string key, Type keyType, out object? typedKey)
    {
        if (keyType == typeof(string) || keyType == typeof(object))
        {
            typedKey = key;
            return true;
        }

        Type targetType = Nullable.GetUnderlyingType(keyType) ?? keyType;

        if (targetType.IsEnum)
        {
            return Enum.TryParse(targetType, key, ignoreCase: false, out typedKey);
        }

        if (targetType == typeof(Guid))
        {
            bool parsed = Guid.TryParse(key, out Guid guid);
            typedKey = parsed ? guid : null;
            return parsed;
        }

        if (typeof(IConvertible).IsAssignableFrom(targetType))
        {
            try
            {
                typedKey = Convert.ChangeType(key, targetType, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                // Key cannot be represented in the dictionary's key type, so it cannot exist.
            }
        }

        typedKey = null;
        return false;
    }

    /// <summary>
    /// Resolves a segment against a <see cref="JsonElement"/>: object → property, array → index.
    /// Primitive results are converted to .NET values.
    /// </summary>
    private static bool TryResolveJsonElement(JsonElement element, PropertyPathSegment segment, out object? value)
    {
        value = null;
        JsonElement child;

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (!element.TryGetProperty(segment.Name, out child))
                {
                    return false;
                }
                break;

            case JsonValueKind.Array:
                if (!segment.Index.HasValue || segment.Index.Value < 0 || segment.Index.Value >= element.GetArrayLength())
                {
                    return false;
                }
                child = element[segment.Index.Value];
                break;

            default:
                return false;
        }

        value = child.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            ? child
            : JsonDataParser.ConvertJsonElementToObject(child);
        return true;
    }
}
