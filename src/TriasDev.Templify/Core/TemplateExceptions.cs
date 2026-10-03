// Copyright (c) 2026 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Core;

/// <summary>
/// Thrown when a variable cannot be resolved and <see cref="MissingVariableBehavior.ThrowException"/> is configured.
/// </summary>
/// <remarks>
/// This is the only processing error that the template processors re-throw; every other error is returned
/// as a failed result. The processors surface it as a plain <see cref="InvalidOperationException"/>
/// (see <see cref="ToPublicException"/>) so the exception type and message seen by callers are unchanged.
/// </remarks>
internal sealed class MissingVariableException : InvalidOperationException
{
    public MissingVariableException(string variableName, string message)
        : base(message)
    {
        VariableName = variableName;
    }

    /// <summary>Gets the name of the variable (or expression) that could not be resolved.</summary>
    public string VariableName { get; }

    /// <summary>
    /// Creates the exception surfaced to callers: a plain <see cref="InvalidOperationException"/> with the same
    /// message (the historical public contract), with this exception as <see cref="Exception.InnerException"/>.
    /// </summary>
    public InvalidOperationException ToPublicException() => new InvalidOperationException(Message, this);
}

/// <summary>
/// Thrown when a template is structurally invalid (unmatched markers, misplaced branches, invalid loop syntax).
/// </summary>
/// <remarks>
/// Derives from <see cref="InvalidOperationException"/> for backward compatibility with code that inspects
/// the detectors directly. The template processors convert it into a failed result.
/// </remarks>
internal sealed class TemplateSyntaxException : InvalidOperationException
{
    public TemplateSyntaxException(ValidationErrorType errorType, string message)
        : base(message)
    {
        ErrorType = errorType;
    }

    /// <summary>Gets the validation error category this syntax error maps to.</summary>
    public ValidationErrorType ErrorType { get; }
}

/// <summary>
/// Thrown when the data does not fit the template (e.g. a loop over a value that is not a collection).
/// </summary>
/// <remarks>
/// Derives from <see cref="InvalidOperationException"/> for backward compatibility. The template processors
/// convert it into a failed result.
/// </remarks>
internal sealed class TemplateDataException : InvalidOperationException
{
    public TemplateDataException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Thrown before anything is written when the output stream holds earlier content after its position that the
/// document would not overwrite, and the stream cannot be truncated (<see cref="Stream.SetLength"/> is not
/// supported). Writing would leave trailing bytes of the earlier content, i.e. a corrupt file.
/// </summary>
/// <remarks>The template processors convert it into a failed result with its message.</remarks>
internal sealed class OutputStreamNotTruncatableException : IOException
{
    internal const string DefaultMessage =
        "Invalid output stream: the output stream has earlier content after the current position that would remain "
        + "after the document, and it cannot be truncated (SetLength is not supported). Pass an empty output stream "
        + "or one that supports SetLength (for example a FileStream opened with FileMode.Create).";

    public OutputStreamNotTruncatableException(Exception innerException)
        : base(DefaultMessage, innerException)
    {
    }

    /// <summary>
    /// Truncates a seekable <paramref name="output"/> at its current position.
    /// </summary>
    /// <exception cref="OutputStreamNotTruncatableException">The stream does not support <see cref="Stream.SetLength"/>.</exception>
    public static void TruncateAtPosition(Stream output)
    {
        try
        {
            output.SetLength(output.Position);
        }
        catch (NotSupportedException ex)
        {
            throw new OutputStreamNotTruncatableException(ex);
        }
    }
}
