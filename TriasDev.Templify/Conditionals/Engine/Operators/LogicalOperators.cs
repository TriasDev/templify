// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals.Engine.Operators;

internal sealed class OrOperator : IConditionOperator
{
    public IReadOnlyList<string> Tokens { get; } = new[] { "or" };
    public int Precedence => OperatorPrecedence.Or;
    public OperatorFixity Fixity => OperatorFixity.Infix;
    public bool Evaluate(ConditionEvaluatorCore core, IReadOnlyList<ConditionNode> operands)
        => core.EvaluateBool(operands[0]) || core.EvaluateBool(operands[1]);
}

internal sealed class AndOperator : IConditionOperator
{
    public IReadOnlyList<string> Tokens { get; } = new[] { "and" };
    public int Precedence => OperatorPrecedence.And;
    public OperatorFixity Fixity => OperatorFixity.Infix;
    public bool Evaluate(ConditionEvaluatorCore core, IReadOnlyList<ConditionNode> operands)
        => core.EvaluateBool(operands[0]) && core.EvaluateBool(operands[1]);
}

internal sealed class NotOperator : IConditionOperator
{
    public IReadOnlyList<string> Tokens { get; } = new[] { "not" };
    public int Precedence => OperatorPrecedence.Not;
    public OperatorFixity Fixity => OperatorFixity.Prefix;
    public bool Evaluate(ConditionEvaluatorCore core, IReadOnlyList<ConditionNode> operands)
        => !core.EvaluateBool(operands[0]);
}
