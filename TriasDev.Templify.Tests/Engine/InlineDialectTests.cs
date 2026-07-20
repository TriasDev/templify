// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;

namespace TriasDev.Templify.Tests.Engine;

public class InlineDialectTests
{
    private static bool Eval(string expr, Dictionary<string, object> data)
    {
        var tokens = new ConditionLexer().Tokenize(expr);
        ConditionNode node = new ConditionParser(ConditionOperatorRegistry.Shared).Parse(tokens);
        return new ConditionEvaluatorCore(new GlobalEvaluationContext(data), InlineConditionDialect.Instance).EvaluateBool(node);
    }

    [Fact]
    public void And_TwoBooleans()
        => Assert.True(Eval("(var1 and var2)", new() { ["var1"] = true, ["var2"] = true }));

    [Fact]
    public void Or_TwoBooleans()
        => Assert.True(Eval("(var1 or var2)", new() { ["var1"] = false, ["var2"] = true }));

    [Fact]
    public void Not_Boolean()
        => Assert.True(Eval("(not IsActive)", new() { ["IsActive"] = false }));

    [Fact]
    public void Comparison_Numeric()
        => Assert.True(Eval("(Count > 0)", new() { ["Count"] = 5 }));

    [Fact]
    public void Nested_Grouping()
        => Assert.True(Eval("((var1 or var2) and var3)", new() { ["var1"] = false, ["var2"] = true, ["var3"] = true }));

    [Fact]
    public void BareStringVariable_IsFalse_InInlineDialect()
        => Assert.False(Eval("(Name)", new() { ["Name"] = "Alice" })); // Inline truthiness: only bool true is true

    [Fact]
    public void NewOperators_WorkInInlineDialect()
        => Assert.True(Eval("(Status in (\"A\", \"B\"))", new() { ["Status"] = "B" }));
}
