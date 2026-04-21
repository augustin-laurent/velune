using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Presentation.ViewModels;
using Velune.Presentation.Views;

namespace Velune.App;

public partial class App : Avalonia.Application
{
    private IUserPreferencesService? _userPreferencesService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _userPreferencesService = Program.AppHost.Services.GetRequiredService<IUserPreferencesService>();
        ApplyThemePreference(_userPreferencesService.Current.Theme);
        _userPreferencesService.PreferencesChanged += OnPreferencesChanged;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Program.AppHost.Services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }

    internal void DisposeResources()
    {
        if (_userPreferencesService is null)
        {
            return;
        }

        _userPreferencesService.PreferencesChanged -= OnPreferencesChanged;
        _userPreferencesService = null;
    }

    private void OnPreferencesChanged(object? sender, EventArgs e)
    {
        if (_userPreferencesService is null)
        {
            return;
        }

        ApplyThemePreference(_userPreferencesService.Current.Theme);
    }

    private void ApplyThemePreference(AppThemePreference themePreference)
    {
        RequestedThemeVariant = themePreference switch
        {
            AppThemePreference.Light => ThemeVariant.Light,
            AppThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    private async void OnAboutMenuClicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is not Window mainWindow)
        {
            return;
        }

        var aboutWindow = AboutWindowFactory.Create();
        await aboutWindow.ShowDialog(mainWindow);
    }

    private void OnPreferencesMenuClicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.DataContext is not MainWindowViewModel viewModel ||
            !viewModel.TogglePreferencesPanelCommand.CanExecute(null))
        {
            return;
        }

        viewModel.TogglePreferencesPanelCommand.Execute(null);
    }
}
