using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Velune.Windows.ViewModels;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

namespace Velune.Windows;

/// <summary>
/// Secondary window for reordering, rotating, duplicating, and deleting PDF pages.
/// </summary>
public sealed partial class PageOrganizerWindow : Window
{
    private const double DragThreshold = 8.0;
    private const double ItemWidth = 160.0;
    private const double ItemHeight = 220.0;
    private const double ItemSpacing = 12.0;
    private const float DisplacementX = 30f;

    private readonly PageOrganizerViewModel _viewModel;
    private readonly Action<bool>? _onClosed;
    private Point _dragStartPoint;
    private bool _isDragging;
    private int _dragSourceIndex = -1;
    private int _currentDropIndex = -1;

    /// <summary>
    /// Initializes the page organizer window with its view model.
    /// </summary>
    /// <param name="viewModel">The view model managing page operations.</param>
    /// <param name="onClosed">Callback invoked when the window closes, indicating whether changes were applied.</param>
    public PageOrganizerWindow(PageOrganizerViewModel viewModel, Action<bool>? onClosed = null)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        _viewModel = viewModel;
        _onClosed = onClosed;

        InitializeComponent();

        Root.DataContext = viewModel;
        Title = viewModel.WindowTitle;

        ConfigureWindow();
        ApplyTheme();

