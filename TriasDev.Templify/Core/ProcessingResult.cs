// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Core;

/// <summary>
/// Represents the result of a template processing operation.
/// </summary>
public sealed class ProcessingResult
{
    /// <summary>
    /// Gets whether the processing completed successfully.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the total number of placeholders that were replaced.
    /// </summary>
    public int ReplacementCount { get; init; }

    /// <summary>
    /// Gets the error message if processing failed; otherwise, null.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets a read-only list of variable names that were found in the template
    /// but not present in the data dictionary.
    /// </summary>
    public IReadOnlyList<string> MissingVariables { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Creates a successful processing result.
    /// </summary>
    public static ProcessingResult Success(int replacementCount, IReadOnlyList<string>? missingVariables = null)
    {
        return new ProcessingResult
        {
            IsSuccess = true,
            ReplacementCount = replacementCount,
            MissingVariables = missingVariables ?? Array.Empty<string>()
        };
    }

    /// <summary>
    /// Creates a failed processing result.
    /// </summary>
    public static ProcessingResult Failure(string errorMessage)
    {
        return new ProcessingResult
        {
            IsSuccess = false,
            ReplacementCount = 0,
            ErrorMessage = errorMessage
        };
    }
}
