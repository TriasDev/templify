// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json;
using TriasDev.Templify.Conditionals;

namespace TriasDev.Templify.Tests.Conditionals.Engine;

/// <summary>
/// Numeric equality, ordering and truthiness across CLR numeric types and JSON numbers (issue #142),
/// plus <c>contains</c> on collections.
/// </summary>
public class NumericSemanticsTests
{
    private static bool Eval(string expression, Dictionary<string, object> data)
        => new ConditionEvaluator().Evaluate(expression, data);

    private static bool EvalJson(string expression, string json)
        => new ConditionEvaluator().Evaluate(expression, json);

    // ---------- Equality ----------

    [Fact]
    public void Cond_Equality_DecimalVsInt()
        => Assert.True(Eval("Price = 10", new() { ["Price"] = 10.00m }));

    [Fact]
    public void Cond_Equality_JsonDecimalTrailingZero()
        => Assert.True(EvalJson("Price = 10.5", "{\"Price\": 10.50}"));

    [Fact]
    public void Cond_In_NumberList_JsonDecimal()
        => Assert.True(EvalJson("Rate in (1.5, 2.0)", "{\"Rate\": 2.0}"));

    [Theory]
    [MemberData(nameof(EqualNumbers))]
    public void Equality_AcrossNumericTypes_IsNumeric(object value, string literal)
    {
        Assert.True(Eval($"X = {literal}", new() { ["X"] = value }));
        Assert.True(Eval($"X == {literal}", new() { ["X"] = value }));
        Assert.False(Eval($"X != {literal}", new() { ["X"] = value }));
    }

    public static TheoryData<object, string> EqualNumbers => new()
    {
        { 5L, "5" },
        { (short)5, "5" },
        { (byte)5, "5" },
        { (sbyte)5, "5" },
        { (ushort)5, "5" },
        { 5u, "5" },
        { 5ul, "5" },
        { 5m, "5.0" },
        { 5.0, "5" },
        { 0.1m, "0.1" },
        { 1.1f, "1.1" },
    };

    [Fact]
    public void Equality_TwoVariablesOfDifferentNumericTypes_IsNumeric()
        => Assert.True(Eval("A = B", new() { ["A"] = 42L, ["B"] = 42.000m }));

    [Fact]
    public void Equality_DifferentNumbers_IsFalse()
        => Assert.False(Eval("Price = 10", new() { ["Price"] = 10.01m }));

    [Fact]
    public void Equality_NaN_EqualsNothing()
    {
        Assert.False(Eval("A = B", new() { ["A"] = double.NaN, ["B"] = double.NaN }));
        Assert.True(Eval("A != 0", new() { ["A"] = double.NaN }));
    }

    [Fact]
    public void Equality_NumberVsString_KeepsStringSemantics()
    {
        // A number compared with a string compares the number's invariant string form (unchanged).
        Assert.True(Eval("Count = \"5\"", new() { ["Count"] = 5 }));
        Assert.False(Eval("Count = \"5.0\"", new() { ["Count"] = 5 }));
    }

    [Fact]
    public void Equality_Booleans_Unchanged()
    {
        Assert.True(Eval("Flag = true", new() { ["Flag"] = true }));
        Assert.False(Eval("Flag = 1", new() { ["Flag"] = true }));
    }

    [Fact]
    public void In_VariableCollectionOfDecimals_MatchesIntLiteral()
        => Assert.True(Eval("5 in Values", new() { ["Values"] = new List<decimal> { 1.5m, 5.00m } }));

    [Fact]
    public void Equality_JsonElementNumber_IsNumeric()
    {
        Dictionary<string, object> data = JsonSerializer.Deserialize<Dictionary<string, object>>("{\"Price\": 10.50}")!;
        Assert.True(Eval("Price = 10.5", data));
    }

    // ---------- Ordering ----------

    [Fact]
    public void Ordering_LongVsDecimal()
        => Assert.True(Eval("A > B", new() { ["A"] = 10L, ["B"] = 9.99m }));

    [Fact]
    public void Ordering_NaN_IsNotOrdered()
    {
        Assert.False(Eval("A < 5", new() { ["A"] = double.NaN }));
        Assert.False(Eval("A > 5", new() { ["A"] = double.NaN }));
    }

    [Fact]
    public void Ordering_Infinity_IsGreaterThanAnyNumber()
        => Assert.True(Eval("A > 1000000", new() { ["A"] = double.PositiveInfinity }));