        Closed += OnWindowClosed;
    }

    /// <summary>
    /// Gets whether the user applied their page changes before closing.
    /// </summary>
    public bool Applied
    {
        get; private set;
    }

    private void ConfigureWindow()
    {
        var appWindow = GetAppWindow();
        appWindow.Resize(new SizeInt32(900, 650));
        appWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "Velune.ico"));

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = false;
        }
    }

    private void ApplyTheme()
    {
        var isLight = Microsoft.UI.Xaml.Application.Current.RequestedTheme == ApplicationTheme.Light;
        Root.RequestedTheme = isLight ? ElementTheme.Light : ElementTheme.Dark;

        var appWindow = GetAppWindow();
        appWindow.TitleBar.BackgroundColor = isLight
            ? global::Windows.UI.Color.FromArgb(255, 255, 255, 255)
            : global::Windows.UI.Color.FromArgb(255, 32, 32, 32);
        appWindow.TitleBar.ForegroundColor = isLight
            ? global::Windows.UI.Color.FromArgb(255, 17, 24, 39)
            : global::Windows.UI.Color.FromArgb(255, 255, 255, 255);
        appWindow.TitleBar.ButtonBackgroundColor = isLight
            ? global::Windows.UI.Color.FromArgb(255, 255, 255, 255)
            : global::Windows.UI.Color.FromArgb(255, 32, 32, 32);
        appWindow.TitleBar.ButtonForegroundColor = isLight
            ? global::Windows.UI.Color.FromArgb(255, 17, 24, 39)
            : global::Windows.UI.Color.FromArgb(255, 255, 255, 255);
        appWindow.TitleBar.InactiveBackgroundColor = appWindow.TitleBar.BackgroundColor;
        appWindow.TitleBar.ButtonInactiveBackgroundColor = appWindow.TitleBar.ButtonBackgroundColor;
    }

    private AppWindow GetAppWindow()
    {
        var handle = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
        return AppWindow.GetFromWindowId(windowId);
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        Closed -= OnWindowClosed;
        _onClosed?.Invoke(Applied);
    }

    // Command bar
    private void OnRotateLeftClick(object sender, RoutedEventArgs e) => _viewModel.RotateLeftSelectedCommand.Execute(null);
    private void OnRotateRightClick(object sender, RoutedEventArgs e) => _viewModel.RotateRightSelectedCommand.Execute(null);
    private void OnDeleteClick(object sender, RoutedEventArgs e) => _viewModel.DeleteSelectedCommand.Execute(null);
    private void OnDuplicateClick(object sender, RoutedEventArgs e) => _viewModel.DuplicateSelectedCommand.Execute(null);
    private void OnReverseClick(object sender, RoutedEventArgs e) => _viewModel.ReverseSelectedCommand.Execute(null);
    private void OnUndoClick(object sender, RoutedEventArgs e) => _viewModel.UndoCommand.Execute(null);
    private void OnRedoClick(object sender, RoutedEventArgs e) => _viewModel.RedoCommand.Execute(null);

    // Selection helpers
    private void OnSelectAllClick(object sender, RoutedEventArgs e) => _viewModel.SelectAllCommand.Execute(null);
    private void OnSelectOddClick(object sender, RoutedEventArgs e) => _viewModel.SelectOddPagesCommand.Execute(null);
    private void OnSelectEvenClick(object sender, RoutedEventArgs e) => _viewModel.SelectEvenPagesCommand.Execute(null);
    private void OnInvertSelectionClick(object sender, RoutedEventArgs e) => _viewModel.InvertSelectionCommand.Execute(null);
    private void OnDeselectAllClick(object sender, RoutedEventArgs e) => _viewModel.DeselectAllCommand.Execute(null);

    #region Drag-Drop

    private void OnPageItemPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        var properties = e.GetCurrentPoint(element).Properties;
        if (!properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragStartPoint = e.GetCurrentPoint(PageGridScrollViewer).Position;
        _isDragging = false;

        if (element.DataContext is PageOrganizerItemViewModel item)
        {
            _dragSourceIndex = _viewModel.Pages.IndexOf(item);

            var isCtrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
                .HasFlag(global::Windows.UI.Core.CoreVirtualKeyStates.Down);
            var isShift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
                .HasFlag(global::Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (!item.IsSelected && !isCtrl && !isShift)
            {
                _viewModel.ToggleSelection(item, false, false);
            }
            else if (isCtrl || isShift)
            {
                _viewModel.ToggleSelection(item, isCtrl, isShift);
            }
        }

        element.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPageItemPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragSourceIndex < 0 || sender is not FrameworkElement)
        {
            return;
        }

        var currentPoint = e.GetCurrentPoint(PageGridScrollViewer).Position;
        var deltaX = Math.Abs(currentPoint.X - _dragStartPoint.X);
        var deltaY = Math.Abs(currentPoint.Y - _dragStartPoint.Y);

        if (!_isDragging && (deltaX > DragThreshold || deltaY > DragThreshold))
        {
            _isDragging = true;
            BeginDrag();
        }

        if (_isDragging)
        {
            UpdateDragGhost(currentPoint);
            UpdateDropTarget(currentPoint);
            AutoScrollGrid(currentPoint.Y);
        }

        e.Handled = true;
    }

    private void OnPageItemPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            element.ReleasePointerCapture(e.Pointer);
        }

        if (_isDragging && _currentDropIndex >= 0)
        {
            CommitDrop();
        }

        EndDrag();
        e.Handled = true;
    }

    private void OnPageItemPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging && sender is Grid grid)
        {
            grid.Scale = new System.Numerics.Vector3(0.97f, 0.97f, 1.0f);
        }
    }

    private void OnPageItemPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging && sender is Grid grid)
        {
            grid.Scale = new System.Numerics.Vector3(1.0f, 1.0f, 1.0f);
        }
    }

    private void BeginDrag()
    {
        var sourceItem = _viewModel.Pages[_dragSourceIndex];
        if (sourceItem.Thumbnail is not null)
        {
            DragGhost.Source = sourceItem.Thumbnail;
        }

        DragGhost.Visibility = Visibility.Visible;

        if (PageGridRepeater.TryGetElement(_dragSourceIndex) is UIElement sourceElement)
        {
            sourceElement.Opacity = 0.4;
        }
    }

    private void UpdateDragGhost(Point position)
    {
        DragGhost.RenderTransform = new TranslateTransform
        {
            X = position.X - 50,
            Y = position.Y - 65
        };
    }

    private void UpdateDropTarget(Point position)
    {
        var newDropIndex = GetDropIndexFromPosition(position);

        if (newDropIndex == _currentDropIndex)
        {
            return;
        }

        _currentDropIndex = newDropIndex;
        AnimateGridDisplacement();
    }

    private int GetDropIndexFromPosition(Point position)
    {
        var cellWidth = ItemWidth + ItemSpacing;
        var cellHeight = ItemHeight + ItemSpacing;
        var scrollOffset = PageGridScrollViewer.VerticalOffset;
        var adjustedY = position.Y + scrollOffset;

        var repeaterWidth = PageGridRepeater.ActualWidth;
        var columns = Math.Max(1, (int)(repeaterWidth / cellWidth));

        var col = (int)(position.X / cellWidth);
        var row = (int)(adjustedY / cellHeight);

        col = Math.Clamp(col, 0, columns - 1);

        var index = (row * columns) + col;
        return Math.Clamp(index, 0, _viewModel.Pages.Count);
    }

    private void AnimateGridDisplacement()
    {
        for (var i = 0; i < _viewModel.Pages.Count; i++)
        {
            var element = PageGridRepeater.TryGetElement(i) as UIElement;
            if (element is null)
            {
                continue;
            }

            var isSource = _viewModel.Pages[i].IsSelected || i == _dragSourceIndex;
            if (isSource)
            {
                continue;
            }

            var shouldShift = i >= _currentDropIndex;
            element.Translation = new System.Numerics.Vector3(shouldShift ? DisplacementX : 0f, 0, 0);
        }
    }

    private void ResetGridDisplacement()
    {
        for (var i = 0; i < _viewModel.Pages.Count; i++)
        {
            var element = PageGridRepeater.TryGetElement(i) as UIElement;
            if (element is null)
            {
                continue;
            }

            element.Translation = System.Numerics.Vector3.Zero;
            element.Opacity = 1.0;
        }
    }

    private void CommitDrop()
    {
        var selected = _viewModel.Pages.Where(p => p.IsSelected).ToList();
        if (selected.Count == 0 && _dragSourceIndex >= 0 && _dragSourceIndex < _viewModel.Pages.Count)
        {
            selected = [_viewModel.Pages[_dragSourceIndex]];
        }

        if (selected.Count > 0)
        {
            _viewModel.MovePages(selected, _currentDropIndex);
        }
    }

    private void EndDrag()
    {
        ResetGridDisplacement();
        _isDragging = false;
        _dragSourceIndex = -1;
        _currentDropIndex = -1;
        DragGhost.Visibility = Visibility.Collapsed;
    }

    private void AutoScrollGrid(double pointerY)
    {
        var viewportHeight = PageGridScrollViewer.ViewportHeight;
        const double edgeZone = 40;
        const double scrollStep = 10;

        if (pointerY < edgeZone)
        {
            PageGridScrollViewer.ChangeView(null, PageGridScrollViewer.VerticalOffset - scrollStep, null, true);
        }
        else if (pointerY > viewportHeight - edgeZone)
        {
            PageGridScrollViewer.ChangeView(null, PageGridScrollViewer.VerticalOffset + scrollStep, null, true);
        }
    }

    #endregion

    // Footer buttons
    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Applied = false;
        Close();
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        Applied = true;
        Close();
    }

    // Keyboard accelerators
    private void OnSelectAllAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _viewModel.SelectAllCommand.Execute(null);
        args.Handled = true;
    }

    private void OnUndoAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _viewModel.UndoCommand.Execute(null);
        args.Handled = true;
    }

    private void OnRedoAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _viewModel.RedoCommand.Execute(null);
        args.Handled = true;
    }

    private void OnDuplicateAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _viewModel.DuplicateSelectedCommand.Execute(null);
        args.Handled = true;
    }

    private void OnDeleteAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _viewModel.DeleteSelectedCommand.Execute(null);
        args.Handled = true;
    }

    private void OnCancelAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        Applied = false;
        Close();
        args.Handled = true;
    }
}
