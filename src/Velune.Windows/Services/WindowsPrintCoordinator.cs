using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Printing;
using Microsoft.UI.Xaml.Shapes;
using Velune.Application.Abstractions;
using Velune.Application.Annotations;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Annotations;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Windows.ViewModels;
using Windows.Foundation;
using Windows.Graphics.Printing;
using WinRT.Interop;

namespace Velune.Windows.Services;

/// <summary>
/// Coordinates the Windows print workflow for document tabs.
/// </summary>
public interface IWindowsPrintCoordinator
{
    /// <summary>
    /// Initiates printing for the specified document tab.
    /// </summary>
    /// <param name="tab">The document tab to print.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure of the print operation.</returns>
    Task<Result> PrintAsync(WindowsDocumentTabViewModel tab, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements printing via the Windows Print Manager and PrintDocument APIs.
/// </summary>
public sealed class WindowsPrintCoordinator : IWindowsPrintCoordinator
{
    private const double PrintRenderZoomFactor = 1.5;

    private readonly object _stateGate = new();
    private readonly WindowsWindowContext _windowContext;
    private readonly IDocumentSessionStore _documentSessionStore;
    private readonly IRenderOrchestrator _renderOrchestrator;
    private readonly IWindowsTextCatalog _textCatalog;
    private IReadOnlyList<WindowsPrintPageSnapshot> _pageSnapshots = [];
    private readonly Dictionary<int, UIElement> _previewPages = [];
    private PrintDocument? _printDocument;
    private IPrintDocumentSource? _printDocumentSource;
    private PrintManager? _printManager;
    private PrintTask? _printTask;
    private WindowsPrintPageDescription? _pageDescription;
    private WindowsPrintJobSnapshot? _currentPrintJob;
    private CancellationToken _currentPrintCancellationToken;
    private string _currentPrintTitle = "Velune";
    private bool _isPrintSessionActive;

    /// <summary>
    /// Initializes the print coordinator with rendering and window dependencies.
    /// </summary>
    /// <param name="windowContext">Provides the active window handle for the print dialog.</param>
    /// <param name="documentSessionStore">Provides access to active document sessions.</param>
    /// <param name="renderOrchestrator">Renders pages at print resolution.</param>
    /// <param name="textCatalog">Provides localized error messages.</param>
    public WindowsPrintCoordinator(
        WindowsWindowContext windowContext,
        IDocumentSessionStore documentSessionStore,
        IRenderOrchestrator renderOrchestrator,
        IWindowsTextCatalog textCatalog)
    {
        ArgumentNullException.ThrowIfNull(windowContext);
        ArgumentNullException.ThrowIfNull(documentSessionStore);
        ArgumentNullException.ThrowIfNull(renderOrchestrator);
        ArgumentNullException.ThrowIfNull(textCatalog);

        _windowContext = windowContext;
        _documentSessionStore = documentSessionStore;
        _renderOrchestrator = renderOrchestrator;
        _textCatalog = textCatalog;
    }

    /// <inheritdoc />
    public async Task<Result> PrintAsync(WindowsDocumentTabViewModel tab, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tab);

        if (!TryBeginPrintSession())
        {
            return ResultFactory.Failure(
                AppError.Validation(
                    "print.in_progress",
                    "Another print operation is already active."));
        }

        var printUiShown = false;
        if (string.IsNullOrWhiteSpace(tab.FilePath) || !System.IO.File.Exists(tab.FilePath))
        {
            CleanupPrintSession();
            return ResultFactory.Failure(
                AppError.NotFound("print.file.missing", _textCatalog.GetString("windows.print.session_missing")));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_documentSessionStore.TryActivate(tab.SessionId))
            {
                CleanupPrintSession();
                return ResultFactory.Failure(
                    AppError.NotFound("print.session.missing", _textCatalog.GetString("windows.print.session_missing")));
            }

            if (!PrintManager.IsSupported())
            {
                CleanupPrintSession();
                return ResultFactory.Failure(
                    AppError.Unsupported(
                        "print.platform.unsupported",
                        "Printing is not supported on this Windows device."));
            }

            var printJob = WindowsPrintJobSnapshotFactory.Create(tab, _textCatalog.GetString("app.name"));
            var wasPrintUiShown = await RunOnUiThreadAsync(async () =>
            {
                _currentPrintJob = printJob;
                _currentPrintCancellationToken = cancellationToken;
                _currentPrintTitle = printJob.Title;
                RegisterPrintDocument();
                return await PrintManagerInterop.ShowPrintUIForWindowAsync(_windowContext.GetWindowHandle());
            });

            if (!wasPrintUiShown)
            {
                return ResultFactory.Failure(
                    AppError.Infrastructure(
                        "print.dialog.not_shown",
                        _textCatalog.GetString("windows.print.dialog_not_shown")));
            }

            printUiShown = wasPrintUiShown;
            return ResultFactory.Success();
        }
        catch (OperationCanceledException)
        {
            return ResultFactory.Failure(
                AppError.Validation("print.cancelled", _textCatalog.GetString("windows.print.cancelled")));
        }
        catch (Exception exception)
        {
            return ResultFactory.Failure(
                AppError.Infrastructure("print.failed", _textCatalog.Format("windows.print.failed", exception.Message)));
        }
        finally
        {
            if (!printUiShown)
            {
                await CleanupPrintSessionAsync();
            }
        }
    }

