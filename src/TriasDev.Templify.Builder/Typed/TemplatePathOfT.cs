// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Linq.Expressions;
using System.Text.Json;

namespace TriasDev.Templify.Builder;

/// <summary>
/// A data path that knows the C# type of the value it reads, so the next level is chosen with the compiler's help:
/// <c>order.Path(o =&gt; o.Customer.Name)</c>. It is the same path value as the string API produces and converts to
/// <see cref="TemplatePath"/> and <see cref="Condition"/> wherever those are expected.
/// </summary>
/// <typeparam name="T">The type of the value at this path.</typeparam>
public readonly struct TemplatePath<T> : IEquatable<TemplatePath<T>>
{
    private readonly JsonNamingPolicy? _naming;

    internal TemplatePath(TemplatePath path, JsonNamingPolicy? naming)
    {
        Untyped = path;
        _naming = naming;
    }

    /// <summary>Gets the path as a plain <see cref="TemplatePath"/>.</summary>
    public TemplatePath Untyped { get; }

    /// <summary>A value below this one: a chain of properties and constant dictionary keys.</summary>
    /// <typeparam name="TProperty">The type of the value.</typeparam>
    /// <param name="member">The member chain, for example <c>o =&gt; o.Customer.Name</c>.</param>
    /// <returns>The path.</returns>
    /// <exception cref="ArgumentException">The expression is not a chain of properties and constant dictionary keys.</exception>
    public TemplatePath<TProperty> Path<TProperty>(Expression<Func<T, TProperty>> member)
    {
        string relative = PathExpression.Relative(member, _naming);
        return new TemplatePath<TProperty>(relative.Length == 0 ? Untyped : Untyped[relative], _naming);
    }

    /// <summary>A collection below this value, to loop over.</summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <param name="member">The member chain, for example <c>o =&gt; o.Lines</c>.</param>
    /// <returns>The collection.</returns>
    public TemplateCollection<TItem> Items<TItem>(Expression<Func<T, IEnumerable<TItem>>> member)
    {
        string relative = PathExpression.Relative(member, _naming);
        return new TemplateCollection<TItem>(relative.Length == 0 ? Untyped : Untyped[relative], _naming);
    }

    /// <summary>The value equals a string: <c>path = "value"</c>.</summary>
    public Condition Eq(string value) => Untyped.Eq(value);

    /// <summary>The value equals a number.</summary>
    public Condition Eq(double value) => Untyped.Eq(value);

    /// <summary>The value differs from a string.</summary>
    public Condition NotEq(string value) => Untyped.NotEq(value);

    /// <summary>The value differs from a number.</summary>
    public Condition NotEq(double value) => Untyped.NotEq(value);

    /// <summary>The value is greater than a number.</summary>
    public Condition Gt(double value) => Untyped.Gt(value);

    /// <summary>The value is at least a number.</summary>
    public Condition Ge(double value) => Untyped.Ge(value);

    /// <summary>The value is less than a number.</summary>
    public Condition Lt(double value) => Untyped.Lt(value);

    /// <summary>The value is at most a number.</summary>
    public Condition Le(double value) => Untyped.Le(value);

    /// <summary>The value is one of the strings.</summary>
    public Condition In(params string[] values) => Untyped.In(values);

    /// <summary>The value contains a string (or a collection contains it).</summary>
    public Condition Contains(string value) => Untyped.Contains(value);

    /// <summary>The value starts with a string.</summary>
    public Condition StartsWith(string value) => Untyped.StartsWith(value);

    /// <summary>The value ends with a string.</summary>
    public Condition EndsWith(string value) => Untyped.EndsWith(value);

    /// <summary>Gets the condition that the value exists.</summary>
    public Condition Exists => Untyped.Exists;

    /// <summary>Gets the condition that the value is empty.</summary>
    public Condition IsEmpty => Untyped.IsEmpty;

    /// <summary>Gets the condition that the value is not empty.</summary>
    public Condition IsNotEmpty => Untyped.IsNotEmpty;

    /// <summary>The plain path.</summary>
    /// <param name="path">The typed path.</param>
    public static implicit operator TemplatePath(TemplatePath<T> path) => path.Untyped;

    /// <summary>The value is truthy.</summary>
    /// <param name="path">The typed path.</param>
    public static implicit operator Condition(TemplatePath<T> path) => path.Untyped;

    /// <summary>The value is not truthy.</summary>
    /// <param name="path">The typed path.</param>
    /// <returns>The condition.</returns>
    public static Condition operator !(TemplatePath<T> path) => !path.Untyped;

    /// <summary>Both are true.</summary>
    /// <param name="left">The typed path.</param>
    /// <param name="right">The other condition (a typed path converts).</param>
    /// <returns>The condition.</returns>
    public static Condition operator &(TemplatePath<T> left, Condition right) => (Condition)left.Untyped & right;

    /// <summary>At least one is true.</summary>
    /// <param name="left">The typed path.</param>
    /// <param name="right">The other condition (a typed path converts).</param>
    /// <returns>The condition.</returns>
    public static Condition operator |(TemplatePath<T> left, Condition right) => (Condition)left.Untyped | right;

    /// <summary>Compares two paths.</summary>
    public static bool operator ==(TemplatePath<T> left, TemplatePath<T> right) => left.Equals(right);

    /// <summary>Compares two paths.</summary>
    public static bool operator !=(TemplatePath<T> left, TemplatePath<T> right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(TemplatePath<T> other) => Untyped.Equals(other.Untyped);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TemplatePath<T> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Untyped.GetHashCode();

    /// <summary>Returns the path as Templify writes it.</summary>
    /// <returns>The dotted path.</returns>
    public override string ToString() => Untyped.ToString();
}

/// <summary>A collection in the data, typed by its items, for <c>ForEach</c>.</summary>
/// <typeparam name="TItem">The item type.</typeparam>
public readonly struct TemplateCollection<TItem> : IEquatable<TemplateCollection<TItem>>
{
    internal TemplateCollection(TemplatePath path, JsonNamingPolicy? naming)
    {
        Untyped = path;
        Naming = naming;
    }

    /// <summary>Gets the path as a plain <see cref="TemplatePath"/>.</summary>
    public TemplatePath Untyped { get; }

    internal JsonNamingPolicy? Naming { get; }

    /// <summary>Gets the condition that the collection exists.</summary>
    public Condition Exists => Untyped.Exists;

    /// <summary>Gets the condition that the collection is empty.</summary>
    public Condition IsEmpty => Untyped.IsEmpty;

    /// <summary>Gets the condition that the collection is not empty.</summary>
    public Condition IsNotEmpty => Untyped.IsNotEmpty;

    /// <summary>The plain path.</summary>
    /// <param name="collection">The collection.</param>
    public static implicit operator TemplatePath(TemplateCollection<TItem> collection) => collection.Untyped;

    /// <summary>The collection is truthy (exists and is not empty).</summary>
    /// <param name="collection">The collection.</param>
    public static implicit operator Condition(TemplateCollection<TItem> collection) => collection.Untyped;

    /// <summary>Compares two collections.</summary>
    public static bool operator ==(TemplateCollection<TItem> left, TemplateCollection<TItem> right) => left.Equals(right);

    /// <summary>Compares two collections.</summary>
    public static bool operator !=(TemplateCollection<TItem> left, TemplateCollection<TItem> right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(TemplateCollection<TItem> other) => Untyped.Equals(other.Untyped);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TemplateCollection<TItem> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Untyped.GetHashCode();

    /// <inheritdoc/>
    public override string ToString() => Untyped.ToString();
}

/// <summary>The current item of a typed loop.</summary>
/// <typeparam name="T">The item type.</typeparam>
public sealed class LoopItem<T>
{
    private readonly JsonNamingPolicy? _naming;

    internal LoopItem(LoopItem item, JsonNamingPolicy? naming)
    {
        Item = item;
        _naming = naming;
    }

    /// <summary>Gets the untyped loop item.</summary>
    public LoopItem Item { get; }

    /// <summary>Gets the item itself.</summary>
    public TemplatePath<T> Self => new TemplatePath<T>(Item.Self, _naming);

    /// <summary>Gets the 0-based index (<c>@index</c>).</summary>
    public TemplatePath Index => Item.Index;

    /// <summary>Gets the 1-based number (<c>@number</c>).</summary>
    public TemplatePath Number => Item.Number;

    /// <summary>Gets the number of items (<c>@count</c>).</summary>
    public TemplatePath Count => Item.Count;

    /// <summary>Gets the condition that this is the first item.</summary>
    public Condition IsFirst => Item.IsFirst;

    /// <summary>Gets the condition that this is the last item.</summary>
    public Condition IsLast => Item.IsLast;

    /// <summary>A value of the item.</summary>
    /// <typeparam name="TProperty">The type of the value.</typeparam>
    /// <param name="member">The member chain, for example <c>l =&gt; l.Description</c>.</param>
    /// <returns>The path.</returns>
    public TemplatePath<TProperty> Path<TProperty>(Expression<Func<T, TProperty>> member) => Self.Path(member);

    /// <summary>A collection of the item, to loop over.</summary>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <param name="member">The member chain.</param>
    /// <returns>The collection.</returns>
    public TemplateCollection<TItem> Items<TItem>(Expression<Func<T, IEnumerable<TItem>>> member) => Self.Items(member);
}
