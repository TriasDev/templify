// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Globalization;

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

        if (value is int i)
        { return i != 0; }
        if (value is ICollection c)
        { return c.Count > 0; }
        return true;
    }

    public override bool AreEqual(object? left, object? right) => ConditionValueOps.AreEqual(left, right);

    public override bool TryCompare(object? left, object? right, out int cmp)
    {
        cmp = 0;

        // Compare exact numeric types (integers, decimal) as decimal to avoid double rounding.
        if (TryGetExactDecimal(left, out decimal ld) && TryGetExactDecimal(right, out decimal rd))
        {
            cmp = ld.CompareTo(rd);
            return true;
        }

        if (TryGetDouble(left, out double l) && TryGetDouble(right, out double r))
        {
            cmp = l.CompareTo(r);
            return true;
        }
        return false;
    }

    /// <summary>Null (treated as 0), integer types and decimal convert exactly to decimal.</summary>
    private static bool TryGetExactDecimal(object? value, out decimal result)
    {
        switch (value)
        {
            case null:
                result = 0m;
                return true;
            case decimal or int or long or short or byte or sbyte or ushort or uint or ulong:
                result = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return true;
            default:
                result = 0m;
                return false;
        }
    }

    /// <summary>
    /// Converts a value to double without round-tripping numeric CLR values through the current
    /// culture: numeric types are converted directly, strings (and any other value's string form)
    /// are parsed with <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    private static bool TryGetDouble(object? value, out double result)
    {
        switch (value)
        {
            case null:
                result = 0d;
                return true;
            case double or float or decimal or int or long or short or byte or sbyte or ushort or uint or ulong:
                result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return true;
            case string s:
                return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
            default:
                return double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }
    }
}

/// <summary>Semantics for inline <c>{{(...)}}</c> placeholders (bool-only truthiness, IComparable compare).</summary>
internal sealed class InlineConditionDialect : ConditionDialect
{
    public static readonly InlineConditionDialect Instance = new();

    public override bool ToBool(object? value) => value is bool b && b;

    public override bool AreEqual(object? left, object? right) => Equals(left, right);

    public override bool TryCompare(object? left, object? right, out int cmp)
    {
        cmp = 0;
        if (left == null || right == null)
        {
            cmp = left == null ? (right == null ? 0 : -1) : 1;
            return true;
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
