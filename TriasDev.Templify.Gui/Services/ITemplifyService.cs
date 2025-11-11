using System;
using System.Threading.Tasks;
using TriasDev.Templify.Core;
using TriasDev.Templify.Gui.Models;

namespace TriasDev.Templify.Gui.Services;

/// <summary>
/// Service for Templify template operations.
/// </summary>
public interface ITemplifyService
{
    /// <summary>
    /// Validates a template file with optional JSON data.
    /// </summary>
    /// <param name="templatePath">Path to the template file (.docx).</param>
    /// <param name="jsonPath">Optional path to JSON data file for validation.</param>
    /// <returns>Validation result with errors and warnings.</returns>
    Task<ValidationResult> ValidateTemplateAsync(string templatePath, string? jsonPath = null);

    /// <summary>
    /// Processes a template with JSON data and generates output.
    /// </summary>
    /// <param name="templatePath">Path to the template file (.docx).</param>
    /// <param name="jsonPath">Path to JSON data file.</param>
    /// <param name="outputPath">Path for the output file.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <returns>Processing result with statistics and any errors.</returns>
    Task<UiProcessingResult> ProcessTemplateAsync(
        string templatePath,
        string jsonPath,
        string outputPath,
        IProgress<double>? progress = null);
}
