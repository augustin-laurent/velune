using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.UseCases;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Presentation.Imaging;

namespace Velune.Presentation.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private enum NotificationKind
    {
        Info,
        Warning,
        Error,
        Confirmation
    }

    private sealed record NotificationEntry(
        string Title,
        string Message,
        NotificationKind Kind,
        string? PrimaryActionLabel = null,
        Action? PrimaryAction = null,
        string? SecondaryActionLabel = null,
        Action? SecondaryAction = null,
        bool IsDismissible = true);

    private const string ViewerRenderJobKey = "viewer";
    private const string ThumbnailRenderJobPrefix = "thumbnail:";
    private const int ThumbnailRequestedWidth = 170;
    private const int ThumbnailRequestedHeight = 150;
    private const double ZoomStep = 0.10;
    private const double MinZoom = 0.25;
    private const double MaxZoom = 5.00;
    private const double ThumbnailZoomFactor = 0.20;
    private const double ViewerContentPadding = 12.0;
    private const double SidebarExpandedWidth = 240;
    private const double ViewportResizeThreshold = 1.0;
    private const double ZoomComparisonTolerance = 0.001;

    private readonly IFilePickerService _filePickerService;
    private readonly OpenDocumentUseCase _openDocumentUseCase;
    private readonly CloseDocumentUseCase _closeDocumentUseCase;
    private readonly ChangePageUseCase _changePageUseCase;
    private readonly ChangeZoomUseCase _changeZoomUseCase;
    private readonly RotateDocumentUseCase _rotateDocumentUseCase;
    private readonly IRenderOrchestrator _renderOrchestrator;
    private readonly IDocumentSessionStore _documentSessionStore;
    private readonly IRecentFilesService _recentFilesService;
    private readonly IPageViewportStore _pageViewportStore;

    private readonly Queue<NotificationEntry> _notificationQueue = [];
    private readonly Dictionary<int, RenderJobHandle> _thumbnailRenderJobs = [];
    private bool _disposed;
    private double _documentViewportWidth;
    private double _documentViewportHeight;
    private bool _isImageAutoFitEnabled;
    private NotificationKind _currentNotificationKind = NotificationKind.Info;
    private bool _isCurrentNotificationDismissible;
    private int _thumbnailGenerationVersion;
    private Action? _notificationPrimaryAction;
    private Action? _notificationSecondaryAction;
    private RenderJobHandle? _currentRenderJob;

    public MainWindowViewModel(
        IFilePickerService filePickerService,
        OpenDocumentUseCase openDocumentUseCase,
        CloseDocumentUseCase closeDocumentUseCase,
        ChangePageUseCase changePageUseCase,
        ChangeZoomUseCase changeZoomUseCase,
        RotateDocumentUseCase rotateDocumentUseCase,
        IRenderOrchestrator renderOrchestrator,
        IDocumentSessionStore documentSessionStore,
        IRecentFilesService recentFilesService,
        IPageViewportStore pageViewportStore)
    {
        ArgumentNullException.ThrowIfNull(filePickerService);
        ArgumentNullException.ThrowIfNull(openDocumentUseCase);
        ArgumentNullException.ThrowIfNull(closeDocumentUseCase);
        ArgumentNullException.ThrowIfNull(changePageUseCase);
        ArgumentNullException.ThrowIfNull(changeZoomUseCase);
        ArgumentNullException.ThrowIfNull(rotateDocumentUseCase);
        ArgumentNullException.ThrowIfNull(renderOrchestrator);
        ArgumentNullException.ThrowIfNull(documentSessionStore);
        ArgumentNullException.ThrowIfNull(recentFilesService);
        ArgumentNullException.ThrowIfNull(pageViewportStore);

        _filePickerService = filePickerService;
        _openDocumentUseCase = openDocumentUseCase;
        _closeDocumentUseCase = closeDocumentUseCase;
        _changePageUseCase = changePageUseCase;
        _changeZoomUseCase = changeZoomUseCase;
        _rotateDocumentUseCase = rotateDocumentUseCase;
        _renderOrchestrator = renderOrchestrator;
        _documentSessionStore = documentSessionStore;
        _recentFilesService = recentFilesService;
        _pageViewportStore = pageViewportStore;

        RecentFiles = [];
        Thumbnails = [];
        RefreshRecentFiles();
    }

    [ObservableProperty]
    private string _title = "Velune";

    [ObservableProperty]
    private string _applicationTitle = "Velune";

    [ObservableProperty]
    private string _sidebarTitle = "Pages";

    [ObservableProperty]
    private string _emptyStateTitle = "Open a document";

    [ObservableProperty]
    private string _emptyStateDescription = "Open a PDF or an image to start viewing it.";

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _hasOpenDocument;

    [ObservableProperty]
    private bool _isCurrentImageDocument;

    [ObservableProperty]
    private string? _userMessage;

    [ObservableProperty]
    private string? _userMessageTitle;

    [ObservableProperty]
    private string? _notificationPrimaryActionLabel;

    [ObservableProperty]
    private string? _notificationSecondaryActionLabel;

    [ObservableProperty]
    private string? _currentDocumentName;

    [ObservableProperty]
    private string? _currentDocumentType;

    [ObservableProperty]
    private string? _currentDocumentPath;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private string _currentZoom = "100%";

    [ObservableProperty]
    private string _currentRotation = "0°";

    [ObservableProperty]
    private bool _isRendering;

    [ObservableProperty]
    private Bitmap? _currentRenderedBitmap;

    [ObservableProperty]
    private bool _isGeneratingThumbnails;

    [ObservableProperty]
    private string _goToPageInput = "1";

    public ObservableCollection<RecentFileItem> RecentFiles
    {
        get;
    }
    public ObservableCollection<PageThumbnailItemViewModel> Thumbnails
    {
        get;
    }

    public bool IsEmptyStateVisible => !HasOpenDocument;
    public bool HasUserMessage => !string.IsNullOrWhiteSpace(UserMessage);
    public bool CanDismissUserMessage => HasUserMessage && _isCurrentNotificationDismissible;
    public bool HasUserMessageTitle => !string.IsNullOrWhiteSpace(UserMessageTitle);
    public bool HasNotificationPrimaryAction => !string.IsNullOrWhiteSpace(NotificationPrimaryActionLabel);
    public bool HasNotificationSecondaryAction => !string.IsNullOrWhiteSpace(NotificationSecondaryActionLabel);
    public bool IsNotificationConfirmation => HasUserMessage && _currentNotificationKind == NotificationKind.Confirmation;
    public MaterialIconKind NotificationIconKind => _currentNotificationKind switch
    {
        NotificationKind.Error => MaterialIconKind.AlertCircleOutline,
        NotificationKind.Warning => MaterialIconKind.AlertCircleOutline,
        NotificationKind.Confirmation => MaterialIconKind.HelpCircleOutline,
        _ => MaterialIconKind.InformationCircleOutline
    };
    public bool HasRecentFiles => RecentFiles.Count > 0;
    public bool HasRenderedPage => CurrentRenderedBitmap is not null;
    public bool HasMultiplePages => TotalPages > 1;
    public bool HasThumbnails => Thumbnails.Count > 0;
    public string HeaderTitle => CurrentDocumentName ?? ApplicationTitle;
    public bool IsImageAutoFitActive => HasOpenDocument && IsCurrentImageDocument && _isImageAutoFitEnabled;
    public bool IsScrollableViewerVisible => !IsImageAutoFitActive;
    public bool IsSidebarVisible => !HasOpenDocument || !IsCurrentImageDocument;
    public bool IsPageNavigationVisible => !HasOpenDocument || !IsCurrentImageDocument;
    public GridLength SidebarColumnWidth => new(SidebarWidth);
    public double SidebarWidth => IsSidebarVisible ? SidebarExpandedWidth : 0;
    public string PageIndicator => TotalPages > 0 ? $"{CurrentPage} / {TotalPages}" : "-";

    public bool CanGoPreviousPage => HasOpenDocument && CurrentPage > 1;
    public bool CanGoNextPage => HasOpenDocument && TotalPages > 0 && CurrentPage < TotalPages;
    public bool CanGoToPage => HasOpenDocument && TotalPages > 0;
    public bool CanUseFitCommands =>
        HasOpenDocument &&
        _documentViewportWidth > 0 &&
        _documentViewportHeight > 0 &&
        (HasRenderedPage || IsCurrentImageDocument);
    public bool ShouldUseTrackpadForPan =>
        HasOpenDocument &&
        (IsCurrentImageDocument ||
         _pageViewportStore.GetPageState(_pageViewportStore.ActivePageIndex).ZoomFactor > 1.0);

    partial void OnHasOpenDocumentChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEmptyStateVisible));
        OnPropertyChanged(nameof(CanGoToPage));
        NotifyViewerModeChanged();
        OnPropertyChanged(nameof(IsSidebarVisible));
        OnPropertyChanged(nameof(IsPageNavigationVisible));
        OnPropertyChanged(nameof(SidebarColumnWidth));
        OnPropertyChanged(nameof(SidebarWidth));
        CloseCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        GoToPageCommand.NotifyCanExecuteChanged();
        ZoomInCommand.NotifyCanExecuteChanged();
        ZoomOutCommand.NotifyCanExecuteChanged();
        FitToWidthCommand.NotifyCanExecuteChanged();
        FitToPageCommand.NotifyCanExecuteChanged();
        RotateLeftCommand.NotifyCanExecuteChanged();
        RotateRightCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsCurrentImageDocumentChanged(bool value)
    {
        NotifyViewerModeChanged();
        OnPropertyChanged(nameof(IsSidebarVisible));
        OnPropertyChanged(nameof(IsPageNavigationVisible));
        OnPropertyChanged(nameof(SidebarColumnWidth));
        OnPropertyChanged(nameof(SidebarWidth));
    }

    partial void OnUserMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasUserMessage));
        OnPropertyChanged(nameof(IsNotificationConfirmation));
        OnPropertyChanged(nameof(NotificationIconKind));
        DismissMessageCommand.NotifyCanExecuteChanged();
        NotificationPrimaryActionCommand.NotifyCanExecuteChanged();
        NotificationSecondaryActionCommand.NotifyCanExecuteChanged();
    }

    partial void OnUserMessageTitleChanged(string? value)
    {
        OnPropertyChanged(nameof(HasUserMessageTitle));
    }

    partial void OnNotificationPrimaryActionLabelChanged(string? value)
    {
        OnPropertyChanged(nameof(HasNotificationPrimaryAction));
        NotificationPrimaryActionCommand.NotifyCanExecuteChanged();
    }

    partial void OnNotificationSecondaryActionLabelChanged(string? value)
    {
        OnPropertyChanged(nameof(HasNotificationSecondaryAction));
        NotificationSecondaryActionCommand.NotifyCanExecuteChanged();
    }

    partial void OnCurrentRenderedBitmapChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(HasRenderedPage));
        FitToWidthCommand.NotifyCanExecuteChanged();
        FitToPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnCurrentDocumentNameChanged(string? value)
    {
        OnPropertyChanged(nameof(HeaderTitle));
    }

    partial void OnCurrentPageChanged(int value)
    {
        OnPropertyChanged(nameof(PageIndicator));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        UpdateSelectedThumbnail();
        GoToPageInput = value.ToString();
    }

    partial void OnTotalPagesChanged(int value)
    {
        OnPropertyChanged(nameof(HasMultiplePages));
        OnPropertyChanged(nameof(PageIndicator));
        OnPropertyChanged(nameof(CanGoToPage));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        GoToPageCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        var filePath = await _filePickerService.PickOpenFileAsync();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            StatusText = "Open cancelled";
            return;
        }

        await OpenDocumentFromPathAsync(filePath);
    }

    [RelayCommand]
    private async Task OpenRecentFileAsync(RecentFileItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.FilePath))
        {
            return;
        }

        await OpenDocumentFromPathAsync(item.FilePath);
    }

    [RelayCommand(CanExecute = nameof(HasOpenDocument))]
    private void Close()
    {
        CancelCurrentRender();
        CancelThumbnailGeneration();

        _closeDocumentUseCase.Execute();
        _pageViewportStore.Clear();
        ClearThumbnails();
        ResetDocumentState();

        ClearNotifications();
        StatusText = "Document closed";
    }

    [RelayCommand(CanExecute = nameof(HasRecentFiles))]
    private void ClearRecentFiles()
    {
        EnqueueConfirmation(
            "Clear recent files?",
            "This removes the recent file shortcuts, but your current session stays open.",
            "Clear",
            ConfirmClearRecentFiles,
            "Keep");

        StatusText = "Confirmation required";
    }

    [RelayCommand(CanExecute = nameof(CanGoPreviousPage))]
    private async Task PreviousPageAsync()
    {
        await ChangeToPageAsync(CurrentPage - 1);
    }

    [RelayCommand(CanExecute = nameof(CanGoNextPage))]
    private async Task NextPageAsync()
    {
        await ChangeToPageAsync(CurrentPage + 1);
    }

    [RelayCommand(CanExecute = nameof(CanGoToPage))]
    private async Task GoToPageAsync()
    {
        if (string.IsNullOrWhiteSpace(GoToPageInput))
        {
            EnqueueWarning("Invalid page number", "Enter a page number.");
            StatusText = "Invalid page number";
            return;
        }

        if (!int.TryParse(GoToPageInput, out var pageNumber))
        {
            EnqueueWarning("Invalid page number", "Page number must be numeric.");
            StatusText = "Invalid page number";
            return;
        }

        if (pageNumber < 1 || pageNumber > TotalPages)
        {
            EnqueueWarning("Invalid page number", $"Page number must be between 1 and {TotalPages}.");
            StatusText = "Invalid page number";
            return;
        }

        await ChangeToPageAsync(pageNumber);
    }

    [RelayCommand(CanExecute = nameof(HasOpenDocument))]
    private async Task SelectThumbnailAsync(PageThumbnailItemViewModel? thumbnail)
    {
        if (thumbnail is null)
        {
            return;
        }

        await ChangeToPageAsync(thumbnail.PageNumber);
    }

    [RelayCommand(CanExecute = nameof(HasOpenDocument))]
    private async Task ZoomInAsync()
    {
        DisableImageAutoFit();

        var pageState = _pageViewportStore.GetPageState(_pageViewportStore.ActivePageIndex);
        var nextZoom = Math.Min(MaxZoom, pageState.ZoomFactor + ZoomStep);

        _pageViewportStore.SetPageState(pageState.WithZoom(nextZoom));
        if (!TryUpdateZoom(nextZoom, ZoomMode.Custom))
        {
            return;
        }

        RefreshPageViewState();

        await RenderCurrentPageAsync();
        StatusText = $"Zoom set to {CurrentZoom}";
    }

    [RelayCommand(CanExecute = nameof(HasOpenDocument))]
    private async Task ZoomOutAsync()
    {
        DisableImageAutoFit();

        var pageState = _pageViewportStore.GetPageState(_pageViewportStore.ActivePageIndex);
        var nextZoom = Math.Max(MinZoom, pageState.ZoomFactor - ZoomStep);

        _pageViewportStore.SetPageState(pageState.WithZoom(nextZoom));
        if (!TryUpdateZoom(nextZoom, ZoomMode.Custom))
        {
            return;
        }

        RefreshPageViewState();

        await RenderCurrentPageAsync();
        StatusText = $"Zoom set to {CurrentZoom}";
    }

    [RelayCommand(CanExecute = nameof(CanUseFitCommands))]
    private async Task FitToWidthAsync()
    {
        if (!await TryApplyViewportFitAsync(ZoomMode.FitToWidth, forceRender: true))
        {
            return;
        }

        StatusText = "Fit to width applied";
    }

    [RelayCommand(CanExecute = nameof(CanUseFitCommands))]
    private async Task FitToPageAsync()
    {
        if (!await TryApplyViewportFitAsync(ZoomMode.FitToPage, forceRender: true))
        {
            return;
        }

        StatusText = "Fit to page applied";
    }

    [RelayCommand(CanExecute = nameof(HasOpenDocument))]
    private async Task RotateLeftAsync()
    {
        var pageState = _pageViewportStore.GetPageState(_pageViewportStore.ActivePageIndex);

        var nextRotation = pageState.Rotation switch
        {
            Rotation.Deg0 => Rotation.Deg270,
            Rotation.Deg90 => Rotation.Deg0,
            Rotation.Deg180 => Rotation.Deg90,
            Rotation.Deg270 => Rotation.Deg180,
            _ => Rotation.Deg0
        };

        _pageViewportStore.SetPageState(pageState.WithRotation(nextRotation));
        if (!TryUpdateRotation(nextRotation))
        {
            return;
        }

        RefreshPageViewState();

        if (CurrentZoomMode is ZoomMode.FitToPage or ZoomMode.FitToWidth)
        {
            await RenderCurrentPageAsync(preserveStatusText: true);
        }

        if (!await TryApplyViewportFitAsync(CurrentZoomMode, forceRender: true))
        {
            await RenderCurrentPageAsync();
        }

        await RefreshThumbnailForActivePageAsync();
        StatusText = $"Rotation set to {CurrentRotation}";
    }

    [RelayCommand(CanExecute = nameof(HasOpenDocument))]
    private async Task RotateRightAsync()
    {
        var pageState = _pageViewportStore.GetPageState(_pageViewportStore.ActivePageIndex);

        var nextRotation = pageState.Rotation switch
        {
            Rotation.Deg0 => Rotation.Deg90,
            Rotation.Deg90 => Rotation.Deg180,
            Rotation.Deg180 => Rotation.Deg270,
            Rotation.Deg270 => Rotation.Deg0,
            _ => Rotation.Deg0
        };

        _pageViewportStore.SetPageState(pageState.WithRotation(nextRotation));
        if (!TryUpdateRotation(nextRotation))
        {
            return;
        }

        RefreshPageViewState();

        if (CurrentZoomMode is ZoomMode.FitToPage or ZoomMode.FitToWidth)
        {
            await RenderCurrentPageAsync(preserveStatusText: true);
        }

        if (!await TryApplyViewportFitAsync(CurrentZoomMode, forceRender: true))
        {
            await RenderCurrentPageAsync();
        }

        await RefreshThumbnailForActivePageAsync();
        StatusText = $"Rotation set to {CurrentRotation}";
    }

    [RelayCommand(CanExecute = nameof(CanDismissUserMessage))]
    private void DismissMessage()
    {
        AdvanceNotificationQueue();
        StatusText = "Notification dismissed";
    }

    [RelayCommand(CanExecute = nameof(HasNotificationPrimaryAction))]
    private void NotificationPrimaryAction()
    {
        var action = _notificationPrimaryAction;
        AdvanceNotificationQueue();
        action?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(HasNotificationSecondaryAction))]
    private void NotificationSecondaryAction()
    {
        var action = _notificationSecondaryAction;
        AdvanceNotificationQueue();
        action?.Invoke();
    }

    [RelayCommand]
    private void SimulateError()
    {
        EnqueueError("Unable to load the requested document.");
        StatusText = "An error was simulated";
    }

    public async Task NavigateToPreviousPageFromTrackpadAsync()
    {
        if (!CanGoPreviousPage || IsRendering)
        {
            return;
        }

        await PreviousPageAsync();
    }

    public async Task NavigateToNextPageFromTrackpadAsync()
    {
        if (!CanGoNextPage || IsRendering)
        {
            return;
        }

        await NextPageAsync();
    }

    public async Task HandleZoomPointerWheelAsync(double deltaY)
    {
        if (!HasOpenDocument || IsRendering || Math.Abs(deltaY) <= double.Epsilon)
        {
            return;
        }

        if (deltaY > 0)
        {
            await ZoomInAsync();
            return;
        }

        await ZoomOutAsync();
    }

    public async Task UpdateDocumentViewportAsync(double viewportWidth, double viewportHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            return;
        }

        var widthChanged = Math.Abs(_documentViewportWidth - viewportWidth) > ViewportResizeThreshold;
        var heightChanged = Math.Abs(_documentViewportHeight - viewportHeight) > ViewportResizeThreshold;

        _documentViewportWidth = viewportWidth;
        _documentViewportHeight = viewportHeight;
        FitToWidthCommand.NotifyCanExecuteChanged();
        FitToPageCommand.NotifyCanExecuteChanged();

        if (!widthChanged && !heightChanged)
        {
            return;
        }

        await TryApplyViewportFitAsync(CurrentZoomMode, preserveStatusText: true);
    }

    private async Task OpenDocumentFromPathAsync(string filePath)
    {
        var result = await _openDocumentUseCase.ExecuteAsync(new OpenDocumentRequest(filePath));

        if (result.IsFailure)
        {
            EnqueueError(result.Error?.Message ?? "Unable to open the selected document.", "Open failed");
            StatusText = "Open failed";
            return;
        }

        RefreshFromSession();

        _pageViewportStore.Initialize(TotalPages > 0 ? TotalPages : 1);
        _pageViewportStore.SetActivePage(new PageIndex(0));
        RefreshPageViewState();

        ClearThumbnails();
        if (!IsCurrentImageDocument)
        {
            BuildThumbnailPlaceholders();
        }

        AddCurrentDocumentToRecentFiles();

        ClearNotifications();
        StatusText = $"Opened {CurrentDocumentName}";

        if (IsCurrentImageDocument)
        {
            if (!await TryApplyViewportFitAsync(ZoomMode.FitToPage, forceRender: true))
            {
                _isImageAutoFitEnabled = true;
                NotifyViewerModeChanged();
                TryUpdateZoom(
                    _pageViewportStore.GetPageState(_pageViewportStore.ActivePageIndex).ZoomFactor,
                    ZoomMode.FitToPage);
                await RenderCurrentPageAsync();
            }
        }
        else
        {
            await RenderCurrentPageAsync();
        }

        if (!IsCurrentImageDocument)
        {
            _ = GenerateThumbnailsAsync();
        }
    }

    private async Task ChangeToPageAsync(int pageNumber)
    {
        if (pageNumber < 1 || (TotalPages > 0 && pageNumber > TotalPages))
        {
            EnqueueWarning("Invalid page number", $"Page number must be between 1 and {TotalPages}.");
            StatusText = "Invalid page number";
            return;
        }

        var result = _changePageUseCase.Execute(
            new ChangePageRequest(new PageIndex(pageNumber - 1)));

        if (result.IsFailure)
        {
            EnqueueError(result.Error?.Message ?? "Unable to change page.", "Page change failed");
            StatusText = "Page change failed";
            return;
        }

        _pageViewportStore.SetActivePage(new PageIndex(pageNumber - 1));
        var activePageState = _pageViewportStore.GetPageState(_pageViewportStore.ActivePageIndex);
        if (!TrySyncSessionToActivePageState(activePageState))
        {
            return;
        }

        RefreshFromSession();
        RefreshPageViewState();

        if (CurrentZoomMode is ZoomMode.FitToPage or ZoomMode.FitToWidth)
        {
            await RenderCurrentPageAsync(preserveStatusText: true);
            await TryApplyViewportFitAsync(CurrentZoomMode, forceRender: true);
        }
        else
        {
            await RenderCurrentPageAsync();
        }

        StatusText = $"Page {CurrentPage}";
    }

    private async Task RenderCurrentPageAsync(bool preserveStatusText = false)
    {
        CancelCurrentRender();

        var session = _documentSessionStore.Current;
        if (session is null)
        {
            CurrentRenderedBitmap?.Dispose();
            CurrentRenderedBitmap = null;
            return;
        }

        var pageState = _pageViewportStore.GetPageState(_pageViewportStore.ActivePageIndex);
        RefreshPageViewState();

        var renderJob = _renderOrchestrator.Submit(
            CreateViewerRenderRequest(pageState));
        _currentRenderJob = renderJob;

        var previousStatusText = StatusText;
        var renderSucceeded = false;

        try
        {
            IsRendering = true;

            var result = await renderJob.Completion;
            if (_currentRenderJob?.JobId != renderJob.JobId)
            {
                return;
            }

            if (result.IsCanceled)
            {
                return;
            }

            if (result.IsFailure)
            {
                EnqueueError(result.Error?.Message ?? "Unable to render the current page.", "Render failed");
                StatusText = "Render failed";
                return;
            }

            if (result.Page is null)
            {
                EnqueueError("No rendered page was returned.", "Render failed");
                StatusText = "Render failed";
                return;
            }

            CurrentRenderedBitmap?.Dispose();
            CurrentRenderedBitmap = RenderedPageBitmapFactory.Create(result.Page);
            renderSucceeded = true;
            StatusText = preserveStatusText ? previousStatusText : $"Rendered page {CurrentPage}";
        }
        finally
        {
            if (_currentRenderJob?.JobId == renderJob.JobId)
            {
                _currentRenderJob = null;
                IsRendering = false;
            }

            if (preserveStatusText && renderSucceeded)
            {
                StatusText = previousStatusText;
            }
        }
    }

    private async Task GenerateThumbnailsAsync()
    {
        CancelThumbnailGeneration();

        var session = _documentSessionStore.Current;
        if (session is null)
        {
            return;
        }

        var generationVersion = ++_thumbnailGenerationVersion;

        try
        {
            IsGeneratingThumbnails = true;

            for (var i = 0; i < Thumbnails.Count; i++)
            {
                if (generationVersion != _thumbnailGenerationVersion)
                {
                    break;
                }

                var thumbnailItem = Thumbnails[i];
                var thumbnailPageIndex = new PageIndex(i);
                var thumbnailPageState = _pageViewportStore.GetPageState(thumbnailPageIndex);

                try
                {
                    var renderJob = _renderOrchestrator.Submit(
                        CreateThumbnailRenderRequest(thumbnailPageIndex, thumbnailPageState.Rotation));

                    _thumbnailRenderJobs[thumbnailPageIndex.Value] = renderJob;

                    var result = await renderJob.Completion;
                    if (!_thumbnailRenderJobs.TryGetValue(thumbnailPageIndex.Value, out var activeJob) ||
                        activeJob.JobId != renderJob.JobId)
                    {
                        continue;
                    }

                    _thumbnailRenderJobs.Remove(thumbnailPageIndex.Value);

                    if (generationVersion != _thumbnailGenerationVersion || result.IsCanceled)
                    {
                        continue;
                    }

                    if (result.IsSuccess && result.Page is not null)
                    {
                        var bitmap = RenderedPageBitmapFactory.Create(result.Page);

                        thumbnailItem.Thumbnail?.Dispose();
                        thumbnailItem.Thumbnail = bitmap;
                    }
                }
                finally
                {
                    thumbnailItem.IsLoading = false;
                }
            }
        }
        finally
        {
            if (generationVersion == _thumbnailGenerationVersion)
            {
                IsGeneratingThumbnails = false;
            }
        }
    }

    private async Task RefreshThumbnailForActivePageAsync()
    {
        if (!HasOpenDocument || CurrentPage < 1 || CurrentPage > Thumbnails.Count)
        {
            return;
        }

        var thumbnailIndex = CurrentPage - 1;
        var thumbnailItem = Thumbnails[thumbnailIndex];
        var pageIndex = new PageIndex(thumbnailIndex);
        var pageState = _pageViewportStore.GetPageState(pageIndex);

        try
        {
            thumbnailItem.IsLoading = true;

            var renderJob = _renderOrchestrator.Submit(
                CreateThumbnailRenderRequest(pageIndex, pageState.Rotation));
            _thumbnailRenderJobs[pageIndex.Value] = renderJob;

            var result = await renderJob.Completion;
            if (!_thumbnailRenderJobs.TryGetValue(pageIndex.Value, out var activeJob) ||
                activeJob.JobId != renderJob.JobId)
            {
                return;
            }

            _thumbnailRenderJobs.Remove(pageIndex.Value);

            if (result.IsSuccess && result.Page is not null)
            {
                var bitmap = RenderedPageBitmapFactory.Create(result.Page);

                thumbnailItem.Thumbnail?.Dispose();
                thumbnailItem.Thumbnail = bitmap;
            }
        }
        finally
        {
            thumbnailItem.IsLoading = false;
        }
    }

    private void BuildThumbnailPlaceholders()
    {
        ClearThumbnails();

        for (var i = 1; i <= TotalPages; i++)
        {
            Thumbnails.Add(new PageThumbnailItemViewModel(i)
            {
                IsSelected = i == CurrentPage
            });
        }

        OnPropertyChanged(nameof(HasThumbnails));
    }

    private void UpdateSelectedThumbnail()
    {
        foreach (var item in Thumbnails)
        {
            item.IsSelected = item.PageNumber == CurrentPage;
        }
    }

    private void ClearThumbnails()
    {
        foreach (var item in Thumbnails)
        {
            item.Dispose();
        }

        Thumbnails.Clear();
        OnPropertyChanged(nameof(HasThumbnails));
    }

    private void CancelCurrentRender()
    {
        if (_currentRenderJob is null)
        {
            return;
        }

        _renderOrchestrator.Cancel(_currentRenderJob.JobId);
        _currentRenderJob = null;
        IsRendering = false;
    }

    private void CancelThumbnailGeneration()
    {
        _thumbnailGenerationVersion++;
        IsGeneratingThumbnails = false;

        foreach (var renderJob in _thumbnailRenderJobs.Values)
        {
            _renderOrchestrator.Cancel(renderJob.JobId);
        }

        _thumbnailRenderJobs.Clear();
    }

    private void RefreshFromSession()
    {
        var session = _documentSessionStore.Current;
        if (session is null)
        {
            ResetDocumentState();
            return;
        }

        IsCurrentImageDocument = session.Metadata.DocumentType is DocumentType.Image;
        HasOpenDocument = true;
        CurrentDocumentName = session.Metadata.FileName;
        CurrentDocumentType = session.Metadata.DocumentType.ToString();
        CurrentDocumentPath = session.Metadata.FilePath;
        CurrentPage = session.Viewport.CurrentPage.Value + 1;
        TotalPages = session.Metadata.PageCount ?? 1;

        EmptyStateTitle = session.Metadata.FileName;
        EmptyStateDescription = $"Opened {session.Metadata.DocumentType} document.";
    }

    private void RefreshPageViewState()
    {
        var pageState = _pageViewportStore.GetPageState(_pageViewportStore.ActivePageIndex);
        CurrentZoom = $"{pageState.ZoomFactor * 100:0}%";
        CurrentRotation = $"{(int)pageState.Rotation}°";
        CurrentPage = pageState.PageIndex.Value + 1;
    }

    private void ResetDocumentState()
    {
        _isImageAutoFitEnabled = false;
        HasOpenDocument = false;
        IsCurrentImageDocument = false;
        CurrentDocumentName = null;
        CurrentDocumentType = null;
        CurrentDocumentPath = null;
        CurrentPage = 1;
        TotalPages = 0;
        CurrentZoom = "100%";
        CurrentRotation = "0°";
        GoToPageInput = "1";
        IsRendering = false;
        IsGeneratingThumbnails = false;

        CurrentRenderedBitmap?.Dispose();
        CurrentRenderedBitmap = null;

        EmptyStateTitle = "Open a document";
        EmptyStateDescription = "Open a PDF or an image to start viewing it.";
    }

    private void DisableImageAutoFit()
    {
        _isImageAutoFitEnabled = false;
        NotifyViewerModeChanged();
    }

    private async Task<bool> TryApplyViewportFitAsync(
        ZoomMode zoomMode,
        bool preserveStatusText = false,
        bool forceRender = false)
    {
        if (zoomMode is ZoomMode.Custom ||
            !HasOpenDocument ||
            _documentViewportWidth <= 0 ||
            _documentViewportHeight <= 0)
        {
            return false;
        }

        if (!TryCalculateFitZoom(zoomMode, out var fitZoom))
        {
            return false;
        }

        var pageState = _pageViewportStore.GetPageState(_pageViewportStore.ActivePageIndex);
        fitZoom = Math.Clamp(fitZoom, MinZoom, MaxZoom);

        var zoomChanged = Math.Abs(pageState.ZoomFactor - fitZoom) > ZoomComparisonTolerance;
        var zoomModeChanged = CurrentZoomMode != zoomMode;
        var shouldEnableImageAutoFit = IsCurrentImageDocument && zoomMode is ZoomMode.FitToPage;

        if (_isImageAutoFitEnabled != shouldEnableImageAutoFit)
        {
            _isImageAutoFitEnabled = shouldEnableImageAutoFit;
            NotifyViewerModeChanged();
        }

        if (!zoomChanged &&
            !zoomModeChanged &&
            !forceRender &&
            CurrentRenderedBitmap is not null)
        {
            return false;
        }

        if (zoomChanged || zoomModeChanged)
        {
            _pageViewportStore.SetPageState(pageState.WithZoom(fitZoom));
            if (!TryUpdateZoom(fitZoom, zoomMode))
            {
                return false;
            }

            RefreshPageViewState();
        }

        await RenderCurrentPageAsync(preserveStatusText);
        return true;
    }

    private bool TryCalculateFitZoom(ZoomMode zoomMode, out double fitZoom)
    {
        var availableWidth = Math.Max(1, _documentViewportWidth - (ViewerContentPadding * 2));
        var availableHeight = Math.Max(1, _documentViewportHeight - (ViewerContentPadding * 2));
        var pageState = _pageViewportStore.GetPageState(_pageViewportStore.ActivePageIndex);

        if (_documentSessionStore.Current is IImageDocumentSession imageSession)
        {
            fitZoom = zoomMode switch
            {
                ZoomMode.FitToWidth => ImageViewportCalculator.CalculateFitWidthZoom(
                    imageSession.ImageMetadata,
                    pageState.Rotation,
                    availableWidth),
                ZoomMode.FitToPage => ImageViewportCalculator.CalculateFitZoom(
                    imageSession.ImageMetadata,
                    pageState.Rotation,
                    availableWidth,
                    availableHeight),
                _ => 0
            };

            return zoomMode is ZoomMode.FitToPage or ZoomMode.FitToWidth;
        }

        if (CurrentRenderedBitmap is null)
        {
            fitZoom = 0;
            return false;
        }

        fitZoom = zoomMode switch
        {
            ZoomMode.FitToWidth => RenderedPageViewportCalculator.CalculateFitToWidthZoom(
                CurrentRenderedBitmap.PixelSize.Width,
                pageState.ZoomFactor,
                availableWidth),
            ZoomMode.FitToPage => RenderedPageViewportCalculator.CalculateFitToPageZoom(
                CurrentRenderedBitmap.PixelSize.Width,
                CurrentRenderedBitmap.PixelSize.Height,
                pageState.ZoomFactor,
                availableWidth,
                availableHeight),
            _ => 0
        };

        return zoomMode is ZoomMode.FitToPage or ZoomMode.FitToWidth;
    }

    private bool TryUpdateZoom(double zoomFactor, ZoomMode zoomMode)
    {
        var result = _changeZoomUseCase.Execute(
            new ChangeZoomRequest(zoomFactor, zoomMode));

        if (result.IsFailure)
        {
            EnqueueError(result.Error?.Message ?? "Unable to update zoom.", "Zoom update failed");
            StatusText = "Zoom update failed";
            return false;
        }

        RefreshFromSession();
        return true;
    }

    private bool TryUpdateRotation(Rotation rotation)
    {
        var result = _rotateDocumentUseCase.Execute(
            new RotateDocumentRequest(rotation));

        if (result.IsFailure)
        {
            EnqueueError(result.Error?.Message ?? "Unable to update rotation.", "Rotation update failed");
            StatusText = "Rotation update failed";
            return false;
        }

        RefreshFromSession();
        return true;
    }

    private bool TrySyncSessionToActivePageState(PageViewportState pageState)
    {
        ArgumentNullException.ThrowIfNull(pageState);

        if (!TryUpdateRotation(pageState.Rotation))
        {
            return false;
        }

        var zoomMode = CurrentZoomMode;
        var targetZoom = zoomMode is ZoomMode.Custom
            ? pageState.ZoomFactor
            : (_documentSessionStore.CurrentViewport?.ZoomFactor ?? pageState.ZoomFactor);

        return TryUpdateZoom(targetZoom, zoomMode);
    }

    private ZoomMode CurrentZoomMode =>
        _documentSessionStore.CurrentViewport?.ZoomMode ?? ZoomMode.Custom;

    private void NotifyViewerModeChanged()
    {
        OnPropertyChanged(nameof(IsImageAutoFitActive));
        OnPropertyChanged(nameof(IsScrollableViewerVisible));
    }

    private static RenderRequest CreateViewerRenderRequest(PageViewportState pageState)
    {
        ArgumentNullException.ThrowIfNull(pageState);

        return new RenderRequest(
            ViewerRenderJobKey,
            pageState.PageIndex,
            pageState.ZoomFactor,
            pageState.Rotation);
    }

    private static RenderRequest CreateThumbnailRenderRequest(PageIndex pageIndex, Rotation rotation)
    {
        return new RenderRequest(
            CreateThumbnailJobKey(pageIndex),
            pageIndex,
            ThumbnailZoomFactor,
            rotation,
            ThumbnailRequestedWidth,
            ThumbnailRequestedHeight);
    }

    private static string CreateThumbnailJobKey(PageIndex pageIndex) =>
        $"{ThumbnailRenderJobPrefix}{pageIndex.Value}";

    private void ConfirmClearRecentFiles()
    {
        _recentFilesService.Clear();
        RefreshRecentFiles();
        EnqueueInfo("Recent files cleared", "The recent files list was cleared.");
        StatusText = "Recent files cleared";
    }

    private void EnqueueInfo(string title, string message, bool replaceCurrent = false)
    {
        EnqueueNotification(new NotificationEntry(title, message, NotificationKind.Info), replaceCurrent);
    }

    private void EnqueueWarning(string title, string message, bool replaceCurrent = false)
    {
        EnqueueNotification(new NotificationEntry(title, message, NotificationKind.Warning), replaceCurrent);
    }

    private void EnqueueError(string message, string title = "Non-fatal error", bool replaceCurrent = false)
    {
        EnqueueNotification(new NotificationEntry(title, message, NotificationKind.Error), replaceCurrent);
    }

    private void EnqueueConfirmation(
        string title,
        string message,
        string confirmLabel,
        Action onConfirm,
        string cancelLabel = "Cancel")
    {
        EnqueueNotification(
            new NotificationEntry(
                title,
                message,
                NotificationKind.Confirmation,
                confirmLabel,
                onConfirm,
                cancelLabel,
                () => StatusText = "Action cancelled",
                IsDismissible: false),
            replaceCurrent: true);
    }

    private void EnqueueNotification(NotificationEntry notification, bool replaceCurrent = false)
    {
        if (replaceCurrent)
        {
            _notificationQueue.Clear();
            ApplyNotification(notification);
            return;
        }

        if (!HasUserMessage)
        {
            ApplyNotification(notification);
            return;
        }

        _notificationQueue.Enqueue(notification);
    }

    private void AdvanceNotificationQueue()
    {
        if (_notificationQueue.Count > 0)
        {
            ApplyNotification(_notificationQueue.Dequeue());
            return;
        }

        ClearNotificationState();
    }

    private void ClearNotifications()
    {
        _notificationQueue.Clear();
        ClearNotificationState();
    }

    private void ApplyNotification(NotificationEntry notification)
    {
        _currentNotificationKind = notification.Kind;
        _isCurrentNotificationDismissible = notification.IsDismissible;
        _notificationPrimaryAction = notification.PrimaryAction;
        _notificationSecondaryAction = notification.SecondaryAction;
        UserMessageTitle = notification.Title;
        UserMessage = notification.Message;
        NotificationPrimaryActionLabel = notification.PrimaryActionLabel;
        NotificationSecondaryActionLabel = notification.SecondaryActionLabel;
        OnPropertyChanged(nameof(CanDismissUserMessage));
        OnPropertyChanged(nameof(IsNotificationConfirmation));
        OnPropertyChanged(nameof(NotificationIconKind));
    }

    private void ClearNotificationState()
    {
        _currentNotificationKind = NotificationKind.Info;
        _isCurrentNotificationDismissible = false;
        _notificationPrimaryAction = null;
        _notificationSecondaryAction = null;
        UserMessageTitle = null;
        UserMessage = null;
        NotificationPrimaryActionLabel = null;
        NotificationSecondaryActionLabel = null;
        OnPropertyChanged(nameof(CanDismissUserMessage));
        OnPropertyChanged(nameof(IsNotificationConfirmation));
        OnPropertyChanged(nameof(NotificationIconKind));
    }

    private void AddCurrentDocumentToRecentFiles()
    {
        if (string.IsNullOrWhiteSpace(CurrentDocumentName) ||
            string.IsNullOrWhiteSpace(CurrentDocumentPath) ||
            string.IsNullOrWhiteSpace(CurrentDocumentType))
        {
            return;
        }

        _recentFilesService.Add(new RecentFileItem(
            CurrentDocumentName,
            CurrentDocumentPath,
            CurrentDocumentType));

        RefreshRecentFiles();
    }

    private void RefreshRecentFiles()
    {
        RecentFiles.Clear();

        foreach (var item in _recentFilesService.GetAll())
        {
            RecentFiles.Add(item);
        }

        OnPropertyChanged(nameof(HasRecentFiles));
        ClearRecentFilesCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            CancelCurrentRender();
            CancelThumbnailGeneration();

            CurrentRenderedBitmap?.Dispose();
            CurrentRenderedBitmap = null;

            ClearThumbnails();
        }

        _disposed = true;
    }

}
