// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals;
using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Conditionals.Engine.Operators;
using TriasDev.Templify.Placeholders;

namespace TriasDev.Templify.Core;

/// <summary>
/// Validates inline expression placeholders such as <c>{{(A and B)}}</c> or <c>{{(Price &gt; 100):yesno}}</c> through
/// the parsed expression, with the parser that processing uses (<see cref="ConditionAstCache.InlineExpressions"/>).
/// Shared by the Word and the OpenDocument validators, which supply the loop-scoped resolution.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>
/// <see cref="ValidationResult.AllPlaceholders"/> lists the variables an expression references (as for
/// <c>{{#if}}</c> conditions), not the expression text; an expression that cannot be parsed contributes nothing.
/// </description></item>
/// <item><description>
/// An expression that cannot be parsed is an <see cref="ValidationErrorType.InvalidConditionalExpression"/> error
/// (processing leaves it unreplaced and reports <see cref="ProcessingWarningType.ExpressionFailed"/>).
/// </description></item>
/// <item><description>
/// With data, a <see cref="ValidationErrorType.MissingVariable"/> error is reported for each variable that processing
/// resolves from the data and that is missing (see <see cref="CollectDataVariables"/>). Comparison operands are not
/// reported: processing treats an unresolved bareword there as a string literal (<c>Status = Active</c>).
/// </description></item>
/// </list>
/// </remarks>
internal static class InlineExpressionValidation
{
    /// <summary>
    /// Gets the names a placeholder contributes to <see cref="ValidationResult.AllPlaceholders"/>: the placeholder name
    /// of a variable placeholder, the referenced variables of an expression placeholder.
    /// </summary>
    public static IEnumerable<string> GetPlaceholderNames(string placeholderName)
    {
        if (!IsExpression(placeholderName))
        {
            return new[] { placeholderName };
        }

        return TryParse(placeholderName, out ConditionNode? node, out _)
            ? ConditionalEvaluator.CollectVariables(node!).Select(v => v.Path).Distinct(StringComparer.Ordinal)
            : Array.Empty<string>();
    }

    /// <summary>
    /// Adds an <see cref="ValidationErrorType.InvalidConditionalExpression"/> error for each expression placeholder in
    /// <paramref name="text"/> that cannot be parsed; an error that is already present is not added again.
    /// </summary>
    public static void CheckSyntax(string text, List<ValidationError> errors)
    {
        if (text.IndexOf("{{(", StringComparison.Ordinal) < 0)
        {
            return;
        }

        foreach (PlaceholderToken placeholder in PlaceholderScanner.FindPlaceholders(text))
        {
            if (!placeholder.IsExpression || TryParse(placeholder.VariableName, out _, out string? error))
            {
                continue;
            }

            string message = $"Invalid expression '{placeholder.VariableName}': {error}";
            if (!errors.Any(e => e.Type == ValidationErrorType.InvalidConditionalExpression && e.Message == message))
            {
                errors.Add(ValidationError.Create(ValidationErrorType.InvalidConditionalExpression, message, placeholder.FullMatch));
            }
        }
    }

    /// <summary>
    /// Gets the variables of an expression placeholder that are missing: resolved by processing from the data (see
    /// <see cref="CollectDataVariables"/>), not loop metadata or the current item, and not resolvable in the scope.
    /// </summary>
    /// <param name="expression">The expression, including its parentheses, e.g. <c>(A and B)</c>.</param>
    /// <param name="canResolve">Resolves a variable path in the current (loop) scope.</param>
    /// <returns>The missing variables; empty for an expression that cannot be parsed (a syntax error).</returns>
    public static IEnumerable<string> GetMissingVariables(string expression, Func<string, bool> canResolve)
    {
        if (!TryParse(expression, out ConditionNode? node, out _))
        {
            return Array.Empty<string>();
        }

        return CollectDataVariables(node!)
            .Select(v => v.Path)
            .Distinct(StringComparer.Ordinal)
            .Where(path => !(path.StartsWith('@') || path.StartsWith('.') || path == "this" || canResolve(path)))
            .ToList();
    }

    /// <summary>
    /// Collects the variables that processing resolves from the data without a fallback: operands evaluated for
    /// truthiness (the whole expression and the operands of <c>and</c>, <c>or</c>, <c>not</c>; a missing one is
    /// false) and the variable operands of <c>in</c> and the string operators (a missing one is null).
    /// </summary>
    /// <remarks>
    /// Not collected: comparison operands and list items (a missing bareword is a string literal, e.g. the
    /// <c>Active</c> in <c>Status = Active</c>) and the operands of <c>exists</c> / <c>is empty</c> /
    /// <c>is not empty</c> (designed for missing values). Nested operator nodes are always descended into.
    /// </remarks>
    internal static IEnumerable<VariableNode> CollectDataVariables(ConditionNode node)
    {
        List<VariableNode> result = new List<VariableNode>();
        Collect(node, isRequired: true, result);
        return result;

        static void Collect(ConditionNode node, bool isRequired, List<VariableNode> result)
        {
            switch (node)
            {
                case VariableNode variable when isRequired:
                    result.Add(variable);
                    break;

                case OperatorNode op:
                    bool isLogical = op.Operator is AndOperator or OrOperator or NotOperator;
                    bool isStrict = op.Operator is InOperator or StringOperatorBase;
                    foreach (ConditionNode operand in op.Operands)
                    {
                        Collect(operand, isLogical || (isStrict && operand is VariableNode), result);
                    }

                    break;

                case ListNode list:
                    foreach (ConditionNode item in list.Items)
                    {
                        Collect(item, isRequired: false, result);
                    }

                    break;
            }
        }
    }

    private static bool IsExpression(string placeholderName) => placeholderName.StartsWith('(');

    private static bool TryParse(string expression, out ConditionNode? node, out string? error)
    {
        try
        {
            node = ConditionAstCache.InlineExpressions.GetOrParse(expression);
            error = null;
            return true;
        }
        catch (ConditionParseException ex)
        {
            node = null;
            error = ex.Message;
            return false;
        }
    }
}
