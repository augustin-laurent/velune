using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private const string ViewerRenderJobKey = "viewer";
    private const string ThumbnailRenderJobPrefix = "thumbnail:";
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
    private readonly IRenderOrchestrator _renderOrchestrator;
    private readonly IDocumentSessionStore _documentSessionStore;
    private readonly IRecentFilesService _recentFilesService;
    private readonly IPageViewportStore _pageViewportStore;

    private readonly Dictionary<int, RenderJobHandle> _thumbnailRenderJobs = [];
    private bool _disposed;
    private double _documentViewportWidth;
    private double _documentViewportHeight;
    private bool _isImageAutoFitEnabled;
    private int _thumbnailGenerationVersion;
    private RenderJobHandle? _currentRenderJob;

    public MainWindowViewModel(
        IFilePickerService filePickerService,
        OpenDocumentUseCase openDocumentUseCase,
        CloseDocumentUseCase closeDocumentUseCase,
        ChangePageUseCase changePageUseCase,
        IRenderOrchestrator renderOrchestrator,
        IDocumentSessionStore documentSessionStore,
        IRecentFilesService recentFilesService,
        IPageViewportStore pageViewportStore)
    {
        ArgumentNullException.ThrowIfNull(filePickerService);
        ArgumentNullException.ThrowIfNull(openDocumentUseCase);
        ArgumentNullException.ThrowIfNull(closeDocumentUseCase);
        ArgumentNullException.ThrowIfNull(changePageUseCase);
        ArgumentNullException.ThrowIfNull(renderOrchestrator);
        ArgumentNullException.ThrowIfNull(documentSessionStore);
        ArgumentNullException.ThrowIfNull(recentFilesService);
        ArgumentNullException.ThrowIfNull(pageViewportStore);

        _filePickerService = filePickerService;
        _openDocumentUseCase = openDocumentUseCase;
        _closeDocumentUseCase = closeDocumentUseCase;
        _changePageUseCase = changePageUseCase;
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
    public bool CanDismissUserMessage => HasUserMessage;
    public bool HasRecentFiles => RecentFiles.Count > 0;
    public bool HasRenderedPage => CurrentRenderedBitmap is not null;
    public bool HasMultiplePages => TotalPages > 1;
    public bool HasThumbnails => Thumbnails.Count > 0;
    public string HeaderTitle => CurrentDocumentName ?? ApplicationTitle;
    public bool IsImageAutoFitActive => HasOpenDocument && IsCurrentImageDocument && _isImageAutoFitEnabled;
    public bool IsScrollableViewerVisible => !IsImageAutoFitActive;
    public bool IsSidebarVisible => !HasOpenDocument || !IsCurrentImageDocument;
    public GridLength SidebarColumnWidth => new(SidebarWidth);
    public double SidebarWidth => IsSidebarVisible ? SidebarExpandedWidth : 0;
    public string PageIndicator => TotalPages > 0 ? $"{CurrentPage} / {TotalPages}" : "-";

    public bool CanGoPreviousPage => HasOpenDocument && CurrentPage > 1;
    public bool CanGoNextPage => HasOpenDocument && TotalPages > 0 && CurrentPage < TotalPages;
    public bool CanGoToPage => HasOpenDocument && TotalPages > 0;
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
        OnPropertyChanged(nameof(SidebarColumnWidth));
        OnPropertyChanged(nameof(SidebarWidth));
        CloseCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        GoToPageCommand.NotifyCanExecuteChanged();
        ZoomInCommand.NotifyCanExecuteChanged();
        ZoomOutCommand.NotifyCanExecuteChanged();
        RotateLeftCommand.NotifyCanExecuteChanged();
        RotateRightCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsCurrentImageDocumentChanged(bool value)
    {
        NotifyViewerModeChanged();
        OnPropertyChanged(nameof(IsSidebarVisible));
        OnPropertyChanged(nameof(SidebarColumnWidth));
        OnPropertyChanged(nameof(SidebarWidth));
    }

    partial void OnUserMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasUserMessage));
        DismissMessageCommand.NotifyCanExecuteChanged();
    }

    partial void OnCurrentRenderedBitmapChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(HasRenderedPage));
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

        UserMessage = null;
        StatusText = "Document closed";
    }

    [RelayCommand(CanExecute = nameof(HasRecentFiles))]
    private void ClearRecentFiles()
    {
        _recentFilesService.Clear();
        RefreshRecentFiles();
        StatusText = "Recent files cleared";
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
            UserMessage = "Enter a page number.";
            StatusText = "Invalid page number";
            return;
        }

        if (!int.TryParse(GoToPageInput, out var pageNumber))
        {
            UserMessage = "Page number must be numeric.";
            StatusText = "Invalid page number";
            return;
        }

        if (pageNumber < 1 || pageNumber > TotalPages)
        {
            UserMessage = $"Page number must be between 1 and {TotalPages}.";
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
        RefreshPageViewState();

        await RenderCurrentPageAsync();
        StatusText = $"Zoom set to {CurrentZoom}";
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
        RefreshPageViewState();

        if (!await TryApplyImageAutoFitAsync(forceRender: true))
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
        RefreshPageViewState();

        if (!await TryApplyImageAutoFitAsync(forceRender: true))
        {
            await RenderCurrentPageAsync();
        }

        await RefreshThumbnailForActivePageAsync();
        StatusText = $"Rotation set to {CurrentRotation}";
    }

    [RelayCommand(CanExecute = nameof(CanDismissUserMessage))]
    private void DismissMessage()
    {
        UserMessage = null;
        StatusText = "Message dismissed";
    }

    [RelayCommand]
    private void SimulateError()
    {
        UserMessage = "Unable to load the requested document.";
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

        if (!widthChanged && !heightChanged)
        {
            return;
        }

        await TryApplyImageAutoFitAsync(preserveStatusText: true);
    }

    private async Task OpenDocumentFromPathAsync(string filePath)
    {
        var result = await _openDocumentUseCase.ExecuteAsync(new OpenDocumentRequest(filePath));

        if (result.IsFailure)
        {
            UserMessage = result.Error?.Message ?? "Unable to open the selected document.";
            StatusText = "Open failed";
            return;
        }

        RefreshFromSession();

        _pageViewportStore.Initialize(TotalPages > 0 ? TotalPages : 1);
        _pageViewportStore.SetActivePage(new PageIndex(0));
        RefreshPageViewState();

        _isImageAutoFitEnabled = IsCurrentImageDocument;
        NotifyViewerModeChanged();

        ClearThumbnails();
        if (!IsCurrentImageDocument)
        {
            BuildThumbnailPlaceholders();
        }

        AddCurrentDocumentToRecentFiles();

        UserMessage = null;
        StatusText = $"Opened {CurrentDocumentName}";

        if (!await TryApplyImageAutoFitAsync(forceRender: true))
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
            UserMessage = $"Page number must be between 1 and {TotalPages}.";
            StatusText = "Invalid page number";
            return;
        }

        var result = _changePageUseCase.Execute(
            new ChangePageRequest(new PageIndex(pageNumber - 1)));

        if (result.IsFailure)
        {
            UserMessage = result.Error?.Message ?? "Unable to change page.";
            StatusText = "Page change failed";
            return;
        }

        _pageViewportStore.SetActivePage(new PageIndex(pageNumber - 1));
        RefreshFromSession();
        RefreshPageViewState();
        await RenderCurrentPageAsync();
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
                UserMessage = result.Error?.Message ?? "Unable to render the current page.";
                StatusText = "Render failed";

                CurrentRenderedBitmap?.Dispose();
                CurrentRenderedBitmap = null;
                return;
            }

            if (result.Page is null)
            {
                UserMessage = "No rendered page was returned.";
                StatusText = "Render failed";

                CurrentRenderedBitmap?.Dispose();
                CurrentRenderedBitmap = null;
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

    private async Task<bool> TryApplyImageAutoFitAsync(
        bool preserveStatusText = false,
        bool forceRender = false)
    {
        if (!_isImageAutoFitEnabled ||
            _documentViewportWidth <= 0 ||
            _documentViewportHeight <= 0 ||
            _documentSessionStore.Current is not IImageDocumentSession imageSession)
        {
            return false;
        }

        var pageState = _pageViewportStore.GetPageState(_pageViewportStore.ActivePageIndex);
        var fitZoom = ImageViewportCalculator.CalculateFitZoom(
            imageSession.ImageMetadata,
            pageState.Rotation,
            Math.Max(1, _documentViewportWidth - (ViewerContentPadding * 2)),
            Math.Max(1, _documentViewportHeight - (ViewerContentPadding * 2)));

        var zoomChanged = Math.Abs(pageState.ZoomFactor - fitZoom) > ZoomComparisonTolerance;
        if (!zoomChanged && !forceRender && CurrentRenderedBitmap is not null)
        {
            return false;
        }

        if (zoomChanged)
        {
            _pageViewportStore.SetPageState(pageState.WithZoom(fitZoom));
            RefreshPageViewState();
        }

        await RenderCurrentPageAsync(preserveStatusText);
        return true;
    }

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
            rotation);
    }

    private static string CreateThumbnailJobKey(PageIndex pageIndex) =>
        $"{ThumbnailRenderJobPrefix}{pageIndex.Value}";

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
