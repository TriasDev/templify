// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Core;

/// <summary>
/// Represents the result of a template validation operation.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the template is valid (no errors found).
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets the list of validation errors found in the template.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; init; } = Array.Empty<ValidationError>();

    /// <summary>
    /// Gets the list of validation warnings found in the template.
    /// Warnings indicate potential issues that don't prevent processing.
    /// </summary>
    public IReadOnlyList<ValidationWarning> Warnings { get; init; } = Array.Empty<ValidationWarning>();

    /// <summary>
    /// Gets all placeholders found in the template (simple, conditional, loop).
    /// </summary>
    public IReadOnlyList<string> AllPlaceholders { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the list of variables that are referenced in the template but not provided in the data.
    /// Only populated when validation is performed with data.
    /// </summary>
    public IReadOnlyList<string> MissingVariables { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Creates a successful validation result with no errors.
    /// </summary>
    /// <param name="allPlaceholders">All placeholders found in the template.</param>
    /// <param name="missingVariables">Missing variables (if data was provided).</param>
    /// <param name="warnings">Optional validation warnings.</param>
    /// <returns>A successful validation result.</returns>
    public static ValidationResult Success(
        IReadOnlyList<string> allPlaceholders,
        IReadOnlyList<string>? missingVariables = null,
        IReadOnlyList<ValidationWarning>? warnings = null)
    {
        return new ValidationResult
        {
            Errors = Array.Empty<ValidationError>(),
            Warnings = warnings ?? Array.Empty<ValidationWarning>(),
            AllPlaceholders = allPlaceholders,
            MissingVariables = missingVariables ?? Array.Empty<string>()
        };
    }

    /// <summary>
    /// Creates a failed validation result with errors.
    /// </summary>
    /// <param name="errors">The validation errors found.</param>
    /// <param name="allPlaceholders">All placeholders found in the template.</param>
    /// <param name="missingVariables">Missing variables (if data was provided).</param>
    /// <param name="warnings">Optional validation warnings.</param>
    /// <returns>A failed validation result.</returns>
    public static ValidationResult Failure(
        IReadOnlyList<ValidationError> errors,
        IReadOnlyList<string> allPlaceholders,
        IReadOnlyList<string>? missingVariables = null,
        IReadOnlyList<ValidationWarning>? warnings = null)
    {
        return new ValidationResult
        {
            Errors = errors,
            Warnings = warnings ?? Array.Empty<ValidationWarning>(),
            AllPlaceholders = allPlaceholders,
            MissingVariables = missingVariables ?? Array.Empty<string>()
        };
    }
}

/// <summary>
/// Represents a validation error found in a template.
/// </summary>
public sealed class ValidationError
{
    /// <summary>
    /// Gets the type of validation error.
    /// </summary>
    public ValidationErrorType Type { get; init; }

    /// <summary>
    /// Gets the error message describing what went wrong.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the approximate location in the template where the error was found (optional).
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// Creates a new validation error.
    /// </summary>
    /// <param name="type">The type of error.</param>
    /// <param name="message">The error message.</param>
    /// <param name="location">The location in the template (optional).</param>
    /// <returns>A validation error.</returns>
    public static ValidationError Create(
        ValidationErrorType type,
        string message,
        string? location = null)
    {
        return new ValidationError
        {
            Type = type,
            Message = message,
            Location = location
        };
    }
}

/// <summary>
/// Defines the types of validation errors that can occur in a template.
/// </summary>
public enum ValidationErrorType
{
    /// <summary>
    /// A conditional start marker ({{#if}}) has no matching end marker ({{/if}}).
    /// </summary>
    UnmatchedConditionalStart,

    /// <summary>
    /// A conditional end marker ({{/if}}) has no matching start marker ({{#if}}).
    /// </summary>
    UnmatchedConditionalEnd,

    /// <summary>
    /// A loop start marker ({{#foreach}}) has no matching end marker ({{/foreach}}).
    /// </summary>
    UnmatchedLoopStart,

    /// <summary>
    /// A loop end marker ({{/foreach}}) has no matching start marker ({{#foreach}}).
    /// </summary>
    UnmatchedLoopEnd,

    /// <summary>
    /// A placeholder has invalid syntax (e.g., malformed expression).
    /// </summary>
    InvalidPlaceholderSyntax,

    /// <summary>
    /// A variable is referenced in the template but not provided in the data.
    /// </summary>
    MissingVariable,

    /// <summary>
    /// A conditional expression is invalid or cannot be evaluated.
    /// </summary>
    InvalidConditionalExpression
}

/// <summary>
/// Represents a validation warning found in a template.
/// Warnings indicate potential issues that don't prevent processing.
/// </summary>
public sealed class ValidationWarning
{
    /// <summary>
    /// Gets the type of validation warning.
    /// </summary>
    public ValidationWarningType Type { get; init; }

    /// <summary>
    /// Gets the warning message describing the potential issue.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the approximate location in the template where the warning was found (optional).
    /// </summary>
    public string? Location { get; init; }

    /// <summary>
    /// Creates a new validation warning.
    /// </summary>
    /// <param name="type">The type of warning.</param>
    /// <param name="message">The warning message.</param>
    /// <param name="location">The location in the template (optional).</param>
    /// <returns>A validation warning.</returns>
    public static ValidationWarning Create(
        ValidationWarningType type,
        string message,
        string? location = null)
    {
        return new ValidationWarning
        {
            Type = type,
            Message = message,
            Location = location
        };
    }
}

/// <summary>
/// Defines the types of validation warnings that can occur in a template.
/// </summary>
public enum ValidationWarningType
{
    /// <summary>
    /// A loop collection is empty, so variables inside the loop could not be validated.
    /// </summary>
    EmptyLoopCollection
}
