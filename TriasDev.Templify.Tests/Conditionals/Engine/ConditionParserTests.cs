// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;
using static TriasDev.Templify.Tests.Helpers.ConditionEngineTestHelper;

namespace TriasDev.Templify.Tests.Conditionals.Engine;

public class ConditionParserTests
{
    [Fact]
    public void Parse_AndWithOr_AndBindsTighter()
    {
        // false or (true and true) => true ; if or bound tighter, (false or true) and false path differs.
        Assert.True(Eval("A or B and C", new() { ["A"] = false, ["B"] = true, ["C"] = true }));
        Assert.False(Eval("A or B and C", new() { ["A"] = false, ["B"] = true, ["C"] = false }));
    }

    [Fact]
    public void Parse_Parentheses_OverridePrecedence()
    {
        Assert.False(Eval("(A or B) and C", new() { ["A"] = false, ["B"] = true, ["C"] = false }));
        Assert.True(Eval("(A or B) and C", new() { ["A"] = false, ["B"] = true, ["C"] = true }));
    }

    [Fact]
    public void Parse_NotWithComparison_ComparisonBindsTighter()
    {
        // not Status = "Active"  =>  not (Status = "Active")
        Assert.False(Eval("not Status = \"Active\"", new() { ["Status"] = "Active" }));
        Assert.True(Eval("not Status = \"Active\"", new() { ["Status"] = "Inactive" }));
    }

    [Fact]
    public void Parse_MissingRightOperand_ThrowsConditionParseException()
    {
        var tokens = new ConditionLexer().Tokenize("Status =");
        Assert.Throws<ConditionParseException>(() =>
            new ConditionParser(ConditionOperatorRegistry.Shared).Parse(tokens));
    }
}
