// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;
using TriasDev.Templify.Core;
using static TriasDev.Templify.Tests.Helpers.ConditionEngineTestHelper;

namespace TriasDev.Templify.Tests.Conditionals.Engine;

public class InlineDialectTests
{
    [Fact]
    public void And_TwoBooleans()
        => Assert.True(EvalInline("(var1 and var2)", new() { ["var1"] = true, ["var2"] = true }));

    [Fact]
    public void Or_TwoBooleans()
        => Assert.True(EvalInline("(var1 or var2)", new() { ["var1"] = false, ["var2"] = true }));

    [Fact]
    public void Not_Boolean()
        => Assert.True(EvalInline("(not IsActive)", new() { ["IsActive"] = false }));

    [Fact]
    public void Comparison_Numeric()
        => Assert.True(EvalInline("(Count > 0)", new() { ["Count"] = 5 }));

    [Fact]
    public void Nested_Grouping()
        => Assert.True(EvalInline("((var1 or var2) and var3)", new() { ["var1"] = false, ["var2"] = true, ["var3"] = true }));

    [Fact]
    public void BareStringVariable_IsFalse_InInlineDialect()
        => Assert.False(EvalInline("(Name)", new() { ["Name"] = "Alice" })); // Inline truthiness: only bool true is true

    [Fact]
    public void NewOperators_WorkInInlineDialect()
        => Assert.True(EvalInline("(Status in (\"A\", \"B\"))", new() { ["Status"] = "B" }));

    [Fact]
    public void Comparison_DecimalGreaterThanIntLiteral()
        => Assert.True(EvalInline("(Price > 5)", new() { ["Price"] = 10.5m }));

    [Fact]
    public void Equality_LongEqualsIntLiteral()
        => Assert.True(EvalInline("(Id = 5)", new() { ["Id"] = 5L }));

    [Fact]
    public void Equality_DecimalEqualsDoubleLiteral()
        => Assert.True(EvalInline("(Price = 10.5)", new() { ["Price"] = 10.50m }));

    [Fact]
    public void Comparison_LongLessThanDecimal()
        => Assert.True(EvalInline("(A < B)", new() { ["A"] = 1L, ["B"] = 1.5m }));

    [Fact]
    public void Comparison_NaN_IsFalse()
        => Assert.False(EvalInline("(A > 0)", new() { ["A"] = double.NaN }));

    [Fact]
    public void Truthiness_Unchanged_NumbersAndStringTrueAreFalse()
    {
        Assert.False(EvalInline("(Count)", new() { ["Count"] = 5 }));
        Assert.False(EvalInline("(Flag)", new() { ["Flag"] = "true" }));
    }
}
