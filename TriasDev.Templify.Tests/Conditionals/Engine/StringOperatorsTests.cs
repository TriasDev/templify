// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;
using static TriasDev.Templify.Tests.Helpers.ConditionEngineTestHelper;

namespace TriasDev.Templify.Tests.Conditionals.Engine;

public class StringOperatorsTests
{
    [Fact]
    public void Contains_Substring_IsTrue()
        => Assert.True(Eval("Description contains \"urgent\"", new() { ["Description"] = "this is urgent" }));

    [Fact]
    public void Contains_IsCaseSensitive()
        => Assert.False(Eval("Description contains \"URGENT\"", new() { ["Description"] = "this is urgent" }));

    [Fact]
    public void StartsWith_Prefix_IsTrue()
        => Assert.True(Eval("Phone startswith \"+49\"", new() { ["Phone"] = "+49 151" }));

    [Fact]
    public void EndsWith_Suffix_IsTrue()
        => Assert.True(Eval("File endswith \".pdf\"", new() { ["File"] = "report.pdf" }));
}
