// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests.Engine;

public class StrictResolutionTests
{
    private static bool Eval(string expr, Dictionary<string, object> data)
    {
        var tokens = new ConditionLexer().Tokenize(expr);
        ConditionNode node = new ConditionParser(ConditionOperatorRegistry.Shared).Parse(tokens);
        return new ConditionEvaluatorCore(new GlobalEvaluationContext(data), DefaultConditionDialect.Instance).EvaluateBool(node);
    }

    [Fact]
    public void In_MissingCollection_IsFalse()
        => Assert.False(Eval("Status in Roles", new() { ["Status"] = "X" }));

    [Fact]
    public void In_BothOperandsMissing_IsFalse()
        => Assert.False(Eval("Status in Roles", new()));

    [Fact]
    public void In_ResolvedCollection_StillMatches()
        => Assert.True(Eval("Status in Roles", new() { ["Status"] = "Admin", ["Roles"] = new List<object> { "User", "Admin" } }));

    [Fact]
    public void Contains_MissingRightOperand_IsFalse()
        => Assert.False(Eval("Desc contains Sub", new() { ["Desc"] = "hello" }));

    [Fact]
    public void Contains_MissingLeftOperand_IsFalse()
        => Assert.False(Eval("Desc contains Sub", new()));

    [Fact]
    public void Contains_LiteralRightOperand_StillMatches()
        => Assert.True(Eval("Desc contains \"ell\"", new() { ["Desc"] = "hello" }));
}
