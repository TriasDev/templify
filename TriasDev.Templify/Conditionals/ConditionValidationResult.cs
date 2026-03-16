// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals;

/// <summary>
/// Represents the result of validating a condition expression.
/// </summary>
public sealed class ConditionValidationResult
{
    /// <summary>
    /// Gets whether the expression is syntactically valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets a read-only list of validation issues found in the expression.
    /// </summary>
    public IReadOnlyList<ConditionValidationIssue> Issues { get; init; } = Array.Empty<ConditionValidationIssue>();

    /// <summary>
    /// Creates a successful validation result with no issues.
    /// </summary>
    public static ConditionValidationResult Success()
    {
        return new ConditionValidationResult { IsValid = true };
    }

    /// <summary>
    /// Creates a failed validation result with the specified issues.
    /// </summary>
    /// <param name="issues">The validation issues found.</param>
    public static ConditionValidationResult Failure(IReadOnlyList<ConditionValidationIssue> issues)
    {
        return new ConditionValidationResult
        {
            IsValid = false,
            Issues = issues ?? throw new ArgumentNullException(nameof(issues))
        };
    }
}
