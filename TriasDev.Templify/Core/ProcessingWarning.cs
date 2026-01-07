// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Core;

/// <summary>
/// Represents a warning that occurred during template processing.
/// Warnings are non-fatal issues that don't prevent processing but may indicate problems with the template or data.
/// </summary>
public sealed class ProcessingWarning
{
    /// <summary>
    /// Gets the type of warning.
    /// </summary>
    public ProcessingWarningType Type { get; }

    /// <summary>
    /// Gets a human-readable message describing the warning.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the name of the variable or collection involved, if applicable.
    /// </summary>
    public string? VariableName { get; }

    /// <summary>
    /// Gets additional context about where the warning occurred (e.g., "loop: Items", "conditional: IsActive").
    /// </summary>
    public string? Context { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingWarning"/> class.
    /// </summary>
    /// <param name="type">The type of warning.</param>
    /// <param name="message">A human-readable message describing the warning.</param>
    /// <param name="variableName">The name of the variable or collection involved, if applicable.</param>
    /// <param name="context">Additional context about where the warning occurred.</param>
    public ProcessingWarning(ProcessingWarningType type, string message, string? variableName = null, string? context = null)
    {
        Type = type;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        VariableName = variableName;
        Context = context;
    }

    /// <summary>
    /// Creates a warning for a missing variable in a placeholder.
    /// </summary>
    public static ProcessingWarning MissingVariable(string variableName)
    {
        return new ProcessingWarning(
            ProcessingWarningType.MissingVariable,
            $"Variable '{variableName}' was not found in the data.",
            variableName,
            "placeholder");
    }

    /// <summary>
    /// Creates a warning for a missing loop collection.
    /// </summary>
    public static ProcessingWarning MissingLoopCollection(string collectionName)
    {
        return new ProcessingWarning(
            ProcessingWarningType.MissingLoopCollection,
            $"Loop collection '{collectionName}' was not found in the data.",
            collectionName,
            $"loop: {collectionName}");
    }

    /// <summary>
    /// Creates a warning for a null loop collection.
    /// </summary>
    public static ProcessingWarning NullLoopCollection(string collectionName)
    {
        return new ProcessingWarning(
            ProcessingWarningType.NullLoopCollection,
            $"Loop collection '{collectionName}' is null.",
            collectionName,
            $"loop: {collectionName}");
    }

    /// <summary>
    /// Creates a warning for a failed expression evaluation.
    /// </summary>
    public static ProcessingWarning ExpressionFailed(string expression, string reason)
    {
        return new ProcessingWarning(
            ProcessingWarningType.ExpressionFailed,
            $"Expression '{expression}' could not be evaluated: {reason}",
            expression,
            "expression");
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        string contextPart = Context != null ? $" [{Context}]" : "";
        return $"{Type}{contextPart}: {Message}";
    }
}

/// <summary>
/// Defines the types of warnings that can occur during template processing.
/// </summary>
public enum ProcessingWarningType
{
    /// <summary>
    /// A placeholder variable was not found in the provided data.
    /// </summary>
    MissingVariable,

    /// <summary>
    /// A loop collection variable was not found in the provided data.
    /// </summary>
    MissingLoopCollection,

    /// <summary>
    /// A loop collection variable was found but its value is null.
    /// </summary>
    NullLoopCollection,

    /// <summary>
    /// An expression (e.g., in a conditional or placeholder) could not be evaluated.
    /// </summary>
    ExpressionFailed
}