    private async Task<Result<IReadOnlyList<WindowsPrintPageSnapshot>>> PreparePrintPageSnapshotsAsync(
        WindowsPrintJobSnapshot printJob,
        CancellationToken cancellationToken)
    {
        var snapshots = new List<WindowsPrintPageSnapshot>();

        foreach (var pageIndex in WindowsPrintPageSnapshotFactory.CreatePageIndices(printJob.TotalPages))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var handle = _renderOrchestrator.Submit(
                new RenderRequest(
                    $"windows-print:{printJob.SessionId.Value}:{pageIndex.Value}",
                    pageIndex,
                    PrintRenderZoomFactor,
                    printJob.Rotation,
                    Priority: RenderPriority.Viewer,
                    UseThumbnailDiskCache: false));

            var result = await handle.Completion.WaitAsync(cancellationToken);
            if (result.IsCanceled)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (result.IsFailure)
            {
                return ResultFactory.Failure<IReadOnlyList<WindowsPrintPageSnapshot>>(result.Error!);
            }

            if (result.Page is null)
            {
                return ResultFactory.Failure<IReadOnlyList<WindowsPrintPageSnapshot>>(
                    AppError.Infrastructure(
                        "print.pages.empty",
                        _textCatalog.GetString("windows.print.pages_empty")));
            }

            snapshots.Add(
                new WindowsPrintPageSnapshot(
                    pageIndex,
                    result.Page,
                    printJob.Rotation,
                    WindowsPrintPageSnapshotFactory.CaptureAnnotationsForPage(printJob.Annotations, pageIndex)));
        }

