// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;
using static TriasDev.Templify.Tests.Helpers.ConditionEngineTestHelper;

namespace TriasDev.Templify.Tests.Conditionals.Engine;

public class StrictResolutionTests
{
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
