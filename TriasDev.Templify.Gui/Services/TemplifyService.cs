using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using TriasDev.Templify.Core;
using TriasDev.Templify.Gui.Models;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Gui.Services;

/// <summary>
/// Service for Templify template operations.
/// </summary>
public class TemplifyService : ITemplifyService
{
    /// <summary>
    /// Validates a template file with optional JSON data.
    /// </summary>
    public async Task<ValidationResult> ValidateTemplateAsync(string templatePath, string? jsonPath = null)
    {
        return await Task.Run(() =>
        {
            PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
            {
                MissingVariableBehavior = MissingVariableBehavior.LeaveUnchanged,
                Culture = CultureInfo.InvariantCulture
            };

            DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

            using FileStream templateStream = File.OpenRead(templatePath);

            if (string.IsNullOrEmpty(jsonPath))
            {
                // Validate template syntax only
                return processor.ValidateTemplate(templateStream);
            }
            else
            {
                // Validate template with data
                string json = File.ReadAllText(jsonPath);
                Dictionary<string, object> data = JsonDataParser.ParseJsonToDataDictionary(json);

                return processor.ValidateTemplate(templateStream, data);
            }
        });
    }

    /// <summary>
    /// Processes a template with JSON data and generates output.
    /// </summary>
    public async Task<UiProcessingResult> ProcessTemplateAsync(
        string templatePath,
        string jsonPath,
        string outputPath,
        IProgress<double>? progress = null)
    {
        return await Task.Run(() =>
        {
            UiProcessingResult result = new UiProcessingResult
            {
                OutputPath = outputPath
            };

            try
            {
                progress?.Report(0.1);

                // Load JSON data using JsonDataParser for proper nested object handling
                string json = File.ReadAllText(jsonPath);
                Dictionary<string, object> data = JsonDataParser.ParseJsonToDataDictionary(json);

                progress?.Report(0.3);

                // Validate template first
                PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
                {
                    MissingVariableBehavior = MissingVariableBehavior.LeaveUnchanged,
                    Culture = CultureInfo.InvariantCulture
                };

                DocumentTemplateProcessor processor = new DocumentTemplateProcessor(options);

                using (FileStream templateStream = File.OpenRead(templatePath))
                {
                    result.Validation = processor.ValidateTemplate(templateStream, data);
                }

                progress?.Report(0.5);

                // Process template
                using (FileStream templateStream = File.OpenRead(templatePath))
                using (FileStream outputStream = File.Create(outputPath))
                {
                    result.Processing = processor.ProcessTemplate(templateStream, outputStream, data);
                }

                progress?.Report(1.0);
            }
            catch (Exception ex)
            {
                result.Processing = ProcessingResult.Failure(ex.Message);
                progress?.Report(1.0);
            }

            return result;
        });
    }
}
