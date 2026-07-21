// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals.Engine.Operators;

internal abstract class StringOperatorBase : IConditionOperator
{
    public abstract IReadOnlyList<string> Tokens { get; }
    public int Precedence => 4;
    public OperatorFixity Fixity => OperatorFixity.Infix;

    public bool Evaluate(ConditionEvaluatorCore core, IReadOnlyList<ConditionNode> operands)
    {
        object? leftValue = core.ResolveValueStrict(operands[0]);
        object? rightValue = core.ResolveValueStrict(operands[1]);
        if (leftValue is null || rightValue is null)
        {
            // Missing operand: short-circuit to false rather than coercing to an empty string.
            return false;
        }

        return Test(ConditionValueOps.ToStr(leftValue), ConditionValueOps.ToStr(rightValue));
    }

    protected abstract bool Test(string left, string right);
}

internal sealed class ContainsOperator : StringOperatorBase
{
    public override IReadOnlyList<string> Tokens { get; } = new[] { "contains" };
    protected override bool Test(string left, string right) => left.Contains(right, StringComparison.Ordinal);
}

internal sealed class StartsWithOperator : StringOperatorBase
{
    public override IReadOnlyList<string> Tokens { get; } = new[] { "startswith" };
    protected override bool Test(string left, string right) => left.StartsWith(right, StringComparison.Ordinal);
}

internal sealed class EndsWithOperator : StringOperatorBase
{
    public override IReadOnlyList<string> Tokens { get; } = new[] { "endswith" };
    protected override bool Test(string left, string right) => left.EndsWith(right, StringComparison.Ordinal);
}
