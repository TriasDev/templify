// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;

namespace TriasDev.Templify.Tests.Engine;

public class ConditionLexerTests
{
    private static List<ConditionToken> Lex(string s) => new ConditionLexer().Tokenize(s).ToList();

    [Fact]
    public void Tokenize_Comparison_ProducesIdentifierOperatorString()
    {
        List<ConditionToken> t = Lex("Status = \"Active\"");
        Assert.Equal(ConditionTokenType.Identifier, t[0].Type);
        Assert.Equal("Status", t[0].Text);
        Assert.Equal(ConditionTokenType.Operator, t[1].Type);
        Assert.Equal("=", t[1].Text);
        Assert.Equal(ConditionTokenType.String, t[2].Type);
        Assert.Equal("Active", t[2].Text);
        Assert.Equal(ConditionTokenType.End, t[3].Type);
    }

    [Fact]
    public void Tokenize_GreaterOrEqual_MatchesLongestOperator()
    {
        List<ConditionToken> t = Lex("Count >= 3");
        Assert.Equal(ConditionTokenType.Operator, t[1].Type);
        Assert.Equal(">=", t[1].Text);
        Assert.Equal(ConditionTokenType.Number, t[2].Type);
        Assert.Equal(3, t[2].LiteralValue);
    }

    [Fact]
    public void Tokenize_ListLiteral_ProducesParensAndCommas()
    {
        List<ConditionToken> t = Lex("Status in (\"A\", \"B\")");
        Assert.Equal("in", t[1].Text);
        Assert.Equal(ConditionTokenType.LParen, t[2].Type);
        Assert.Equal(ConditionTokenType.String, t[3].Type);
        Assert.Equal(ConditionTokenType.Comma, t[4].Type);
        Assert.Equal(ConditionTokenType.RParen, t[6].Type);
    }

    [Fact]
    public void Tokenize_WordOperators_AreLowercasedOperators()
    {
        List<ConditionToken> t = Lex("A AND B IS EMPTY");
        Assert.Equal(ConditionTokenType.Operator, t[1].Type);
        Assert.Equal("and", t[1].Text);
        Assert.Equal("is", t[3].Text);
        Assert.Equal("empty", t[4].Text);
    }

    [Fact]
    public void Tokenize_CurlyQuotes_AreNormalized()
    {
        List<ConditionToken> t = Lex("Status = “Active”");
        Assert.Equal(ConditionTokenType.String, t[2].Type);
        Assert.Equal("Active", t[2].Text);
    }

    [Fact]
    public void Tokenize_BooleanAndNull_ProduceLiteralTokens()
    {
        List<ConditionToken> t = Lex("Flag = true");
        Assert.Equal(ConditionTokenType.Boolean, t[2].Type);
        Assert.Equal(true, t[2].LiteralValue);
    }
}
