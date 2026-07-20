// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections;

namespace TriasDev.Templify.Conditionals.Engine.Operators;

/// <summary>Collection membership: <c>scalar in source</c>. Source may be a collection variable,
/// a list literal <c>("A","B")</c>, or a comma-separated string <c>"A,B"</c>.</summary>
internal sealed class InOperator : IConditionOperator
{
    public IReadOnlyList<string> Tokens { get; } = new[] { "in" };
    public int Precedence => 4;
    public OperatorFixity Fixity => OperatorFixity.Infix;

    public bool Evaluate(ConditionEvaluatorCore core, IReadOnlyList<ConditionNode> operands)
    {
        object? left = core.EvaluateValue(operands[0]);
        object? right = core.EvaluateValue(operands[1]);

        foreach (object? candidate in EnumerateCandidates(right))
        {
            if (ConditionValueOps.AreEqual(left, candidate))
            {
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<object?> EnumerateCandidates(object? right)
    {
        if (right is string s)
        {
            foreach (string part in s.Split(','))
            {
                yield return part.Trim();
            }
            yield break;
        }

        if (right is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                yield return item;
            }
            yield break;
        }

        yield return right;
    }
}
