// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Threading.Tasks;
using TriasDev.Templify.Core;
using TriasDev.Templify.Gui.Models;
using TriasDev.Templify.Gui.Services;

namespace TriasDev.Templify.Gui.ViewModels;

/// <summary>
/// Design-time view model for the XAML previewer (<c>Design.DataContext</c>).
/// Uses no-op services so that the designer can instantiate it without DI.
/// </summary>
public class DesignMainWindowViewModel : MainWindowViewModel
{
    public DesignMainWindowViewModel()
        : base(new DesignTemplifyService(), new DesignFileDialogService(), new DesignFileLauncher())
    {
        TemplatePath = "/path/to/template.docx";
        JsonPath = "/path/to/data.json";
        OutputPath = "/path/to/template-output.docx";
        Results.Add("✓ Template processed successfully!");
    }

    private sealed class DesignTemplifyService : ITemplifyService
    {
        public Task<ValidationResult> ValidateTemplateAsync(
            string templatePath,
            string? jsonPath = null,
            bool enableHtmlEntityReplacement = false,
            CultureInfo? culture = null) => Task.FromResult(new ValidationResult());

        public Task<UiProcessingResult> ProcessTemplateAsync(
            string templatePath,
            string jsonPath,
            string outputPath,
            bool enableHtmlEntityReplacement = false,
            CultureInfo? culture = null,
            IProgress<double>? progress = null) => Task.FromResult(new UiProcessingResult());
    }

    private sealed class DesignFileDialogService : IFileDialogService
    {
        public Task<string?> OpenTemplateFileAsync() => Task.FromResult<string?>(null);

        public Task<string?> OpenJsonFileAsync() => Task.FromResult<string?>(null);

        public Task<string?> SaveOutputFileAsync(string defaultName) => Task.FromResult<string?>(null);
    }

    private sealed class DesignFileLauncher : IFileLauncher
    {
        public void Open(string path)
        {
        }
    }
}
