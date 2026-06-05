using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Velune.Windows.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.System;

namespace Velune.Windows.Controls;

public sealed partial class PagesPanelControl : UserControl
{
    public PagesPanelControl()
    {
        InitializeComponent();
    }

    public WindowsMainViewModel? ViewModel
    {
        get => DataContext as WindowsMainViewModel;
        set => DataContext = value;
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
        if (ViewModel is not { } viewModel ||
            element is null ||
            ResolveThumbnailItem(element) is not WindowsPageThumbnailViewModel thumbnail)
        {
            return;
        }

        _ = viewModel.EnsureThumbnailRenderedAsync(thumbnail);
    }

    private void OnOpenPageOrganizerClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        WindowsDocumentTabViewModel? tab = viewModel.ActiveDocumentTab;
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
                _ = viewModel.ApplyPageOrganizerResultAsync(vm.GetFinalPageOrder(), vm.GetRotations());
            }
        });

        window.Activate();
    }

    private void OnThumbnailItemRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (ViewModel is { } viewModel &&
            sender is FrameworkElement element &&
            ResolveThumbnailItem(element) is WindowsPageThumbnailViewModel thumbnail)
        {
            viewModel.SelectedThumbnailPageNumber = thumbnail.PageNumber;
        }
    }

    private void OnRibbonRotateLeftClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        if (viewModel.ActiveDocumentTab is not null)
        {
            viewModel.SelectedThumbnailPageNumber = viewModel.ActiveDocumentTab.CurrentPage;
        }

        _ = viewModel.RotateSelectedPageAsync(false);
    }

    private void OnRibbonRotateRightClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        if (viewModel.ActiveDocumentTab is not null)
        {
            viewModel.SelectedThumbnailPageNumber = viewModel.ActiveDocumentTab.CurrentPage;
        }

        _ = viewModel.RotateSelectedPageAsync(true);
    }

    private void OnThumbnailRotateLeftClick(object sender, RoutedEventArgs e) =>
        _ = ViewModel?.RotateSelectedPageAsync(false);

    private void OnThumbnailRotateRightClick(object sender, RoutedEventArgs e) =>
        _ = ViewModel?.RotateSelectedPageAsync(true);

    private void OnThumbnailMoveUpClick(object sender, RoutedEventArgs e) =>
        _ = ViewModel?.MoveSelectedPageAsync(-1);

    private void OnThumbnailMoveDownClick(object sender, RoutedEventArgs e) =>
        _ = ViewModel?.MoveSelectedPageAsync(1);

    private void OnThumbnailDeleteClick(object sender, RoutedEventArgs e) =>
        _ = ViewModel?.DeleteSelectedPageAsync();

    private void OnThumbnailItemPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel ||
            sender is not UIElement element ||
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

        _thumbnailDragSourceIndex = viewModel.ActiveDocumentTab?.Thumbnails.IndexOf(thumbnail) ?? -1;
        _thumbnailDragStartPoint = e.GetCurrentPoint(ThumbnailScrollViewer).Position;
        _isDraggingThumbnail = false;
        element.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnThumbnailItemPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel ||
            _thumbnailDragSourceIndex < 0 ||
            sender is not UIElement)
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
            WindowsDocumentTabViewModel? tab = viewModel.ActiveDocumentTab;
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
        if (ViewModel is not { } viewModel)
        {
            return;
        }

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
                WindowsDocumentTabViewModel? tab = viewModel.ActiveDocumentTab;
                if (tab is not null)
                {
                    int adjustedTarget = targetIndex > sourceIndex ? targetIndex - 1 : targetIndex;
                    tab.Thumbnails.Move(sourceIndex, adjustedTarget);
                    await viewModel.HandleThumbnailReorderAsync(sourceIndex + 1, adjustedTarget);
                }
            }
        }
        else if (clickedThumbnail is not null)
        {
            await viewModel.ChangePageAsync(clickedThumbnail.PageNumber);
        }
    }

    private async void OnThumbnailItemKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel ||
            e.Key is not VirtualKey.Enter and not VirtualKey.Space ||
            sender is not FrameworkElement element ||
            ResolveThumbnailItem(element) is not WindowsPageThumbnailViewModel thumbnail)
        {
            return;
        }

        await viewModel.ChangePageAsync(thumbnail.PageNumber);
        e.Handled = true;
    }

    private WindowsPageThumbnailViewModel? ResolveThumbnailItem(FrameworkElement element)
    {
        if (element.DataContext is WindowsPageThumbnailViewModel thumbnail)
        {
            return thumbnail;
        }

        if (element.Tag is int pageNumber &&
            ViewModel?.ActiveDocumentTab is { } tab)
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
        WindowsDocumentTabViewModel? tab = ViewModel?.ActiveDocumentTab;
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
        WindowsDocumentTabViewModel? tab = ViewModel?.ActiveDocumentTab;
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
        WindowsDocumentTabViewModel? tab = ViewModel?.ActiveDocumentTab;
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
        if (ViewModel is not { } viewModel ||
            !viewModel.CanAcceptThumbnailDrop)
        {
            e.AcceptedOperation = DataPackageOperation.None;
            HideThumbnailDropIndicator();
            return;
        }

        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = viewModel.Labels.Insert;
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
        if (ViewModel is not { } viewModel ||
            !viewModel.CanAcceptThumbnailDrop ||
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

        await viewModel.HandleThumbnailFilesDroppedAsync(filePaths, insertionIndex);
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
        if (ViewModel?.ActiveDocumentTab is not { } tab)
        {
            return 0;
        }

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
        if (ViewModel?.ActiveDocumentTab is not { } tab)
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
            ViewModel?.ActiveDocumentTab is not { } tab ||
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
}

