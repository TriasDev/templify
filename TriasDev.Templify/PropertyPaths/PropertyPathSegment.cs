// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.PropertyPaths;

/// <summary>
/// Represents a single segment in a property path.
/// </summary>
internal sealed class PropertyPathSegment
{
    /// <summary>
    /// Gets the name of the segment (property name or index value).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets whether this segment is an array/collection indexer.
    /// </summary>
    public bool IsIndexer { get; }

    /// <summary>
    /// Gets the numeric index value if this is an indexer segment.
    /// </summary>
    public int? Index { get; }

    public PropertyPathSegment(string name, bool isIndexer = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Segment name cannot be null or whitespace.", nameof(name));
        }

        Name = name;
        IsIndexer = isIndexer;

        if (isIndexer && int.TryParse(name, out int index))
        {
            Index = index;
        }
    }

    public override string ToString()
    {
        return IsIndexer ? $"[{Name}]" : Name;
    }
}
