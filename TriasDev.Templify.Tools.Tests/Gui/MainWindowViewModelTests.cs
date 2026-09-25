// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using TriasDev.Templify.Core;
using TriasDev.Templify.Gui.ViewModels;

namespace TriasDev.Templify.Tools.Tests.Gui;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeTemplifyService _service = new();
    private readonly FakeFileDialogService _dialogs = new();
    private readonly FakeFileLauncher _launcher = new();

    public void Dispose() => _temp.Dispose();

    private MainWindowViewModel CreateViewModel() => new(_service, _dialogs, _launcher);

    private async Task<MainWindowViewModel> CreateReadyViewModelAsync()
    {
        MainWindowViewModel vm = CreateViewModel();
        _dialogs.TemplateFile = _temp.File("template.docx");
        _dialogs.JsonFile = _temp.File("data.json");
        File.WriteAllText(_dialogs.TemplateFile, "template");
        File.WriteAllText(_dialogs.JsonFile, "{}");
        await vm.BrowseTemplateCommand.ExecuteAsync(null);
        await vm.BrowseJsonCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public void Commands_InitialState_OnlyBrowseAndClearEnabled()
    {
        MainWindowViewModel vm = CreateViewModel();

        Assert.True(vm.BrowseTemplateCommand.CanExecute(null));
        Assert.True(vm.BrowseJsonCommand.CanExecute(null));
        Assert.True(vm.BrowseOutputCommand.CanExecute(null));
        Assert.True(vm.ClearCommand.CanExecute(null));
        Assert.False(vm.ValidateTemplateCommand.CanExecute(null));
        Assert.False(vm.ProcessTemplateCommand.CanExecute(null));
        Assert.False(vm.OpenOutputFileCommand.CanExecute(null));
        Assert.False(vm.GenerateWarningReportCommand.CanExecute(null));
        Assert.False(vm.OpenWarningReportCommand.CanExecute(null));
    }

    [Fact]
    public async Task BrowseTemplate_SetsDefaultOutputPathNextToTemplate()
    {
        MainWindowViewModel vm = CreateViewModel();
        _dialogs.TemplateFile = _temp.File("invoice.docx");

        await vm.BrowseTemplateCommand.ExecuteAsync(null);

        Assert.Equal(_temp.File("invoice-output.docx"), vm.OutputPath);
        Assert.True(vm.ValidateTemplateCommand.CanExecute(null));
        Assert.False(vm.ProcessTemplateCommand.CanExecute(null));
    }

    [Fact]
    public async Task BrowseJson_AfterUserChoseOutput_KeepsChosenOutputPath()
    {
        MainWindowViewModel vm = CreateViewModel();
        _dialogs.TemplateFile = _temp.File("invoice.docx");
        _dialogs.SaveFile = _temp.File("custom.docx");
        _dialogs.JsonFile = _temp.File("data.json");

        await vm.BrowseTemplateCommand.ExecuteAsync(null);
        await vm.BrowseOutputCommand.ExecuteAsync(null);
        await vm.BrowseJsonCommand.ExecuteAsync(null);

        Assert.Equal(_temp.File("custom.docx"), vm.OutputPath);
        Assert.True(vm.ProcessTemplateCommand.CanExecute(null));
    }

    [Fact]
    public async Task BrowseTemplate_WithoutUserOutput_UpdatesDefaultOutputPath()
    {
        MainWindowViewModel vm = CreateViewModel();
        _dialogs.TemplateFile = _temp.File("first.docx");
        await vm.BrowseTemplateCommand.ExecuteAsync(null);

        _dialogs.TemplateFile = _temp.File("second.docx");
        await vm.BrowseTemplateCommand.ExecuteAsync(null);

        Assert.Equal(_temp.File("second-output.docx"), vm.OutputPath);
    }

    [Fact]
    public async Task Clear_ResetsUserChosenOutput()
    {
        MainWindowViewModel vm = CreateViewModel();
        _dialogs.SaveFile = _temp.File("custom.docx");
        await vm.BrowseOutputCommand.ExecuteAsync(null);

        vm.ClearCommand.Execute(null);
        _dialogs.TemplateFile = _temp.File("invoice.docx");
        await vm.BrowseTemplateCommand.ExecuteAsync(null);

        Assert.Equal(_temp.File("invoice-output.docx"), vm.OutputPath);
    }

    [Fact]
    public async Task OutputNotice_ExistingOutputFile_WarnsAboutOverwrite()
    {
        File.WriteAllText(_temp.File("invoice-output.docx"), "existing");
        MainWindowViewModel vm = CreateViewModel();
        _dialogs.TemplateFile = _temp.File("invoice.docx");

        await vm.BrowseTemplateCommand.ExecuteAsync(null);

        Assert.True(vm.HasOutputNotice);
        Assert.Contains("overwritten", vm.OutputNotice);
    }

    [Fact]
    public async Task OutputNotice_NewOutputFile_NoNotice()
    {
        MainWindowViewModel vm = CreateViewModel();
        _dialogs.TemplateFile = _temp.File("invoice.docx");

        await vm.BrowseTemplateCommand.ExecuteAsync(null);

        Assert.False(vm.HasOutputNotice);
    }

    [Fact]
    public async Task ProcessTemplate_Success_EnablesOpenOutputFile()
    {
        MainWindowViewModel vm = await CreateReadyViewModelAsync();
        bool canExecuteChangedRaised = false;
        vm.OpenOutputFileCommand.CanExecuteChanged += (_, _) => canExecuteChangedRaised = true;
        Assert.False(vm.OpenOutputFileCommand.CanExecute(null));

        await vm.ProcessTemplateCommand.ExecuteAsync(null);

        Assert.True(canExecuteChangedRaised);
        Assert.True(vm.OpenOutputFileCommand.CanExecute(null));
        Assert.Equal("Processing complete", vm.StatusMessage);
        Assert.Contains(vm.Results, r => r.Contains("processed successfully"));

        vm.OpenOutputFileCommand.Execute(null);
        Assert.Equal([vm.OutputPath!], _launcher.OpenedFiles);
    }

    [Fact]
    public async Task ProcessTemplate_Failure_SurfacesErrorMessage()
    {
        MainWindowViewModel vm = await CreateReadyViewModelAsync();
        _service.ProcessingResult = ProcessingResult.Failure("Boom: template is broken");

        await vm.ProcessTemplateCommand.ExecuteAsync(null);

        Assert.Equal("Processing failed", vm.StatusMessage);
        Assert.Contains(vm.Results, r => r.Contains("Boom: template is broken"));
        Assert.False(vm.OpenOutputFileCommand.CanExecute(null));
        Assert.False(vm.IsProcessing);
    }

    [Fact]
    public async Task ProcessTemplate_ServiceThrows_SurfacesErrorMessage()
    {
        MainWindowViewModel vm = await CreateReadyViewModelAsync();
        _service.ProcessException = new InvalidOperationException("Unexpected failure");

        await vm.ProcessTemplateCommand.ExecuteAsync(null);

        Assert.Equal("Processing failed", vm.StatusMessage);
        Assert.Contains(vm.Results, r => r.Contains("Unexpected failure"));
        Assert.False(vm.IsProcessing);
    }

    [Fact]
    public async Task ProcessTemplate_OutputEqualsTemplate_ShowsFriendlyErrorWithoutProcessing()
    {
        MainWindowViewModel vm = await CreateReadyViewModelAsync();
        _dialogs.SaveFile = vm.TemplatePath;
        await vm.BrowseOutputCommand.ExecuteAsync(null);

        await vm.ProcessTemplateCommand.ExecuteAsync(null);

        Assert.Equal(0, _service.ProcessCallCount);
        Assert.Equal("Invalid output file", vm.StatusMessage);
        Assert.Contains(vm.Results, r => r.Contains("different from the template"));
    }

    [Fact]
    public async Task ProcessTemplate_WhileRunning_DisablesBrowseClearAndProcess()
    {
        MainWindowViewModel vm = await CreateReadyViewModelAsync();
        _service.Gate = new TaskCompletionSource();

        Task running = vm.ProcessTemplateCommand.ExecuteAsync(null);

        Assert.True(vm.IsProcessing);
        Assert.True(vm.IsProgressIndeterminate);
        Assert.False(vm.BrowseTemplateCommand.CanExecute(null));
        Assert.False(vm.BrowseJsonCommand.CanExecute(null));
        Assert.False(vm.BrowseOutputCommand.CanExecute(null));
        Assert.False(vm.ClearCommand.CanExecute(null));
        Assert.False(vm.ValidateTemplateCommand.CanExecute(null));
        Assert.False(vm.ProcessTemplateCommand.CanExecute(null));

        _service.Gate.SetResult();
        await running;

        Assert.False(vm.IsProcessing);
        Assert.False(vm.IsProgressIndeterminate);
        Assert.True(vm.BrowseTemplateCommand.CanExecute(null));
        Assert.True(vm.ClearCommand.CanExecute(null));
        Assert.True(vm.ProcessTemplateCommand.CanExecute(null));
    }

    [Fact]
    public void IsProgressIndeterminate_ProgressReported_IsDeterminate()
    {
        MainWindowViewModel vm = CreateViewModel();

        vm.IsProcessing = true;
        Assert.True(vm.IsProgressIndeterminate);

        vm.Progress = 0.5;
        Assert.False(vm.IsProgressIndeterminate);
    }

    [Fact]
    public async Task GenerateWarningReport_SavesReportAndOffersToOpenIt()
    {
        MainWindowViewModel vm = await CreateReadyViewModelAsync();
        _service.ProcessingResult = ProcessingResult.Success(
            1,
            warnings: [ProcessingWarning.MissingVariable("Customer.Name")]);
        await vm.ProcessTemplateCommand.ExecuteAsync(null);
        Assert.True(vm.GenerateWarningReportCommand.CanExecute(null));

        string reportPath = _temp.File("warnings.docx");
        _dialogs.SaveFile = reportPath;
        await vm.GenerateWarningReportCommand.ExecuteAsync(null);

        Assert.True(File.Exists(reportPath));
        Assert.True(new FileInfo(reportPath).Length > 0);
        Assert.Equal(reportPath, vm.LastWarningReportPath);
        Assert.Empty(_launcher.OpenedFiles); // not auto-opened
        Assert.True(vm.OpenWarningReportCommand.CanExecute(null));

        vm.OpenWarningReportCommand.Execute(null);
        Assert.Equal([reportPath], _launcher.OpenedFiles);
    }

    [Fact]
    public async Task GenerateWarningReport_NoWarnings_IsDisabled()
    {
        MainWindowViewModel vm = await CreateReadyViewModelAsync();

        await vm.ProcessTemplateCommand.ExecuteAsync(null);

        Assert.False(vm.GenerateWarningReportCommand.CanExecute(null));
    }

    [Fact]
    public async Task ValidateTemplate_InvalidTemplate_ListsErrors()
    {
        MainWindowViewModel vm = await CreateReadyViewModelAsync();
        _service.ValidationResult = ValidationResult.Failure(
            [ValidationError.Create(ValidationErrorType.UnmatchedConditionalStart, "Unclosed if block")],
            []);

        await vm.ValidateTemplateCommand.ExecuteAsync(null);

        Assert.Equal("Validation failed", vm.StatusMessage);
        Assert.Contains(vm.Results, r => r.Contains("Unclosed if block"));
    }

    [Fact]
    public void AvailableCultures_StartsWithInvariantAndSelectsIt()
    {
        MainWindowViewModel vm = CreateViewModel();

        Assert.NotEmpty(vm.AvailableCultures);
        Assert.Equal(CultureInfo.InvariantCulture, vm.AvailableCultures[0].Culture);
        Assert.Same(vm.AvailableCultures[0], vm.SelectedCulture);
    }

    [Fact]
    public void DesignViewModel_CanBeCreatedWithoutServices()
    {
        DesignMainWindowViewModel vm = new();

        Assert.NotNull(vm.TemplatePath);
    }
}
