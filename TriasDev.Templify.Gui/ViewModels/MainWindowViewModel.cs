// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TriasDev.Templify.Core;
using TriasDev.Templify.Gui.Models;
using TriasDev.Templify.Gui.Services;

namespace TriasDev.Templify.Gui.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ITemplifyService _templifyService;
    private readonly IFileDialogService _fileDialogService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ValidateTemplateCommand))]
    [NotifyCanExecuteChangedFor(nameof(ProcessTemplateCommand))]
    private string? _templatePath;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ProcessTemplateCommand))]
    private string? _jsonPath;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ProcessTemplateCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenOutputFileCommand))]
    private string? _outputPath;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ValidateTemplateCommand))]
    [NotifyCanExecuteChangedFor(nameof(ProcessTemplateCommand))]
    private bool _isProcessing;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private ObservableCollection<string> _results = new();

    /// <summary>
    /// Gets or sets whether HTML entity replacement is enabled.
    /// When enabled, HTML entities like &lt;br&gt;, &amp;nbsp;, etc. are converted
    /// to their Word equivalents before processing.
    /// </summary>
    [ObservableProperty]
    private bool _enableHtmlEntityReplacement;

    /// <summary>
    /// Stores the last processing result to enable warning report generation.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateWarningReportCommand))]
    private UiProcessingResult? _lastProcessingResult;

    public MainWindowViewModel(
        ITemplifyService templifyService,
        IFileDialogService fileDialogService)
    {
        _templifyService = templifyService;
        _fileDialogService = fileDialogService;
    }

    [RelayCommand]
    private async Task BrowseTemplateAsync()
    {
        string? selectedFile = await _fileDialogService.OpenTemplateFileAsync();
        if (selectedFile != null)
        {
            TemplatePath = selectedFile;
            UpdateOutputPath();
        }
    }

    [RelayCommand]
    private async Task BrowseJsonAsync()
    {
        string? selectedFile = await _fileDialogService.OpenJsonFileAsync();
        if (selectedFile != null)
        {
            JsonPath = selectedFile;
            UpdateOutputPath();
        }
    }

    [RelayCommand]
    private async Task BrowseOutputAsync()
    {
        string defaultName = GenerateOutputFileName();
        string? selectedFile = await _fileDialogService.SaveOutputFileAsync(defaultName);
        if (selectedFile != null)
        {
            OutputPath = selectedFile;
        }
    }

    [RelayCommand(CanExecute = nameof(CanValidate))]
    private async Task ValidateTemplateAsync()
    {
        if (string.IsNullOrEmpty(TemplatePath))
        {
            return;
        }

        IsProcessing = true;
        StatusMessage = "Validating template...";
        Results.Clear();

        try
        {
            ValidationResult validation = await _templifyService.ValidateTemplateAsync(
                TemplatePath,
                JsonPath,
                EnableHtmlEntityReplacement);

            if (validation.IsValid)
            {
                Results.Add("✓ Template is valid!");
                Results.Add($"✓ Found {validation.AllPlaceholders.Count} placeholders");

                if (validation.AllPlaceholders.Count > 0)
                {
                    Results.Add($"  Placeholders: {string.Join(", ", validation.AllPlaceholders.Take(10))}");
                    if (validation.AllPlaceholders.Count > 10)
                    {
                        Results.Add($"  ... and {validation.AllPlaceholders.Count - 10} more");
                    }
                }
            }
            else
            {
                Results.Add("✗ Template has validation errors:");
                foreach (ValidationError error in validation.Errors)
                {
                    Results.Add($"  - {error.Type}: {error.Message}");
                }
            }

            if (!string.IsNullOrEmpty(JsonPath) && validation.MissingVariables.Count > 0)
            {
                Results.Add($"⚠ {validation.MissingVariables.Count} missing variables:");
                foreach (string missing in validation.MissingVariables.Take(5))
                {
                    Results.Add($"  - {missing}");
                }
                if (validation.MissingVariables.Count > 5)
                {
                    Results.Add($"  ... and {validation.MissingVariables.Count - 5} more");
                }
            }

            StatusMessage = validation.IsValid ? "Validation successful" : "Validation failed";
        }
        catch (Exception ex)
        {
            Results.Add($"✗ Error during validation: {ex.Message}");
            StatusMessage = "Validation failed";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private bool CanValidate() => !string.IsNullOrEmpty(TemplatePath) && !IsProcessing;

    [RelayCommand(CanExecute = nameof(CanProcess))]
    private async Task ProcessTemplateAsync()
    {
        if (string.IsNullOrEmpty(TemplatePath) || string.IsNullOrEmpty(JsonPath) || string.IsNullOrEmpty(OutputPath))
        {
            return;
        }

        IsProcessing = true;
        StatusMessage = "Processing template...";
        Results.Clear();
        Progress = 0;

        try
        {
            Progress<double> progressReporter = new Progress<double>(p => Progress = p);

            UiProcessingResult result = await _templifyService.ProcessTemplateAsync(
                TemplatePath,
                JsonPath,
                OutputPath,
                EnableHtmlEntityReplacement,
                progressReporter);

            // Store for warning report generation
            LastProcessingResult = result;

            if (result.Success)
            {
                Results.Add("✓ Template processed successfully!");
                Results.Add($"✓ Made {result.Processing.ReplacementCount} replacements");
                Results.Add($"✓ Output saved to: {result.OutputPath}");

                if (result.Processing.HasWarnings)
                {
                    Results.Add($"⚠ {result.Processing.Warnings.Count} processing warnings:");
                    foreach (ProcessingWarning warning in result.Processing.Warnings.Take(5))
                    {
                        string truncatedMessage = TruncateMessage(warning.Message, 60);
                        Results.Add($"  - {warning.Type}: {warning.VariableName} - {truncatedMessage}");
                    }
                    if (result.Processing.Warnings.Count > 5)
                    {
                        Results.Add($"  ... and {result.Processing.Warnings.Count - 5} more");
                    }
                    Results.Add("  (Use 'Generate Warning Report' for full details)");
                }

                StatusMessage = "Processing complete";
            }
            else
            {
                Results.Add($"✗ Processing failed: {result.Processing.ErrorMessage}");
                StatusMessage = "Processing failed";
            }
        }
        catch (Exception ex)
        {
            Results.Add($"✗ Error during processing: {ex.Message}");
            StatusMessage = "Processing failed";
        }
        finally
        {
            IsProcessing = false;
            Progress = 0;
        }
    }

    private bool CanProcess() =>
        !string.IsNullOrEmpty(TemplatePath) &&
        !string.IsNullOrEmpty(JsonPath) &&
        !string.IsNullOrEmpty(OutputPath) &&
        !IsProcessing;

    [RelayCommand(CanExecute = nameof(CanOpenOutput))]
    private void OpenOutputFile()
    {
        if (string.IsNullOrEmpty(OutputPath) || !File.Exists(OutputPath))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = OutputPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Results.Add($"✗ Failed to open output file: {ex.Message}");
        }
    }

    private bool CanOpenOutput() => !string.IsNullOrEmpty(OutputPath) && File.Exists(OutputPath);

    [RelayCommand]
    private void Clear()
    {
        TemplatePath = null;
        JsonPath = null;
        OutputPath = null;
        Results.Clear();
        StatusMessage = "Ready";
        Progress = 0;
        LastProcessingResult = null;
    }

    [RelayCommand(CanExecute = nameof(CanGenerateWarningReport))]
    private async Task GenerateWarningReportAsync()
    {
        if (LastProcessingResult == null || !LastProcessingResult.Processing.HasWarnings)
        {
            return;
        }

        try
        {
            // Generate default filename based on template name
            string defaultName = "warning-report.docx";
            if (!string.IsNullOrEmpty(TemplatePath))
            {
                string templateName = Path.GetFileNameWithoutExtension(TemplatePath);
                defaultName = $"{templateName}-warnings.docx";
            }

            // Ask user where to save
            string? savePath = await _fileDialogService.SaveOutputFileAsync(defaultName);
            if (string.IsNullOrEmpty(savePath))
            {
                return; // User cancelled
            }

            // Generate and save the report
            byte[] reportBytes = LastProcessingResult.Processing.GetWarningReportBytes();
            await File.WriteAllBytesAsync(savePath, reportBytes);

            Results.Add($"✓ Warning report saved to: {savePath}");
            StatusMessage = "Warning report generated";

            // Offer to open the report
            Process.Start(new ProcessStartInfo
            {
                FileName = savePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Results.Add($"✗ Failed to generate warning report: {ex.Message}");
        }
    }

    private bool CanGenerateWarningReport() =>
        LastProcessingResult != null &&
        LastProcessingResult.Success &&
        LastProcessingResult.Processing.HasWarnings;

    private void UpdateOutputPath()
    {
        if (string.IsNullOrEmpty(TemplatePath))
        {
            return;
        }

        string dir = Path.GetDirectoryName(TemplatePath) ?? ".";
        string filename = GenerateOutputFileName();
        OutputPath = Path.Combine(dir, filename);
    }

    private string GenerateOutputFileName()
    {
        if (!string.IsNullOrEmpty(TemplatePath))
        {
            string templateName = Path.GetFileNameWithoutExtension(TemplatePath);
            return $"{templateName}-output.docx";
        }

        return "output.docx";
    }

    private static string TruncateMessage(string message, int maxLength)
    {
        if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
        {
            return message;
        }

        return message[..(maxLength - 3)] + "...";
    }
}
