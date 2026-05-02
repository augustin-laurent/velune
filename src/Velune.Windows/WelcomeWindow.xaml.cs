using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Velune.Application.DTOs;
using Velune.Windows.Services;
using Velune.Windows.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using WinRT.Interop;

namespace Velune.Windows;

public sealed partial class WelcomeWindow : Window
{
    private readonly WindowsMainViewModel _viewModel;
    private readonly WindowsWindowContext _windowContext;
    private readonly WindowsWindowCoordinator _windowCoordinator;
    private readonly IWindowsFileDialogService _fileDialogService;

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
        Root.DataContext = viewModel;
        Title = viewModel.Labels.AppName;

        ConfigureWindow();
        Activated += OnActivated;
        Closed += OnClosed;
    }

    private void ConfigureWindow()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);

        var appWindow = ResolveAppWindow();
        appWindow.Resize(new SizeInt32(1712, 963));
        appWindow.Move(new PointInt32(0, 0));
        appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        appWindow.TitleBar.ButtonForegroundColor = Colors.Black;
        appWindow.TitleBar.ButtonHoverBackgroundColor = global::Windows.UI.Color.FromArgb(255, 235, 239, 246);
        appWindow.TitleBar.ButtonPressedBackgroundColor = global::Windows.UI.Color.FromArgb(255, 222, 227, 235);
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
        Activated -= OnActivated;
        Closed -= OnClosed;
        _windowContext.ClearActiveWindow(this);
        _windowCoordinator.NotifyWelcomeClosed(this);
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
            var items = await e.DataView.GetStorageItemsAsync();
            var paths = items
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
            var path = await _fileDialogService.PickOpenDocumentAsync();
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
        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        return AppWindow.GetFromWindowId(windowId);
    }
}
