// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
    private readonly IFileLauncher _fileLauncher;

    /// <summary>
    /// True once the user explicitly picked an output file; the default output path
    /// is then no longer derived from the template path.
    /// </summary>
    private bool _outputPathChosenByUser;

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

    /// <summary>
    /// Informational message about the output file (e.g. that an existing file will be overwritten).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutputNotice))]
    private string? _outputNotice;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ValidateTemplateCommand))]
    [NotifyCanExecuteChangedFor(nameof(ProcessTemplateCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseTemplateCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseJsonCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseOutputCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenOutputFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerateWarningReportCommand))]
    [NotifyPropertyChangedFor(nameof(IsProgressIndeterminate))]
    private bool _isProcessing;

    /// <summary>
    /// Progress of the current operation in the range 0..1.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProgressIndeterminate))]
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
    /// Available cultures for formatting.
    /// </summary>
    public ObservableCollection<CultureOption> AvailableCultures { get; } = new(CreateCultureOptions());

    /// <summary>
    /// Gets or sets the selected culture for formatting.
    /// </summary>
    [ObservableProperty]
    private CultureOption _selectedCulture = null!;

    /// <summary>
    /// Stores the last processing result to enable warning report generation.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateWarningReportCommand))]
    private UiProcessingResult? _lastProcessingResult;

    /// <summary>
    /// Path of the most recently saved warning report, if any.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenWarningReportCommand))]
    private string? _lastWarningReportPath;

    public MainWindowViewModel(
        ITemplifyService templifyService,
        IFileDialogService fileDialogService,
        IFileLauncher fileLauncher)
    {
        _templifyService = templifyService;
        _fileDialogService = fileDialogService;
        _fileLauncher = fileLauncher;
        _selectedCulture = AvailableCultures[0];
    }

    /// <summary>
    /// True while an operation runs that has not reported any progress yet.
    /// </summary>
    public bool IsProgressIndeterminate => IsProcessing && Progress <= 0;

    /// <summary>
    /// Whether <see cref="OutputNotice"/> contains a message.
    /// </summary>
    public bool HasOutputNotice => !string.IsNullOrEmpty(OutputNotice);

    partial void OnOutputPathChanged(string? value) => UpdateOutputNotice();

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private async Task BrowseTemplateAsync()
    {
        string? selectedFile = await _fileDialogService.OpenTemplateFileAsync();
        if (selectedFile != null)
        {
            TemplatePath = selectedFile;
            MatchChosenOutputExtension();
            UpdateOutputPath();
        }
    }

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private async Task BrowseJsonAsync()
    {
        string? selectedFile = await _fileDialogService.OpenJsonFileAsync();
        if (selectedFile != null)
        {
            JsonPath = selectedFile;
            UpdateOutputPath();
        }
    }

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private async Task BrowseOutputAsync()
    {
        string defaultName = GenerateOutputFileName();
        string? selectedFile = await _fileDialogService.SaveOutputFileAsync(defaultName);
        if (selectedFile != null)
        {
            // The native save dialog already asks for overwrite confirmation.
            _outputPathChosenByUser = true;
            OutputPath = selectedFile;
        }
    }

    private bool CanBrowse() => !IsProcessing;

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
                EnableHtmlEntityReplacement,
                SelectedCulture.Culture);

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

        if (TemplifyService.PathsAreEqual(TemplatePath, OutputPath))
        {
            Results.Clear();
            Results.Add("✗ The output file must be different from the template file. Choose another output file.");
            StatusMessage = "Invalid output file";
            return;
        }

        if (TemplifyService.PathsAreEqual(JsonPath, OutputPath))
        {
            Results.Clear();
            Results.Add("✗ The output file must be different from the JSON data file. Choose another output file.");
            StatusMessage = "Invalid output file";
            return;
        }

        IsProcessing = true;
        StatusMessage = "Processing template...";
        Results.Clear();
        Progress = 0;
        LastWarningReportPath = null;

        try
        {
            Progress<double> progressReporter = new Progress<double>(p => Progress = p);

            UiProcessingResult result = await _templifyService.ProcessTemplateAsync(
                TemplatePath,
                JsonPath,
                OutputPath,
                EnableHtmlEntityReplacement,
                SelectedCulture.Culture,
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

            // The output file may have been created (or replaced) by this run.
            OpenOutputFileCommand.NotifyCanExecuteChanged();
            UpdateOutputNotice();
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
            _fileLauncher.Open(OutputPath);
        }
        catch (Exception ex)
        {
            Results.Add($"✗ Failed to open output file: {ex.Message}");
        }
    }

    private bool CanOpenOutput() => !IsProcessing && !string.IsNullOrEmpty(OutputPath) && File.Exists(OutputPath);

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear()
    {
        _outputPathChosenByUser = false;
        TemplatePath = null;
        JsonPath = null;
        OutputPath = null;
        Results.Clear();
        StatusMessage = "Ready";
        Progress = 0;
        LastProcessingResult = null;
        LastWarningReportPath = null;
        SelectedCulture = AvailableCultures[0];
    }

    private bool CanClear() => !IsProcessing;

    [RelayCommand(CanExecute = nameof(CanGenerateWarningReport))]
    private async Task GenerateWarningReportAsync()
    {
        if (LastProcessingResult == null || !LastProcessingResult.Processing.HasWarnings)
        {
            return;
        }

        UiProcessingResult processingResult = LastProcessingResult;

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

            IsProcessing = true;
            StatusMessage = "Generating warning report...";

            // Report generation builds a Word document; keep it off the UI thread.
            byte[] reportBytes = await Task.Run(() => processingResult.Processing.GetWarningReportBytes());
            await File.WriteAllBytesAsync(savePath, reportBytes);

            LastWarningReportPath = savePath;
            Results.Add($"✓ Warning report saved to: {savePath}");
            Results.Add("  (Use 'Open Warning Report' to view it)");
            StatusMessage = "Warning report generated";
        }
        catch (Exception ex)
        {
            Results.Add($"✗ Failed to generate warning report: {ex.Message}");
            StatusMessage = "Warning report failed";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private bool CanGenerateWarningReport() =>
        !IsProcessing &&
        LastProcessingResult != null &&
        LastProcessingResult.Success &&
        LastProcessingResult.Processing.HasWarnings;

    [RelayCommand(CanExecute = nameof(CanOpenWarningReport))]
    private void OpenWarningReport()
    {
        if (string.IsNullOrEmpty(LastWarningReportPath))
        {
            return;
        }

        try
        {
            _fileLauncher.Open(LastWarningReportPath);
        }
        catch (Exception ex)
        {
            Results.Add($"✗ Failed to open warning report: {ex.Message}");
        }
    }

    private bool CanOpenWarningReport() => !string.IsNullOrEmpty(LastWarningReportPath);

    private void UpdateOutputPath()
    {
        // Never override an output file the user picked explicitly.
        if (_outputPathChosenByUser || string.IsNullOrEmpty(TemplatePath))
        {
            return;
        }

        string dir = Path.GetDirectoryName(TemplatePath) ?? ".";
        string filename = GenerateOutputFileName();
        OutputPath = Path.Combine(dir, filename);
    }

    /// <summary>
    /// Keeps a user-chosen output path but switches its extension between .docx and .odt when the new template
    /// produces the other format (an OpenDocument template never produces a .docx file, and vice versa).
    /// </summary>
    private void MatchChosenOutputExtension()
    {
        if (!_outputPathChosenByUser || string.IsNullOrEmpty(OutputPath))
        {
            return;
        }

        string expected = TemplifyService.GetOutputExtension(TemplatePath);
        string current = Path.GetExtension(OutputPath);
        bool isDocumentExtension = current.Equals(TemplifyService.DocxExtension, StringComparison.OrdinalIgnoreCase)
            || current.Equals(TemplifyService.OdtExtension, StringComparison.OrdinalIgnoreCase);
        if (isDocumentExtension && !current.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            OutputPath = Path.ChangeExtension(OutputPath, expected);
        }
    }

    private void UpdateOutputNotice()
    {
        OutputNotice = !string.IsNullOrEmpty(OutputPath) && File.Exists(OutputPath)
            ? "⚠ The output file already exists and will be overwritten."
            : null;
    }

    private string GenerateOutputFileName()
    {
        if (!string.IsNullOrEmpty(TemplatePath))
        {
            string templateName = Path.GetFileNameWithoutExtension(TemplatePath);
            return $"{templateName}-output{TemplifyService.GetOutputExtension(TemplatePath)}";
        }

        return "output.docx";
    }

    /// <summary>
    /// Builds the culture list. Cultures that are unavailable (e.g. when running with
    /// invariant globalization) are skipped instead of crashing the application.
    /// </summary>
    private static List<CultureOption> CreateCultureOptions()
    {
        List<CultureOption> options = [new CultureOption("Invariant", CultureInfo.InvariantCulture)];

        (string DisplayName, string Name)[] candidates =
        [
            ("English (US)", "en-US"),
            ("German (DE)", "de-DE"),
            ("French (FR)", "fr-FR"),
            ("Spanish (ES)", "es-ES"),
        ];

        foreach ((string displayName, string name) in candidates)
        {
            try
            {
                options.Add(new CultureOption(displayName, CultureInfo.GetCultureInfo(name)));
            }
            catch (CultureNotFoundException)
            {
                // Culture data is not available on this system; skip it.
            }
        }

        return options;
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
