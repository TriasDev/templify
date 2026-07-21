// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine.Operators;

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>Central registry mapping tokens to operators. Register once; the parser reads it.</summary>
internal sealed class ConditionOperatorRegistry
{
    private readonly Dictionary<string, IConditionOperator> _infix = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IConditionOperator> _prefix = new(StringComparer.Ordinal);
    private readonly List<IConditionOperator> _postfix = new();

    public static ConditionOperatorRegistry Shared { get; } = CreateDefault();

    public IReadOnlyList<IConditionOperator> PostfixOperators => _postfix;

    public IConditionOperator? FindInfix(string token) => _infix.GetValueOrDefault(token);

    public IConditionOperator? FindPrefix(string token) => _prefix.GetValueOrDefault(token);

    public void Register(IConditionOperator op)
    {
        switch (op.Fixity)
        {
            case OperatorFixity.Infix:
                foreach (string token in op.Tokens)
                { _infix[token] = op; }
                break;
            case OperatorFixity.Prefix:
                foreach (string token in op.Tokens)
                { _prefix[token] = op; }
                break;
            case OperatorFixity.Postfix:
                _postfix.Add(op);
                break;
        }
    }

    private static ConditionOperatorRegistry CreateDefault()
    {
        ConditionOperatorRegistry r = new();
        r.Register(new OrOperator());
        r.Register(new AndOperator());
        r.Register(new NotOperator());
        r.Register(new EqualOperator());
        r.Register(new NotEqualOperator());
        r.Register(new GreaterOperator());
        r.Register(new LessOperator());
        r.Register(new GreaterOrEqualOperator());
        r.Register(new LessOrEqualOperator());
        r.Register(new InOperator());
        r.Register(new ContainsOperator());
        r.Register(new StartsWithOperator());
        r.Register(new EndsWithOperator());
        r.Register(new ExistsOperator());
        r.Register(new IsEmptyOperator());
        r.Register(new IsNotEmptyOperator());
        return r;
    }
}
