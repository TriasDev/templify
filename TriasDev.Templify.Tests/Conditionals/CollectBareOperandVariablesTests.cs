// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals;

namespace TriasDev.Templify.Tests.Conditionals;

/// <summary>
/// <see cref="ConditionalEvaluator.CollectBareOperandVariables"/> finds the variables evaluated for truthiness on
/// their own.
/// </summary>
public sealed class CollectBareOperandVariablesTests
{
    [Theory]
    [InlineData("A", "A")]
    [InlineData("not A", "A")]
    [InlineData("A and B", "A,B")]
    [InlineData("A or not B", "A,B")]
    [InlineData("(A or B) and not (C and D)", "A,B,C,D")]
    [InlineData("A and Status = \"x\"", "A")]
    [InlineData("Customer.Name", "Customer.Name")]
    [InlineData("Items[0].Flag or [Exists]", "Items[0].Flag,Exists")]
    [InlineData("(A and B) = true", "A,B")]
    [InlineData("not (A = B)", "")]
    [InlineData("A exists", "")]
    [InlineData("A is empty or B is not empty", "")]
    [InlineData("Status = Active", "")]
    [InlineData("A != B", "")]
    [InlineData("A > 1", "")]
    [InlineData("A in (B, C)", "")]
    [InlineData("A contains B", "")]
    [InlineData("A startswith B or C endswith D", "")]
    [InlineData("true and not null", "")]
    [InlineData("\"text\"", "")]
    public void CollectsBareOperands(string expression, string expected)
    {
        IEnumerable<string> paths = ConditionalEvaluator
            .CollectBareOperandVariables(ConditionalEvaluator.Parse(expression))
            .Select(v => v.Path);

        Assert.Equal(expected, string.Join(",", paths));
    }
}