    // ---------- Truthiness ----------

    [Fact]
    public void Cond_Truthiness_DecimalZero_IsFalse()
        => Assert.False(Eval("Total", new() { ["Total"] = 0m }));

    [Fact]
    public void Cond_Truthiness_LongZero_IsFalse()
        => Assert.False(Eval("Total", new() { ["Total"] = 0L }));

    [Fact]
    public void Cond_Truthiness_JsonDecimalZero_IsFalse()
        => Assert.False(EvalJson("Total", "{\"Total\": 0.0}"));

    [Theory]
    [MemberData(nameof(ZeroValues))]
    public void Truthiness_AnyNumericZero_IsFalse(object zero)
        => Assert.False(Eval("X", new() { ["X"] = zero }));

    public static TheoryData<object> ZeroValues => new()
    {
        0, 0L, 0m, 0.00m, 0.0, -0.0, 0f, (short)0, (byte)0, (sbyte)0, (ushort)0, 0u, 0ul, double.NaN,
    };

    [Theory]
    [MemberData(nameof(NonZeroValues))]
    public void Truthiness_NonZeroNumber_IsTrue(object value)
        => Assert.True(Eval("X", new() { ["X"] = value }));

    public static TheoryData<object> NonZeroValues => new()
    {
        1, -1L, 0.01m, 0.5, 2f, (short)3, (byte)1, double.PositiveInfinity,
    };

    [Fact]
    public void Truthiness_JsonElementZero_IsFalse()
    {
        Dictionary<string, object> data = JsonSerializer.Deserialize<Dictionary<string, object>>("{\"Total\": 0.0}")!;
        Assert.False(Eval("Total", data));
    }

    [Fact]
    public void Cond_Truthiness_NonCollectionEmptyEnumerable_IsFalse()
    {
        IEnumerable<int> empty = Enumerable.Empty<int>().Where(x => x > 0);
        Assert.False(Eval("Items", new() { ["Items"] = empty }));
    }

    [Fact]
    public void Truthiness_NonCollectionNonEmptyEnumerable_IsTrue()
    {
        IEnumerable<int> items = Enumerable.Range(1, 3).Where(x => x > 1);
        Assert.True(Eval("Items", new() { ["Items"] = items }));
    }

    // ---------- Identifiers that look like special floating values ----------

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void Cond_Infinity_IdentifierParsedAsNumber(string name)
        => Assert.True(Eval(name, new() { [name] = true }));

    // ---------- contains / startswith / endswith on collections ----------

    [Fact]
    public void Cond_Contains_OnListVariable()
        => Assert.True(Eval("Tags contains \"urgent\"", new() { ["Tags"] = new List<string> { "urgent", "x" } }));

    [Fact]
    public void Cond_Contains_OnListVariable_FalsePositiveOnTypeName()
        => Assert.False(Eval("Tags contains \"List\"", new() { ["Tags"] = new List<string> { "a" } }));

    [Fact]
    public void Contains_OnList_IsExactElementMatch_NotSubstring()
        => Assert.False(Eval("Tags contains \"urg\"", new() { ["Tags"] = new List<string> { "urgent" } }));

    [Fact]
    public void Contains_OnNumberArray_UsesNumericEquality()
        => Assert.True(Eval("Codes contains 2", new() { ["Codes"] = new[] { 1.0m, 2.00m } }));

    [Fact]
    public void Contains_OnJsonArray_ChecksMembership()
        => Assert.True(EvalJson("Tags contains \"b\"", "{\"Tags\": [\"a\", \"b\"]}"));

    [Fact]
    public void Contains_OnEmptyList_IsFalse()
        => Assert.False(Eval("Tags contains \"a\"", new() { ["Tags"] = new List<string>() }));

    [Fact]
    public void StartsWithAndEndsWith_OnCollection_AreFalse()
    {
        Dictionary<string, object> data = new() { ["Tags"] = new List<string> { "abc" } };
        Assert.False(Eval("Tags startswith \"System\"", data));
        Assert.False(Eval("Tags startswith \"a\"", data));
        Assert.False(Eval("Tags endswith \"]\"", data));
        Assert.False(Eval("Tags endswith \"c\"", data));
    }

    [Fact]
    public void Contains_OnString_KeepsSubstringSemantics()
        => Assert.True(Eval("Name contains \"lic\"", new() { ["Name"] = "Alice" }));
}
