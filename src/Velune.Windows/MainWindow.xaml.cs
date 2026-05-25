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
    private readonly WindowsWindowContext _windowContext;
    private readonly WindowsWindowCoordinator _windowCoordinator;
    private readonly TaskCompletionSource _loadedCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private InputNonClientPointerSource? _nonClientPointerSource;
    private bool _isAnnotationInteractionActive;
    private bool _isTextSelectionInteractionActive;
    private bool _isMovingAnnotation;
    private bool _isCapturingSignaturePad;
    private bool _hasPresentedDocument;

    public WindowsMainViewModel ViewModel
    {
        get;
    }

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

        ViewModel = viewModel;
        _windowContext = windowContext;
        _windowCoordinator = windowCoordinator;

        InitializeComponent();

        windowContext.SetActiveWindow(this);
        Root.DataContext = viewModel;
        Title = viewModel.Labels.AppName;

        ContextMenuDeleteItem.Text = viewModel.Labels.AnnotationMenuDelete;
        ContextMenuRotate90Item.Text = viewModel.Labels.AnnotationMenuRotate90;
        ContextMenuResetRotationItem.Text = viewModel.Labels.AnnotationMenuResetRotation;
        ContextMenuFlipHItem.Text = viewModel.Labels.AnnotationMenuFlipH;
        ContextMenuFlipVItem.Text = viewModel.Labels.AnnotationMenuFlipV;

        ConfigureTitleBar();
        ApplyTheme();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
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
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
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
        ViewModel.NotifyBindingsRefresh();
        _ = EnsureActiveTabHydratedAfterLoadAsync();
    }

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateTitleBarInteractiveRegions();
    }

    private void OnDocumentScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ViewModel.SetDocumentViewerSize(e.NewSize.Width, e.NewSize.Height);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(WindowsMainViewModel.SelectedPreferenceTheme), StringComparison.Ordinal) ||
            string.Equals(e.PropertyName, nameof(WindowsMainViewModel.ActiveDocumentTab), StringComparison.Ordinal))
        {
            ApplyTheme();
        }

        if (!string.Equals(e.PropertyName, nameof(WindowsMainViewModel.ActiveDocumentTab), StringComparison.Ordinal))
        {
            return;
        }

        if (ViewModel.ActiveDocumentTab is not null)
        {
            _hasPresentedDocument = true;
            return;
        }

        if (!_hasPresentedDocument || ViewModel.DocumentTabs.Count != 0)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_hasPresentedDocument ||
                ViewModel.ActiveDocumentTab is not null ||
                ViewModel.DocumentTabs.Count > 0)
            {
                return;
            }

            _windowCoordinator.ReturnToWelcome(this);
        });
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
        catch
        {
            // Window is not ready.
        }

        foreach (WindowsDocumentTabViewModel tab in ViewModel.DocumentTabs)
        {
            tab.SetTheme(isLight);
        }
    }

    private bool IsLightTheme()
    {
        return string.Equals(
                ViewModel.SelectedPreferenceTheme,
                ViewModel.Labels.PreferencesLight,
                StringComparison.Ordinal)
            || (string.Equals(
                    ViewModel.SelectedPreferenceTheme,
                    ViewModel.Labels.PreferencesSystem,
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
        catch
        {
            // Do nothing
        }
    }

    private static bool IsSystemLightTheme()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            object? value = key?.GetValue("AppsUseLightTheme");
            if (value is int intValue)
            {
                return intValue == 1;
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
            ViewModel.ActivateTabCommand.CanExecute(tab))
        {
            await ViewModel.ActivateTabCommand.ExecuteAsync(tab);
        }
    }

    private async void OnTabClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsDocumentTabViewModel tab } &&
            ViewModel.ActivateTabCommand.CanExecute(tab))
        {
            await ViewModel.ActivateTabCommand.ExecuteAsync(tab);
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
            ViewModel.CloseTabCommand.CanExecute(tab))
        {
            await ViewModel.CloseTabCommand.ExecuteAsync(tab);
        }
    }

    private async void OnCloseActiveTabMenuClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ActiveDocumentTab is { } tab &&
            ViewModel.CloseTabCommand.CanExecute(tab))
        {
            await ViewModel.CloseTabCommand.ExecuteAsync(tab);
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
            if (ViewModel.SearchTextCommand.CanExecute(null))
            {
                await ViewModel.SearchTextCommand.ExecuteAsync(null);
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
                ViewModel.OpenSearchResultCommand.CanExecute(result))
            {
                await ViewModel.OpenSearchResultCommand.ExecuteAsync(result);
            }
        }
        catch (Exception exception)
        {
            ReportUnhandledUiCommandError(exception);
        }
    }

    private void ReportUnhandledUiCommandError(Exception exception)
    {
        DispatcherQueue.TryEnqueue(() => ViewModel.StatusText = exception.Message);
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
        if (ViewModel.OpenSearchCommand.CanExecute(null))
        {
            ViewModel.OpenSearchCommand.Execute(null);
        }

        args.Handled = true;
    }

    private async void OnSaveKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (ViewModel.SaveDocumentCommand.CanExecute(null))
        {
            await ViewModel.SaveDocumentCommand.ExecuteAsync(null);
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

        if (ViewModel.UndoActionCommand.CanExecute(null))
        {
            ViewModel.UndoActionCommand.Execute(null);
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

        if (ViewModel.RedoActionCommand.CanExecute(null))
        {
            ViewModel.RedoActionCommand.Execute(null);
        }

        args.Handled = true;
    }

    private void OnToggleTextBoldKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!ViewModel.HasSelectedTextAnnotation)
        {
            return;
        }

        ViewModel.ToggleTextBoldCommand.Execute(null);
        args.Handled = true;
    }

    private void OnToggleTextItalicKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!ViewModel.HasSelectedTextAnnotation)
        {
            return;
        }

        ViewModel.ToggleTextItalicCommand.Execute(null);
        args.Handled = true;
    }

    private void OnToggleTextUnderlineKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!ViewModel.HasSelectedTextAnnotation)
        {
            return;
        }

        ViewModel.ToggleTextUnderlineCommand.Execute(null);
        args.Handled = true;
    }

    private void OnEscapeKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel.ActiveDocumentTab?.HasInlineTextEditor is true)
        {
            ViewModel.CommitInlineTextAnnotation();
            ViewModel.SelectedAnnotationId = null;
            args.Handled = true;
            return;
        }

        if (ViewModel.SelectedAnnotationId is not null)
        {
            ViewModel.SelectedAnnotationId = null;
            args.Handled = true;
        }
    }

    private void OnDeleteAnnotationKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel.SelectedAnnotationId is not null)
        {
            ViewModel.DeleteSelectedAnnotation();
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

        ViewModel.AdjustDocumentTextSelection(1);
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

        ViewModel.AdjustDocumentTextSelection(-1);
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

        ViewModel.AdjustDocumentTextSelectionByWord(1);
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

        ViewModel.AdjustDocumentTextSelectionByWord(-1);
        args.Handled = true;
    }

    private async void OnRotateRightKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ViewModel.RotateSelectedPageAsync(clockwise: true);
    }

    private async void OnRotateLeftKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ViewModel.RotateSelectedPageAsync(clockwise: false);
    }

    private async void OnFitPageKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (ViewModel.FitPageCommand.CanExecute(null))
        {
            await ViewModel.FitPageCommand.ExecuteAsync(null);
        }
    }

    private async void OnActualSizeKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (ViewModel.ActualSizeCommand.CanExecute(null))
        {
            await ViewModel.ActualSizeCommand.ExecuteAsync(null);
        }
    }

    private void OnFlipHorizontalKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (ViewModel.SelectedAnnotationId is not null)
        {
            ViewModel.FlipSelectedAnnotationHorizontally();
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

        if (ViewModel.NextPageCommand.CanExecute(null))
        {
            await ViewModel.NextPageCommand.ExecuteAsync(null);
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

        if (ViewModel.PreviousPageCommand.CanExecute(null))
        {
            await ViewModel.PreviousPageCommand.ExecuteAsync(null);
        }
    }

    private async void OnZoomInKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (ViewModel.ZoomInCommand.CanExecute(null))
        {
            await ViewModel.ZoomInCommand.ExecuteAsync(null);
        }
    }

    private async void OnZoomOutKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (ViewModel.ZoomOutCommand.CanExecute(null))
        {
            await ViewModel.ZoomOutCommand.ExecuteAsync(null);
        }
    }

    private void OnDocumentLayerRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (ViewModel.SelectedAnnotationId is null)
        {
            AnnotationContextMenu.Hide();
            e.Handled = true;
        }
    }

    private void OnContextMenuDelete(object sender, RoutedEventArgs e)
    {
        ViewModel.DeleteSelectedAnnotation();
    }

    private void OnContextMenuRotate90(object sender, RoutedEventArgs e)
    {
        ViewModel.RotateSelectedAnnotation90();
    }

    private void OnContextMenuResetRotation(object sender, RoutedEventArgs e)
    {
        ViewModel.ResetSelectedAnnotationRotation();
    }

    private void OnContextMenuFlipH(object sender, RoutedEventArgs e)
    {
        ViewModel.FlipSelectedAnnotationHorizontally();
    }

    private void OnContextMenuFlipV(object sender, RoutedEventArgs e)
    {
        ViewModel.FlipSelectedAnnotationVertically();
    }

    private bool CopySelectedDocumentTextToClipboard()
    {
        if (string.IsNullOrWhiteSpace(ViewModel.SelectedDocumentText))
        {
            return false;
        }

        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(ViewModel.SelectedDocumentText);
            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();
            ViewModel.NotifySelectedDocumentTextCopied();
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
            await ViewModel.EnsureActiveTabHydratedAsync();
        }
        catch (Exception exception)
        {
            ReportUnhandledUiCommandError(exception);
        }
    }

    private void SyncSearchQueryFromTextBox(object sender)
    {
        if (sender is TextBox textBox &&
            ViewModel.ActiveDocumentTab is { } tab)
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

    private void OnThumbnailItemLoaded(object sender, RoutedEventArgs e)
    {
        QueueThumbnailRenderFromElement(sender as FrameworkElement);
    }

    private void OnThumbnailElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        QueueThumbnailRenderFromElement(args.Element as FrameworkElement);
    }

    private void QueueThumbnailRenderFromElement(FrameworkElement? element)
    {
        if (element is not null && ResolveThumbnailItem(element) is WindowsPageThumbnailViewModel thumbnail)
        {
            _ = ViewModel.EnsureThumbnailRenderedAsync(thumbnail);
        }
    }

    private void OnOpenPageOrganizerClicked(object sender, RoutedEventArgs e)
    {
        WindowsDocumentTabViewModel? tab = ViewModel.ActiveDocumentTab;
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
                _ = ViewModel.ApplyPageOrganizerResultAsync(vm.GetFinalPageOrder(), vm.GetRotations());
            }
        });

        window.Activate();
    }

    private void OnThumbnailItemRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement element &&
            ResolveThumbnailItem(element) is WindowsPageThumbnailViewModel thumbnail)
        {
            ViewModel.SelectedThumbnailPageNumber = thumbnail.PageNumber;
        }
    }

    private void OnRibbonRotateLeftClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ActiveDocumentTab is not null)
        {
            ViewModel.SelectedThumbnailPageNumber = ViewModel.ActiveDocumentTab.CurrentPage;
        }

        _ = ViewModel.RotateSelectedPageAsync(false);
    }

    private void OnRibbonRotateRightClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ActiveDocumentTab is not null)
        {
            ViewModel.SelectedThumbnailPageNumber = ViewModel.ActiveDocumentTab.CurrentPage;
        }

        _ = ViewModel.RotateSelectedPageAsync(true);
    }

    private void OnThumbnailRotateLeftClick(object sender, RoutedEventArgs e) =>
        _ = ViewModel.RotateSelectedPageAsync(false);

    private void OnThumbnailRotateRightClick(object sender, RoutedEventArgs e) =>
        _ = ViewModel.RotateSelectedPageAsync(true);

    private void OnThumbnailMoveUpClick(object sender, RoutedEventArgs e) =>
        _ = ViewModel.MoveSelectedPageAsync(-1);

    private void OnThumbnailMoveDownClick(object sender, RoutedEventArgs e) =>
        _ = ViewModel.MoveSelectedPageAsync(1);

    private void OnThumbnailDeleteClick(object sender, RoutedEventArgs e) =>
        _ = ViewModel.DeleteSelectedPageAsync();

    private void OnThumbnailItemPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not UIElement element ||
            element is not FrameworkElement frameworkElement ||
            ResolveThumbnailItem(frameworkElement) is not WindowsPageThumbnailViewModel thumbnail)
        {
            return;
        }

        PointerPointProperties? properties = e.GetCurrentPoint(element).Properties;
        if (!properties.IsLeftButtonPressed)
        {
            return;
        }

        _thumbnailDragSourceIndex = ViewModel.ActiveDocumentTab?.Thumbnails.IndexOf(thumbnail) ?? -1;
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
            WindowsDocumentTabViewModel? tab = ViewModel.ActiveDocumentTab;
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
        e.Handled = true;

        if (sender is UIElement element)
        {
            element.ReleasePointerCapture(e.Pointer);
        }

        bool wasDragging = _isDraggingThumbnail;
        int sourceIndex = _thumbnailDragSourceIndex;
        int targetIndex = _thumbnailDropTargetIndex;
        WindowsPageThumbnailViewModel? clickedThumbnail = !wasDragging &&
            sourceIndex >= 0 &&
            sender is FrameworkElement releasedElement
                ? ResolveThumbnailItem(releasedElement)
                : null;

        ThumbnailDragGhost.Visibility = Visibility.Collapsed;
        ThumbnailDropIndicator.Visibility = Visibility.Collapsed;
        ResetThumbnailDisplacement();

        _thumbnailDragSourceIndex = -1;
        _thumbnailDropTargetIndex = -1;
        _isDraggingThumbnail = false;

        if (wasDragging && sourceIndex >= 0 && targetIndex >= 0)
        {
            if (sourceIndex != targetIndex && targetIndex != sourceIndex + 1)
            {
                WindowsDocumentTabViewModel? tab = ViewModel.ActiveDocumentTab;
                if (tab is not null)
                {
                    int adjustedTarget = targetIndex > sourceIndex ? targetIndex - 1 : targetIndex;
                    tab.Thumbnails.Move(sourceIndex, adjustedTarget);
                    await ViewModel.HandleThumbnailReorderAsync(sourceIndex + 1, adjustedTarget);
                }
            }
        }
        else if (clickedThumbnail is not null)
        {
            await ViewModel.ChangePageAsync(clickedThumbnail.PageNumber);
        }
    }

    private WindowsPageThumbnailViewModel? ResolveThumbnailItem(FrameworkElement element)
    {
        if (element.DataContext is WindowsPageThumbnailViewModel thumbnail)
        {
            return thumbnail;
        }

        if (element.Tag is int pageNumber &&
            ViewModel.ActiveDocumentTab is { } tab)
        {
            int index = pageNumber - 1;
            if (index >= 0 &&
                index < tab.Thumbnails.Count &&
                tab.Thumbnails[index].PageNumber == pageNumber)
            {
                return tab.Thumbnails[index];
            }

            return tab.Thumbnails.FirstOrDefault(item => item.PageNumber == pageNumber);
        }

        return null;
    }

    private void UpdateThumbnailDropIndicator(double pointerY)
    {
        double scrollOffset = ThumbnailScrollViewer.VerticalOffset;
        double adjustedY = pointerY + scrollOffset;
        int index = (int)Math.Round(adjustedY / ThumbnailItemHeight);
        WindowsDocumentTabViewModel? tab = ViewModel.ActiveDocumentTab;
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
        WindowsDocumentTabViewModel? tab = ViewModel.ActiveDocumentTab;
        if (tab is null)
        {
            return;
        }

        for (int i = 0; i < tab.Thumbnails.Count; i++)
        {
            if (ThumbnailListView.TryGetElement(i) is not UIElement element)
            {
                continue;
            }

            bool shouldDisplace = i >= _thumbnailDropTargetIndex && i != _thumbnailDragSourceIndex;
            element.Translation = new System.Numerics.Vector3(0, shouldDisplace ? gapSize : 0f, 0);
        }
    }

    private void ResetThumbnailDisplacement()
    {
        WindowsDocumentTabViewModel? tab = ViewModel.ActiveDocumentTab;
        if (tab is null)
        {
            return;
        }

        for (int i = 0; i < tab.Thumbnails.Count; i++)
        {
            if (ThumbnailListView.TryGetElement(i) is UIElement element)
            {
                element.Translation = System.Numerics.Vector3.Zero;
            }
        }
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
        if (!ViewModel.CanAcceptThumbnailDrop)
        {
            e.AcceptedOperation = DataPackageOperation.None;
            HideThumbnailDropIndicator();
            return;
        }

        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = ViewModel.Labels.Insert;
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
        if (!ViewModel.CanAcceptThumbnailDrop ||
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

        await ViewModel.HandleThumbnailFilesDroppedAsync(filePaths, insertionIndex);
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
        if (ViewModel.ActiveDocumentTab is null)
        {
            return 0;
        }

        WindowsDocumentTabViewModel? tab = ViewModel.ActiveDocumentTab;
        for (int i = 0; i < tab.Thumbnails.Count; i++)
        {
            if (GetThumbnailItemBounds(i) is not { } bounds)
            {
                continue;
            }

            if (position.Y < bounds.Y + (bounds.Height / 2))
            {
                return Math.Clamp(i, 0, tab.Thumbnails.Count);
            }
        }

        double adjustedY = position.Y + ThumbnailScrollViewer.VerticalOffset;
        int estimatedIndex = (int)Math.Round(adjustedY / ThumbnailItemHeight);
        return Math.Clamp(estimatedIndex, 0, tab.Thumbnails.Count);
    }

    private double ResolveThumbnailDropIndicatorY(int insertionIndex)
    {
        if (ViewModel.ActiveDocumentTab is not { } tab)
        {
            return Math.Max(0, insertionIndex * ThumbnailItemHeight - ThumbnailScrollViewer.VerticalOffset);
        }

        if (insertionIndex <= 0 &&
            GetThumbnailItemBounds(0) is { } firstBounds)
        {
            return Math.Max(0, firstBounds.Y);
        }

        if (insertionIndex >= tab.Thumbnails.Count &&
            GetThumbnailItemBounds(tab.Thumbnails.Count - 1) is { } lastBounds)
        {
            return Math.Max(0, lastBounds.Y + lastBounds.Height);
        }

        if (GetThumbnailItemBounds(insertionIndex) is { } targetBounds)
        {
            return Math.Max(0, targetBounds.Y);
        }

        return Math.Max(0, insertionIndex * ThumbnailItemHeight - ThumbnailScrollViewer.VerticalOffset);
    }

    private Rect? GetThumbnailItemBounds(int index)
    {
        if (index < 0 ||
            ViewModel.ActiveDocumentTab is not { } tab ||
            index >= tab.Thumbnails.Count)
        {
            return null;
        }

        if (ThumbnailListView.TryGetElement(index) is not FrameworkElement element)
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
            ViewModel.SelectedAnnotationId = overlay.Id;
        }
    }

    private void OnAnnotationToolClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsAnnotationToolItem tool } &&
            ViewModel.SelectAnnotationToolCommand.CanExecute(tool.Tool))
        {
            ViewModel.SelectAnnotationToolCommand.Execute(tool.Tool);
        }
    }

    private void OnAnnotationColorClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsAnnotationColorItem color } &&
            ViewModel.SelectAnnotationColorCommand.CanExecute(color))
        {
            ViewModel.SelectAnnotationColorCommand.Execute(color);
        }
    }

    private void OnAnnotationFillColorClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsAnnotationColorItem color })
        {
            ViewModel.SelectAnnotationFillColor(color);
        }
    }

    private void OnAnnotationTextTransparentBackgroundClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.AnnotationFillEnabled = false;
    }

    private void OnAnnotationBorderColorClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsAnnotationColorItem color })
        {
            ViewModel.SelectAnnotationBorderColor(color);
        }
    }

    private void OnSignatureAssetClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SignatureAsset asset } &&
            ViewModel.SelectSignatureAssetCommand.CanExecute(asset.Id))
        {
            ViewModel.SelectSignatureAssetCommand.Execute(asset.Id);
        }
    }

    private void OnDeleteSignatureAssetClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SignatureAsset asset } &&
            ViewModel.DeleteSelectedSignatureAssetCommand.CanExecute(asset.Id))
        {
            ViewModel.DeleteSelectedSignatureAssetCommand.Execute(asset.Id);
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
            ViewModel.BeginCommentEdit(comment);
            e.Handled = true;
        }
    }

    private void OnCommentEditLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WindowsCommentOverlayViewModel comment })
        {
            ViewModel.CommitCommentEdit(comment);
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
                        ViewModel.CommitCommentEdit(comment);
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
            !ViewModel.DeleteAnnotationByIdCommand.CanExecute(annotationId))
        {
            return;
        }

        ViewModel.DeleteAnnotationByIdCommand.Execute(annotationId);
        e.Handled = true;
    }

    private void OnDeleteSelectedAnnotationButtonClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.DeleteSelectedAnnotation();
    }

    private void OnAnnotationMenuEditClicked(object sender, RoutedEventArgs e)
    {
        if (ResolveAnnotationIdFromMenuContext(sender) is { } id)
        {
            ViewModel.BeginEditAnnotationById(id);
        }
    }

    private void OnAnnotationMenuHideClicked(object sender, RoutedEventArgs e)
    {
        if (ResolveAnnotationIdFromMenuContext(sender) is { } id)
        {
            ViewModel.ToggleAnnotationVisibility(id);
        }
    }

    private void OnAnnotationMenuLockClicked(object sender, RoutedEventArgs e)
    {
        if (ResolveAnnotationIdFromMenuContext(sender) is { } id)
        {
            ViewModel.ToggleAnnotationLock(id);
        }
    }

    private void OnAnnotationMenuDeleteClicked(object sender, RoutedEventArgs e)
    {
        if (ResolveAnnotationIdFromMenuContext(sender) is { } id &&
            ViewModel.DeleteAnnotationByIdCommand.CanExecute(id))
        {
            ViewModel.DeleteAnnotationByIdCommand.Execute(id);
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

    private static bool IsDescendantOf(DependencyObject child, DependencyObject parent)
    {
        DependencyObject? current = child;
        while (current is not null)
        {
            if (ReferenceEquals(current, parent))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void OnInlineTextEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (ViewModel.ActiveDocumentTab?.InlineTextEditor is { } editor)
        {
            editor.Text = textBox.Text;
        }

        ViewModel.UpdateInlineTextAnnotation(textBox.Text);
    }

    private void OnInlineTextEditorLostFocus(object sender, RoutedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (ViewModel.ActiveDocumentTab?.InlineTextEditor is null)
            {
                return;
            }

            object? focused = FocusManager.GetFocusedElement(Root.XamlRoot);
            if (focused is DependencyObject dependencyObject &&
                (IsDescendantOf(dependencyObject, TextAnnotationToolbar) ||
                 IsDescendantOf(dependencyObject, AnnotationsPanel)))
            {
                return;
            }

            ViewModel.CommitInlineTextAnnotation();
        });
    }

    private void OnInlineTextEditorKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Escape:
                ViewModel.CommitInlineTextAnnotation();
                ViewModel.SelectedAnnotationId = null;
                e.Handled = true;
                return;
            case VirtualKey.Enter:
                {
                    CoreVirtualKeyStates altState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);
                    if (altState.HasFlag(CoreVirtualKeyStates.Down))
                    {
                        return;
                    }

                    ViewModel.CommitInlineTextAnnotation();
                    e.Handled = true;
                    break;
                }
        }
    }

    private void FocusInlineTextEditor()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (ViewModel.ActiveDocumentTab?.HasInlineTextEditor is not true)
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
        ViewModel.BeginSignatureCapture(point.X, point.Y, layer.ActualWidth, layer.ActualHeight);
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
        ViewModel.UpdateSignatureCapture(point.X, point.Y, layer.ActualWidth, layer.ActualHeight);
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

        ViewModel.CompleteSignatureCapture();
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

        ViewModel.CompleteSignatureCapture();
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

        if (ViewModel.ActiveDocumentTab?.SelectedAnnotationTool is AnnotationTool.Select)
        {
            if (ViewModel.BeginAnnotationMove(point.X, point.Y, layer.ActualWidth, layer.ActualHeight))
            {
                _isMovingAnnotation = true;
                element.CapturePointer(e.Pointer);
                e.Handled = true;
                return;
            }

            ViewModel.ClearDocumentTextSelection();
            _isTextSelectionInteractionActive = ViewModel.BeginDocumentTextSelection(
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

        if (ViewModel.ActiveDocumentTab?.SelectedAnnotationTool is AnnotationTool.Highlight)
        {
            ViewModel.ClearDocumentTextSelection();
            _isTextSelectionInteractionActive = ViewModel.BeginDocumentTextSelection(
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

        _isAnnotationInteractionActive = ViewModel.BeginAnnotationInteraction(
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
            ViewModel.UpdateAnnotationMove(point.X, point.Y, layer.ActualWidth, layer.ActualHeight);
            e.Handled = true;
            return;
        }

        if (_isTextSelectionInteractionActive)
        {
            ViewModel.UpdateDocumentTextSelection(point.X, point.Y, layer.ActualWidth, layer.ActualHeight);
            e.Handled = true;
            return;
        }

        if (!_isAnnotationInteractionActive)
        {
            return;
        }

        ViewModel.UpdateAnnotationInteraction(point.X, point.Y, layer.ActualWidth, layer.ActualHeight);
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
            ViewModel.CompleteAnnotationMove(point.X, point.Y, layer.ActualWidth, layer.ActualHeight);
            element.ReleasePointerCapture(e.Pointer);
            _isMovingAnnotation = false;
            e.Handled = true;
            return;
        }

        if (_isTextSelectionInteractionActive)
        {
            ViewModel.UpdateDocumentTextSelection(point.X, point.Y, layer.ActualWidth, layer.ActualHeight);
            if (ViewModel.ActiveDocumentTab?.SelectedAnnotationTool is AnnotationTool.Highlight)
            {
                ViewModel.CreateHighlightFromTextSelection();
            }
            else
            {
                ViewModel.CompleteDocumentTextSelection();
            }

            element.ReleasePointerCapture(e.Pointer);
            _isTextSelectionInteractionActive = false;
            e.Handled = true;
            return;
        }

        AnnotationTool? annotationTool = ViewModel.ActiveDocumentTab?.SelectedAnnotationTool;
        ViewModel.CompleteAnnotationInteraction(point.X, point.Y, layer.ActualWidth, layer.ActualHeight);
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
            ViewModel.CompleteDocumentTextSelection();
        }

        ViewModel.CancelAnnotationMove();
        ViewModel.CancelAnnotationInteraction();
        _isAnnotationInteractionActive = false;
        _isTextSelectionInteractionActive = false;
        _isMovingAnnotation = false;
        e.Handled = true;
    }
}
