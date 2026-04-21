using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Velune.Presentation.ViewModels;

namespace Velune.Presentation.Views;

public partial class MainWindow : Window
{
    private const double ThumbnailDragThreshold = 6;
    private const double ThumbnailAutoScrollEdge = 28;
    private const double ThumbnailAutoScrollStep = 12;
    private const double ThumbnailGhostLift = 4;

    private readonly DispatcherTimer _thumbnailAutoScrollTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private double _trackpadNavigationAccumulator;
    private int? _draggedThumbnailPageNumber;
    private int _thumbnailAutoScrollDirection;
    private int? _thumbnailPendingDropIndex;
    private Point _thumbnailDragPointerOffset;
    private Point? _thumbnailDragStartPoint;
    private Point? _lastThumbnailDragPosition;
    private bool _didReorderThumbnailDuringDrag;
    private bool _isDraggingThumbnail;
    private bool _isReleasingThumbnailPointer;
    private bool _suppressNextThumbnailTap;
    private Control? _documentTextSelectionLayer;
    private Control? _documentTextSelectionCoordinateLayer;
    private bool _isSelectingDocumentText;

    public MainWindow()
    {
        InitializeComponent();
        _thumbnailAutoScrollTimer.Tick += OnThumbnailAutoScrollTick;
    }

