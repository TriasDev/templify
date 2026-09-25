// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Placeholders;

/// <summary>
/// Evaluates inline expression placeholders such as <c>{{(A and B)}}</c> or <c>{{(Count &gt; 0):yesno}}</c>.
/// Shared by the Word and the text template processors.
/// </summary>
internal static class ExpressionPlaceholderEvaluator
{
    /// <summary>
    /// Evaluates <paramref name="expression"/> (including its surrounding parentheses) as a boolean.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> with the boolean result in <paramref name="value"/> when the expression could be
    /// evaluated; otherwise <see langword="false"/> after adding an <see cref="ProcessingWarningType.ExpressionFailed"/>
    /// warning.
    /// </returns>
    public static bool TryEvaluate(
        string expression,
        IEvaluationContext context,
        IWarningCollector warningCollector,
        out object? value)
    {
        try
        {
            IReadOnlyList<ConditionToken> tokens = new ConditionLexer(allowSingleQuotedStrings: true).Tokenize(expression);
            ConditionNode node = new ConditionParser(ConditionOperatorRegistry.Shared).Parse(tokens);
            value = new ConditionEvaluatorCore(context, InlineConditionDialect.Instance).EvaluateBool(node);
            return true;
        }
        catch (Exception ex) when (ex is ConditionParseException or ArgumentException or InvalidOperationException or InvalidCastException)
        {
            warningCollector.AddWarning(ProcessingWarning.ExpressionFailed(expression, ex.Message));
            value = null;
            return false;
        }
    }
}
