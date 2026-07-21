// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests.Engine;

public class MembershipOperatorTests
{
    private static bool Eval(string expr, Dictionary<string, object> data)
    {
        var tokens = new ConditionLexer().Tokenize(expr);
        ConditionNode node = new ConditionParser(ConditionOperatorRegistry.Shared).Parse(tokens);
        return new ConditionEvaluatorCore(new GlobalEvaluationContext(data), DefaultConditionDialect.Instance).EvaluateBool(node);
    }

    [Fact]
    public void In_CollectionVariable_Matches()
        => Assert.True(Eval("Status in Roles", new() { ["Status"] = "Admin", ["Roles"] = new List<object> { "User", "Admin" } }));

    [Fact]
    public void In_CollectionVariable_NoMatch()
        => Assert.False(Eval("Status in Roles", new() { ["Status"] = "Guest", ["Roles"] = new List<object> { "User", "Admin" } }));

    [Fact]
    public void In_ListLiteral_Matches()
        => Assert.True(Eval("Status in (\"Active\", \"Pending\")", new() { ["Status"] = "Pending" }));

    [Fact]
    public void In_CommaString_Matches()
        => Assert.True(Eval("Status in \"Active,Pending\"", new() { ["Status"] = "Active" }));

    [Fact]
    public void NotIn_Negation_Works()
        => Assert.True(Eval("not Status in Roles", new() { ["Status"] = "Guest", ["Roles"] = new List<object> { "User", "Admin" } }));
}