    [ActivatorUtilitiesConstructor]
    public MainWindow(MainWindowViewModel viewModel)
        : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        DataContext = viewModel;
        viewModel.UpdateWindowWidth(Width);
    }

    private async void OnDocumentPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (!viewModel.HasOpenDocument)
        {
            _trackpadNavigationAccumulator = 0;
            return;
        }

        if (HasZoomModifier(e.KeyModifiers))
        {
            _trackpadNavigationAccumulator = 0;

            if (Math.Abs(e.Delta.Y) <= double.Epsilon)
            {
                return;
            }

            e.Handled = true;
            await viewModel.HandleZoomPointerWheelAsync(e.Delta.Y);
            return;
        }

        if (viewModel.ShouldUseTrackpadForPan && CanPanDocumentViewer(e.Delta.Y))
        {
            _trackpadNavigationAccumulator = 0;
            return;
        }

        if (Math.Abs(e.Delta.Y) <= double.Epsilon)
        {
            return;
        }

        _trackpadNavigationAccumulator += e.Delta.Y;

        if (_trackpadNavigationAccumulator >= 1.0)
        {
            _trackpadNavigationAccumulator = 0;
            e.Handled = true;
            await viewModel.NavigateToPreviousPageFromTrackpadAsync();
            return;
        }

        if (_trackpadNavigationAccumulator <= -1.0)
        {
            _trackpadNavigationAccumulator = 0;
            e.Handled = true;
            await viewModel.NavigateToNextPageFromTrackpadAsync();
        }
    }

    private async void OnDocumentViewerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        await viewModel.UpdateDocumentViewportAsync(
            e.NewSize.Width,
            e.NewSize.Height);
    }

    private static bool HasZoomModifier(KeyModifiers modifiers)
    {
        return modifiers.HasFlag(KeyModifiers.Control) ||
               modifiers.HasFlag(KeyModifiers.Meta);
    }

    private bool CanPanDocumentViewer(double deltaY)
    {
        if (Math.Abs(deltaY) <= double.Epsilon)
        {
            return false;
        }

        var extentHeight = DocumentScrollViewer.Extent.Height;
        var viewportHeight = DocumentScrollViewer.Viewport.Height;
        if (extentHeight <= viewportHeight + double.Epsilon)
        {
            return false;
        }

        var offsetY = DocumentScrollViewer.Offset.Y;
        var maxOffsetY = Math.Max(0, extentHeight - viewportHeight);

        return deltaY > 0
            ? offsetY > double.Epsilon
            : offsetY < maxOffsetY - double.Epsilon;
    }

    private static bool HasCopyModifier(KeyModifiers modifiers)
    {
        return modifiers.HasFlag(KeyModifiers.Control) ||
               modifiers.HasFlag(KeyModifiers.Meta);
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            !HasCopyModifier(e.KeyModifiers) ||
            e.Key is not Key.C ||
            !viewModel.HasSelectedDocumentText ||
            string.IsNullOrWhiteSpace(viewModel.SelectedDocumentText))
        {
            return;
        }

        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox)
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        await clipboard.SetTextAsync(viewModel.SelectedDocumentText);
        viewModel.StatusText = "Selected text copied";
        e.Handled = true;
    }

    private async void OnGoToPageInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Enter ||
            DataContext is not MainWindowViewModel viewModel ||
            !viewModel.GoToPageCommand.CanExecute(null))
        {
            return;
        }

        await viewModel.GoToPageCommand.ExecuteAsync(null);
        e.Handled = true;
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.UpdateWindowWidth(e.NewSize.Width);
    }

    private void OnDocumentNameInputGotFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            viewModel.IsEditingDocumentName ||
            !viewModel.BeginDocumentNameEditCommand.CanExecute(null))
        {
            return;
        }

        viewModel.BeginDocumentNameEditCommand.Execute(null);
    }

    private void OnDocumentNameInputLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            !viewModel.IsEditingDocumentName ||
            !viewModel.CommitDocumentNameEditCommand.CanExecute(null))
        {
            return;
        }

        viewModel.CommitDocumentNameEditCommand.Execute(null);
    }

    private void OnDocumentNameInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (e.Key is Key.Enter &&
            viewModel.CommitDocumentNameEditCommand.CanExecute(null))
        {
            viewModel.CommitDocumentNameEditCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Escape &&
            viewModel.CancelDocumentNameEditCommand.CanExecute(null))
        {
            viewModel.CancelDocumentNameEditCommand.Execute(null);
            e.Handled = true;
        }
    }

    private async void OnSearchInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Enter ||
            DataContext is not MainWindowViewModel viewModel ||
            !viewModel.OpenSearchCommand.CanExecute(null))
        {
            return;
        }

        await viewModel.OpenSearchCommand.ExecuteAsync(null);
        e.Handled = true;
    }

    private async void OnAboutMenuClicked(object? sender, RoutedEventArgs e)
    {
        var aboutWindow = AboutWindowFactory.Create();
        await aboutWindow.ShowDialog(this);
        e.Handled = true;
    }

    private async void OnDocumentTextSelectionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control layer ||
            DataContext is not MainWindowViewModel viewModel ||
            e.GetCurrentPoint(layer).Properties.PointerUpdateKind is not PointerUpdateKind.LeftButtonPressed ||
            !viewModel.HasOpenDocument ||
            viewModel.IsRendering)
        {
            ResetDocumentTextSelectionInteraction();
            return;
        }

        if (!await viewModel.EnsureDocumentTextReadyForSelectionAsync())
        {
            viewModel.ClearDocumentTextSelection();
            return;
        }

        _documentTextSelectionLayer = layer;
        var coordinateLayer = ResolveDocumentTextSelectionCoordinateLayer(layer);
        _documentTextSelectionCoordinateLayer = coordinateLayer;
        _isSelectingDocumentText = true;
        var documentPosition = MapPointerPositionToDocument(
            coordinateLayer,
            viewModel,
            GetDocumentSelectionVisualPosition(e, coordinateLayer));
        if (!viewModel.BeginDocumentTextSelection(documentPosition.X, documentPosition.Y))
        {
            ResetDocumentTextSelectionInteraction();
            return;
        }

        e.Pointer.Capture(layer);
        e.Handled = true;
    }

    private void OnDocumentTextSelectionPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isSelectingDocumentText ||
            sender is not Control layer ||
            !ReferenceEquals(layer, _documentTextSelectionLayer) ||
            DataContext is not MainWindowViewModel viewModel ||
            !e.GetCurrentPoint(layer).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var coordinateLayer = _documentTextSelectionCoordinateLayer ?? ResolveDocumentTextSelectionCoordinateLayer(layer);
        _documentTextSelectionCoordinateLayer = coordinateLayer;
        var currentVisualPosition = GetDocumentSelectionVisualPosition(e, coordinateLayer);
        var currentDocumentPosition = MapPointerPositionToDocument(coordinateLayer, viewModel, currentVisualPosition);
        viewModel.UpdateDocumentTextSelection(currentDocumentPosition.X, currentDocumentPosition.Y);
        e.Handled = true;
    }

    private void OnDocumentTextSelectionPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isSelectingDocumentText ||
            sender is not Control layer ||
            !ReferenceEquals(layer, _documentTextSelectionLayer) ||
            DataContext is not MainWindowViewModel viewModel)
        {
            ResetDocumentTextSelectionInteraction();
            return;
        }

        var coordinateLayer = _documentTextSelectionCoordinateLayer ?? ResolveDocumentTextSelectionCoordinateLayer(layer);
        _documentTextSelectionCoordinateLayer = coordinateLayer;
        var currentDocumentPosition = MapPointerPositionToDocument(
            coordinateLayer,
            viewModel,
            GetDocumentSelectionVisualPosition(e, coordinateLayer));
        viewModel.UpdateDocumentTextSelection(currentDocumentPosition.X, currentDocumentPosition.Y);
        viewModel.CompleteDocumentTextSelection();

        e.Pointer.Capture(null);
        ResetDocumentTextSelectionInteraction();
        e.Handled = true;
    }

    private void OnDocumentTextSelectionPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        ResetDocumentTextSelectionInteraction();
    }

    private void OnThumbnailPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control ||
            control.DataContext is not PageThumbnailItemViewModel thumbnail ||
            e.GetCurrentPoint(control).Properties.PointerUpdateKind is not PointerUpdateKind.LeftButtonPressed)
        {
            ClearThumbnailDragState();
            return;
        }

        _draggedThumbnailPageNumber = thumbnail.SourcePageNumber;
        _thumbnailPendingDropIndex = null;
        _thumbnailDragStartPoint = e.GetPosition(ThumbnailDragSurface);
        _lastThumbnailDragPosition = _thumbnailDragStartPoint;
        _thumbnailDragPointerOffset = e.GetPosition(GetThumbnailDragVisual(control) ?? control);
        _thumbnailAutoScrollDirection = 0;
        _didReorderThumbnailDuringDrag = false;
        _isDraggingThumbnail = false;
        _suppressNextThumbnailTap = false;
        SetThumbnailDragState(null);
        HideThumbnailGhost();
        e.Pointer.Capture(control);
    }

    private async void OnThumbnailPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedThumbnailPageNumber is null ||
            _thumbnailDragStartPoint is null ||
            !e.GetCurrentPoint(ThumbnailDragSurface).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var currentPosition = e.GetPosition(ThumbnailDragSurface);
        _lastThumbnailDragPosition = currentPosition;

        if (!_isDraggingThumbnail)
        {
            var delta = currentPosition - _thumbnailDragStartPoint.Value;

            if (Math.Abs(delta.X) < ThumbnailDragThreshold &&
                Math.Abs(delta.Y) < ThumbnailDragThreshold)
            {
                return;
            }

            _isDraggingThumbnail = true;
            _suppressNextThumbnailTap = true;
            SetThumbnailDragState(_draggedThumbnailPageNumber);
            if (sender is Control control &&
                control.DataContext is PageThumbnailItemViewModel thumbnail)
            {
                ShowThumbnailGhost(thumbnail, control, currentPosition);
            }
            e.Handled = true;
        }

        if (!_isDraggingThumbnail ||
            _draggedThumbnailPageNumber is null ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        UpdateThumbnailGhostPosition(currentPosition);
        UpdateThumbnailAutoScrollState(currentPosition);
        e.Handled = true;
        await PreviewThumbnailReorderAsync(viewModel, currentPosition);
    }

    private async void OnThumbnailPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var wasDraggingThumbnail = _isDraggingThumbnail;
        var draggedThumbnailPageNumber = _draggedThumbnailPageNumber;
        var pendingDropIndex = _thumbnailPendingDropIndex;
        var viewModel = DataContext as MainWindowViewModel;

        _isReleasingThumbnailPointer = true;

        try
        {
            if (sender is Control control)
            {
                e.Pointer.Capture(null);
            }

            if (wasDraggingThumbnail)
            {
                e.Handled = true;

                if (draggedThumbnailPageNumber is not null &&
                    pendingDropIndex is not null &&
                    viewModel is not null)
                {
                    var currentIndex = GetCurrentThumbnailIndex(viewModel, draggedThumbnailPageNumber.Value);
                    if (currentIndex >= 0 && currentIndex != pendingDropIndex.Value)
                    {
                        await CommitThumbnailReorderAsync(
                            viewModel,
                            draggedThumbnailPageNumber.Value,
                            pendingDropIndex.Value);
                    }
                }
            }
        }
        finally
        {
            _isReleasingThumbnailPointer = false;
            ClearThumbnailDragState();
        }
    }

    private void OnThumbnailPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_isReleasingThumbnailPointer)
        {
            return;
        }

        ClearThumbnailDragState();
    }

    private async void OnThumbnailTapped(object? sender, TappedEventArgs e)
    {
        if (_suppressNextThumbnailTap)
        {
            _suppressNextThumbnailTap = false;
            e.Handled = true;
            return;
        }

        if (sender is not Control control ||
            control.DataContext is not PageThumbnailItemViewModel thumbnail ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        await viewModel.HandleThumbnailActivatedAsync(thumbnail.SourcePageNumber);
        e.Handled = true;
    }

    private void ClearThumbnailDragState()
    {
        StopThumbnailAutoScroll();
        HideThumbnailGhost();
        HideThumbnailDropIndicator();
        SetThumbnailDragState(null);
        _draggedThumbnailPageNumber = null;
        _thumbnailPendingDropIndex = null;
        _thumbnailDragStartPoint = null;
        _lastThumbnailDragPosition = null;
        _thumbnailAutoScrollDirection = 0;
        _didReorderThumbnailDuringDrag = false;
        _isDraggingThumbnail = false;
    }

    private List<(Control Control, Rect Bounds)> GetThumbnailHitboxes(int? excludedSourcePageNumber = null)
    {
        if (ThumbnailItemsControl is null)
        {
            return [];
        }

        var hitboxes = new List<(Control Control, Rect Bounds)>();

        foreach (var control in ThumbnailItemsControl.GetVisualDescendants().OfType<Control>())
        {
            if (!control.Classes.Contains("thumbnail-hitbox"))
            {
                continue;
            }

            if (excludedSourcePageNumber.HasValue &&
                control.DataContext is PageThumbnailItemViewModel thumbnail &&
                thumbnail.SourcePageNumber == excludedSourcePageNumber.Value)
            {
                continue;
            }

            if (control.TranslatePoint(default, ThumbnailDragSurface) is not { } origin)
            {
                continue;
            }

            hitboxes.Add((control, new Rect(origin, control.Bounds.Size)));
        }

        hitboxes.Sort(static (left, right) => left.Bounds.Top.CompareTo(right.Bounds.Top));
        return hitboxes;
    }

    private static int GetCurrentThumbnailIndex(MainWindowViewModel viewModel, int sourcePageNumber)
    {
        for (var i = 0; i < viewModel.Thumbnails.Count; i++)
        {
            if (viewModel.Thumbnails[i].SourcePageNumber == sourcePageNumber)
            {
                return i;
            }
        }

        return -1;
    }

    private void SetThumbnailDragState(int? draggedThumbnailPageNumber)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        foreach (var thumbnail in viewModel.Thumbnails)
        {
            thumbnail.IsDragging = draggedThumbnailPageNumber.HasValue &&
                                   thumbnail.SourcePageNumber == draggedThumbnailPageNumber.Value;
        }
    }

    private async Task PreviewThumbnailReorderAsync(MainWindowViewModel viewModel, Point currentPosition)
    {
        if (_draggedThumbnailPageNumber is null)
        {
            return;
        }

        var dropPreview = GetThumbnailDropPreview(currentPosition);
        if (dropPreview is null)
        {
            HideThumbnailDropIndicator();
            return;
        }

        ShowThumbnailDropIndicator(dropPreview.Value);

        var currentIndex = GetCurrentThumbnailIndex(viewModel, _draggedThumbnailPageNumber.Value);
        if (currentIndex < 0)
        {
            return;
        }

        _thumbnailPendingDropIndex = dropPreview.Value.FinalIndex;
        _didReorderThumbnailDuringDrag = currentIndex != _thumbnailPendingDropIndex.Value;
        await Task.CompletedTask;
    }

    private void ShowThumbnailGhost(
        PageThumbnailItemViewModel thumbnail,
        Control? sourceHitbox,
        Point currentPosition)
    {
        if (sourceHitbox is null)
        {
            return;
        }

        ThumbnailDragGhostPreviewImage.Source = thumbnail.Thumbnail;
        ThumbnailDragGhostLoadingText.IsVisible = thumbnail.IsLoading || thumbnail.Thumbnail is null;
        ThumbnailDragGhostPageLabel.Text = $"Page {thumbnail.DisplayPageNumber}";
        ThumbnailDragGhostHost.IsVisible = true;
        UpdateThumbnailGhostPosition(currentPosition);
    }

    private void HideThumbnailGhost()
    {
        if (ThumbnailDragGhostHost is not null)
        {
            ThumbnailDragGhostHost.IsVisible = false;
            Canvas.SetLeft(ThumbnailDragGhostHost, 0);
            Canvas.SetTop(ThumbnailDragGhostHost, 0);
        }

        if (ThumbnailDragGhostPreviewImage is not null)
        {
            ThumbnailDragGhostPreviewImage.Source = null;
        }

        if (ThumbnailDragGhostLoadingText is not null)
        {
            ThumbnailDragGhostLoadingText.IsVisible = false;
        }

        if (ThumbnailDragGhostPageLabel is not null)
        {
            ThumbnailDragGhostPageLabel.Text = string.Empty;
        }
    }

    private void ShowThumbnailDropIndicator(ThumbnailDropPreview dropPreview)
    {
        if (ThumbnailDropIndicator is null)
        {
            return;
        }

        ThumbnailDropIndicator.Width = Math.Max(0, dropPreview.Width - 8);
        ThumbnailDropIndicator.IsVisible = true;
        Canvas.SetLeft(ThumbnailDropIndicator, dropPreview.Left + 4);
        Canvas.SetTop(ThumbnailDropIndicator, dropPreview.Top - 2);
    }

    private void HideThumbnailDropIndicator()
    {
        if (ThumbnailDropIndicator is null)
        {
            return;
        }

        ThumbnailDropIndicator.IsVisible = false;
        Canvas.SetLeft(ThumbnailDropIndicator, 0);
        Canvas.SetTop(ThumbnailDropIndicator, 0);
    }

    private void UpdateThumbnailGhostPosition(Point currentPosition)
    {
        if (ThumbnailDragGhostHost is null || !ThumbnailDragGhostHost.IsVisible)
        {
            return;
        }

        Canvas.SetLeft(ThumbnailDragGhostHost, currentPosition.X - _thumbnailDragPointerOffset.X);
        Canvas.SetTop(ThumbnailDragGhostHost, currentPosition.Y - _thumbnailDragPointerOffset.Y - ThumbnailGhostLift);
    }

    private static Control? GetThumbnailDragVisual(Control hitbox)
    {
        return hitbox
            .GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(control => control.Classes.Contains("thumbnail-item"));
    }

    private ThumbnailDropPreview? GetThumbnailDropPreview(Point pointerPosition)
    {
        if (_draggedThumbnailPageNumber is null)
        {
            return null;
        }

        var hitboxes = GetThumbnailHitboxes(_draggedThumbnailPageNumber.Value);
        if (hitboxes.Count == 0)
        {
            return null;
        }

        var left = hitboxes[0].Bounds.Left;
        var width = hitboxes[0].Bounds.Width;

        var firstMidpoint = hitboxes[0].Bounds.Top + (hitboxes[0].Bounds.Height / 2);
        if (pointerPosition.Y < firstMidpoint)
        {
            return new ThumbnailDropPreview(0, hitboxes[0].Bounds.Top, left, width);
        }

        for (var i = 1; i < hitboxes.Count; i++)
        {
            var midpoint = hitboxes[i].Bounds.Top + (hitboxes[i].Bounds.Height / 2);
            if (pointerPosition.Y < midpoint)
            {
                return new ThumbnailDropPreview(i, hitboxes[i].Bounds.Top, left, width);
            }
        }

        var lastBounds = hitboxes[^1].Bounds;
        return new ThumbnailDropPreview(hitboxes.Count, lastBounds.Bottom, left, width);
    }

    private void UpdateThumbnailAutoScrollState(Point pointerPosition)
    {
        if (ThumbnailScrollViewer is null || ThumbnailDragSurface is null)
        {
            StopThumbnailAutoScroll();
            return;
        }

        var scrollViewerOrigin = ThumbnailScrollViewer.TranslatePoint(default, ThumbnailDragSurface);
        if (scrollViewerOrigin is null)
        {
            StopThumbnailAutoScroll();
            return;
        }

        var pointerY = pointerPosition.Y - scrollViewerOrigin.Value.Y;
        var direction = 0;

        if (pointerY < ThumbnailAutoScrollEdge)
        {
            direction = -1;
        }
        else if (pointerY > ThumbnailScrollViewer.Bounds.Height - ThumbnailAutoScrollEdge)
        {
            direction = 1;
        }

        _thumbnailAutoScrollDirection = direction;
        if (_thumbnailAutoScrollDirection == 0)
        {
            StopThumbnailAutoScroll();
            return;
        }

        if (!_thumbnailAutoScrollTimer.IsEnabled)
        {
            _thumbnailAutoScrollTimer.Start();
        }
    }

    private void StopThumbnailAutoScroll()
    {
        if (_thumbnailAutoScrollTimer.IsEnabled)
        {
            _thumbnailAutoScrollTimer.Stop();
        }
    }

    private async void OnThumbnailAutoScrollTick(object? sender, EventArgs e)
    {
        if (!_isDraggingThumbnail ||
            _thumbnailAutoScrollDirection == 0 ||
            _lastThumbnailDragPosition is null ||
            ThumbnailScrollViewer is null ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var maxOffsetY = Math.Max(0, ThumbnailScrollViewer.Extent.Height - ThumbnailScrollViewer.Viewport.Height);
        if (maxOffsetY <= 0)
        {
            StopThumbnailAutoScroll();
            return;
        }

        var nextOffsetY = Math.Clamp(
            ThumbnailScrollViewer.Offset.Y + (_thumbnailAutoScrollDirection * ThumbnailAutoScrollStep),
            0,
            maxOffsetY);

        if (Math.Abs(nextOffsetY - ThumbnailScrollViewer.Offset.Y) <= double.Epsilon)
        {
            return;
        }

        ThumbnailScrollViewer.Offset = new Vector(ThumbnailScrollViewer.Offset.X, nextOffsetY);
        UpdateThumbnailGhostPosition(_lastThumbnailDragPosition.Value);
        await PreviewThumbnailReorderAsync(viewModel, _lastThumbnailDragPosition.Value);
    }

    private async Task CommitThumbnailReorderAsync(
        MainWindowViewModel viewModel,
        int sourcePageNumber,
        int targetIndex)
    {
        await viewModel.HandleThumbnailReorderToIndexAsync(sourcePageNumber, targetIndex);
        viewModel.CompleteThumbnailReorderDrag();
    }

    private readonly record struct ThumbnailDropPreview(int FinalIndex, double Top, double Left, double Width);

    private Control ResolveDocumentTextSelectionCoordinateLayer(Control fallbackLayer)
    {
        if (ScrollableDocumentInteractionLayer.IsVisible)
        {
            return ScrollableDocumentInteractionLayer;
        }

        if (AutoFitDocumentInteractionLayer.IsVisible)
        {
            return AutoFitDocumentInteractionLayer;
        }

        return fallbackLayer;
    }

    private Point GetDocumentSelectionVisualPosition(PointerEventArgs e, Control coordinateLayer)
    {
        if (ReferenceEquals(coordinateLayer, ScrollableDocumentInteractionLayer) &&
            DocumentScrollViewer is not null)
        {
            var viewportPosition = e.GetPosition(DocumentScrollViewer);

            if (coordinateLayer.TranslatePoint(default, DocumentScrollViewer) is { } contentOriginInViewport)
            {
                return new Point(
                    viewportPosition.X - contentOriginInViewport.X,
                    viewportPosition.Y - contentOriginInViewport.Y);
            }

            return e.GetPosition(coordinateLayer);
        }

        return e.GetPosition(coordinateLayer);
    }

    private static Point MapPointerPositionToDocument(Control layer, MainWindowViewModel viewModel, Point visualPosition)
    {
        if (!viewModel.TryMapViewerPointToDocumentTextSpace(
                visualPosition.X,
                visualPosition.Y,
                layer.Bounds.Width,
                layer.Bounds.Height,
                out var point))
        {
            return visualPosition;
        }

        return new Point(point.X, point.Y);
    }

    private void ResetDocumentTextSelectionInteraction()
    {
        _documentTextSelectionLayer = null;
        _documentTextSelectionCoordinateLayer = null;
        _isSelectingDocumentText = false;
    }
}
