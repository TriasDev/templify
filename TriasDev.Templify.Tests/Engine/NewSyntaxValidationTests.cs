// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals;

namespace TriasDev.Templify.Tests.Engine;

/// <summary>
/// Verifies that the public <see cref="ConditionEvaluator.Validate"/> accepts the operator syntax the
/// expression engine supports, matching what <c>Evaluate</c> accepts (parser-based validation, F1).
/// </summary>
public class NewSyntaxValidationTests
{
    private readonly ConditionEvaluator _evaluator = new();

    [Theory]
    [InlineData("Status in (\"Active\", \"Pending\")")] // Previously broken: whitespace after the comma.
    [InlineData("Status in (\"A\",\"B\")")]
    [InlineData("Role in Roles")]
    [InlineData("not Role in Roles")]
    [InlineData("Email contains \"@x\"")]
    [InlineData("Name startswith \"A\"")]
    [InlineData("Notes exists")]
    [InlineData("Notes is empty")]
    [InlineData("Notes is not empty")]
    [InlineData("(A or B) and C")]
    public void Validate_NewSyntax_IsValid(string expression)
    {
        ConditionValidationResult result = _evaluator.Validate(expression);

        Assert.True(result.IsValid, $"Expected '{expression}' to be valid.");
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Validate_TrailingOperator_StillReportsMissingOperand()
    {
        ConditionValidationResult result = _evaluator.Validate("Status =");

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Type == ConditionValidationIssueType.MissingOperand);
    }
}
