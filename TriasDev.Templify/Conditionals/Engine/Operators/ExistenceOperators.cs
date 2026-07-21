// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;

namespace TriasDev.Templify.Conditionals.Engine.Operators;

/// <summary>Postfix <c>Foo exists</c>: variable is present in the context, regardless of value.</summary>
internal sealed class ExistsOperator : IConditionOperator
{
    public IReadOnlyList<string> Tokens { get; } = new[] { "exists" };
    public int Precedence => 5;
    public OperatorFixity Fixity => OperatorFixity.Postfix;

    public bool Evaluate(ConditionEvaluatorCore core, IReadOnlyList<ConditionNode> operands)
    {
        if (operands[0] is VariableNode variable)
        {
            return core.TryResolveVariable(variable.Path, out _);
        }
        return core.EvaluateValue(operands[0]) != null;
    }
}

/// <summary>Postfix <c>Foo is empty</c>: null / empty-or-whitespace string / empty collection (missing = empty).</summary>
internal sealed class IsEmptyOperator : IConditionOperator
{
    public IReadOnlyList<string> Tokens { get; } = new[] { "is", "empty" };
    public int Precedence => 5;
    public OperatorFixity Fixity => OperatorFixity.Postfix;

    public bool Evaluate(ConditionEvaluatorCore core, IReadOnlyList<ConditionNode> operands)
        => IsEmptyValue(core.ResolveValueStrict(operands[0]));

    internal static bool IsEmptyValue(object? value)
    {
        if (value == null)
        {
            return true;
        }
        if (value is string s)
        {
            return string.IsNullOrWhiteSpace(s);
        }
        if (value is ICollection c)
        {
            return c.Count == 0;
        }
        return false;
    }
}

/// <summary>Postfix <c>Foo is not empty</c>: negation of <c>is empty</c>.</summary>
internal sealed class IsNotEmptyOperator : IConditionOperator
{
    public IReadOnlyList<string> Tokens { get; } = new[] { "is", "not", "empty" };
    public int Precedence => 5;
    public OperatorFixity Fixity => OperatorFixity.Postfix;

    public bool Evaluate(ConditionEvaluatorCore core, IReadOnlyList<ConditionNode> operands)
        => !IsEmptyOperator.IsEmptyValue(core.ResolveValueStrict(operands[0]));
}
