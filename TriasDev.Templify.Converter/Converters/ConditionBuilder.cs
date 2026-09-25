// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Conditionals;

namespace TriasDev.Templify.Converter.Converters;

/// <summary>
/// The result of translating an OpenXMLTemplates <c>conditionalRemove_*</c> tag into a Templify condition.
/// </summary>
public sealed class ConditionBuildResult
{
    /// <summary>The Templify condition expression, or null if the tag could not be translated.</summary>
    public string? Expression { get; internal set; }

    /// <summary>The first operand (the variable the condition is primarily about).</summary>
    public string VariablePath { get; internal set; } = string.Empty;

    /// <summary>The OpenXMLTemplates operators found in the tag, in order.</summary>
    public List<string> Operators { get; } = new();

    /// <summary>The comparison values found in the tag, in order.</summary>
    public List<string> ComparisonValues { get; } = new();

    /// <summary>Reasons why the tag cannot be converted automatically.</summary>
    public List<string> Errors { get; } = new();

    /// <summary>Notes about valid conversions that should be reviewed by a human.</summary>
    public List<string> ReviewNotes { get; } = new();

    /// <summary>True if a valid expression was produced.</summary>
    public bool IsValid => Expression != null && Errors.Count == 0;
}

/// <summary>
/// Translates the argument part of an OpenXMLTemplates <c>conditionalRemove_*</c> tag into a Templify
/// condition expression. Used by both the analyzer and the converter.
/// </summary>
/// <remarks>
/// <para>
/// OpenXMLTemplates evaluates the arguments strictly from left to right, without operator precedence:
/// <c>a_or_b_and_c</c> means <c>(a or b) and c</c>, and a trailing <c>not</c> negates everything before it.
/// The builder emits parentheses where Templify's precedence (<c>not</c> &gt; <c>and</c> &gt; <c>or</c>)
/// would otherwise change the meaning.
/// </para>
/// <para>
/// The tag is split on <c>_</c>, but only the known operator tokens act as separators: consecutive
/// non-operator tokens are joined back together, so <c>is_active</c> stays a single variable name.
/// Every generated expression is checked with the core <see cref="ConditionEvaluator.Validate(string)"/>.
/// </para>
/// </remarks>
public static class ConditionBuilder
{
    private static readonly Dictionary<string, string> _comparisonOperators = new(StringComparer.Ordinal)
    {
        ["eq"] = "=",
        ["ne"] = "!=",
        ["gt"] = ">",
        ["lt"] = "<",
        ["gte"] = ">=",
        ["lte"] = "<=",
    };

    private static readonly ConditionEvaluator _evaluator = new ConditionEvaluator();

    private enum ExpressionKind
    {
        Operand,
        Comparison,
        Not,
        And,
        Or,
    }

    /// <summary>
    /// Returns true if <paramref name="token"/> is an OpenXMLTemplates condition operator.
    /// </summary>
    public static bool IsOperator(string token)
    {
        return token is "and" or "or" or "not" || _comparisonOperators.ContainsKey(token);
    }

