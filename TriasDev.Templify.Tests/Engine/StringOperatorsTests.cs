// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests.Engine;

public class StringOperatorsTests
{
    private static bool Eval(string expr, Dictionary<string, object> data)
    {
        var tokens = new ConditionLexer().Tokenize(expr);
        ConditionNode node = new ConditionParser(ConditionOperatorRegistry.Shared).Parse(tokens);
        return new ConditionEvaluatorCore(new GlobalEvaluationContext(data), DefaultConditionDialect.Instance).EvaluateBool(node);
    }

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
