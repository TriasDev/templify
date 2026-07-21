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
            VariableNode var => ResolveOrLiteral(var),
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

    /// <summary>
    /// Resolves a node's value for operators that must not fall back to a variable's path text
    /// when it is missing (e.g. <c>in</c>, string operators, emptiness checks). A
    /// <see cref="VariableNode"/> that fails to resolve yields <c>null</c> (missing = absent)
    /// rather than the literal-fallback behavior used by comparison operators.
    /// </summary>
    public object? ResolveValueStrict(ConditionNode node)
    {
        if (node is VariableNode v)
        {
            return TryResolveVariable(v.Path, out object? value) ? value : null;
        }
        return EvaluateValue(node);
    }

    /// <summary>
    /// Resolves a variable for use as a comparison operand, falling back to its own path
    /// text as a string literal when it cannot be resolved. This preserves the historical
    /// <c>ConditionalEvaluator</c> behavior where unquoted bareword comparison operands
    /// (e.g. the <c>Active</c> in <c>Status = Active</c>) are treated as string literals
    /// when they do not match a known variable. Truthiness checks (see <see cref="EvaluateBool"/>)
    /// intentionally do NOT fall back this way, so a lone missing variable still evaluates to false.
    /// </summary>
    private object? ResolveOrLiteral(VariableNode var)
    {
        return _context.TryResolveVariable(var.Path, out object? value) ? value : var.Path;
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
