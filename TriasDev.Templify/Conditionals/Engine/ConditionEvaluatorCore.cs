// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Core;

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>Walks a <see cref="ConditionNode"/> AST, resolving variables and applying operators.</summary>
internal sealed class ConditionEvaluatorCore
{
    private readonly IEvaluationContext _context;

    public ConditionEvaluatorCore(IEvaluationContext context, ConditionDialect dialect)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
    }

    public ConditionDialect Dialect { get; }

    public bool TryResolveVariable(string path, out object? value) => _context.TryResolveVariable(path, out value);

    /// <summary>Evaluates a node to its boolean truth value.</summary>
    public bool EvaluateBool(ConditionNode node)
    {
        return node switch
        {
            OperatorNode op => op.Operator.Evaluate(this, op.Operands),
            LiteralNode lit => Dialect.ToBool(lit.Value),
            VariableNode var => Dialect.ToBool(Resolve(var)),
            _ => false
        };
    }

    /// <summary>Evaluates a node to its underlying value (for operand comparison).</summary>
    public object? EvaluateValue(ConditionNode node)
    {
        return node switch
        {
            LiteralNode lit => lit.Value,
            VariableNode var => Resolve(var),
            ListNode list => MaterializeList(list),
            OperatorNode op => op.Operator.Evaluate(this, op.Operands),
            _ => null
        };
    }

    private object? Resolve(VariableNode var)
    {
        _context.TryResolveVariable(var.Path, out object? value);
        return value;
    }

    private List<object?> MaterializeList(ListNode list)
    {
        List<object?> items = new(list.Items.Count);
        foreach (ConditionNode item in list.Items)
        {
            items.Add(EvaluateValue(item));
        }
        return items;
    }
}
