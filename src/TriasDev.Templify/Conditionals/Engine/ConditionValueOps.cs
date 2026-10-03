// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>
/// Dialect-independent value helpers (equality, string coercion) used directly by the
/// <c>in</c> operator and the string operators, and by <see cref="DefaultConditionDialect"/>'s
/// <c>AreEqual</c> implementation. This class does NOT govern <c>=</c>/<c>!=</c> in the Inline
/// dialect, which compares operands via <c>object.Equals</c> (numbers numerically) instead.
/// </summary>
internal static class ConditionValueOps
{
    /// <summary>
    /// Compares two values for equality: bool-aware (case-insensitive booleans), numeric across
    /// numeric types when both operands are numbers (<c>10 = 10.00m</c>, see
    /// <see cref="NumericValue"/>), ordinal string comparison otherwise (so a number compared with
    /// a string compares the number's invariant string form).
    /// </summary>
    public static bool AreEqual(object? left, object? right)
    {
        if (left == null && right == null)
        {
            return true;
        }

        if (left == null || right == null)
        {
            return false;
        }

        if (left is bool || right is bool || IsBooleanLiteral(left) || IsBooleanLiteral(right))
        {
            return string.Equals(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        if (NumericValue.TryFrom(left, out NumericValue ln) && NumericValue.TryFrom(right, out NumericValue rn))
        {
            return ln.NumericEquals(rn);
        }

        return ToStr(left) == ToStr(right);
    }

    /// <summary>
    /// Coerces a value to its ordinal string form (empty string for null). Numeric values are
    /// formatted with <see cref="CultureInfo.InvariantCulture"/> so results do not depend on the
    /// current culture (e.g. <c>1.5m</c> is always <c>"1.5"</c>, never <c>"1,5"</c>).
    /// </summary>
    public static string ToStr(object? value) => value switch
    {
        null => string.Empty,
        double or float or decimal or int or long or short or byte or sbyte or ushort or uint or ulong
            => ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static bool IsBooleanLiteral(object? value)
    {
        string? str = value as string;
        return str != null && (str.Equals("true", StringComparison.OrdinalIgnoreCase)
            || str.Equals("false", StringComparison.OrdinalIgnoreCase));
    }
}
