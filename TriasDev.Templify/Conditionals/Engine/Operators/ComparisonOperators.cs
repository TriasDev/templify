// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals.Engine.Operators;

internal abstract class ComparisonOperatorBase : IConditionOperator
{
    public abstract IReadOnlyList<string> Tokens { get; }
    public int Precedence => 4;
    public OperatorFixity Fixity => OperatorFixity.Infix;
    public bool Evaluate(ConditionEvaluatorCore core, IReadOnlyList<ConditionNode> operands)
        => Compare(core, core.EvaluateValue(operands[0]), core.EvaluateValue(operands[1]));
    protected abstract bool Compare(ConditionEvaluatorCore core, object? left, object? right);
}

internal sealed class EqualOperator : ComparisonOperatorBase
{
    public override IReadOnlyList<string> Tokens { get; } = new[] { "=", "==" };
    protected override bool Compare(ConditionEvaluatorCore core, object? l, object? r) => core.Dialect.AreEqual(l, r);
}

internal sealed class NotEqualOperator : ComparisonOperatorBase
{
    public override IReadOnlyList<string> Tokens { get; } = new[] { "!=" };
    protected override bool Compare(ConditionEvaluatorCore core, object? l, object? r) => !core.Dialect.AreEqual(l, r);
}

internal sealed class GreaterOperator : ComparisonOperatorBase
{
    public override IReadOnlyList<string> Tokens { get; } = new[] { ">" };
    protected override bool Compare(ConditionEvaluatorCore core, object? l, object? r)
        => core.Dialect.TryCompare(l, r, out int c) && c > 0;
}

internal sealed class LessOperator : ComparisonOperatorBase
{
    public override IReadOnlyList<string> Tokens { get; } = new[] { "<" };
    protected override bool Compare(ConditionEvaluatorCore core, object? l, object? r)
        => core.Dialect.TryCompare(l, r, out int c) && c < 0;
}

internal sealed class GreaterOrEqualOperator : ComparisonOperatorBase
{
    public override IReadOnlyList<string> Tokens { get; } = new[] { ">=" };
    protected override bool Compare(ConditionEvaluatorCore core, object? l, object? r)
        => core.Dialect.TryCompare(l, r, out int c) && c >= 0;
}

internal sealed class LessOrEqualOperator : ComparisonOperatorBase
{
    public override IReadOnlyList<string> Tokens { get; } = new[] { "<=" };
    protected override bool Compare(ConditionEvaluatorCore core, object? l, object? r)
        => core.Dialect.TryCompare(l, r, out int c) && c <= 0;
}
