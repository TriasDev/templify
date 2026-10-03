// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;
using static TriasDev.Templify.Tests.Helpers.ConditionEngineTestHelper;

namespace TriasDev.Templify.Tests.Conditionals.Engine;

public class MembershipOperatorTests
{
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
