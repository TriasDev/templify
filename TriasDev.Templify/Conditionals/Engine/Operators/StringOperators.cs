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
        string left = ConditionValueOps.ToStr(core.EvaluateValue(operands[0]));
        string right = ConditionValueOps.ToStr(core.EvaluateValue(operands[1]));
        return Test(left, right);
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
