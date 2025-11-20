// Copyright (c) 2025 TriasDev GmbH & Co. KG
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using TriasDev.Templify.Gui.Services;
using TriasDev.Templify.Gui.ViewModels;
using TriasDev.Templify.Gui.Views;

namespace TriasDev.Templify.Gui;

public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            // Create MainWindow first
            MainWindow mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;

            // Configure dependency injection after window is created
            ServiceCollection services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            // Create ViewModel with DI
            MainWindowViewModel viewModel = Services.GetRequiredService<MainWindowViewModel>();
            mainWindow.DataContext = viewModel;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Core services
        services.AddSingleton<ITemplifyService, TemplifyService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();

        // FileDialogService needs IStorageProvider from the window
        // We'll register it as a factory that gets the StorageProvider from the MainWindow
        services.AddTransient<IFileDialogService>(provider =>
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                MainWindow? mainWindow = desktop.MainWindow as MainWindow;
                if (mainWindow?.StorageProvider != null)
                {
                    return new FileDialogService(mainWindow.StorageProvider);
                }
            }

            throw new InvalidOperationException("MainWindow not available for FileDialogService");
        });
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        DataAnnotationsValidationPlugin[] dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (DataAnnotationsValidationPlugin plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
