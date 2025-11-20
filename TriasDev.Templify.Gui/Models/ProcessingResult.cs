using CoreProcessingResult = TriasDev.Templify.Core.ProcessingResult;
using CoreValidationResult = TriasDev.Templify.Core.ValidationResult;

namespace TriasDev.Templify.Gui.Models;

/// <summary>
/// Represents the combined result of validating and processing a template with data in the GUI.
/// </summary>
public class UiProcessingResult
{
    /// <summary>
    /// Template validation result.
    /// </summary>
    public CoreValidationResult Validation { get; set; } = new();

    /// <summary>
    /// Processing result from Templify.
    /// </summary>
    public CoreProcessingResult Processing { get; set; } = CoreProcessingResult.Failure("Not processed");

    /// <summary>
    /// Whether the processing was successful.
    /// </summary>
    public bool Success => Processing.IsSuccess;

    /// <summary>
    /// Path to the generated output file.
    /// </summary>
    public string? OutputPath { get; set; }
}
