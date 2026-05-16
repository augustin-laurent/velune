using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Win32;
using Velune.Application.DTOs;
using Velune.Windows.Services;
using Velune.Windows.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using WinRT.Interop;

namespace Velune.Windows;

/// <summary>
/// Landing window displayed on startup that shows recent files and a drop zone for opening documents.
/// </summary>
public sealed partial class WelcomeWindow
{
    private readonly WindowsMainViewModel _viewModel;

    public WindowsMainViewModel ViewModel => _viewModel;
    private readonly WindowsWindowContext _windowContext;
    private readonly WindowsWindowCoordinator _windowCoordinator;
    private readonly IWindowsFileDialogService _fileDialogService;

    /// <summary>
    /// Initializes the welcome window with its dependencies.
    /// </summary>
    /// <param name="viewModel">The shared main view model.</param>
    /// <param name="windowContext">Provides the active window handle.</param>
    /// <param name="windowCoordinator">Coordinates transitions to the workspace window.</param>
    /// <param name="fileDialogService">Provides native file picker dialogs.</param>
    public WelcomeWindow(
        WindowsMainViewModel viewModel,
        WindowsWindowContext windowContext,
        WindowsWindowCoordinator windowCoordinator,
        IWindowsFileDialogService fileDialogService)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(windowContext);
        ArgumentNullException.ThrowIfNull(windowCoordinator);
        ArgumentNullException.ThrowIfNull(fileDialogService);

        _viewModel = viewModel;
        _windowContext = windowContext;
        _windowCoordinator = windowCoordinator;
        _fileDialogService = fileDialogService;

        InitializeComponent();

        _windowContext.SetActiveWindow(this);
        Title = viewModel.Labels.AppName;

