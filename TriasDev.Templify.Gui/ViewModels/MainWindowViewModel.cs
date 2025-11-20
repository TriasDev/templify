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
            return;

        IsProcessing = true;
        StatusMessage = "Validating template...";
        Results.Clear();

        try
        {
            ValidationResult validation = await _templifyService.ValidateTemplateAsync(TemplatePath, JsonPath);

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
            return;

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
                progressReporter);

            if (result.Success)
            {
                Results.Add("✓ Template processed successfully!");
                Results.Add($"✓ Made {result.Processing.ReplacementCount} replacements");
                Results.Add($"✓ Output saved to: {result.OutputPath}");

                if (result.Validation.MissingVariables.Count > 0)
                {
                    Results.Add($"⚠ {result.Validation.MissingVariables.Count} missing variables:");
                    foreach (string missing in result.Validation.MissingVariables.Take(5))
                    {
                        Results.Add($"  - {missing}");
                    }
                    if (result.Validation.MissingVariables.Count > 5)
                    {
                        Results.Add($"  ... and {result.Validation.MissingVariables.Count - 5} more");
                    }
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
            return;

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
    }

    private void UpdateOutputPath()
    {
        if (string.IsNullOrEmpty(TemplatePath))
            return;

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
}
