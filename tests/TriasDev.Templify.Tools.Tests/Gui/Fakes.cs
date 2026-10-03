// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Core;
using TriasDev.Templify.Gui.Models;
using TriasDev.Templify.Gui.Services;

namespace TriasDev.Templify.Tools.Tests.Gui;

internal sealed class FakeTemplifyService : ITemplifyService
{
    /// <summary>Result returned by <see cref="ProcessTemplateAsync"/>; defaults to success.</summary>
    public ProcessingResult ProcessingResult { get; set; } = ProcessingResult.Success(3);

    /// <summary>When set, <see cref="ProcessTemplateAsync"/> throws this exception.</summary>
    public Exception? ProcessException { get; set; }

    /// <summary>When set, processing waits for this task before completing.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>Whether a successful run writes the output file.</summary>
    public bool WriteOutputOnSuccess { get; set; } = true;

    public int ProcessCallCount { get; private set; }

    public ValidationResult ValidationResult { get; set; } = new();

    public Task<ValidationResult> ValidateTemplateAsync(
        string templatePath,
        string? jsonPath = null,
        bool enableHtmlEntityReplacement = false,
        CultureInfo? culture = null) => Task.FromResult(ValidationResult);

    public async Task<UiProcessingResult> ProcessTemplateAsync(
        string templatePath,
        string jsonPath,
        string outputPath,
        bool enableHtmlEntityReplacement = false,
        CultureInfo? culture = null,
        IProgress<double>? progress = null)
    {
        ProcessCallCount++;

        if (Gate != null)
        {
            await Gate.Task;
        }

        if (ProcessException != null)
        {
            throw ProcessException;
        }

        if (ProcessingResult.IsSuccess && WriteOutputOnSuccess)
        {
            await File.WriteAllTextAsync(outputPath, "output");
        }

        return new UiProcessingResult { OutputPath = outputPath, Processing = ProcessingResult };
    }
}

internal sealed class FakeFileDialogService : IFileDialogService
{
    public string? TemplateFile { get; set; }

    public string? JsonFile { get; set; }

    public string? SaveFile { get; set; }

    public Task<string?> OpenTemplateFileAsync() => Task.FromResult(TemplateFile);

    public Task<string?> OpenJsonFileAsync() => Task.FromResult(JsonFile);

    public Task<string?> SaveOutputFileAsync(string defaultName) => Task.FromResult(SaveFile);
}

internal sealed class FakeFileLauncher : IFileLauncher
{
    public List<string> OpenedFiles { get; } = new();

    public void Open(string path) => OpenedFiles.Add(path);
}

/// <summary>
/// Creates a unique temporary directory that is deleted on dispose.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "templify-tools-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