        ConfigureWindow();
        ApplyTheme();
        ApplySelectedLanguageIndicator();
        ApplySelectedThemeIndicator();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Activated += OnActivated;
        Closed += OnClosed;
    }

    private void ConfigureWindow()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);

        AppWindow appWindow = ResolveAppWindow();
        DisplayArea displayArea = DisplayArea.GetFromWindowId(
            Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this)),
            DisplayAreaFallback.Primary);
        int workWidth = displayArea.WorkArea.Width;
        int workHeight = displayArea.WorkArea.Height;
        int windowWidth = Math.Min(1712, (int)(workWidth * 0.85));
        int windowHeight = Math.Min(963, (int)(workHeight * 0.85));
        appWindow.Resize(new SizeInt32(windowWidth, windowHeight));
        int x = (workWidth - windowWidth) / 2;
        int y = (workHeight - windowHeight) / 2;
        appWindow.Move(new PointInt32(x, y));
        appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        appWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "Velune.ico"));
    }

    private void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState != WindowActivationState.Deactivated)
        {
            _windowContext.SetActiveWindow(this);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        Activated -= OnActivated;
        Closed -= OnClosed;
        _windowContext.ClearActiveWindow(this);
        _windowCoordinator.NotifyWelcomeClosed(this);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(WindowsMainViewModel.SelectedPreferenceTheme), StringComparison.Ordinal))
        {
            DispatcherQueue.TryEnqueue(ApplyTheme);
        }
    }

    private void ApplyTheme()
    {
        bool isLight = IsLightTheme();
        Root.RequestedTheme = isLight ? ElementTheme.Light : ElementTheme.Dark;

        try
        {
            AppWindow appWindow = ResolveAppWindow();
            appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            appWindow.TitleBar.ButtonForegroundColor = isLight
                ? global::Windows.UI.Color.FromArgb(255, 17, 24, 39)
                : global::Windows.UI.Color.FromArgb(255, 255, 255, 255);
            appWindow.TitleBar.ButtonHoverBackgroundColor = isLight
                ? global::Windows.UI.Color.FromArgb(20, 0, 0, 0)
                : global::Windows.UI.Color.FromArgb(36, 255, 255, 255);
            appWindow.TitleBar.ButtonPressedBackgroundColor = isLight
                ? global::Windows.UI.Color.FromArgb(31, 0, 0, 0)
                : global::Windows.UI.Color.FromArgb(24, 255, 255, 255);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Velune] Welcome TitleBar apply failed: {ex.Message}");
        }
    }

    private bool IsLightTheme()
    {
        return string.Equals(
                _viewModel.SelectedPreferenceTheme,
                _viewModel.Labels.PreferencesLight,
                StringComparison.Ordinal)
            || (string.Equals(
                    _viewModel.SelectedPreferenceTheme,
                    _viewModel.Labels.PreferencesSystem,
                    StringComparison.Ordinal)
                && IsSystemLightTheme());
    }

    private static bool IsSystemLightTheme()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            if (key?.GetValue("AppsUseLightTheme") is int intValue)
            {
                return intValue == 1;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Velune] System theme detection failed: {ex.Message}");
        }

        return false;
    }

    private void OnHomeDropZoneDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.IsContentVisible = true;
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
        }

        e.Handled = true;
    }

    private async void OnHomeDropZoneDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        try
        {
            IReadOnlyList<IStorageItem>? items = await e.DataView.GetStorageItemsAsync();
            string[] paths = items
                .OfType<StorageFile>()
                .Select(file => file.Path)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToArray();

            if (paths.Length > 0)
            {
                await _windowCoordinator.OpenWorkspaceWithFilesAsync(paths);
            }
        }
        catch (Exception exception)
        {
            DispatcherQueue.TryEnqueue(() => _viewModel.StatusText = exception.Message);
        }
    }

    private async void OnOpenFilesClicked(object sender, RoutedEventArgs e)
    {
        await OpenPickedDocumentThroughDropPipelineAsync();
    }

    private async void OnOpenFilesMenuClicked(object sender, RoutedEventArgs e)
    {
        await OpenPickedDocumentThroughDropPipelineAsync();
    }

    private void OnRecentFilePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid && FindAccentBar(grid) is { } bar)
        {
            bar.Opacity = 1;
        }
    }

    private void OnRecentFilePointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid && FindAccentBar(grid) is { } bar)
        {
            bar.Opacity = 0;
        }
    }

    private static Rectangle? FindAccentBar(Grid grid)
    {
        foreach (UIElement? child in grid.Children)
        {
            if (child is Rectangle { Name: "AccentBar" } rect)
            {
                return rect;
            }
        }

        return null;
    }

    private async void OnRecentFileItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not RecentFileItem item ||
            string.IsNullOrWhiteSpace(item.FilePath))
        {
            return;
        }

        await OpenPathsThroughDropPipelineAsync([item.FilePath]);
    }

    private async Task OpenPickedDocumentThroughDropPipelineAsync()
    {
        try
        {
            string? path = await _fileDialogService.PickOpenDocumentAsync();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            await OpenPathsThroughDropPipelineAsync([path]);
        }
        catch (Exception exception)
        {
            DispatcherQueue.TryEnqueue(() => _viewModel.StatusText = exception.Message);
        }
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        // Empty handler for settings on welcome page
    }

    private void OnLanguageClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: string tag })
        {
            return;
        }

        _viewModel.SelectedPreferenceLanguage = tag switch
        {
            "en" => _viewModel.Labels.PreferencesEnglish,
            "fr" => _viewModel.Labels.PreferencesFrench,
            "es" => _viewModel.Labels.PreferencesSpanish,
            _ => _viewModel.Labels.PreferencesSystem
        };

        ApplySelectedLanguageIndicator();
    }

    private void OnThemeClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: string tag })
        {
            return;
        }

        _viewModel.SelectedPreferenceTheme = tag switch
        {
            "Light" => _viewModel.Labels.PreferencesLight,
            "Dark" => _viewModel.Labels.PreferencesDark,
            _ => _viewModel.Labels.PreferencesSystem
        };

        ApplySelectedThemeIndicator();
    }

    private void ApplySelectedLanguageIndicator()
    {
        string selected = _viewModel.SelectedPreferenceLanguage;
        LanguageEnglishItem.IsChecked = string.Equals(selected, _viewModel.Labels.PreferencesEnglish, StringComparison.Ordinal);
        LanguageFrenchItem.IsChecked = string.Equals(selected, _viewModel.Labels.PreferencesFrench, StringComparison.Ordinal);
        LanguageSpanishItem.IsChecked = string.Equals(selected, _viewModel.Labels.PreferencesSpanish, StringComparison.Ordinal);
    }

    private void ApplySelectedThemeIndicator()
    {
        string selected = _viewModel.SelectedPreferenceTheme;
        ThemeSystemItem.IsChecked = string.Equals(selected, _viewModel.Labels.PreferencesSystem, StringComparison.Ordinal);
        ThemeLightItem.IsChecked = string.Equals(selected, _viewModel.Labels.PreferencesLight, StringComparison.Ordinal);
        ThemeDarkItem.IsChecked = string.Equals(selected, _viewModel.Labels.PreferencesDark, StringComparison.Ordinal);
    }

    private async Task OpenPathsThroughDropPipelineAsync(IReadOnlyList<string> paths)
    {
        try
        {
            await _windowCoordinator.OpenWorkspaceWithFilesAsync(paths);
        }
        catch (Exception exception)
        {
            DispatcherQueue.TryEnqueue(() => _viewModel.StatusText = exception.Message);
        }
    }

    private AppWindow ResolveAppWindow()
    {
        IntPtr windowHandle = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        return AppWindow.GetFromWindowId(windowId);
    }
}
