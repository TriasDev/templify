// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests.Engine;

public class ConditionParserTests
{
    private static bool Eval(string expr, Dictionary<string, object> data)
    {
        var tokens = new ConditionLexer().Tokenize(expr);
        ConditionNode node = new ConditionParser(ConditionOperatorRegistry.Shared).Parse(tokens);
        return new ConditionEvaluatorCore(new GlobalEvaluationContext(data), DefaultConditionDialect.Instance).EvaluateBool(node);
    }

    [Fact]
    public void AndBindsTighterThanOr()
    {
        // false or (true and true) => true ; if or bound tighter, (false or true) and false path differs.
        Assert.True(Eval("A or B and C", new() { ["A"] = false, ["B"] = true, ["C"] = true }));
        Assert.False(Eval("A or B and C", new() { ["A"] = false, ["B"] = true, ["C"] = false }));
    }

    [Fact]
    public void ParenthesesOverridePrecedence()
    {
        Assert.False(Eval("(A or B) and C", new() { ["A"] = false, ["B"] = true, ["C"] = false }));
        Assert.True(Eval("(A or B) and C", new() { ["A"] = false, ["B"] = true, ["C"] = true }));
    }

    [Fact]
    public void ComparisonBindsTighterThanNot()
    {
        // not Status = "Active"  =>  not (Status = "Active")
        Assert.False(Eval("not Status = \"Active\"", new() { ["Status"] = "Active" }));
        Assert.True(Eval("not Status = \"Active\"", new() { ["Status"] = "Inactive" }));
    }

    [Fact]
    public void MalformedExpression_Throws()
    {
        var tokens = new ConditionLexer().Tokenize("Status =");
        Assert.Throws<ConditionParseException>(() =>
            new ConditionParser(ConditionOperatorRegistry.Shared).Parse(tokens));
    }
}
