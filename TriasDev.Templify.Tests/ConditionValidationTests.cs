// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals;

namespace TriasDev.Templify.Tests;

public class ConditionValidationTests
{
    private readonly ConditionEvaluator _evaluator = new();

    #region Valid Expressions

    [Fact]
    public void Validate_SimpleVariable_ReturnsValid()
    {
        ConditionValidationResult result = _evaluator.Validate("IsActive");

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Validate_ComparisonWithSingleEquals_ReturnsValid()
    {
        ConditionValidationResult result = _evaluator.Validate("Count = 5");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ComparisonWithDoubleEquals_ReturnsValid()
    {
        ConditionValidationResult result = _evaluator.Validate("Count == 5");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EqualityWithQuotedString_ReturnsValid()
    {
        ConditionValidationResult result = _evaluator.Validate("Status = \"Active\"");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_LogicalAnd_ReturnsValid()
    {
        ConditionValidationResult result = _evaluator.Validate("A and B");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_LogicalOr_ReturnsValid()
    {
        ConditionValidationResult result = _evaluator.Validate("A or B");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_NotOperator_ReturnsValid()
    {
        ConditionValidationResult result = _evaluator.Validate("not IsDisabled");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ComplexExpression_ReturnsValid()
    {
        ConditionValidationResult result = _evaluator.Validate("Count > 0 and IsEnabled");

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("A = B")]
    [InlineData("A == B")]
    [InlineData("A != B")]
    [InlineData("A > B")]
    [InlineData("A < B")]
    [InlineData("A >= B")]
    [InlineData("A <= B")]
    public void Validate_AllComparisonOperators_ReturnsValid(string expression)
    {
        ConditionValidationResult result = _evaluator.Validate(expression);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_NestedPath_ReturnsValid()
    {
        ConditionValidationResult result = _evaluator.Validate("Customer.Address.City");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ComplexWithMultipleOperators_ReturnsValid()
    {
        ConditionValidationResult result = _evaluator.Validate("Status = \"Active\" and Count > 0 or IsEnabled");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_NotWithComparison_ReturnsValid()
    {
        ConditionValidationResult result = _evaluator.Validate("not Status = \"Inactive\"");

        Assert.True(result.IsValid);
    }

    #endregion

    #region Empty Expression

    [Fact]
    public void Validate_EmptyString_ReturnsEmptyExpression()
    {
        ConditionValidationResult result = _evaluator.Validate("");

        Assert.False(result.IsValid);
        Assert.Single(result.Issues);
        Assert.Equal(ConditionValidationIssueType.EmptyExpression, result.Issues[0].Type);
    }

    [Fact]
    public void Validate_WhitespaceOnly_ReturnsEmptyExpression()
    {
        ConditionValidationResult result = _evaluator.Validate("   ");

        Assert.False(result.IsValid);
        Assert.Single(result.Issues);
        Assert.Equal(ConditionValidationIssueType.EmptyExpression, result.Issues[0].Type);
    }

    [Fact]
    public void Validate_NullExpression_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _evaluator.Validate(null!));
    }

    #endregion

    #region Unbalanced Quotes

    [Fact]
    public void Validate_UnclosedQuote_ReturnsUnbalancedQuotes()
    {
        ConditionValidationResult result = _evaluator.Validate("Status = \"Active");

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Type == ConditionValidationIssueType.UnbalancedQuotes);
    }

    #endregion

    #region Unknown Operators

    [Fact]
    public void Validate_DollarSign_ReturnsUnknownOperator()
    {
        ConditionValidationResult result = _evaluator.Validate("A $ B");

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i =>
            i.Type == ConditionValidationIssueType.UnknownOperator && i.Token == "$");
    }

    [Fact]
    public void Validate_DoubleAmpersand_ReturnsUnknownOperator()
    {
        ConditionValidationResult result = _evaluator.Validate("A && B");

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i =>
            i.Type == ConditionValidationIssueType.UnknownOperator && i.Token == "&&");
    }

    [Fact]
    public void Validate_DoublePipe_ReturnsUnknownOperator()
    {
        ConditionValidationResult result = _evaluator.Validate("A || B");

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i =>
            i.Type == ConditionValidationIssueType.UnknownOperator && i.Token == "||");
    }

    [Fact]
    public void Validate_DiamondOperator_ReturnsUnknownOperator()
    {
        ConditionValidationResult result = _evaluator.Validate("A <> B");

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i =>
            i.Type == ConditionValidationIssueType.UnknownOperator && i.Token == "<>");
    }

    [Fact]
    public void Validate_TripleEquals_ReturnsUnknownOperator()
    {
        ConditionValidationResult result = _evaluator.Validate("A === B");

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i =>
            i.Type == ConditionValidationIssueType.UnknownOperator && i.Token == "===");
    }

    #endregion

    #region Missing Operand

    [Fact]
    public void Validate_TrailingOperator_ReturnsMissingOperand()
    {
        ConditionValidationResult result = _evaluator.Validate("Status =");

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i =>
            i.Type == ConditionValidationIssueType.MissingOperand && i.Token == "=");
    }

    [Fact]
    public void Validate_LeadingOperator_ReturnsMissingOperand()
    {
        ConditionValidationResult result = _evaluator.Validate("= Active");

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i =>
            i.Type == ConditionValidationIssueType.MissingOperand && i.Token == "=");
    }

    #endregion

    #region Consecutive Operators

    [Fact]
    public void Validate_TwoComparisonOperators_ReturnsConsecutiveOperators()
    {
        ConditionValidationResult result = _evaluator.Validate("A = = B");

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i =>
            i.Type == ConditionValidationIssueType.ConsecutiveOperators);
    }

    #endregion

    #region Consecutive Operands

    [Fact]
    public void Validate_TwoOperandsWithoutOperator_ReturnsConsecutiveOperands()
    {
        ConditionValidationResult result = _evaluator.Validate("A B");

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i =>
            i.Type == ConditionValidationIssueType.ConsecutiveOperands);
    }

    #endregion

    #region Multiple Issues

    [Fact]
    public void Validate_MultipleIssues_ReturnsAll()
    {
        // "$ B C" has unknown operator $ at start (also MissingOperand) and consecutive operands B C
        ConditionValidationResult result = _evaluator.Validate("$ B C");

        Assert.False(result.IsValid);
        Assert.True(result.Issues.Count >= 2);
    }

    #endregion

    #region Via IConditionContext

    [Fact]
    public void Validate_ViaConditionContext_DelegatesToEvaluator()
    {
        Dictionary<string, object> data = new() { ["X"] = true };
        IConditionContext context = _evaluator.CreateConditionContext(data);

        ConditionValidationResult validResult = context.Validate("X = true");
        Assert.True(validResult.IsValid);

        ConditionValidationResult invalidResult = context.Validate("A $ B");
        Assert.False(invalidResult.IsValid);
    }

    #endregion

    #region Via IConditionEvaluator Interface

    [Fact]
    public void Validate_ViaInterface_Works()
    {
        IConditionEvaluator evaluator = new ConditionEvaluator();

        ConditionValidationResult result = evaluator.Validate("Count > 0");

        Assert.True(result.IsValid);
    }

    #endregion
}
