// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;

namespace TriasDev.Templify.Tests.Conditionals.Engine;

public class ConditionValueOpsTests
{
    [Fact]
    public void AreEqual_SameStrings_ReturnsTrue()
        => Assert.True(ConditionValueOps.AreEqual("Active", "Active"));

    [Fact]
    public void AreEqual_DifferentCaseStrings_ReturnsFalse()
        => Assert.False(ConditionValueOps.AreEqual("active", "Active"));

    [Fact]
    public void AreEqual_BoolAndLowercaseLiteral_ReturnsTrue()
        => Assert.True(ConditionValueOps.AreEqual(true, "true"));

    [Fact]
    public void AreEqual_BothNull_ReturnsTrue()
        => Assert.True(ConditionValueOps.AreEqual(null, null));

    [Fact]
    public void AreEqual_OneNull_ReturnsFalse()
        => Assert.False(ConditionValueOps.AreEqual(null, "x"));

    [Fact]
    public void AreEqual_DecimalAndInt_SameValue_ReturnsTrue()
        => Assert.True(ConditionValueOps.AreEqual(10.00m, 10));

    [Fact]
    public void AreEqual_LongAndDouble_SameValue_ReturnsTrue()
        => Assert.True(ConditionValueOps.AreEqual(2L, 2.0));

    [Fact]
    public void AreEqual_NumberAndNumericString_ComparesStringForm()
    {
        Assert.True(ConditionValueOps.AreEqual(5, "5"));
        Assert.False(ConditionValueOps.AreEqual(5.0m, "5"));
    }

    [Fact]
    public void AreEqual_NaN_ReturnsFalse()
        => Assert.False(ConditionValueOps.AreEqual(double.NaN, double.NaN));
}
