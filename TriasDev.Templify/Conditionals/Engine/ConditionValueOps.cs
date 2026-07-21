// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>
/// Dialect-independent value helpers (equality, string coercion) used directly by the
/// <c>in</c> operator and the string operators, and by <see cref="DefaultConditionDialect"/>'s
/// <c>AreEqual</c> implementation. This class does NOT govern <c>=</c>/<c>!=</c> in the Inline
/// dialect, which compares operands via <c>object.Equals</c> instead.
/// </summary>
internal static class ConditionValueOps
{
    /// <summary>Compares two values for equality (bool-aware, case-insensitive booleans, ordinal otherwise).</summary>
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

        return left.ToString() == right.ToString();
    }

    /// <summary>Coerces a value to its ordinal string form (empty string for null).</summary>
    public static string ToStr(object? value) => value?.ToString() ?? string.Empty;

    private static bool IsBooleanLiteral(object? value)
    {
        string? str = value as string;
        return str != null && (str.Equals("true", StringComparison.OrdinalIgnoreCase)
            || str.Equals("false", StringComparison.OrdinalIgnoreCase));
    }
}
