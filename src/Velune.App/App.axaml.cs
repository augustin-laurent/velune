using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Presentation.Localization;
using Velune.Presentation.ViewModels;
using Velune.Presentation.Views;

namespace Velune.App;

public partial class App : Avalonia.Application
{
    private IUserPreferencesService? _userPreferencesService;
    private ILocalizationService? _localizationService;
    private NativeMenuLocalizationBinding? _appMenuLocalizationBinding;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _userPreferencesService = Program.AppHost.Services.GetRequiredService<IUserPreferencesService>();
        _localizationService = Program.AppHost.Services.GetRequiredService<ILocalizationService>();
        ApplyThemePreference(_userPreferencesService.Current.Theme);
        _userPreferencesService.PreferencesChanged += OnPreferencesChanged;
        AttachAppMenuLocalization();

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
        _appMenuLocalizationBinding?.Detach();
        _appMenuLocalizationBinding = null;
        _localizationService = null;

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

        var aboutWindow = AboutWindowFactory.Create(_localizationService);
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

    private void AttachAppMenuLocalization()
    {
        _appMenuLocalizationBinding?.Detach();
        _appMenuLocalizationBinding = null;

        if (_localizationService is null ||
            NativeMenu.GetMenu(this) is not NativeMenu menu)
        {
            return;
        }

        _appMenuLocalizationBinding = new NativeMenuLocalizationBinding(
            this,
            menu,
            _localizationService,
            NativeMenuLocalizer.LocalizeAppMenu);
    }
}
