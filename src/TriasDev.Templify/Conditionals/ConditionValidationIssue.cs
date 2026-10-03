// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Conditionals;

/// <summary>
/// Represents an issue found during condition expression validation.
/// </summary>
public sealed class ConditionValidationIssue
{
    /// <summary>
    /// Gets the type of validation issue.
    /// </summary>
    public ConditionValidationIssueType Type { get; }

    /// <summary>
    /// Gets a human-readable message describing the issue.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the offending token, if applicable.
    /// </summary>
    public string? Token { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConditionValidationIssue"/> class.
    /// </summary>
    /// <param name="type">The type of validation issue.</param>
    /// <param name="message">A human-readable message describing the issue.</param>
    /// <param name="token">The offending token, if applicable.</param>
    public ConditionValidationIssue(ConditionValidationIssueType type, string message, string? token = null)
    {
        Type = type;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Token = token;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        string tokenPart = Token != null ? $" (token: '{Token}')" : "";
        return $"{Type}{tokenPart}: {Message}";
    }
}

/// <summary>
/// Defines the types of issues that can be found during condition expression validation.
/// </summary>
public enum ConditionValidationIssueType
{
    /// <summary>
    /// The expression is empty or whitespace-only.
    /// </summary>
    EmptyExpression,

    /// <summary>
    /// An unrecognized operator-like token was found (e.g., "===" , "$", "&amp;&amp;", "||").
    /// </summary>
    UnknownOperator,

    /// <summary>
    /// The expression contains unbalanced quotes.
    /// </summary>
    UnbalancedQuotes,

    /// <summary>
    /// An operator is missing a required operand (e.g., "Status =" or "= Active").
    /// </summary>
    MissingOperand,

    /// <summary>
    /// Two operators appear consecutively without an operand between them.
    /// </summary>
    ConsecutiveOperators,

    /// <summary>
    /// Two operands appear consecutively without an operator between them.
    /// </summary>
    ConsecutiveOperands
}