    /// <summary>
    /// Build a Templify condition from the part of the tag after <c>conditionalRemove_</c>.
    /// </summary>
    public static ConditionBuildResult Build(string tagArguments)
    {
        ArgumentNullException.ThrowIfNull(tagArguments);

        ConditionBuildResult result = new ConditionBuildResult();
        List<string> tokens = Tokenize(tagArguments);

        if (tokens.Count == 0 || IsOperator(tokens[0]))
        {
            result.Errors.Add($"Condition '{tagArguments}' does not start with a variable name");
            return result;
        }

        result.VariablePath = tokens[0];
        string expression = tokens[0];
        ExpressionKind kind = ExpressionKind.Operand;

        int i = 1;
        while (i < tokens.Count)
        {
            string token = tokens[i];

            if (!IsOperator(token))
            {
                result.Errors.Add($"Unexpected '{token}' in condition '{tagArguments}' (expected an operator)");
                return result;
            }

            result.Operators.Add(token);

            if (token == "not")
            {
                (expression, kind) = Negate(expression, kind);
                i++;
                continue;
            }

            if (i + 1 >= tokens.Count || IsOperator(tokens[i + 1]))
            {
                result.Errors.Add($"Operator '{token}' in condition '{tagArguments}' has no operand");
                return result;
            }

            string operand = tokens[i + 1];
            i += 2;

            if (_comparisonOperators.TryGetValue(token, out string? symbol))
            {
                // OpenXMLTemplates always compares the FIRST variable's value, even after other
                // operators, and discards everything evaluated so far. That cannot be expressed
                // faithfully, so only a comparison directly after the first variable is converted.
                if (kind != ExpressionKind.Operand)
                {
                    result.Errors.Add(
                        $"Comparison '{token}' in condition '{tagArguments}' does not directly follow the first variable; " +
                        "OpenXMLTemplates would compare the first variable and discard the preceding logic");
                    return result;
                }

                result.ComparisonValues.Add(operand);
                expression = $"{expression} {symbol} {FormatValue(operand)}";
                kind = ExpressionKind.Comparison;
                continue;
            }

            if (token == "and")
            {
                // Left-to-right evaluation: (a or b) and c
                string left = kind == ExpressionKind.Or ? $"({expression})" : expression;
                expression = $"{left} and {operand}";
                kind = ExpressionKind.And;
            }
            else
            {
                expression = $"{expression} or {operand}";
                kind = ExpressionKind.Or;
            }
        }

        ConditionValidationResult validation = _evaluator.Validate(expression);
        if (!validation.IsValid)
        {
            string issues = string.Join("; ", validation.Issues.Select(issue => issue.Message));
            result.Errors.Add($"Generated condition '{expression}' is not valid Templify syntax: {issues}");
            return result;
        }

        if (result.Operators.Count > 1)
        {
            result.ReviewNotes.Add(
                $"Condition '{tagArguments}' combines several operators; OpenXMLTemplates evaluates them left to right, converted to '{expression}'");
        }

        foreach (string name in new[] { result.VariablePath }.Concat(OperandNames(tokens)))
        {
            if (name.Contains('_') && !IsNumeric(name))
            {
                result.ReviewNotes.Add(
                    $"Name '{name}' contains '_'; it was kept intact, but OpenXMLTemplates splits tags on '_' and would have read '{name.Substring(0, name.IndexOf('_'))}'");
            }
        }

        result.Expression = expression;
        return result;
    }

    /// <summary>
    /// Split the tag arguments on '_' and re-join consecutive non-operator tokens, so only the known
    /// operator tokens act as separators. Empty tokens (from "__") are ignored.
    /// </summary>
    internal static List<string> Tokenize(string tagArguments)
    {
        List<string> tokens = new();
        List<string> pending = new();

        foreach (string part in tagArguments.Split('_'))
        {
            if (part.Length == 0)
            {
                continue;
            }

            if (IsOperator(part))
            {
                if (pending.Count > 0)
                {
                    tokens.Add(string.Join("_", pending));
                    pending.Clear();
                }

                tokens.Add(part);
            }
            else
            {
                pending.Add(part);
            }
        }

        if (pending.Count > 0)
        {
            tokens.Add(string.Join("_", pending));
        }

        return tokens;
    }

    private static IEnumerable<string> OperandNames(List<string> tokens)
    {
        for (int i = 1; i < tokens.Count; i++)
        {
            if (tokens[i] is "and" or "or" && i + 1 < tokens.Count)
            {
                yield return tokens[i + 1];
            }
        }
    }

    private static (string Expression, ExpressionKind Kind) Negate(string expression, ExpressionKind kind)
    {
        return kind switch
        {
            // not binds weaker than comparisons, so "not a = 1" already means "not (a = 1)"
            ExpressionKind.Operand or ExpressionKind.Comparison => ($"not {expression}", ExpressionKind.Not),
            _ => ($"not ({expression})", ExpressionKind.Not),
        };
    }

    private static string FormatValue(string value)
    {
        return IsNumeric(value) ? value : $"\"{value}\"";
    }

    /// <summary>
    /// Culture-invariant numeric check (e.g. "1.5" is numeric in every culture, "1,5" never is).
    /// </summary>
    internal static bool IsNumeric(string value)
    {
        return decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _);
    }
}
