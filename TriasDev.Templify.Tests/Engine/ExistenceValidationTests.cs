// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals;

namespace TriasDev.Templify.Tests.Engine;

public class ExistenceValidationTests
{
    private static bool IsValid(string expression) => new ConditionalEvaluator().Validate(expression).IsValid;

    [Fact]
    public void Validate_Exists_IsValid()
        => Assert.True(IsValid("Notes exists"));

    [Fact]
    public void Validate_IsEmpty_IsValid()
        => Assert.True(IsValid("Notes is empty"));

    [Fact]
    public void Validate_IsNotEmpty_IsValid()
        => Assert.True(IsValid("Notes is not empty"));

    [Fact]
    public void Validate_ComparisonAndExists_IsValid()
        => Assert.True(IsValid("Status = \"Active\" and Notes exists"));
}
