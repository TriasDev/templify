// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>
/// Dialect-independent value helpers shared by operators (equality, string coercion).
/// These preserve the historical <c>ConditionalEvaluator</c> equality semantics so that
/// <c>=</c>, <c>in</c>, and the string operators behave identically regardless of dialect.
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
