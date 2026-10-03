// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Text.Json;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Tests.Utilities;

/// <summary>
/// Unit tests for <see cref="NumericValue"/> paths that the condition tests do not reach:
/// values outside the decimal range, double-only strings and NaN.
/// </summary>
public sealed class NumericValueTests
{
    [Fact]
    public void TryFrom_JsonNumberOutsideDecimalRange_UsesDouble()
    {
        using JsonDocument json = JsonDocument.Parse("1e300");

        Assert.True(NumericValue.TryFrom(json.RootElement, out NumericValue value));
        Assert.True(NumericValue.TryFrom(1e300, out NumericValue expected));
        Assert.True(value.NumericEquals(expected));
        Assert.True(value.TryCompareTo(ExactOf(1), out int cmp));
        Assert.True(cmp > 0);
    }

    [Fact]
    public void TryFrom_JsonString_IsNotANumber()
    {
        using JsonDocument json = JsonDocument.Parse("\"5\"");

        Assert.False(NumericValue.TryFrom(json.RootElement, out _));
        Assert.False(NumericValue.TryFrom("5", out _));
        Assert.False(NumericValue.TryFrom(null, out _));
    }

    [Theory]
    [InlineData("1e300")]
    [InlineData("-1e300")]
    public void TryParse_OutsideDecimalRange_ParsesAsDouble(string text)
    {
        Assert.True(NumericValue.TryParse(text, out NumericValue value));
        Assert.False(value.IsNaN);
        Assert.False(value.IsZero);
        Assert.False(value.NumericEquals(ExactOf(0)));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("1,5")] // NumberStyles.Float allows no separators other than the invariant "."
    public void TryParse_NonNumericText_ReturnsFalse(string? text)
    {
        Assert.False(NumericValue.TryParse(text, out _));
    }

    [Fact]
    public void TryFrom_Infinity_IsOrderedAboveEveryFiniteValue()
    {
        Assert.True(NumericValue.TryFrom(double.PositiveInfinity, out NumericValue infinity));

        Assert.True(infinity.TryCompareTo(ExactOf(decimal.MaxValue), out int cmp));
        Assert.True(cmp > 0);
    }

    [Fact]
    public void NaN_IsNotEqualAndNotOrdered()
    {
        Assert.True(NumericValue.TryFrom(double.NaN, out NumericValue nan));
        Assert.True(NumericValue.TryFrom(float.NaN, out NumericValue floatNan));

        Assert.True(nan.IsNaN);
        Assert.True(floatNan.IsNaN);
        Assert.False(nan.NumericEquals(nan));
        Assert.False(nan.TryCompareTo(ExactOf(1), out _));
        Assert.False(ExactOf(1).TryCompareTo(nan, out _));
    }

    [Fact]
    public void SameFloatingKind_ComparesAsDouble()
    {
        // 0.1f + 0.2f differs from 0.3f in float arithmetic; comparing two floats must not round through decimal
        Assert.True(NumericValue.TryFrom(0.1f + 0.2f, out NumericValue sum));
        Assert.True(NumericValue.TryFrom(0.3f, out NumericValue third));

        Assert.Equal((0.1f + 0.2f).CompareTo(0.3f) == 0, sum.NumericEquals(third));
    }

    [Fact]
    public void FloatAgainstDecimal_ComparesThroughDecimal()
    {
        Assert.True(NumericValue.TryFrom(0.1f, out NumericValue single));

        Assert.True(single.NumericEquals(ExactOf(0.1m)));
    }

    private static NumericValue ExactOf(decimal value)
    {
        Assert.True(NumericValue.TryFrom(value, out NumericValue number));
        return number;
    }
}
