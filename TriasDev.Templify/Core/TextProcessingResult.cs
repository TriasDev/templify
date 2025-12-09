// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace TriasDev.Templify.Core;

/// <summary>
/// Represents the result of a text template processing operation.
/// </summary>
public sealed class TextProcessingResult
{
    /// <summary>
    /// Gets whether the processing completed successfully.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the processed text output. Empty if processing failed.
    /// </summary>
    public string ProcessedText { get; init; } = string.Empty;

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
    /// <param name="processedText">The processed text output.</param>
    /// <param name="replacementCount">The number of placeholders replaced.</param>
    /// <param name="missingVariables">Optional list of missing variable names.</param>
    /// <returns>A successful result.</returns>
    public static TextProcessingResult Success(
        string processedText,
        int replacementCount,
        IReadOnlyList<string>? missingVariables = null)
    {
        return new TextProcessingResult
        {
            IsSuccess = true,
            ProcessedText = processedText ?? string.Empty,
            ReplacementCount = replacementCount,
            MissingVariables = missingVariables ?? Array.Empty<string>()
        };
    }

    /// <summary>
    /// Creates a failed processing result.
    /// </summary>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <returns>A failed result.</returns>
    public static TextProcessingResult Failure(string errorMessage)
    {
        return new TextProcessingResult
        {
            IsSuccess = false,
            ProcessedText = string.Empty,
            ReplacementCount = 0,
            ErrorMessage = errorMessage
        };
    }
}