        return ResultFactory.Success<IReadOnlyList<WindowsPrintPageSnapshot>>(snapshots);
    }

    private void RegisterPrintDocument()
    {
        UnregisterPrintDocument();

        _printManager = PrintManagerInterop.GetForWindow(_windowContext.GetWindowHandle());
        _printManager.PrintTaskRequested += OnPrintTaskRequested;

        _printDocument = new PrintDocument();
        _printDocumentSource = _printDocument.DocumentSource;
        _printDocument.Paginate += OnPaginate;
        _printDocument.GetPreviewPage += OnGetPreviewPage;
        _printDocument.AddPages += OnAddPages;
    }

    private void UnregisterPrintDocument()
    {
        if (_printTask is not null)
        {
            _printTask.Completed -= OnPrintTaskCompleted;
            _printTask = null;
        }

        if (_printManager is not null)
        {
            _printManager.PrintTaskRequested -= OnPrintTaskRequested;
            _printManager = null;
        }

        if (_printDocument is not null)
        {
            _printDocument.Paginate -= OnPaginate;
            _printDocument.GetPreviewPage -= OnGetPreviewPage;
            _printDocument.AddPages -= OnAddPages;
            _printDocument = null;
        }

        _printDocumentSource = null;
    }

    private void OnPrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
    {
        if (_printDocumentSource is null)
        {
            return;
        }

        var printJob = _currentPrintJob;
        var printDocumentSource = _printDocumentSource;
        if (printJob is null || printDocumentSource is null)
        {
            return;
        }

        _printTask = args.Request.CreatePrintTask(_currentPrintTitle, sourceRequested =>
        {
            _ = PreparePrintSourceAsync(
                sourceRequested,
                printJob,
                printDocumentSource,
                _currentPrintCancellationToken);
        });
        _printTask.Completed += OnPrintTaskCompleted;
    }

    private async Task PreparePrintSourceAsync(
        PrintTaskSourceRequestedArgs sourceRequested,
        WindowsPrintJobSnapshot printJob,
        IPrintDocumentSource printDocumentSource,
        CancellationToken cancellationToken)
    {
        var deferral = sourceRequested.GetDeferral();
        try
        {
            var snapshotsResult = await PreparePrintPageSnapshotsAsync(printJob, cancellationToken);
            var snapshots = snapshotsResult.IsSuccess && snapshotsResult.Value is not null
                ? snapshotsResult.Value
                : [];

            if (TrySetPrintPageSnapshots(printDocumentSource, snapshots))
            {
                sourceRequested.SetSource(printDocumentSource);
            }
        }
        catch (OperationCanceledException)
        {
            if (TrySetPrintPageSnapshots(printDocumentSource, []))
            {
                sourceRequested.SetSource(printDocumentSource);
            }
        }
        catch
        {
            if (TrySetPrintPageSnapshots(printDocumentSource, []))
            {
                sourceRequested.SetSource(printDocumentSource);
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    private bool TrySetPrintPageSnapshots(
        IPrintDocumentSource printDocumentSource,
        IReadOnlyList<WindowsPrintPageSnapshot> pageSnapshots)
    {
        lock (_stateGate)
        {
            if (!ReferenceEquals(_printDocumentSource, printDocumentSource))
            {
                return false;
            }

            _pageSnapshots = pageSnapshots;
            return true;
        }
    }

    private void OnPaginate(object sender, PaginateEventArgs args)
    {
        if (_printDocument is null)
        {
            return;
        }

        _previewPages.Clear();
        _pageDescription = WindowsPrintPageDescription.FromPrintTaskOptions(args.PrintTaskOptions);
        _printDocument.SetPreviewPageCount(_pageSnapshots.Count, PreviewPageCountType.Final);
    }

    private void OnGetPreviewPage(object sender, GetPreviewPageEventArgs args)
    {
        if (_printDocument is null ||
            args.PageNumber < 1 ||
            args.PageNumber > _pageSnapshots.Count)
        {
            return;
        }

        if (!_previewPages.TryGetValue(args.PageNumber, out var previewPage))
        {
            previewPage = WindowsPrintPageElementFactory.Create(
                _pageSnapshots[args.PageNumber - 1],
                _pageDescription ?? WindowsPrintPageDescription.Default);
            _previewPages[args.PageNumber] = previewPage;
        }

        _printDocument.SetPreviewPage(args.PageNumber, previewPage);
    }

    private void OnAddPages(object sender, AddPagesEventArgs args)
    {
        if (_printDocument is null)
        {
            return;
        }

        try
        {
            foreach (var snapshot in _pageSnapshots)
            {
                _printDocument.AddPage(
                    WindowsPrintPageElementFactory.Create(
                        snapshot,
                        _pageDescription ?? WindowsPrintPageDescription.Default));
            }
        }
        finally
        {
            _printDocument.AddPagesComplete();
        }
    }

    private void OnPrintTaskCompleted(PrintTask sender, PrintTaskCompletedEventArgs args)
    {
        CleanupPrintSessionOnUiThread();
    }

    private void CleanupPrintSessionOnUiThread()
    {
        var dispatcherQueue = _windowContext.GetDispatcherQueue();
        if (dispatcherQueue is not null && !dispatcherQueue.HasThreadAccess && dispatcherQueue.TryEnqueue(CleanupPrintSession))
        {
            return;
        }

        CleanupPrintSession();
    }

    private Task CleanupPrintSessionAsync()
    {
        return RunOnUiThreadAsync(CleanupPrintSession);
    }

    private Task RunOnUiThreadAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcherQueue = _windowContext.GetDispatcherQueue();
        if (dispatcherQueue is null || dispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }))
        {
            completion.SetException(new InvalidOperationException("The Windows UI dispatcher is not available."));
        }

        return completion.Task;
    }

    private Task<T> RunOnUiThreadAsync<T>(Func<Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var dispatcherQueue = _windowContext.GetDispatcherQueue();
        if (dispatcherQueue is null || dispatcherQueue.HasThreadAccess)
        {
            return operation();
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                completion.SetResult(await operation());
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }))
        {
            completion.SetException(new InvalidOperationException("The Windows UI dispatcher is not available."));
        }

        return completion.Task;
    }

    private bool TryBeginPrintSession()
    {
        lock (_stateGate)
        {
            if (_isPrintSessionActive)
            {
                return false;
            }

            _isPrintSessionActive = true;
            return true;
        }
    }

    private void CleanupPrintSession()
    {
        lock (_stateGate)
        {
            UnregisterPrintDocument();
            _pageSnapshots = [];
            _previewPages.Clear();
            _pageDescription = null;
            _currentPrintJob = null;
            _currentPrintCancellationToken = default;
            _currentPrintTitle = _textCatalog.GetString("app.name");
            _isPrintSessionActive = false;
        }
    }
}

