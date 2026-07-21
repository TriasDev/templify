// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests.Engine;

public class SingleQuoteLexingTests
{
    private static bool Eval(string expr, Dictionary<string, object> data)
    {
        IReadOnlyList<ConditionToken> tokens = new ConditionLexer(allowSingleQuotedStrings: true).Tokenize(expr);
        ConditionNode node = new ConditionParser(ConditionOperatorRegistry.Shared).Parse(tokens);
        return new ConditionEvaluatorCore(new GlobalEvaluationContext(data), InlineConditionDialect.Instance).EvaluateBool(node);
    }

    [Fact]
    public void InlinePath_SingleQuotedString_MatchesTrue()
        => Assert.True(Eval("(Status = 'Active')", new() { ["Status"] = "Active" }));

    [Fact]
    public void InlinePath_SingleQuotedString_MatchesFalse()
        => Assert.False(Eval("(Status = 'Active')", new() { ["Status"] = "Inactive" }));

    [Fact]
    public void InlinePath_MixedQuoteStyles_BothWork()
        => Assert.True(Eval(
            "(A = 'x' or B = \"y\")",
            new() { ["A"] = "x", ["B"] = "z" }));

    [Fact]
    public void InlinePath_MixedQuoteStyles_OtherBranch_BothWork()
        => Assert.True(Eval(
            "(A = 'x' or B = \"y\")",
            new() { ["A"] = "notx", ["B"] = "y" }));

    [Fact]
    public void Lexer_SingleQuoteEscape_ProducesSingleStringToken()
    {
        IReadOnlyList<ConditionToken> tokens = new ConditionLexer(allowSingleQuotedStrings: true).Tokenize("('a\\'b')");

        ConditionToken stringToken = tokens.Single(t => t.Type == ConditionTokenType.String);
        Assert.Equal("a'b", stringToken.Text);
    }

    [Fact]
    public void Lexer_DefaultOff_SingleQuoteIsNotStringDelimiter()
    {
        IReadOnlyList<ConditionToken> tokens = new ConditionLexer().Tokenize("'x'");

        Assert.NotEqual(ConditionTokenType.String, tokens[0].Type);
    }
}
