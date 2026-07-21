// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>Thrown when a condition expression cannot be parsed.</summary>
internal sealed class ConditionParseException : Exception
{
    public ConditionParseException(string message) : base(message) { }
}

/// <summary>Precedence-climbing (Pratt) parser turning tokens into a <see cref="ConditionNode"/> AST.</summary>
internal sealed class ConditionParser
{
    private readonly ConditionOperatorRegistry _registry;
    private IReadOnlyList<ConditionToken> _tokens = Array.Empty<ConditionToken>();
    private int _pos;

    public ConditionParser(ConditionOperatorRegistry registry)
        => _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public ConditionNode Parse(IReadOnlyList<ConditionToken> tokens)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _pos = 0;
        ConditionNode node = ParseExpression(0);
        if (Current.Type != ConditionTokenType.End)
        {
            throw new ConditionParseException($"Unexpected token '{Current.Text}'.");
        }
        return node;
    }

    private ConditionToken Current => _tokens[_pos];

    private ConditionToken Advance() => _tokens[_pos++];

    private ConditionNode ParseExpression(int minPrecedence)
    {
        ConditionNode left = ParsePrefix();

        while (true)
        {
            ConditionNode? postfixed = TryApplyPostfix(left);
            if (postfixed != null)
            { left = postfixed; continue; }

            if (Current.Type != ConditionTokenType.Operator)
            { break; }
            IConditionOperator? infix = _registry.FindInfix(Current.Text);
            if (infix == null || infix.Precedence < minPrecedence)
            { break; }

            Advance();
            ConditionNode right = ParseExpression(infix.Precedence + 1);
            left = new OperatorNode(infix, new[] { left, right });
        }

        return left;
    }

    private ConditionNode ParsePrefix()
    {
        if (Current.Type == ConditionTokenType.Operator)
        {
            IConditionOperator? prefix = _registry.FindPrefix(Current.Text);
            if (prefix != null)
            {
                Advance();
                ConditionNode operand = ParseExpression(prefix.Precedence);
                return new OperatorNode(prefix, new[] { operand });
            }
        }

        return ParsePrimary();
    }

    private ConditionNode ParsePrimary()
    {
        ConditionToken token = Current;
        switch (token.Type)
        {
            case ConditionTokenType.LParen:
                return ParseParenthesized();
            case ConditionTokenType.String:
                Advance();
                return new LiteralNode(token.Text);
            case ConditionTokenType.Number:
            case ConditionTokenType.Boolean:
            case ConditionTokenType.Null:
                Advance();
                return new LiteralNode(token.LiteralValue);
            case ConditionTokenType.Identifier:
                Advance();
                return new VariableNode(token.Text);
            default:
                throw new ConditionParseException($"Expected an operand but found '{token.Text}'.");
        }
    }

    private ConditionNode ParseParenthesized()
    {
        Advance(); // consume '('
        ConditionNode first = ParseExpression(0);

        if (Current.Type == ConditionTokenType.Comma)
        {
            List<ConditionNode> items = new() { first };
            while (Current.Type == ConditionTokenType.Comma)
            {
                Advance();
                items.Add(ParseExpression(0));
            }
            Expect(ConditionTokenType.RParen);
            return new ListNode(items);
        }

        Expect(ConditionTokenType.RParen);
        return first;
    }

    private ConditionNode? TryApplyPostfix(ConditionNode left)
    {
        foreach (IConditionOperator op in OrderedByTokenCountDescending())
        {
            if (MatchesSequence(op.Tokens))
            {
                _pos += op.Tokens.Count;
                return new OperatorNode(op, new[] { left });
            }
        }
        return null;
    }

    private IEnumerable<IConditionOperator> OrderedByTokenCountDescending()
    {
        // Longest sequence first so "is not empty" wins over any "is ..." prefix.
        List<IConditionOperator> ordered = new(_registry.PostfixOperators);
        ordered.Sort((a, b) => b.Tokens.Count.CompareTo(a.Tokens.Count));
        return ordered;
    }

    private bool MatchesSequence(IReadOnlyList<string> sequence)
    {
        for (int i = 0; i < sequence.Count; i++)
        {
            ConditionToken t = _tokens[Math.Min(_pos + i, _tokens.Count - 1)];
            if (t.Type != ConditionTokenType.Operator || !string.Equals(t.Text, sequence[i], StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private void Expect(ConditionTokenType type)
    {
        if (Current.Type != type)
        {
            throw new ConditionParseException($"Expected {type} but found '{Current.Text}'.");
        }
        Advance();
    }
}