internal sealed record WindowsPrintJobSnapshot(
    DocumentId SessionId,
    string Title,
    int TotalPages,
    Rotation Rotation,
    IReadOnlyList<DocumentAnnotation> Annotations);

internal static class WindowsPrintJobSnapshotFactory
{
    public static WindowsPrintJobSnapshot Create(WindowsDocumentTabViewModel tab, string fallbackTitle)
    {
        ArgumentNullException.ThrowIfNull(tab);

        return new WindowsPrintJobSnapshot(
            tab.SessionId,
            string.IsNullOrWhiteSpace(tab.Title) ? fallbackTitle : tab.Title,
            tab.TotalPages,
            tab.Rotation,
            tab.Annotations
                .Where(annotation => annotation.Kind is not DocumentAnnotationKind.Note)
                .Select(annotation => annotation.DeepCopy())
                .ToArray());
    }
}

internal sealed record WindowsPrintPageSnapshot(
    PageIndex PageIndex,
    RenderedPage Page,
    Rotation Rotation,
    IReadOnlyList<DocumentAnnotation> Annotations);

internal static class WindowsPrintPageSnapshotFactory
{
    public static IReadOnlyList<PageIndex> CreatePageIndices(int totalPages)
    {
        return totalPages <= 0
            ? []
            : Enumerable.Range(0, totalPages).Select(index => new PageIndex(index)).ToArray();
    }

    public static IReadOnlyList<DocumentAnnotation> CaptureAnnotationsForPage(
        IEnumerable<DocumentAnnotation> annotations,
        PageIndex pageIndex)
    {
        ArgumentNullException.ThrowIfNull(annotations);

        return annotations
            .Where(annotation => annotation.PageIndex == pageIndex)
            .Select(annotation => annotation.DeepCopy())
            .ToArray();
    }
}

internal sealed record WindowsPrintPageDescription(
    Size PageSize,
    Rect ImageableRect)
{
    public static WindowsPrintPageDescription Default
    {
        get;
    } = new(new Size(816, 1056), new Rect(48, 48, 720, 960));

    public static WindowsPrintPageDescription FromPrintTaskOptions(PrintTaskOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var description = options.GetPageDescription(0);
        return new WindowsPrintPageDescription(description.PageSize, description.ImageableRect);
    }
}

internal sealed record WindowsPrintContentLayout(
    double Left,
    double Top,
    double Width,
    double Height);

internal static class WindowsPrintLayoutCalculator
{
    public static WindowsPrintContentLayout Calculate(
        WindowsPrintPageDescription pageDescription,
        double sourceWidth,
        double sourceHeight)
    {
        ArgumentNullException.ThrowIfNull(pageDescription);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);

        var imageableRect = pageDescription.ImageableRect;
        if (imageableRect.Width <= 0 || imageableRect.Height <= 0)
        {
            imageableRect = new Rect(
                0,
                0,
                Math.Max(1, pageDescription.PageSize.Width),
                Math.Max(1, pageDescription.PageSize.Height));
        }

        var scale = Math.Min(imageableRect.Width / sourceWidth, imageableRect.Height / sourceHeight);
        var width = Math.Max(1, sourceWidth * scale);
        var height = Math.Max(1, sourceHeight * scale);

        return new WindowsPrintContentLayout(
            imageableRect.X + Math.Max(0, (imageableRect.Width - width) / 2),
            imageableRect.Y + Math.Max(0, (imageableRect.Height - height) / 2),
            width,
            height);
    }
}

internal static class WindowsPrintPageElementFactory
{
    public static UIElement Create(
        WindowsPrintPageSnapshot snapshot,
        WindowsPrintPageDescription pageDescription)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(pageDescription);

        var layout = WindowsPrintLayoutCalculator.Calculate(
            pageDescription,
            snapshot.Page.Width,
            snapshot.Page.Height);

