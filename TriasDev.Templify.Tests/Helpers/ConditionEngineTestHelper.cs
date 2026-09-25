// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests.Helpers;

/// <summary>
/// Runs an expression through the condition engine (lexer → parser → evaluator) directly,
/// without the public <c>ConditionEvaluator</c> facade or its AST cache.
/// Import with <c>using static</c>.
/// </summary>
internal static class ConditionEngineTestHelper
{
    /// <summary>
    /// Evaluates <paramref name="expression"/> with the block dialect (<c>{{#if ...}}</c> semantics).
    /// </summary>
    public static bool Eval(string expression, Dictionary<string, object> data)
        => Evaluate(expression, data, DefaultConditionDialect.Instance, allowSingleQuotedStrings: false);

    /// <summary>
    /// Evaluates <paramref name="expression"/> with the inline dialect (<c>{{(...) ? a : b}}</c> semantics).
    /// </summary>
    public static bool EvalInline(string expression, Dictionary<string, object> data, bool allowSingleQuotedStrings = false)
        => Evaluate(expression, data, InlineConditionDialect.Instance, allowSingleQuotedStrings);

    private static bool Evaluate(
        string expression,
        Dictionary<string, object> data,
        ConditionDialect dialect,
        bool allowSingleQuotedStrings)
    {
        IReadOnlyList<ConditionToken> tokens = new ConditionLexer(allowSingleQuotedStrings).Tokenize(expression);
        ConditionNode node = new ConditionParser(ConditionOperatorRegistry.Shared).Parse(tokens);
        return new ConditionEvaluatorCore(new GlobalEvaluationContext(data), dialect).EvaluateBool(node);
    }
}
