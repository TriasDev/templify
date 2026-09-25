// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using TriasDev.Templify.Conditionals;

namespace TriasDev.Templify.Tests.Conditionals;

/// <summary>
/// Unit tests for <see cref="ConditionValidationIssue"/>.
/// </summary>
public sealed class ConditionValidationIssueTests
{
    [Fact]
    public void ToString_WithToken_IncludesToken()
    {
        ConditionValidationIssue issue = new ConditionValidationIssue(ConditionValidationIssueType.UnknownOperator, "Unknown operator", "=~");

        Assert.Equal("UnknownOperator (token: '=~'): Unknown operator", issue.ToString());
    }

    [Fact]
    public void ToString_WithoutToken_OmitsTokenPart()
    {
        ConditionValidationIssue issue = new ConditionValidationIssue(ConditionValidationIssueType.EmptyExpression, "Expression is empty");

        Assert.Equal("EmptyExpression: Expression is empty", issue.ToString());
        Assert.Null(issue.Token);
    }

    [Fact]
    public void Constructor_NullMessage_ThrowsArgumentNullException()
    {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => new ConditionValidationIssue(ConditionValidationIssueType.EmptyExpression, null!));
        Assert.Equal("message", ex.ParamName);
    }
}
