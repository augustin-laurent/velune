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

    private async void OnCloseActiveTabMenuClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ActiveDocumentTab is { } tab &&
            ViewModel.CloseTabCommand.CanExecute(tab))
        {
            await ViewModel.CloseTabCommand.ExecuteAsync(tab);
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

    private void SyncSearchQueryFromTextBox(object sender)
    {
        if (sender is TextBox textBox &&
            ViewModel.ActiveDocumentTab is { } tab)
        {
            tab.SearchQuery = textBox.Text;
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

    private void OnThumbnailMoveUpClick(object sender, RoutedEventArgs e) =>
        _ = ViewModel.MoveSelectedPageAsync(-1);

    private void OnThumbnailMoveDownClick(object sender, RoutedEventArgs e) =>
        _ = ViewModel.MoveSelectedPageAsync(1);

    private void OnThumbnailDeleteClick(object sender, RoutedEventArgs e) =>
        _ = ViewModel.DeleteSelectedPageAsync();

    private void OnCommentCardDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (BeginCommentEditFromElement(sender))
        {
            e.Handled = true;
        }
    }

    private void OnCommentCardKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is not VirtualKey.Enter and not VirtualKey.F2 ||
            !BeginCommentEditFromElement(sender))
        {
            return;
        }

        e.Handled = true;
    }

    private bool BeginCommentEditFromElement(object sender)
    {
        if (sender is not FrameworkElement { DataContext: WindowsCommentOverlayViewModel comment })
        {
            return false;
        }

        ViewModel.BeginCommentEdit(comment);
        return true;
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

    private void OnAnnotationTextDraftSubmitted(object sender, EventArgs e)
    {
        DocumentPageLayer.Focus(FocusState.Programmatic);
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
                 IsDescendantOf(dependencyObject, AnnotationsPanelControl)))
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
