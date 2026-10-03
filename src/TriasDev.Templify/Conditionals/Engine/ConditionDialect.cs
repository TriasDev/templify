// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Globalization;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>Externally-observable semantic policy for an entry point (truthiness + base comparison).</summary>
internal abstract class ConditionDialect
{
    /// <summary>Coerces a value to a boolean (truthiness).</summary>
    public abstract bool ToBool(object? value);

    /// <summary>Equality used by the <c>=</c>/<c>!=</c> operators.</summary>
    public abstract bool AreEqual(object? left, object? right);

    /// <summary>Ordered comparison used by <c>&gt;</c>/<c>&lt;</c>/<c>&gt;=</c>/<c>&lt;=</c>.
    /// Returns false when the operands are not comparable in this dialect.</summary>
    public abstract bool TryCompare(object? left, object? right, out int cmp);
}

/// <summary>Semantics for <c>{{#if}}</c>, text templates, and the standalone API (rich truthiness, numeric compare).</summary>
internal sealed class DefaultConditionDialect : ConditionDialect
{
    public static readonly DefaultConditionDialect Instance = new();

    public override bool ToBool(object? value)
    {
        if (value == null)
        { return false; }
        if (value is bool b)
        { return b; }

        if (value is string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            { return false; }
            string lower = s.ToLowerInvariant();
            if (lower == "false" || lower == "0")
            { return false; }
            if (lower == "true" || lower == "1")
            { return true; }
            return true;
        }

        // Any numeric zero (0, 0L, 0m, 0.0, -0.0, JSON 0.0, ...) is falsy; NaN is falsy too (like JavaScript).
        if (NumericValue.TryFrom(value, out NumericValue number))
        { return !number.IsZero && !number.IsNaN; }
        if (value is ICollection c)
        { return c.Count > 0; }
        if (value is IEnumerable e)
        { return HasAny(e); }
        return true;
    }

    private static bool HasAny(IEnumerable enumerable)
    {
        IEnumerator enumerator = enumerable.GetEnumerator();
        try
        {
            return enumerator.MoveNext();
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    public override bool AreEqual(object? left, object? right) => ConditionValueOps.AreEqual(left, right);

    public override bool TryCompare(object? left, object? right, out int cmp)
    {
        cmp = 0;
        return TryGetNumber(left, out NumericValue l)
            && TryGetNumber(right, out NumericValue r)
            && l.TryCompareTo(r, out cmp);
    }

    /// <summary>
    /// Converts an operand to a number without round-tripping numeric CLR values through the
    /// current culture: null is treated as 0, numeric types (and JSON numbers) are normalized
    /// directly, strings (and any other value's string form) are parsed with
    /// <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    private static bool TryGetNumber(object? value, out NumericValue result)
    {
        if (value is null)
        {
            return NumericValue.TryFrom(0, out result);
        }

        if (NumericValue.TryFrom(value, out result))
        {
            return true;
        }

        return NumericValue.TryParse(value as string ?? value.ToString(), out result);
    }
}

/// <summary>Semantics for inline <c>{{(...)}}</c> placeholders (bool-only truthiness, numeric compare across
/// numeric types, IComparable compare otherwise).</summary>
internal sealed class InlineConditionDialect : ConditionDialect
{
    public static readonly InlineConditionDialect Instance = new();

    public override bool ToBool(object? value) => value is bool b && b;

    public override bool AreEqual(object? left, object? right)
    {
        // Numbers compare numerically across CLR types (5L = 5, 10.50m = 10.5); everything else keeps object.Equals.
        if (NumericValue.TryFrom(left, out NumericValue ln) && NumericValue.TryFrom(right, out NumericValue rn))
        {
            return ln.NumericEquals(rn);
        }

        return Equals(left, right);
    }

    public override bool TryCompare(object? left, object? right, out int cmp)
    {
        cmp = 0;
        if (left == null || right == null)
        {
            cmp = left == null ? (right == null ? 0 : -1) : 1;
            return true;
        }

        if (NumericValue.TryFrom(left, out NumericValue ln) && NumericValue.TryFrom(right, out NumericValue rn))
        {
            return ln.TryCompareTo(rn, out cmp);
        }

        if (left is IComparable lc && right is IComparable)
        {
            try
            { cmp = lc.CompareTo(right); return true; }
            catch (ArgumentException) { return false; }
            catch (InvalidCastException) { return false; }
        }
        return false;
    }
}
