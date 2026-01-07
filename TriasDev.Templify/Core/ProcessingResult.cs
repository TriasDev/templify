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
    /// Gets a read-only list of warnings that occurred during processing.
    /// Warnings are non-fatal issues such as missing variables, null collections, or failed expressions.
    /// </summary>
    public IReadOnlyList<ProcessingWarning> Warnings { get; init; } = Array.Empty<ProcessingWarning>();

    /// <summary>
    /// Gets whether any warnings were generated during processing.
    /// </summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>
    /// Generates a Word document report containing all processing warnings.
    /// The report is formatted using Templify itself with an embedded template.
    /// </summary>
    /// <returns>A MemoryStream containing the .docx warning report. The caller is responsible for disposing the stream.</returns>
    /// <remarks>
    /// If there are no warnings, the report will still be generated but will show "0 warnings".
    /// The returned stream is positioned at the beginning and ready to be read or saved.
    /// </remarks>
    public MemoryStream GetWarningReport()
    {
        return WarningReportGenerator.GenerateReport(Warnings);
    }

    /// <summary>
    /// Generates a Word document report containing all processing warnings and returns it as a byte array.
    /// The report is formatted using Templify itself with an embedded template.
    /// </summary>
    /// <returns>A byte array containing the .docx warning report.</returns>
    /// <remarks>
    /// If there are no warnings, the report will still be generated but will show "0 warnings".
    /// This method is a convenience wrapper around <see cref="GetWarningReport"/> for scenarios
    /// where a byte array is more convenient than a stream.
    /// </remarks>
    public byte[] GetWarningReportBytes()
    {
        return WarningReportGenerator.GenerateReportBytes(Warnings);
    }

    /// <summary>
    /// Creates a successful processing result.
    /// </summary>
    public static ProcessingResult Success(
        int replacementCount,
        IReadOnlyList<string>? missingVariables = null,
        IReadOnlyList<ProcessingWarning>? warnings = null)
    {
        return new ProcessingResult
        {
            IsSuccess = true,
            ReplacementCount = replacementCount,
            MissingVariables = missingVariables ?? Array.Empty<string>(),
            Warnings = warnings ?? Array.Empty<ProcessingWarning>()
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
