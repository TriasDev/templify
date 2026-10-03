// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using TriasDev.Templify.Core;
using TriasDev.Templify.Gui.Models;
using TriasDev.Templify.Replacements;
using TriasDev.Templify.Utilities;

namespace TriasDev.Templify.Gui.Services;

/// <summary>
/// Service for Templify template operations. Word (.docx) and OpenDocument Text (.odt, .ott) templates are both
/// supported: <see cref="TemplateProcessor"/> detects the format from the file content.
/// </summary>
public class TemplifyService : ITemplifyService
{
    /// <summary>Output extension for Word templates.</summary>
    internal const string DocxExtension = ".docx";

    /// <summary>Output extension for OpenDocument templates (.odt and .ott both produce .odt).</summary>
    internal const string OdtExtension = ".odt";

    /// <summary>
    /// Validates a template file with optional JSON data.
    /// </summary>
    public async Task<ValidationResult> ValidateTemplateAsync(
        string templatePath,
        string? jsonPath = null,
        bool enableHtmlEntityReplacement = false,
        CultureInfo? culture = null)
    {
        return await Task.Run(() =>
        {
            CultureInfo effectiveCulture = culture ?? CultureInfo.InvariantCulture;
            PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
            {
                MissingVariableBehavior = MissingVariableBehavior.LeaveUnchanged,
                Culture = effectiveCulture,
                TextReplacements = enableHtmlEntityReplacement ? TextReplacements.HtmlEntities : null
            };

            TemplateProcessor processor = new TemplateProcessor(options);

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
        bool enableHtmlEntityReplacement = false,
        CultureInfo? culture = null,
        IProgress<double>? progress = null)
    {
        return await Task.Run(() =>
        {
            UiProcessingResult result = new UiProcessingResult
            {
                OutputPath = outputPath
            };

            if (PathsAreEqual(templatePath, outputPath))
            {
                result.Processing = ProcessingResult.Failure(
                    "The output file must be different from the template file.");
                return result;
            }

            // Write to a temporary file next to the output and only move it into place on success,
            // so that a failed run never leaves an empty or partial document behind.
            string outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".";
            string tempPath = Path.Combine(
                outputDirectory,
                $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                progress?.Report(0.1);

                // Load JSON data using JsonDataParser for proper nested object handling
                string json = File.ReadAllText(jsonPath);
                Dictionary<string, object> data = JsonDataParser.ParseJsonToDataDictionary(json);

                progress?.Report(0.3);

                // Configure options with optional HTML entity replacement
                CultureInfo effectiveCulture = culture ?? CultureInfo.InvariantCulture;
                PlaceholderReplacementOptions options = new PlaceholderReplacementOptions
                {
                    MissingVariableBehavior = MissingVariableBehavior.LeaveUnchanged,
                    Culture = effectiveCulture,
                    TextReplacements = enableHtmlEntityReplacement ? TextReplacements.HtmlEntities : null
                };

                TemplateProcessor processor = new TemplateProcessor(options);

                using (FileStream templateStream = File.OpenRead(templatePath))
                {
                    result.Validation = processor.ValidateTemplate(templateStream, data);
                }

                progress?.Report(0.5);

                // Process template into the temporary file
                using (FileStream templateStream = File.OpenRead(templatePath))
                using (FileStream outputStream = File.Create(tempPath))
                {
                    result.Processing = processor.ProcessTemplate(templateStream, outputStream, data);
                }

                progress?.Report(0.9);

                if (result.Processing.IsSuccess)
                {
                    File.Move(tempPath, outputPath, overwrite: true);
                }

                progress?.Report(1.0);
            }
            catch (Exception ex)
            {
                result.Processing = ProcessingResult.Failure(ex.Message);
                progress?.Report(1.0);
            }
            finally
            {
                TryDelete(tempPath);
            }

            return result;
        });
    }

    /// <summary>
    /// Returns the extension of the document produced from a template: <c>.odt</c> for an OpenDocument Text
    /// document or template, otherwise <c>.docx</c>. The format is detected from the file content; when the file
    /// cannot be read (or is not a supported format), the template's extension decides.
    /// </summary>
    internal static string GetOutputExtension(string? templatePath)
    {
        if (string.IsNullOrWhiteSpace(templatePath))
        {
            return DocxExtension;
        }

        try
        {
            if (File.Exists(templatePath))
            {
                using FileStream stream = File.OpenRead(templatePath);
                TemplateFormat format = TemplateProcessor.DetectFormat(stream);
                if (format != TemplateFormat.Unknown)
                {
                    return format == TemplateFormat.Odt ? OdtExtension : DocxExtension;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Fall back to the file name below.
        }

        string extension = Path.GetExtension(templatePath);
        return extension.Equals(".odt", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ott", StringComparison.OrdinalIgnoreCase)
            ? OdtExtension
            : DocxExtension;
    }

    /// <summary>
    /// Returns true if both paths point to the same file (compared case-insensitively,
    /// since the default file systems on Windows and macOS are case-insensitive).
    /// </summary>
    internal static bool PathsAreEqual(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(first),
                Path.GetFullPath(second),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup of the temporary file.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort cleanup of the temporary file.
        }
    }
}
