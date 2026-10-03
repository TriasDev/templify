// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using TriasDev.Templify.Conditionals.Engine.Operators;

namespace TriasDev.Templify.Conditionals.Engine;

/// <summary>
/// Central registry mapping tokens to operators: the single source of truth for the lexer (word
/// operators), the parser and the validator's structural analysis. Adding an operator means
/// implementing <see cref="IConditionOperator"/> and adding it to <see cref="CreateDefault"/>.
/// </summary>
/// <remarks>
/// A registry is immutable once created, so the <see cref="Shared"/> instance is safe to use
/// from multiple threads.
/// </remarks>
internal sealed class ConditionOperatorRegistry
{
    private readonly FrozenDictionary<string, IConditionOperator> _infix;
    private readonly FrozenDictionary<string, IConditionOperator> _prefix;
    private readonly FrozenSet<string> _postfixTokens;

    private ConditionOperatorRegistry(IEnumerable<IConditionOperator> operators)
    {
        Dictionary<string, IConditionOperator> infix = new(StringComparer.Ordinal);
        Dictionary<string, IConditionOperator> prefix = new(StringComparer.Ordinal);
        List<IConditionOperator> postfix = new();

        foreach (IConditionOperator op in operators)
        {
            switch (op.Fixity)
            {
                case OperatorFixity.Infix:
                    foreach (string token in op.Tokens)
                    { infix[token] = op; }
                    break;
                case OperatorFixity.Prefix:
                    foreach (string token in op.Tokens)
                    { prefix[token] = op; }
                    break;
                case OperatorFixity.Postfix:
                    postfix.Add(op);
                    break;
            }
        }

        _infix = infix.ToFrozenDictionary(StringComparer.Ordinal);
        _prefix = prefix.ToFrozenDictionary(StringComparer.Ordinal);

        // Longest token sequence first so that "is not empty" wins over any "is ..." prefix.
        // OrderByDescending is stable: operators with equally long sequences keep registration order.
        PostfixOperators = postfix.OrderByDescending(op => op.Tokens.Count).ToArray();

        _postfixTokens = postfix.SelectMany(op => op.Tokens).ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        // Every operator token made of letters is a keyword for the lexer (e.g. "and", "in", "is", "empty").
        WordOperators = infix.Keys.Concat(prefix.Keys).Concat(_postfixTokens)
            .Where(token => token.All(char.IsLetter))
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Gets the registry with all built-in operators.</summary>
    public static ConditionOperatorRegistry Shared { get; } = CreateDefault();

    /// <summary>Gets the postfix operators, longest token sequence first (the order the parser tries them in).</summary>
    public IReadOnlyList<IConditionOperator> PostfixOperators { get; }

    /// <summary>Gets the operator keywords made of letters (case-insensitive), e.g. <c>and</c>, <c>in</c>, <c>empty</c>.</summary>
    public FrozenSet<string> WordOperators { get; }

    /// <summary>Creates a registry with the given operators.</summary>
    public static ConditionOperatorRegistry Create(params IConditionOperator[] operators) => new(operators);

    public IConditionOperator? FindInfix(string token) => _infix.GetValueOrDefault(token);

    public IConditionOperator? FindPrefix(string token) => _prefix.GetValueOrDefault(token);

    /// <summary>
    /// Checks whether <paramref name="token"/> (case-insensitive) is part of a postfix operator's token
    /// sequence (e.g. <c>exists</c>, <c>is</c>, <c>empty</c>).
    /// </summary>
    public bool IsPostfixToken(string token) => _postfixTokens.Contains(token);

    private static ConditionOperatorRegistry CreateDefault() => Create(
        new OrOperator(),
        new AndOperator(),
        new NotOperator(),
        new EqualOperator(),
        new NotEqualOperator(),
        new GreaterOperator(),
        new LessOperator(),
        new GreaterOrEqualOperator(),
        new LessOrEqualOperator(),
        new InOperator(),
        new ContainsOperator(),
        new StartsWithOperator(),
        new EndsWithOperator(),
        new ExistsOperator(),
        new IsEmptyOperator(),
        new IsNotEmptyOperator());
}
