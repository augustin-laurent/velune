using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Win32;
using Velune.Domain.Annotations;
using Velune.Windows.Services;
using Velune.Windows.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using WinRT.Interop;
using WindowActivatedEventArgs = Microsoft.UI.Xaml.WindowActivatedEventArgs;

namespace Velune.Windows;

/// <summary>
/// The main workspace window hosting document tabs, annotations, and page thumbnails.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly WindowsMainViewModel _viewModel;

    public WindowsMainViewModel ViewModel => _viewModel;
    private readonly WindowsWindowContext _windowContext;
    private readonly WindowsWindowCoordinator _windowCoordinator;
    private readonly TaskCompletionSource _loadedCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private InputNonClientPointerSource? _nonClientPointerSource;
    private bool _isAnnotationInteractionActive;
    private bool _isTextSelectionInteractionActive;
    private bool _isMovingAnnotation;
    private bool _isCapturingSignaturePad;
    private bool _hasPresentedDocument;

    /// <summary>
    /// Initializes the main window with its view model and window management dependencies.
    /// </summary>
    /// <param name="viewModel">The main view model driving this window.</param>
    /// <param name="windowContext">Provides the active window handle and dispatcher.</param>
    /// <param name="windowCoordinator">Coordinates window transitions between welcome and workspace.</param>
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
        Title = viewModel.Labels.AppName;

        ContextMenuDeleteItem.Text = viewModel.Labels.AnnotationMenuDelete;
        ContextMenuRotate90Item.Text = viewModel.Labels.AnnotationMenuRotate90;
        ContextMenuResetRotationItem.Text = viewModel.Labels.AnnotationMenuResetRotation;
        ContextMenuFlipHItem.Text = viewModel.Labels.AnnotationMenuFlipH;
        ContextMenuFlipVItem.Text = viewModel.Labels.AnnotationMenuFlipV;

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

        IntPtr windowHandle = WindowNative.GetWindowHandle(this);
        AppWindow appWindow = ResolveAppWindow();
        appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        appWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "Velune.ico"));
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

    private void OnDocumentScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _viewModel.SetDocumentViewerSize(e.NewSize.Width, e.NewSize.Height);
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
        bool isLight = IsLightTheme();
        Root.RequestedTheme = isLight ? ElementTheme.Light : ElementTheme.Dark;

        TitleBarDragRegion.Background = new SolidColorBrush(Colors.Transparent);
        TitleBarDragHandle.Background = new SolidColorBrush(Colors.Transparent);

        try
        {
            ApplyTitleBarColors(isLight);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Velune] TitleBar theme apply failed: {ex.Message}");
        }

        foreach (WindowsDocumentTabViewModel tab in _viewModel.DocumentTabs)
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
            AppWindow appWindow = ResolveAppWindow();
            appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            appWindow.TitleBar.ButtonForegroundColor = isLight
                ? global::Windows.UI.Color.FromArgb(255, 31, 41, 55)
                : global::Windows.UI.Color.FromArgb(255, 255, 255, 255);
            appWindow.TitleBar.ButtonInactiveForegroundColor = isLight
                ? global::Windows.UI.Color.FromArgb(255, 107, 114, 128)
                : global::Windows.UI.Color.FromArgb(255, 158, 158, 158);
            appWindow.TitleBar.ButtonHoverBackgroundColor = isLight
                ? global::Windows.UI.Color.FromArgb(20, 0, 0, 0)
                : global::Windows.UI.Color.FromArgb(36, 255, 255, 255);
            appWindow.TitleBar.ButtonPressedBackgroundColor = isLight
                ? global::Windows.UI.Color.FromArgb(31, 0, 0, 0)
                : global::Windows.UI.Color.FromArgb(24, 255, 255, 255);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Velune] TitleBar colors apply failed: {ex.Message}");
        }
    }

    private static bool IsSystemLightTheme()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            if (key is not null)
            {
                object? value = key.GetValue("AppsUseLightTheme");
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
        IntPtr windowHandle = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
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

        double scale = Root.XamlRoot.RasterizationScale;
        Rect interactiveBounds = TitleBarInteractiveRegion
            .TransformToVisual(Root)
            .TransformBounds(new Rect(
                0,
                0,
                TitleBarInteractiveRegion.ActualWidth,
                TitleBarInteractiveRegion.ActualHeight));

        SetTitleBar(TitleBarDragRegion);
        _nonClientPointerSource.SetRegionRects(
            NonClientRegionKind.Passthrough,
            [ToRectInt32(interactiveBounds, scale)]);
    }



    private static RectInt32 ToRectInt32(Rect bounds, double scale)
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

    private void OnSearchKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (_viewModel.OpenSearchCommand.CanExecute(null))
        {
            _viewModel.OpenSearchCommand.Execute(null);
        }

        args.Handled = true;
    }

    private async void OnSaveKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (_viewModel.SaveDocumentCommand.CanExecute(null))
        {
            await _viewModel.SaveDocumentCommand.ExecuteAsync(null);
        }
    }

    private void OnUndoKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        if (_viewModel.UndoActionCommand.CanExecute(null))
        {
            _viewModel.UndoActionCommand.Execute(null);
        }

        args.Handled = true;
    }

    private void OnRedoKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        if (_viewModel.RedoActionCommand.CanExecute(null))
        {
            _viewModel.RedoActionCommand.Execute(null);
        }

        args.Handled = true;
    }

    private void OnDeleteAnnotationKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (_viewModel.SelectedAnnotationId is not null)
        {
            _viewModel.DeleteSelectedAnnotation();
            args.Handled = true;
        }
    }

    private void OnExtendSelectionRightKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        _viewModel.AdjustDocumentTextSelection(1);
        args.Handled = true;
    }

    private void OnShrinkSelectionLeftKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        _viewModel.AdjustDocumentTextSelection(-1);
        args.Handled = true;
    }

    private void OnExtendSelectionWordRightKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        _viewModel.AdjustDocumentTextSelectionByWord(1);
        args.Handled = true;
    }

    private void OnShrinkSelectionWordLeftKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        _viewModel.AdjustDocumentTextSelectionByWord(-1);
        args.Handled = true;
    }

    private async void OnRotateRightKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await _viewModel.RotateSelectedPageAsync(clockwise: true);
    }

    private async void OnRotateLeftKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await _viewModel.RotateSelectedPageAsync(clockwise: false);
    }

    private async void OnFitPageKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (_viewModel.FitPageCommand.CanExecute(null))
        {
            await _viewModel.FitPageCommand.ExecuteAsync(null);
        }
    }

    private async void OnActualSizeKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (_viewModel.ActualSizeCommand.CanExecute(null))
        {
            await _viewModel.ActualSizeCommand.ExecuteAsync(null);
        }
    }

    private void OnFlipHorizontalKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (_viewModel.SelectedAnnotationId is not null)
        {
            _viewModel.FlipSelectedAnnotationHorizontally();
        }
    }

    private async void OnNextPageKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        args.Handled = true;

        if (_viewModel.NextPageCommand.CanExecute(null))
        {
            await _viewModel.NextPageCommand.ExecuteAsync(null);
        }
    }

    private async void OnPreviousPageKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        args.Handled = true;

        if (_viewModel.PreviousPageCommand.CanExecute(null))
        {
            await _viewModel.PreviousPageCommand.ExecuteAsync(null);
        }
    }

    private async void OnZoomInKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (_viewModel.ZoomInCommand.CanExecute(null))
        {
            await _viewModel.ZoomInCommand.ExecuteAsync(null);
        }
    }

    private async void OnZoomOutKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (_viewModel.ZoomOutCommand.CanExecute(null))
        {
            await _viewModel.ZoomOutCommand.ExecuteAsync(null);
        }
    }

    private void OnDocumentLayerRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (_viewModel.SelectedAnnotationId is null)
        {
            AnnotationContextMenu.Hide();
            e.Handled = true;
        }
    }

    private void OnContextMenuDelete(object sender, RoutedEventArgs e)
    {
        _viewModel.DeleteSelectedAnnotation();
    }

    private void OnContextMenuRotate90(object sender, RoutedEventArgs e)
    {
        _viewModel.RotateSelectedAnnotation90();
    }

    private void OnContextMenuResetRotation(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetSelectedAnnotationRotation();
    }

    private void OnContextMenuFlipH(object sender, RoutedEventArgs e)
    {
        _viewModel.FlipSelectedAnnotationHorizontally();
    }

    private void OnContextMenuFlipV(object sender, RoutedEventArgs e)
    {
        _viewModel.FlipSelectedAnnotationVertically();
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
        object? focusedElement = FocusManager.GetFocusedElement(Root.XamlRoot);
        return focusedElement is TextBox or PasswordBox or RichEditBox;
    }

    private static bool IsPointerFromTextInput(object source)
    {
        var current = source as DependencyObject;
        while (current is not null)
        {
            if (current is TextBox or PasswordBox or RichEditBox)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    /// <summary>
    /// Returns a task that completes when the root visual tree has loaded.
    /// </summary>
    /// <returns>A task representing the window-loaded event.</returns>
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

    private const double ThumbnailDragThreshold = 6;
    private const double ThumbnailItemHeight = 172;

    private int _thumbnailDragSourceIndex = -1;
    private bool _isDraggingThumbnail;
    private Point _thumbnailDragStartPoint;
    private int _thumbnailDropTargetIndex = -1;

    private async void OnThumbnailItemTapped(object sender, TappedRoutedEventArgs e)
    {
        if (_isDraggingThumbnail)
        {
            return;
        }

        if (sender is FrameworkElement { DataContext: WindowsPageThumbnailViewModel thumbnail })
        {
            await _viewModel.ChangePageAsync(thumbnail.PageNumber);
        }
    }

    private void OnOpenPageOrganizerClicked(object sender, RoutedEventArgs e)
    {
        WindowsDocumentTabViewModel? tab = _viewModel.ActiveDocumentTab;
        if (tab is null)
        {
            return;
        }

        var app = (App)Microsoft.UI.Xaml.Application.Current;
        PageOrganizerViewModel vm = app.Services.GetRequiredService<PageOrganizerViewModel>();
        vm.Initialize(tab.SessionId, tab.FilePath, tab.TotalPages, tab.Thumbnails);

        var window = new PageOrganizerWindow(vm, applied =>
        {
            if (applied && vm.HasChanges)
            {
                _ = _viewModel.ApplyPageOrganizerResultAsync(vm.GetFinalPageOrder(), vm.GetRotations());
            }
        });

        window.Activate();
    }

    private void OnThumbnailItemRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsPageThumbnailViewModel thumbnail })
        {
            _viewModel.SelectedThumbnailPageNumber = thumbnail.PageNumber;
        }
    }

    private void OnRibbonRotateLeftClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ActiveDocumentTab is not null)
        {
            _viewModel.SelectedThumbnailPageNumber = _viewModel.ActiveDocumentTab.CurrentPage;
        }

        _ = _viewModel.RotateSelectedPageAsync(false);
    }

    private void OnRibbonRotateRightClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ActiveDocumentTab is not null)
        {
            _viewModel.SelectedThumbnailPageNumber = _viewModel.ActiveDocumentTab.CurrentPage;
        }

        _ = _viewModel.RotateSelectedPageAsync(true);
    }

    private void OnThumbnailRotateLeftClick(object sender, RoutedEventArgs e) =>
        _ = _viewModel.RotateSelectedPageAsync(false);

    private void OnThumbnailRotateRightClick(object sender, RoutedEventArgs e) =>
        _ = _viewModel.RotateSelectedPageAsync(true);

    private void OnThumbnailMoveUpClick(object sender, RoutedEventArgs e) =>
        _ = _viewModel.MoveSelectedPageAsync(-1);

    private void OnThumbnailMoveDownClick(object sender, RoutedEventArgs e) =>
        _ = _viewModel.MoveSelectedPageAsync(1);

    private void OnThumbnailDeleteClick(object sender, RoutedEventArgs e) =>
        _ = _viewModel.DeleteSelectedPageAsync();

    private void OnThumbnailItemPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not UIElement element ||
            element is not FrameworkElement { DataContext: WindowsPageThumbnailViewModel thumbnail })
        {
            return;
        }

        _thumbnailDragSourceIndex = _viewModel.ActiveDocumentTab?.Thumbnails.IndexOf(thumbnail) ?? -1;
        _thumbnailDragStartPoint = e.GetCurrentPoint(ThumbnailScrollViewer).Position;
        _isDraggingThumbnail = false;
        element.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnThumbnailItemPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_thumbnailDragSourceIndex < 0 || sender is not UIElement element)
        {
            return;
        }

        Point currentPoint = e.GetCurrentPoint(ThumbnailScrollViewer).Position;

        if (!_isDraggingThumbnail)
        {
            double dx = currentPoint.X - _thumbnailDragStartPoint.X;
            double dy = currentPoint.Y - _thumbnailDragStartPoint.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < ThumbnailDragThreshold)
            {
                return;
            }

            _isDraggingThumbnail = true;
            WindowsDocumentTabViewModel? tab = _viewModel.ActiveDocumentTab;
            if (tab is not null && _thumbnailDragSourceIndex < tab.Thumbnails.Count)
            {
                ThumbnailDragGhost.Source = tab.Thumbnails[_thumbnailDragSourceIndex].Image;
                ThumbnailDragGhost.Visibility = Visibility.Visible;
            }
        }

        if (_isDraggingThumbnail)
        {
            ThumbnailDragGhost.Margin = new Thickness(
                currentPoint.X - 40,
                currentPoint.Y - 52,
                0, 0);

            UpdateThumbnailDropIndicator(currentPoint.Y);
            AutoScrollThumbnails(currentPoint.Y);
        }

        e.Handled = true;
    }

    private async void OnThumbnailItemPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is UIElement element)
        {
            element.ReleasePointerCapture(e.Pointer);
        }

        ThumbnailDragGhost.Visibility = Visibility.Collapsed;
        ThumbnailDropIndicator.Visibility = Visibility.Collapsed;
        ResetThumbnailDisplacement();

        if (_isDraggingThumbnail && _thumbnailDragSourceIndex >= 0 && _thumbnailDropTargetIndex >= 0)
        {
            int sourceIndex = _thumbnailDragSourceIndex;
            int targetIndex = _thumbnailDropTargetIndex;

            if (sourceIndex != targetIndex && targetIndex != sourceIndex + 1)
            {
                WindowsDocumentTabViewModel? tab = _viewModel.ActiveDocumentTab;
                if (tab is not null)
                {
                    int adjustedTarget = targetIndex > sourceIndex ? targetIndex - 1 : targetIndex;
                    tab.Thumbnails.Move(sourceIndex, adjustedTarget);
                    await _viewModel.HandleThumbnailReorderAsync(sourceIndex + 1, adjustedTarget);
                }
            }
        }

        _thumbnailDragSourceIndex = -1;
        _thumbnailDropTargetIndex = -1;
        _isDraggingThumbnail = false;
        e.Handled = true;
    }

    private void UpdateThumbnailDropIndicator(double pointerY)
    {
        double scrollOffset = ThumbnailScrollViewer.VerticalOffset;
        double adjustedY = pointerY + scrollOffset;
        int index = (int)Math.Round(adjustedY / ThumbnailItemHeight);
        WindowsDocumentTabViewModel? tab = _viewModel.ActiveDocumentTab;
        if (tab is null)
        {
            return;
        }

        int newIndex = Math.Clamp(index, 0, tab.Thumbnails.Count);
        if (newIndex == _thumbnailDropTargetIndex)
        {
            return;
        }

        _thumbnailDropTargetIndex = newIndex;
        double indicatorY = (_thumbnailDropTargetIndex * ThumbnailItemHeight) - scrollOffset;
        ThumbnailDropIndicator.Margin = new Thickness(8, indicatorY, 8, 0);
        ThumbnailDropIndicator.Visibility = Visibility.Visible;

        AnimateThumbnailDisplacement();
    }

    private void AnimateThumbnailDisplacement()
    {
        const float gapSize = 20f;
        Panel? panel = GetThumbnailPanel();
        if (panel is null)
        {
            return;
        }

        for (int i = 0; i < panel.Children.Count; i++)
        {
            UIElement? templateRoot = GetTemplateRootFromContainer(panel.Children[i]);
            if (templateRoot is null)
            {
                continue;
            }

            bool shouldDisplace = i >= _thumbnailDropTargetIndex && i != _thumbnailDragSourceIndex;
            templateRoot.Translation = new System.Numerics.Vector3(0, shouldDisplace ? gapSize : 0f, 0);
        }
    }

    private void ResetThumbnailDisplacement()
    {
        Panel? panel = GetThumbnailPanel();
        if (panel is null)
        {
            return;
        }

        foreach (UIElement? child in panel.Children)
        {
            UIElement? templateRoot = GetTemplateRootFromContainer(child);
            if (templateRoot is not null)
            {
                templateRoot.Translation = System.Numerics.Vector3.Zero;
            }
        }
    }

    private Panel? GetThumbnailPanel()
    {
        if (VisualTreeHelper.GetChildrenCount(ThumbnailListView) == 0)
        {
            return null;
        }

        DependencyObject? child = VisualTreeHelper.GetChild(ThumbnailListView, 0);
        return child as Panel;
    }

    private static UIElement? GetTemplateRootFromContainer(UIElement container)
    {
        int count = VisualTreeHelper.GetChildrenCount(container);
        return count == 0 ? null : VisualTreeHelper.GetChild(container, 0) as UIElement;
    }

    private void AutoScrollThumbnails(double pointerY)
    {
        double viewportHeight = ThumbnailScrollViewer.ViewportHeight;
        const double edgeZone = 30;
        const double scrollStep = 8;

        if (pointerY < edgeZone)
        {
            ThumbnailScrollViewer.ChangeView(null, ThumbnailScrollViewer.VerticalOffset - scrollStep, null, true);
        }
        else if (pointerY > viewportHeight - edgeZone)
        {
            ThumbnailScrollViewer.ChangeView(null, ThumbnailScrollViewer.VerticalOffset + scrollStep, null, true);
        }
    }

    private void OnThumbnailPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Grid grid)
        {
            return;
        }

        Rectangle? bar = FindChild<Rectangle>(grid, "HoverBar");
        bar?.Opacity = 0.5;
    }

    private void OnThumbnailPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Grid grid)
        {
            return;
        }

        Rectangle? bar = FindChild<Rectangle>(grid, "HoverBar");
        bar?.Opacity = 0;
    }

    private void OnThumbnailExternalDragOver(object sender, DragEventArgs e)
    {
        if (!_viewModel.CanAcceptThumbnailDrop)
        {
            e.AcceptedOperation = DataPackageOperation.None;
            HideThumbnailDropIndicator();
            return;
        }

        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = _viewModel.Labels.Insert;
            e.DragUIOverride.IsGlyphVisible = true;
            UpdateThumbnailExternalDropIndicator(e.GetPosition(ThumbnailDragSurface));
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
            HideThumbnailDropIndicator();
        }
    }

    private void OnThumbnailExternalDragLeave(object sender, DragEventArgs e)
    {
        HideThumbnailDropIndicator();
    }

    private async void OnThumbnailExternalDrop(object sender, DragEventArgs e)
    {
        if (!_viewModel.CanAcceptThumbnailDrop ||
            !e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            HideThumbnailDropIndicator();
            return;
        }

        int insertionIndex = ResolveThumbnailDropIndex(e.GetPosition(ThumbnailDragSurface));
        HideThumbnailDropIndicator();

        IReadOnlyList<IStorageItem>? items = await e.DataView.GetStorageItemsAsync();
        var filePaths = items
            .OfType<StorageFile>()
            .Select(file => file.Path)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();

        if (filePaths.Count == 0)
        {
            return;
        }

        await _viewModel.HandleThumbnailFilesDroppedAsync(filePaths, insertionIndex);
    }

    private void UpdateThumbnailExternalDropIndicator(Point position)
    {
        int insertionIndex = ResolveThumbnailDropIndex(position);
        double indicatorY = ResolveThumbnailDropIndicatorY(insertionIndex);
        ThumbnailDropIndicator.Margin = new Thickness(8, indicatorY, 8, 0);
        ThumbnailDropIndicator.Visibility = Visibility.Visible;
    }

    private void HideThumbnailDropIndicator()
    {
        ThumbnailDropIndicator.Visibility = Visibility.Collapsed;
    }

    private int ResolveThumbnailDropIndex(Point position)
    {
        if (_viewModel.ActiveDocumentTab is null)
        {
            return 0;
        }

        WindowsDocumentTabViewModel? tab = _viewModel.ActiveDocumentTab;
        Panel? panel = GetThumbnailPanel();
        if (panel is not null)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(panel);
            for (int i = 0; i < childCount; i++)
            {
                if (GetThumbnailItemBounds(panel, i) is not { } bounds)
                {
                    continue;
                }

                if (position.Y < bounds.Y + (bounds.Height / 2))
                {
                    return Math.Clamp(i, 0, tab.Thumbnails.Count);
                }
            }

            if (childCount > 0)
            {
                return tab.Thumbnails.Count;
            }
        }

        double adjustedY = position.Y + ThumbnailScrollViewer.VerticalOffset;
        int estimatedIndex = (int)Math.Round(adjustedY / ThumbnailItemHeight);
        return Math.Clamp(estimatedIndex, 0, tab.Thumbnails.Count);
    }

    private double ResolveThumbnailDropIndicatorY(int insertionIndex)
    {
        Panel? panel = GetThumbnailPanel();
        if (panel is null)
        {
            return Math.Max(0, insertionIndex * ThumbnailItemHeight - ThumbnailScrollViewer.VerticalOffset);
        }

        int childCount = VisualTreeHelper.GetChildrenCount(panel);
        if (childCount == 0)
        {
            return 0;
        }

        if (insertionIndex <= 0 &&
            GetThumbnailItemBounds(panel, 0) is { } firstBounds)
        {
            return Math.Max(0, firstBounds.Y);
        }

        if (insertionIndex >= childCount &&
            GetThumbnailItemBounds(panel, childCount - 1) is { } lastBounds)
        {
            return Math.Max(0, lastBounds.Y + lastBounds.Height);
        }

        if (GetThumbnailItemBounds(panel, insertionIndex) is { } targetBounds)
        {
            return Math.Max(0, targetBounds.Y);
        }

        return Math.Max(0, insertionIndex * ThumbnailItemHeight - ThumbnailScrollViewer.VerticalOffset);
    }

    private Rect? GetThumbnailItemBounds(Panel panel, int index)
    {
        if (index < 0 || index >= VisualTreeHelper.GetChildrenCount(panel))
        {
            return null;
        }

        if (VisualTreeHelper.GetChild(panel, index) is not FrameworkElement element)
        {
            return null;
        }

        Point topLeft = element.TransformToVisual(ThumbnailDragSurface).TransformPoint(new Point(0, 0));
        return new Rect(topLeft.X, topLeft.Y, element.ActualWidth, element.ActualHeight);
    }

    private static T? FindChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            DependencyObject? child = VisualTreeHelper.GetChild(parent, i);
            if (child is T element && element.Name == name)
            {
                return element;
            }

            T? result = FindChild<T>(child, name);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private void OnAnnotationListItemTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsAnnotationOverlayViewModel overlay })
        {
            _viewModel.SelectedAnnotationId = overlay.Id;
        }
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

    private void OnAnnotationFillColorClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsAnnotationColorItem color })
        {
            _viewModel.SelectAnnotationFillColor(color);
        }
    }

    private void OnAnnotationTextDraftKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Enter)
        {
            CoreVirtualKeyStates shiftState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
            if (!shiftState.HasFlag(CoreVirtualKeyStates.Down))
            {
                DocumentPageLayer.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }
    }

    private void OnCommentCardDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsCommentOverlayViewModel comment })
        {
            _viewModel.BeginCommentEdit(comment);
            e.Handled = true;
        }
    }

    private void OnCommentEditLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsCommentOverlayViewModel comment })
        {
            _viewModel.CommitCommentEdit(comment);
        }
    }

    private void OnCommentEditKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: WindowsCommentOverlayViewModel comment })
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Escape:
                comment.IsEditing = false;
                e.Handled = true;
                break;
            case VirtualKey.Enter:
                {
                    CoreVirtualKeyStates altState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);
                    if (!altState.HasFlag(CoreVirtualKeyStates.Down))
                    {
                        _viewModel.CommitCommentEdit(comment);
                        e.Handled = true;
                    }

                    break;
                }
        }
    }

    private void OnDeleteAnnotationTapped(object sender, TappedRoutedEventArgs e)
    {
        Guid annotationId = sender is FrameworkElement { DataContext: WindowsAnnotationOverlayViewModel overlay }
            ? overlay.Id
            : sender is FrameworkElement { DataContext: WindowsCommentOverlayViewModel comment }
                ? comment.Id
                : Guid.Empty;

        if (annotationId == Guid.Empty ||
            !_viewModel.DeleteAnnotationByIdCommand.CanExecute(annotationId))
        {
            return;
        }

        _viewModel.DeleteAnnotationByIdCommand.Execute(annotationId);
        e.Handled = true;
    }

    private void OnAnnotationMenuEditClicked(object sender, RoutedEventArgs e)
    {
        if (ResolveAnnotationIdFromMenuContext(sender) is { } id)
        {
            _viewModel.BeginEditAnnotationById(id);
        }
    }

    private void OnAnnotationMenuHideClicked(object sender, RoutedEventArgs e)
    {
        if (ResolveAnnotationIdFromMenuContext(sender) is { } id)
        {
            _viewModel.ToggleAnnotationVisibility(id);
        }
    }

    private void OnAnnotationMenuLockClicked(object sender, RoutedEventArgs e)
    {
        if (ResolveAnnotationIdFromMenuContext(sender) is { } id)
        {
            _viewModel.ToggleAnnotationLock(id);
        }
    }

    private void OnAnnotationMenuDeleteClicked(object sender, RoutedEventArgs e)
    {
        if (ResolveAnnotationIdFromMenuContext(sender) is { } id &&
            _viewModel.DeleteAnnotationByIdCommand.CanExecute(id))
        {
            _viewModel.DeleteAnnotationByIdCommand.Execute(id);
        }
    }

    private static Guid? ResolveAnnotationIdFromMenuContext(object sender)
    {
        if (sender is FrameworkElement { Tag: Guid tagId })
        {
            return tagId;
        }

        if (sender is not FrameworkElement element)
        {
            return null;
        }

        FrameworkElement? parent = element;
        while (parent is not null)
        {
            switch (parent.DataContext)
            {
                case WindowsAnnotationOverlayViewModel overlay:
                    return overlay.Id;
                case WindowsCommentOverlayViewModel comment:
                    return comment.Id;
                default:
                    parent = VisualTreeHelper.GetParent(parent) as FrameworkElement;
                    break;
            }
        }

        return null;
    }

    private void OnInlineTextEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (_viewModel.ActiveDocumentTab?.InlineTextEditor is { } editor)
        {
            editor.Text = textBox.Text;
        }

        _viewModel.UpdateInlineTextAnnotation(textBox.Text);
    }

    private void OnInlineTextEditorLostFocus(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ActiveDocumentTab?.InlineTextEditor is null)
        {
            return;
        }

        _viewModel.CommitInlineTextAnnotation();
    }

    private void OnInlineTextEditorKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Escape:
                _viewModel.CancelInlineTextAnnotation();
                e.Handled = true;
                return;
            case VirtualKey.Enter:
                {
                    CoreVirtualKeyStates altState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);
                    if (altState.HasFlag(CoreVirtualKeyStates.Down))
                    {
                        return;
                    }

                    _viewModel.CommitInlineTextAnnotation();
                    e.Handled = true;
                    break;
                }
        }
    }

    private void FocusInlineTextEditor()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_viewModel.ActiveDocumentTab?.HasInlineTextEditor is not true)
            {
                return;
            }

            InlineTextEditorBox.Focus(FocusState.Programmatic);
            InlineTextEditorBox.SelectAll();
        });
    }

    private void OnSignaturePadPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement layer ||
            sender is not UIElement element)
        {
            return;
        }

        Point point = e.GetCurrentPoint(layer).Position;
        _isCapturingSignaturePad = true;
        _viewModel.BeginSignatureCapture(point.X, point.Y, layer.ActualWidth, layer.ActualHeight);
        element.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnSignaturePadPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isCapturingSignaturePad ||
            sender is not FrameworkElement layer)
        {
            return;
        }

        Point point = e.GetCurrentPoint(layer).Position;
        _viewModel.UpdateSignatureCapture(point.X, point.Y, layer.ActualWidth, layer.ActualHeight);
        e.Handled = true;
    }

    private void OnSignaturePadPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isCapturingSignaturePad)
        {
            return;
        }

        if (sender is UIElement element)
        {
            element.ReleasePointerCapture(e.Pointer);
        }

        _viewModel.CompleteSignatureCapture();
        _isCapturingSignaturePad = false;
        e.Handled = true;
    }

    private void OnSignaturePadPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (!_isCapturingSignaturePad)
        {
            return;
        }

        if (sender is UIElement element)
        {
            element.ReleasePointerCapture(e.Pointer);
        }

        _viewModel.CompleteSignatureCapture();
        _isCapturingSignaturePad = false;
        e.Handled = true;
    }

    private void OnDocumentLayerPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement layer ||
            sender is not UIElement element)
        {
            return;
        }

        Point point = e.GetCurrentPoint(layer).Position;
        if (IsPointerFromTextInput(e.OriginalSource))
        {
            return;
        }

        if (_viewModel.ActiveDocumentTab?.SelectedAnnotationTool is AnnotationTool.Select)
        {
            if (_viewModel.BeginAnnotationMove(point.X, point.Y, layer.ActualWidth, layer.ActualHeight))
            {
                _isMovingAnnotation = true;
                element.CapturePointer(e.Pointer);
                e.Handled = true;
                return;
            }

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

        if (_viewModel.ActiveDocumentTab?.SelectedAnnotationTool is AnnotationTool.Highlight)
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

        Point point = e.GetCurrentPoint(layer).Position;
        if (_isMovingAnnotation)
        {
            _viewModel.UpdateAnnotationMove(point.X, point.Y, layer.ActualWidth, layer.ActualHeight);
            e.Handled = true;
            return;
        }

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
        if ((!_isAnnotationInteractionActive && !_isTextSelectionInteractionActive && !_isMovingAnnotation) ||
            sender is not FrameworkElement layer ||
            sender is not UIElement element)
        {
            return;
        }

        Point point = e.GetCurrentPoint(layer).Position;
        if (_isMovingAnnotation)
        {
            _viewModel.CompleteAnnotationMove(point.X, point.Y, layer.ActualWidth, layer.ActualHeight);
            element.ReleasePointerCapture(e.Pointer);
            _isMovingAnnotation = false;
            e.Handled = true;
            return;
        }

        if (_isTextSelectionInteractionActive)
        {
            _viewModel.UpdateDocumentTextSelection(point.X, point.Y, layer.ActualWidth, layer.ActualHeight);
            if (_viewModel.ActiveDocumentTab?.SelectedAnnotationTool is AnnotationTool.Highlight)
            {
                _viewModel.CreateHighlightFromTextSelection();
            }
            else
            {
                _viewModel.CompleteDocumentTextSelection();
            }

            element.ReleasePointerCapture(e.Pointer);
            _isTextSelectionInteractionActive = false;
            e.Handled = true;
            return;
        }

        AnnotationTool? annotationTool = _viewModel.ActiveDocumentTab?.SelectedAnnotationTool;
        _viewModel.CompleteAnnotationInteraction(point.X, point.Y, layer.ActualWidth, layer.ActualHeight);
        element.ReleasePointerCapture(e.Pointer);
        _isAnnotationInteractionActive = false;
        if (annotationTool is AnnotationTool.Text)
        {
            FocusInlineTextEditor();
        }

        e.Handled = true;
    }

    private void OnDocumentLayerPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (!_isAnnotationInteractionActive && !_isTextSelectionInteractionActive && !_isMovingAnnotation)
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

        _viewModel.CancelAnnotationMove();
        _viewModel.CancelAnnotationInteraction();
        _isAnnotationInteractionActive = false;
        _isTextSelectionInteractionActive = false;
        _isMovingAnnotation = false;
        e.Handled = true;
    }
}
