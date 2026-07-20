// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>Base type for parsed condition-expression AST nodes.</summary>
internal abstract class ConditionNode
{
}

/// <summary>A literal value (string, number, bool, or null).</summary>
internal sealed class LiteralNode : ConditionNode
{
    public LiteralNode(object? value) => Value = value;

    public object? Value { get; }
}

/// <summary>A variable reference resolved through the evaluation context (supports dotted/indexed paths).</summary>
internal sealed class VariableNode : ConditionNode
{
    public VariableNode(string path) => Path = path ?? throw new ArgumentNullException(nameof(path));

    public string Path { get; }
}

/// <summary>A list literal, e.g. <c>("A", "B")</c>. Valid as the right-hand side of <c>in</c>.</summary>
internal sealed class ListNode : ConditionNode
{
    public ListNode(IReadOnlyList<ConditionNode> items) => Items = items ?? throw new ArgumentNullException(nameof(items));

    public IReadOnlyList<ConditionNode> Items { get; }
}

/// <summary>An operator application with its operand nodes.</summary>
internal sealed class OperatorNode : ConditionNode
{
    public OperatorNode(IConditionOperator @operator, IReadOnlyList<ConditionNode> operands)
    {
        Operator = @operator ?? throw new ArgumentNullException(nameof(@operator));
        Operands = operands ?? throw new ArgumentNullException(nameof(operands));
    }

    public IConditionOperator Operator { get; }

    public IReadOnlyList<ConditionNode> Operands { get; }
}
