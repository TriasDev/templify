// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.RegularExpressions;

namespace TriasDev.Templify.PropertyPaths;

/// <summary>
/// Represents a parsed property path that can navigate through nested objects, collections, and dictionaries.
/// Supports mixed notation: Customer.Address.City, Items[0].Name, Settings[Theme], etc.
/// </summary>
internal sealed class PropertyPath
{
    private static readonly Regex _pathPattern = new Regex(
        @"^(\w+)(?:\.(\w+)|\[([^\]]+)\])*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Gets the segments that make up this path.
    /// </summary>
    public IReadOnlyList<PropertyPathSegment> Segments { get; }

    /// <summary>
    /// Gets whether this is a simple path (single segment, no nesting).
    /// </summary>
    public bool IsSimple => Segments.Count == 1 && !Segments[0].IsIndexer;

    /// <summary>
    /// Gets the original path string.
    /// </summary>
    public string OriginalPath { get; }

    private PropertyPath(string originalPath, IReadOnlyList<PropertyPathSegment> segments)
    {
        OriginalPath = originalPath ?? throw new ArgumentNullException(nameof(originalPath));
        Segments = segments ?? throw new ArgumentNullException(nameof(segments));
    }

    /// <summary>
    /// Parses a property path string into a PropertyPath object.
    /// </summary>
    /// <param name="path">The path string (e.g., "Customer.Address.City" or "Items[0].Name")</param>
    /// <returns>A parsed PropertyPath object.</returns>
    /// <exception cref="ArgumentException">Thrown if the path format is invalid.</exception>
    public static PropertyPath Parse(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
        }

        List<PropertyPathSegment> segments = new List<PropertyPathSegment>();

        // Use a more flexible parsing approach
        int i = 0;
        StringBuilder currentSegment = new StringBuilder();
        bool inBracket = false;
        char previousChar = '\0';

        while (i < path.Length)
        {
            char c = path[i];

            if (c == '.')
            {
                if (inBracket)
                {
                    throw new ArgumentException($"Invalid path: unexpected '.' inside brackets at position {i}.", nameof(path));
                }

                // Check for consecutive dots
                if (previousChar == '.')
                {
                    throw new ArgumentException($"Invalid path: consecutive dots at position {i}.", nameof(path));
                }

                if (currentSegment.Length > 0)
                {
                    segments.Add(new PropertyPathSegment(currentSegment.ToString(), isIndexer: false));
                    currentSegment.Clear();
                }
                // Allow dots after brackets (e.g., Orders[0].Customer)
                // Simply skip the dot if currentSegment is empty
            }
            else if (c == '[')
            {
                if (inBracket)
                {
                    throw new ArgumentException($"Invalid path: nested brackets at position {i}.", nameof(path));
                }

                if (currentSegment.Length > 0)
                {
                    segments.Add(new PropertyPathSegment(currentSegment.ToString(), isIndexer: false));
                    currentSegment.Clear();
                }

                inBracket = true;
            }
            else if (c == ']')
            {
                if (!inBracket)
                {
                    throw new ArgumentException($"Invalid path: unexpected ']' at position {i}.", nameof(path));
                }

                if (currentSegment.Length == 0)
                {
                    throw new ArgumentException($"Invalid path: empty brackets at position {i}.", nameof(path));
                }

                segments.Add(new PropertyPathSegment(currentSegment.ToString(), isIndexer: true));
                currentSegment.Clear();
                inBracket = false;
            }
            else if (char.IsLetterOrDigit(c) || c == '_')
            {
                currentSegment.Append(c);
            }
            else
            {
                throw new ArgumentException($"Invalid path: unexpected character '{c}' at position {i}.", nameof(path));
            }

            previousChar = c;
            i++;
        }

        if (inBracket)
        {
            throw new ArgumentException("Invalid path: unclosed bracket.", nameof(path));
        }

        if (currentSegment.Length > 0)
        {
            segments.Add(new PropertyPathSegment(currentSegment.ToString(), isIndexer: false));
        }

        if (segments.Count == 0)
        {
            throw new ArgumentException("Invalid path: no valid segments found.", nameof(path));
        }

        return new PropertyPath(path, segments);
    }

    /// <summary>
    /// Tries to parse a property path string.
    /// </summary>
    /// <param name="path">The path string to parse.</param>
    /// <param name="propertyPath">The parsed PropertyPath if successful; otherwise, null.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(string path, out PropertyPath? propertyPath)
    {
        try
        {
            propertyPath = Parse(path);
            return true;
        }
        catch
        {
            propertyPath = null;
            return false;
        }
    }

    public override string ToString()
    {
        return OriginalPath;
    }
}
