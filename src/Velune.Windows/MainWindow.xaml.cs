using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Velune.Domain.Annotations;
using Velune.Windows.Services;
using Velune.Windows.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

namespace Velune.Windows;

public sealed partial class MainWindow : Window
{
    private readonly WindowsMainViewModel _viewModel;
    private readonly WindowsWindowContext _windowContext;
    private readonly WindowsWindowCoordinator _windowCoordinator;
    private readonly TaskCompletionSource _loadedCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private InputNonClientPointerSource? _nonClientPointerSource;
    private bool _isAnnotationInteractionActive;
    private bool _isTextSelectionInteractionActive;
    private bool _hasPresentedDocument;

    public MainWindow(
        WindowsMainViewModel viewModel,
        WindowsWindowContext windowContext,
        WindowsWindowCoordinator windowCoordinator)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(windowContext);
        ArgumentNullException.ThrowIfNull(windowCoordinator);

        _viewModel = viewModel;
        _windowContext = windowContext;
        _windowCoordinator = windowCoordinator;

        InitializeComponent();

        windowContext.SetActiveWindow(this);
        Root.DataContext = viewModel;
        Title = viewModel.Labels.AppName;

        ConfigureTitleBar();
        ApplyTheme();

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Activated += OnActivated;
        Root.Loaded += OnRootLoaded;
        Root.SizeChanged += OnRootSizeChanged;
        Closed += OnClosed;
    }

    private void ConfigureTitleBar()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);

        var windowHandle = WindowNative.GetWindowHandle(this);
        var appWindow = ResolveAppWindow();
        appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        _nonClientPointerSource = InputNonClientPointerSource.GetForWindowId(
            Win32Interop.GetWindowIdFromWindow(windowHandle));
    }

    private void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState != WindowActivationState.Deactivated)
        {
            _windowContext.SetActiveWindow(this);
        }

        ApplyTheme();
        ApplyTitleBarColors(IsLightTheme());
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        Activated -= OnActivated;
        Root.Loaded -= OnRootLoaded;
        Root.SizeChanged -= OnRootSizeChanged;
        Closed -= OnClosed;
        _windowContext.ClearActiveWindow(this);
        _windowCoordinator.NotifyWorkspaceClosed(this);
    }

    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        _loadedCompletionSource.TrySetResult();
        UpdateTitleBarInteractiveRegions();
        _viewModel.NotifyBindingsRefresh();
        _ = EnsureActiveTabHydratedAfterLoadAsync();
    }

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateTitleBarInteractiveRegions();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(WindowsMainViewModel.SelectedPreferenceTheme), StringComparison.Ordinal) ||
            string.Equals(e.PropertyName, nameof(WindowsMainViewModel.ActiveDocumentTab), StringComparison.Ordinal))
        {
            ApplyTheme();
        }

        if (string.Equals(e.PropertyName, nameof(WindowsMainViewModel.ActiveDocumentTab), StringComparison.Ordinal))
        {
            if (_viewModel.ActiveDocumentTab is not null)
            {
                _hasPresentedDocument = true;
                return;
            }

            if (!_hasPresentedDocument || _viewModel.DocumentTabs.Count != 0)
            {
                return;
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                if (!_hasPresentedDocument ||
                    _viewModel.ActiveDocumentTab is not null ||
                    _viewModel.DocumentTabs.Count > 0)
                {
                    return;
                }

                _windowCoordinator.ReturnToWelcome(this);
            });
        }
    }

    private void ApplyTheme()
    {
        var isLight = IsLightTheme();
        Root.RequestedTheme = isLight ? ElementTheme.Light : ElementTheme.Dark;

        TitleBarDragRegion.Background = new SolidColorBrush(Colors.Transparent);
        TitleBarDragHandle.Background = new SolidColorBrush(Colors.Transparent);

        try
        {
            ApplyTitleBarColors(isLight);
        }
        catch
        {
            // Window not ready.
        }

        foreach (var tab in _viewModel.DocumentTabs)
        {
            tab.SetTheme(isLight);
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

    private void ApplyTitleBarColors(bool isLight)
    {
        try
        {
            var appWindow = ResolveAppWindow();
            appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            appWindow.TitleBar.ButtonForegroundColor = ResolveThemeColor(
                "TextPrimaryBrush",
                isLight ? "#111827" : "#FFFFFF");
            appWindow.TitleBar.ButtonInactiveForegroundColor = ResolveThemeColor(
                "TextSecondaryBrush",
                isLight ? "#6B7280" : "#9E9E9E");
            appWindow.TitleBar.ButtonHoverBackgroundColor = ResolveThemeColor(
                "TitleBarButtonHoverBrush",
                isLight ? "#14000000" : "#24FFFFFF");
            appWindow.TitleBar.ButtonPressedBackgroundColor = ResolveThemeColor(
                "TitleBarButtonPressedBrush",
                isLight ? "#1F000000" : "#18FFFFFF");
        }
        catch
        {
            // Window not ready.
        }
    }

    private static global::Windows.UI.Color ResolveThemeColor(string resourceKey, string fallbackHex)
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(resourceKey, out var resource) &&
            resource is SolidColorBrush brush)
        {
            return brush.Color;
        }

        return ParseColor(fallbackHex);
    }

    private static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            if (key is not null)
            {
                var value = key.GetValue("AppsUseLightTheme");
                if (value is int intValue)
                {
                    return intValue == 1;
                }
            }
        }
        catch
        {
            // Fall back to dark if registry access fails.
        }

        return false;
    }

    private AppWindow ResolveAppWindow()
    {
        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        return AppWindow.GetFromWindowId(windowId);
    }

    private void UpdateTitleBarInteractiveRegions()
    {
        if (_nonClientPointerSource is null ||
            Root.XamlRoot is null ||
            TitleBarInteractiveRegion.ActualWidth <= 0 ||
            TitleBarInteractiveRegion.ActualHeight <= 0)
        {
            return;
        }

        var scale = Root.XamlRoot.RasterizationScale;
        var interactiveBounds = TitleBarInteractiveRegion
            .TransformToVisual(Root)
            .TransformBounds(new global::Windows.Foundation.Rect(
                0,
                0,
                TitleBarInteractiveRegion.ActualWidth,
                TitleBarInteractiveRegion.ActualHeight));

        SetTitleBar(TitleBarDragRegion);
        _nonClientPointerSource.SetRegionRects(
            NonClientRegionKind.Passthrough,
            [ToRectInt32(interactiveBounds, scale)]);
    }

    private static global::Windows.UI.Color ParseColor(string hex)
    {
        var normalized = hex.Trim().TrimStart('#');
        var alpha = (byte)255;
        var channelOffset = 0;

        if (normalized.Length == 8)
        {
            alpha = Convert.ToByte(normalized[..2], 16);
            channelOffset = 2;
        }

        return global::Windows.UI.Color.FromArgb(
            alpha,
            Convert.ToByte(normalized.Substring(channelOffset, 2), 16),
            Convert.ToByte(normalized.Substring(channelOffset + 2, 2), 16),
            Convert.ToByte(normalized.Substring(channelOffset + 4, 2), 16));
    }

    private static RectInt32 ToRectInt32(global::Windows.Foundation.Rect bounds, double scale)
    {
        return new RectInt32(
            (int)Math.Floor(bounds.X * scale),
            (int)Math.Floor(bounds.Y * scale),
            Math.Max(0, (int)Math.Ceiling(bounds.Width * scale)),
            Math.Max(0, (int)Math.Ceiling(bounds.Height * scale)));
    }

    private async void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListView { SelectedItem: WindowsDocumentTabViewModel tab } &&
            _viewModel.ActivateTabCommand.CanExecute(tab))
        {
            await _viewModel.ActivateTabCommand.ExecuteAsync(tab);
        }
    }

    private async void OnTabClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsDocumentTabViewModel tab } &&
            _viewModel.ActivateTabCommand.CanExecute(tab))
        {
            await _viewModel.ActivateTabCommand.ExecuteAsync(tab);
        }
    }

    private void OnTabPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsDocumentTabViewModel tab })
        {
            tab.RefreshTabChrome(true);
        }
    }

    private void OnTabPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsDocumentTabViewModel tab })
        {
            tab.RefreshTabChrome(false);
        }
    }

    private async void OnCloseTabClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsDocumentTabViewModel tab } &&
            _viewModel.CloseTabCommand.CanExecute(tab))
        {
            await _viewModel.CloseTabCommand.ExecuteAsync(tab);
        }
    }

    private async void OnCloseActiveTabMenuClicked(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ActiveDocumentTab is { } tab &&
            _viewModel.CloseTabCommand.CanExecute(tab))
        {
            await _viewModel.CloseTabCommand.ExecuteAsync(tab);
        }
    }

    private async void OnSearchBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        SyncSearchQueryFromTextBox(sender);

        try
        {
            if (_viewModel.SearchTextCommand.CanExecute(null))
            {
                await _viewModel.SearchTextCommand.ExecuteAsync(null);
            }
        }
        catch (Exception exception)
        {
            ReportUnhandledUiCommandError(exception);
        }
    }

    private async void OnSearchResultSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (sender is ListView { SelectedItem: WindowsSearchResultItemViewModel result } &&
                _viewModel.OpenSearchResultCommand.CanExecute(result))
            {
                await _viewModel.OpenSearchResultCommand.ExecuteAsync(result);
            }
        }
        catch (Exception exception)
        {
            ReportUnhandledUiCommandError(exception);
        }
    }

    private void ReportUnhandledUiCommandError(Exception exception)
    {
        DispatcherQueue.TryEnqueue(() => _viewModel.StatusText = exception.Message);
    }

    private void OnCopySelectedTextKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused() || !CopySelectedDocumentTextToClipboard())
        {
            return;
        }

        args.Handled = true;
    }

    private bool CopySelectedDocumentTextToClipboard()
    {
        if (string.IsNullOrWhiteSpace(_viewModel.SelectedDocumentText))
        {
            return false;
        }

        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(_viewModel.SelectedDocumentText);
            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();
            _viewModel.NotifySelectedDocumentTextCopied();
            return true;
        }
        catch (Exception exception)
        {
            ReportUnhandledUiCommandError(exception);
            return false;
        }
    }

    private bool IsTextInputFocused()
    {
        var focusedElement = FocusManager.GetFocusedElement(Root.XamlRoot);
        return focusedElement is TextBox or PasswordBox or RichEditBox;
    }

    public Task WaitUntilLoadedAsync()
    {
        return _loadedCompletionSource.Task;
    }

    private async Task EnsureActiveTabHydratedAfterLoadAsync()
    {
        try
        {
            await _viewModel.EnsureActiveTabHydratedAsync();
        }
        catch (Exception exception)
        {
            ReportUnhandledUiCommandError(exception);
        }
    }

    private void SyncSearchQueryFromTextBox(object sender)
    {
        if (sender is TextBox textBox &&
            _viewModel.ActiveDocumentTab is { } tab)
        {
            tab.SearchQuery = textBox.Text;
        }
    }

    private async void OnThumbnailItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is WindowsPageThumbnailViewModel thumbnail)
        {
            await _viewModel.ChangePageAsync(thumbnail.PageNumber);
        }
    }

    private void OnThumbnailPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
        {
            var bar = FindChild<Microsoft.UI.Xaml.Shapes.Rectangle>(grid, "HoverBar");
            if (bar is not null)
            {
                bar.Opacity = 0.5;
            }
        }
    }

    private void OnThumbnailPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
        {
            var bar = FindChild<Microsoft.UI.Xaml.Shapes.Rectangle>(grid, "HoverBar");
            if (bar is not null)
            {
                bar.Opacity = 0;
            }
        }
    }

    private static T? FindChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T element && element.Name == name)
            {
                return element;
            }

            var result = FindChild<T>(child, name);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private void OnAnnotationToolClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsAnnotationToolItem tool } &&
            _viewModel.SelectAnnotationToolCommand.CanExecute(tool.Tool))
        {
            _viewModel.SelectAnnotationToolCommand.Execute(tool.Tool);
        }
    }

    private void OnAnnotationColorClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsAnnotationColorItem color } &&
            _viewModel.SelectAnnotationColorCommand.CanExecute(color))
        {
            _viewModel.SelectAnnotationColorCommand.Execute(color);
        }
    }

    private void OnDeleteAnnotationTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsAnnotationOverlayViewModel overlay } &&
            _viewModel.DeleteAnnotationByIdCommand.CanExecute(overlay.Id))
        {
            _viewModel.DeleteAnnotationByIdCommand.Execute(overlay.Id);
            e.Handled = true;
        }
    }

    private void OnDocumentLayerPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement layer ||
            sender is not UIElement element)
        {
            return;
        }

        var point = e.GetCurrentPoint(layer).Position;
        if (_viewModel.ActiveDocumentTab?.SelectedAnnotationTool is AnnotationTool.Select)
        {
            _viewModel.ClearDocumentTextSelection();
            _isTextSelectionInteractionActive = _viewModel.BeginDocumentTextSelection(
                point.X,
                point.Y,
                layer.ActualWidth,
                layer.ActualHeight);

            if (_isTextSelectionInteractionActive)
            {
                element.CapturePointer(e.Pointer);
                e.Handled = true;
            }

            return;
        }

        _isAnnotationInteractionActive = _viewModel.BeginAnnotationInteraction(
            point.X,
            point.Y,
            layer.ActualWidth,
            layer.ActualHeight);

        if (_isAnnotationInteractionActive)
        {
            element.CapturePointer(e.Pointer);
            e.Handled = true;
        }
    }

    private void OnDocumentLayerPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement layer)
        {
            return;
        }

        var point = e.GetCurrentPoint(layer).Position;
        if (_isTextSelectionInteractionActive)
        {
            _viewModel.UpdateDocumentTextSelection(point.X, point.Y, layer.ActualWidth, layer.ActualHeight);
            e.Handled = true;
            return;
        }

        if (!_isAnnotationInteractionActive)
        {
            return;
        }

        _viewModel.UpdateAnnotationInteraction(point.X, point.Y, layer.ActualWidth, layer.ActualHeight);
        e.Handled = true;
    }

    private void OnDocumentLayerPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if ((!_isAnnotationInteractionActive && !_isTextSelectionInteractionActive) ||
            sender is not FrameworkElement layer ||
            sender is not UIElement element)
        {
            return;
        }

        var point = e.GetCurrentPoint(layer).Position;
        if (_isTextSelectionInteractionActive)
        {
            _viewModel.UpdateDocumentTextSelection(point.X, point.Y, layer.ActualWidth, layer.ActualHeight);
            _viewModel.CompleteDocumentTextSelection();
            element.ReleasePointerCapture(e.Pointer);
            _isTextSelectionInteractionActive = false;
            e.Handled = true;
            return;
        }

        _viewModel.CompleteAnnotationInteraction(point.X, point.Y, layer.ActualWidth, layer.ActualHeight);
        element.ReleasePointerCapture(e.Pointer);
        _isAnnotationInteractionActive = false;
        e.Handled = true;
    }

    private void OnDocumentLayerPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (!_isAnnotationInteractionActive && !_isTextSelectionInteractionActive)
        {
            return;
        }

        if (sender is UIElement element)
        {
            element.ReleasePointerCapture(e.Pointer);
        }

        if (_isTextSelectionInteractionActive)
        {
            _viewModel.CompleteDocumentTextSelection();
        }

        _viewModel.CancelAnnotationInteraction();
        _isAnnotationInteractionActive = false;
        _isTextSelectionInteractionActive = false;
        e.Handled = true;
    }
}