        var root = new Canvas
        {
            Width = pageDescription.PageSize.Width,
            Height = pageDescription.PageSize.Height,
            Background = CreateBrush("#FFFFFF")
        };

        var contentLayer = new Grid
        {
            Width = layout.Width,
            Height = layout.Height,
            Background = CreateBrush("#FFFFFF")
        };
        Canvas.SetLeft(contentLayer, layout.Left);
        Canvas.SetTop(contentLayer, layout.Top);

        contentLayer.Children.Add(new Image
        {
            Source = WindowsBitmapFactory.Create(snapshot.Page),
            Width = layout.Width,
            Height = layout.Height,
            Stretch = Stretch.Fill
        });

        var annotationLayer = new Canvas
        {
            Width = layout.Width,
            Height = layout.Height,
            IsHitTestVisible = false
        };

        foreach (var annotation in snapshot.Annotations)
        {
            annotationLayer.Children.Add(CreateAnnotationElement(
                annotation,
                layout.Width,
                layout.Height,
                snapshot.Rotation));
        }

        contentLayer.Children.Add(annotationLayer);
        root.Children.Add(contentLayer);
        return root;
    }

    private static UIElement CreateAnnotationElement(
        DocumentAnnotation annotation,
        double pageWidth,
        double pageHeight,
        Rotation rotation)
    {
        if (annotation.Kind is DocumentAnnotationKind.Ink)
        {
            var polyline = new Polyline
            {
                Points = CreateInkPoints(annotation, pageWidth, pageHeight, rotation),
                Stroke = CreateBrush(annotation.Appearance.StrokeHex),
                StrokeThickness = Math.Max(1.5, annotation.Appearance.StrokeThickness),
                Opacity = annotation.Appearance.Opacity
            };

            return polyline;
        }

        var bounds = annotation.Bounds is { } annotationBounds
            ? DocumentAnnotationCoordinateMapper.MapRegionToVisualBounds(annotationBounds, rotation)
            : new NormalizedTextRegion(0.1, 0.1, 0.2, 0.08);

        var width = Math.Max(12, bounds.Width * pageWidth);
        var height = Math.Max(12, bounds.Height * pageHeight);
        var border = new Border
        {
            Width = width,
            Height = height,
            Background = CreateBrush(
                annotation.Appearance.FillHex ?? annotation.Appearance.StrokeHex,
                annotation.Kind is DocumentAnnotationKind.Highlight ? (byte)90 : (byte)220),
            BorderBrush = CreateBrush(annotation.Appearance.StrokeHex),
            BorderThickness = annotation.Kind is DocumentAnnotationKind.Highlight
                ? new Thickness(0)
                : new Thickness(Math.Max(1, annotation.Appearance.StrokeThickness)),
            CornerRadius = new CornerRadius(annotation.Kind is DocumentAnnotationKind.Stamp ? 2 : 4),
            Opacity = annotation.Appearance.Opacity,
            Padding = new Thickness(4)
        };

        if (annotation.Kind is DocumentAnnotationKind.Text
            or DocumentAnnotationKind.Note
            or DocumentAnnotationKind.Stamp
            or DocumentAnnotationKind.Signature)
        {
            border.Child = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(annotation.Text) ? annotation.Kind.ToString() : annotation.Text,
                Foreground = CreateBrush("#111827"),
                FontSize = Math.Clamp(height / 4, 8, 18),
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
        }

        Canvas.SetLeft(border, bounds.X * pageWidth);
        Canvas.SetTop(border, bounds.Y * pageHeight);
        return border;
    }

    private static PointCollection CreateInkPoints(
        DocumentAnnotation annotation,
        double pageWidth,
        double pageHeight,
        Rotation rotation)
    {
        var points = new PointCollection();
        foreach (var point in annotation.Points)
        {
            var mapped = DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(
                point,
                pageWidth,
                pageHeight,
                rotation);
            points.Add(new Point(mapped.X, mapped.Y));
        }

        return points;
    }

    private static SolidColorBrush CreateBrush(string hex, byte alpha = 255)
    {
        var normalized = hex.Trim().TrimStart('#');
        if (normalized.Length != 6)
        {
            normalized = "000000";
        }

        return new SolidColorBrush(global::Windows.UI.Color.FromArgb(
            alpha,
            Convert.ToByte(normalized[..2], 16),
            Convert.ToByte(normalized.Substring(2, 2), 16),
            Convert.ToByte(normalized.Substring(4, 2), 16)));
    }
}
