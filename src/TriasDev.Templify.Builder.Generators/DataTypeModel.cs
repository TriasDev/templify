// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace TriasDev.Templify.Builder.Generators;

/// <summary>An object type to generate extension properties for (fully qualified C# name).</summary>
internal sealed record DataTypeModel(string TypeName, bool IsItem, EquatableArray<PropertyModel> Properties);

/// <summary>How a data property is reached.</summary>
internal enum PropertyKind
{
    /// <summary><c>TemplatePath&lt;T&gt;</c> through <c>Path(x =&gt; x.Name)</c>.</summary>
    Path,

    /// <summary><c>TemplateCollection&lt;TItem&gt;</c> through <c>Items&lt;TItem&gt;(x =&gt; x.Name)</c>.</summary>
    Collection,
}

/// <summary>
/// A data property: its C# name (escaped if a keyword), its type (or item type for a collection) and the receivers it is
/// not generated for because they already have a member of that name.
/// </summary>
internal sealed record PropertyModel(string Name, string Identifier, PropertyKind Kind, string TypeName, bool SkipOnPath, bool SkipOnLoopItem, string? Obsolete);

/// <summary>A name collision to report (TBG001).</summary>
internal sealed record CollisionModel(string TypeName, string Property, string Receiver, LocationModel? Location);

/// <summary>A source location that survives between generator runs (a <see cref="Microsoft.CodeAnalysis.Location"/> does not compare by value).</summary>
internal sealed record LocationModel(string FilePath, Microsoft.CodeAnalysis.Text.TextSpan Span, Microsoft.CodeAnalysis.Text.LinePositionSpan LineSpan)
{
    public Microsoft.CodeAnalysis.Location ToLocation() => Microsoft.CodeAnalysis.Location.Create(FilePath, Span, LineSpan);
}

/// <summary>What one root (a <c>DataAs&lt;T&gt;</c> call or an assembly attribute) contributes.</summary>
internal sealed record RootModel(EquatableArray<DataTypeModel> Types, EquatableArray<CollisionModel> Collisions);

/// <summary>An immutable array that compares by its items, for the incremental pipeline's caching.</summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _items;

    public EquatableArray(ImmutableArray<T> items) => _items = items;

    public ImmutableArray<T> Items => _items.IsDefault ? ImmutableArray<T>.Empty : _items;

    public bool Equals(EquatableArray<T> other) => Items.SequenceEqual(other.Items);

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        int hash = 17;
        foreach (T item in Items)
        {
            hash = (hash * 31) + item.GetHashCode();
        }

        return hash;
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
