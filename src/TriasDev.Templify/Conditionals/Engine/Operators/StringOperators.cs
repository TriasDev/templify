// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;

namespace TriasDev.Templify.Conditionals.Engine.Operators;

/// <summary>
/// Base for <c>contains</c>/<c>startswith</c>/<c>endswith</c>. On strings these are ordinal,
/// case-sensitive substring/prefix/suffix tests. When the left operand is a collection
/// (any non-string <see cref="IEnumerable"/>), <c>contains</c> tests membership using the same
/// element equality as <c>in</c>, and <c>startswith</c>/<c>endswith</c> evaluate to false; a
/// collection is never compared through its <see cref="object.ToString"/> (type name).
/// </summary>

internal abstract class StringOperatorBase : IConditionOperator
{
    public abstract IReadOnlyList<string> Tokens { get; }
    public int Precedence => OperatorPrecedence.Comparison;
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

        if (leftValue is IEnumerable collection and not string)
        {
            return TestCollection(collection, rightValue);
        }

        if (rightValue is IEnumerable and not string)
        {
            // A collection on the right has no meaningful string form; never match its type name.
            return false;
        }

        return Test(ConditionValueOps.ToStr(leftValue), ConditionValueOps.ToStr(rightValue));
    }

    protected abstract bool Test(string left, string right);

    /// <summary>Evaluates the operator with a collection on the left. Defaults to false.</summary>
    protected virtual bool TestCollection(IEnumerable collection, object right) => false;
}

internal sealed class ContainsOperator : StringOperatorBase
{
    public override IReadOnlyList<string> Tokens { get; } = new[] { "contains" };
    protected override bool Test(string left, string right) => left.Contains(right, StringComparison.Ordinal);

    /// <summary>Membership: true when any element equals <paramref name="right"/> (same equality as <c>in</c>).</summary>
    protected override bool TestCollection(IEnumerable collection, object right)
    {
        foreach (object? item in collection)
        {
            if (ConditionValueOps.AreEqual(item, right))
            {
                return true;
            }
        }
        return false;
    }
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
