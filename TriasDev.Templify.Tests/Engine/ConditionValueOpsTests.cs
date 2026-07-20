// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals.Engine;

namespace TriasDev.Templify.Tests.Engine;

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
}
