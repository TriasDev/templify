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
        => IsEmptyValue(ResolveOperandValue(core, operands[0]));

    /// <summary>
    /// Resolves the operand's raw value for emptiness checks. Unlike
    /// <see cref="ConditionEvaluatorCore.EvaluateValue"/>, a <see cref="VariableNode"/> that fails to
    /// resolve yields <c>null</c> (missing = empty) rather than falling back to its path text as a
    /// string literal.
    /// </summary>
    internal static object? ResolveOperandValue(ConditionEvaluatorCore core, ConditionNode operand)
    {
        if (operand is VariableNode variable)
        {
            return core.TryResolveVariable(variable.Path, out object? value) ? value : null;
        }
        return core.EvaluateValue(operand);
    }

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
        => !IsEmptyOperator.IsEmptyValue(IsEmptyOperator.ResolveOperandValue(core, operands[0]));
}
