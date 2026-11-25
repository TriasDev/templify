// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace TriasDev.Templify.Builder;

/// <summary>
/// A path into the data a template is filled with, for example <c>customer.address.city</c>. Paths are values:
/// they are built and checked in code and written into the template as Templify placeholders.
/// </summary>
/// <remarks>
/// Start from <see cref="TemplateComposer.Data"/> (or <see cref="Root"/>) and append segments with the indexer:
/// <c>template.Data["customer"]["address.city"]</c>. Each segment consists of letters, digits and <c>_</c>, as
/// Templify's placeholder grammar requires. An invalid segment throws <see cref="ArgumentException"/> where the path is
/// written, so the stack trace points at the line in the definition.
/// </remarks>
public readonly partial struct TemplatePath : IEquatable<TemplatePath>
{
    private readonly string? _value;

    private TemplatePath(string value, LoopScope? scope = null, bool isMetadata = false)
    {
        _value = value;
        Scope = scope;
        IsMetadata = isMetadata;
    }

    /// <summary>Gets a value indicating whether this is the empty path (the root of the data).</summary>
    internal bool IsRoot => _value is null;

    /// <summary>Gets the loop whose iteration variable this path starts from; null for a path from the data's root.</summary>
    internal LoopScope? Scope { get; }

    /// <summary>Gets a value indicating whether this is loop metadata (<c>@index</c>, <c>@first</c>, …) of <see cref="Scope"/>.</summary>
    internal bool IsMetadata { get; }

    /// <summary>The path of a loop's iteration variable, for example <c>line</c>.</summary>
    internal static TemplatePath ForItem(LoopScope scope) => new TemplatePath(scope.ItemName, scope);

    /// <summary>A loop metadata path, for example <c>@index</c>, of the innermost loop <paramref name="scope"/>.</summary>
    internal static TemplatePath ForMetadata(LoopScope scope, string name) => new TemplatePath("@" + name, scope, isMetadata: true);

    /// <summary>
    /// Appends one or more dot-separated segments, for example <c>path["address.city"]</c>.
    /// </summary>
    /// <param name="relative">The segments to append, separated by dots.</param>
    /// <exception cref="ArgumentException">A segment is empty or contains characters other than letters, digits and <c>_</c>.</exception>
    public TemplatePath this[string relative]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(relative);
            if (IsMetadata)
            {
                throw new InvalidOperationException($"Loop metadata ({_value}) has no properties.");
            }

            string[] segments = relative.Split('.');
            foreach (string segment in segments)
            {
                ValidateSegment(segment, relative);
            }

            if (_value is null && string.Equals(segments[0], "this", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "'this' cannot start a data path: Templify reads {{this}} as the current loop item.",
                    nameof(relative));
            }

            return new TemplatePath(_value is null ? relative : _value + "." + relative, Scope);
        }
    }

    /// <summary>
    /// Creates a path from the root of the data, for example <c>TemplatePath.Root("customer.name")</c>.
    /// </summary>
    /// <param name="path">The dot-separated segments of the path.</param>
    /// <returns>The path.</returns>
    public static TemplatePath Root(string path) => default(TemplatePath)[path];

    /// <summary>Gets a condition that holds when the path exists in the data: <c>path exists</c>.</summary>
    public Condition Exists => new PostfixCondition(ForCondition(), "exists");

    /// <summary>Gets a condition that holds when the value is missing, null, an empty text or an empty collection: <c>path is empty</c>.</summary>
    public Condition IsEmpty => new PostfixCondition(ForCondition(), "is empty");

    /// <summary>Gets a condition that holds when the value is not empty: <c>path is not empty</c>.</summary>
    public Condition IsNotEmpty => new PostfixCondition(ForCondition(), "is not empty");

    /// <summary>The value equals a text: <c>path = "value"</c> (ordinal, case-sensitive).</summary>
    /// <param name="value">The text.</param>
    /// <returns>The condition.</returns>
    public Condition Eq(string value) => Compare("=", ConditionText.String(value, nameof(value)));

    /// <summary>The value equals a number: <c>path = 3</c> (numeric across number types).</summary>
    /// <param name="value">The number.</param>
    /// <returns>The condition.</returns>
    public Condition Eq(double value) => Compare("=", ConditionText.Number(value, nameof(value)));

    /// <summary>The value differs from a text: <c>path != "value"</c>.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The condition.</returns>
    public Condition NotEq(string value) => Compare("!=", ConditionText.String(value, nameof(value)));

    /// <summary>The value differs from a number: <c>path != 3</c>.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The condition.</returns>
    public Condition NotEq(double value) => Compare("!=", ConditionText.Number(value, nameof(value)));

    /// <summary>The value is greater than a number: <c>path &gt; 3</c>.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The condition.</returns>
    public Condition Gt(double value) => Compare(">", ConditionText.Number(value, nameof(value)));

    /// <summary>The value is greater than or equal to a number: <c>path &gt;= 3</c>.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The condition.</returns>
    public Condition Ge(double value) => Compare(">=", ConditionText.Number(value, nameof(value)));

    /// <summary>The value is less than a number: <c>path &lt; 3</c>.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The condition.</returns>
    public Condition Lt(double value) => Compare("<", ConditionText.Number(value, nameof(value)));

    /// <summary>The value is less than or equal to a number: <c>path &lt;= 3</c>.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The condition.</returns>
    public Condition Le(double value) => Compare("<=", ConditionText.Number(value, nameof(value)));

    /// <summary>The value is one of several texts: <c>path in ("a", "b")</c>.</summary>
    /// <param name="values">The texts; at least one.</param>
    /// <returns>The condition.</returns>
    public Condition In(params string[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
        {
            throw new ArgumentException("In needs at least one value.", nameof(values));
        }

        return new InCondition(ForCondition(), values.Select(v => ConditionText.String(v, nameof(values))).ToArray());
    }

    /// <summary>The text contains a text, or the collection contains an item: <c>path contains "x"</c> (ordinal).</summary>
    /// <param name="value">The text to look for.</param>
    /// <returns>The condition.</returns>
    public Condition Contains(string value) => Compare("contains", ConditionText.String(value, nameof(value)));

    /// <summary>The text starts with a text: <c>path startswith "x"</c> (ordinal).</summary>
    /// <param name="value">The prefix.</param>
    /// <returns>The condition.</returns>
    public Condition StartsWith(string value) => Compare("startswith", ConditionText.String(value, nameof(value)));

    /// <summary>The text ends with a text: <c>path endswith "x"</c> (ordinal).</summary>
    /// <param name="value">The suffix.</param>
    /// <returns>The condition.</returns>
    public Condition EndsWith(string value) => Compare("endswith", ConditionText.String(value, nameof(value)));

    /// <summary>
    /// The value is truthy: not null, not <c>false</c>, not an empty or blank text, not zero, not an empty
    /// collection (<c>{{#if path}}</c>).
    /// </summary>
    /// <param name="path">The path.</param>
    public static implicit operator Condition(TemplatePath path) => new TruthCondition(path.ForCondition());

    /// <summary>The value is not truthy: <c>not path</c>.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The condition.</returns>
    public static Condition operator !(TemplatePath path) => !(Condition)path;

    /// <summary>Both values are truthy: <c>a and b</c>. (Mixed with a <see cref="Condition"/>, its operator applies.)</summary>
    /// <param name="left">The first path.</param>
    /// <param name="right">The second path.</param>
    /// <returns>The condition.</returns>
    public static Condition operator &(TemplatePath left, TemplatePath right) => (Condition)left & (Condition)right;

    /// <summary>At least one value is truthy: <c>a or b</c>.</summary>
    /// <param name="left">The first path.</param>
    /// <param name="right">The second path.</param>
    /// <returns>The condition.</returns>
    public static Condition operator |(TemplatePath left, TemplatePath right) => (Condition)left | (Condition)right;

    /// <summary>Returns the path as Templify writes it, for example <c>customer.address.city</c>.</summary>
    /// <returns>The dotted path; empty for the root.</returns>
    public override string ToString() => _value ?? string.Empty;

    /// <inheritdoc />
    public bool Equals(TemplatePath other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TemplatePath other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>Compares two paths.</summary>
    /// <param name="left">The first path.</param>
    /// <param name="right">The second path.</param>
    /// <returns><see langword="true"/> when both paths are equal.</returns>
    public static bool operator ==(TemplatePath left, TemplatePath right) => left.Equals(right);

    /// <summary>Compares two paths.</summary>
    /// <param name="left">The first path.</param>
    /// <param name="right">The second path.</param>
    /// <returns><see langword="true"/> when the paths differ.</returns>
    public static bool operator !=(TemplatePath left, TemplatePath right) => !left.Equals(right);

    private Condition Compare(string op, string literal) => new ComparisonCondition(ForCondition(), op, literal);

    private TemplatePath ForCondition()
    {
        if (IsRoot)
        {
            throw new InvalidOperationException("A condition needs a data path; the root of the data (template.Data) cannot be tested.");
        }

        ConditionText.CheckPath(this, "path");
        return this;
    }

    private static void ValidateSegment(string segment, string relative)
    {
        if (!SegmentPattern().IsMatch(segment))
        {
            throw new ArgumentException(
                $"'{relative}' is not a valid data path: each dot-separated segment must consist of letters, digits and '_' (offending segment: '{segment}').",
                nameof(relative));
        }
    }

    // Templify's placeholder grammar (PlaceholderScanner) accepts \w characters in names.
    [GeneratedRegex(@"^\w+$", RegexOptions.CultureInvariant)]
    private static partial Regex SegmentPattern();
}
