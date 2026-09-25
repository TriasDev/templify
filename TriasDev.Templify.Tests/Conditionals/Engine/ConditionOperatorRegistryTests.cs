// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;

namespace TriasDev.Templify.Tests.Conditionals.Engine;

/// <summary>
/// The operator registry is the single source of truth for operator keywords, precedence and
/// postfix matching order (issue #155).
/// </summary>
public sealed class ConditionOperatorRegistryTests
{
    private static readonly ConditionOperatorRegistry _registry = ConditionOperatorRegistry.Shared;

    [Fact]
    public void WordOperators_AreTheLetterTokensOfAllOperators()
    {
        string[] expected = { "and", "contains", "empty", "endswith", "exists", "in", "is", "not", "or", "startswith" };

        Assert.Equal(expected, _registry.WordOperators.OrderBy(w => w, StringComparer.Ordinal));
        Assert.Contains("AND", (IReadOnlySet<string>)_registry.WordOperators);
    }

    [Theory]
    [InlineData("or", OperatorPrecedence.Or)]
    [InlineData("and", OperatorPrecedence.And)]
    [InlineData("=", OperatorPrecedence.Comparison)]
    [InlineData("==", OperatorPrecedence.Comparison)]
    [InlineData("!=", OperatorPrecedence.Comparison)]
    [InlineData(">=", OperatorPrecedence.Comparison)]
    [InlineData("in", OperatorPrecedence.Comparison)]
    [InlineData("contains", OperatorPrecedence.Comparison)]
    [InlineData("endswith", OperatorPrecedence.Comparison)]
    public void FindInfix_ReturnsOperatorWithNamedPrecedence(string token, int precedence)
    {
        IConditionOperator? op = _registry.FindInfix(token);

        Assert.NotNull(op);
        Assert.Equal(precedence, op.Precedence);
        Assert.Equal(OperatorFixity.Infix, op.Fixity);
    }

    [Fact]
    public void FindPrefix_Not()
    {
        IConditionOperator? op = _registry.FindPrefix("not");

        Assert.NotNull(op);
        Assert.Equal(OperatorPrecedence.Not, op.Precedence);
        Assert.Null(_registry.FindInfix("not"));
    }

    [Fact]
    public void PostfixOperators_AreOrderedLongestSequenceFirst()
    {
        List<string> sequences = _registry.PostfixOperators.Select(op => string.Join(" ", op.Tokens)).ToList();

        Assert.Equal(new[] { "is not empty", "is empty", "exists" }, sequences);
        Assert.Same(_registry.PostfixOperators, _registry.PostfixOperators);
    }

    [Theory]
    [InlineData("exists", true)]
    [InlineData("IS", true)]
    [InlineData("empty", true)]
    [InlineData("and", false)]
    [InlineData("=", false)]
    public void IsPostfixToken(string token, bool expected)
    {
        Assert.Equal(expected, _registry.IsPostfixToken(token));
    }

    [Fact]
    public void Shared_IsSafeForConcurrentParsing()
    {
        Parallel.For(0, 200, i =>
        {
            IReadOnlyList<ConditionToken> tokens = new ConditionLexer().Tokenize($"A{i} > {i} and not (B is not empty or C in (1, 2))");
            ConditionNode node = new ConditionParser(_registry).Parse(tokens);
            Assert.IsType<OperatorNode>(node);
        });
    }
}
