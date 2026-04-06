using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.UseCases;
using Velune.Domain.ValueObjects;
using Velune.Presentation.Imaging;

namespace Velune.Presentation.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private const double ZoomStep = 0.10;
    private const double MinZoom = 0.25;
    private const double MaxZoom = 5.00;
    private const double ThumbnailZoomFactor = 0.20;

    private readonly IFilePickerService _filePickerService;
    private readonly OpenDocumentUseCase _openDocumentUseCase;
    private readonly CloseDocumentUseCase _closeDocumentUseCase;
    private readonly ChangePageUseCase _changePageUseCase;
    private readonly RenderVisiblePageUseCase _renderPageUseCase;
    private readonly IDocumentSessionStore _documentSessionStore;
    private readonly IRecentFilesService _recentFilesService;
    private readonly IPageViewportStore _pageViewportStore;

    private CancellationTokenSource? _renderCancellationTokenSource;
    private CancellationTokenSource? _thumbnailCancellationTokenSource;
    private bool _disposed;

    public MainWindowViewModel(
        IFilePickerService filePickerService,
        OpenDocumentUseCase openDocumentUseCase,
        CloseDocumentUseCase closeDocumentUseCase,
        ChangePageUseCase changePageUseCase,
        RenderVisiblePageUseCase renderPageUseCase,
        IDocumentSessionStore documentSessionStore,
        IRecentFilesService recentFilesService,
        IPageViewportStore pageViewportStore)
    {
        ArgumentNullException.ThrowIfNull(filePickerService);
        ArgumentNullException.ThrowIfNull(openDocumentUseCase);
        ArgumentNullException.ThrowIfNull(closeDocumentUseCase);
        ArgumentNullException.ThrowIfNull(changePageUseCase);
        ArgumentNullException.ThrowIfNull(renderPageUseCase);
        ArgumentNullException.ThrowIfNull(documentSessionStore);
        ArgumentNullException.ThrowIfNull(recentFilesService);
        ArgumentNullException.ThrowIfNull(pageViewportStore);

        _filePickerService = filePickerService;
        _openDocumentUseCase = openDocumentUseCase;
        _closeDocumentUseCase = closeDocumentUseCase;
        _changePageUseCase = changePageUseCase;
        _renderPageUseCase = renderPageUseCase;
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
    public string PageIndicator => TotalPages > 0 ? $"{CurrentPage} / {TotalPages}" : "-";

    public bool CanGoPreviousPage => HasOpenDocument && CurrentPage > 1;
    public bool CanGoNextPage => HasOpenDocument && TotalPages > 0 && CurrentPage < TotalPages;
    public bool CanGoToPage => HasOpenDocument && TotalPages > 0;
    public bool ShouldUseTrackpadForPan =>
        HasOpenDocument &&
        _pageViewportStore.GetPageState(_pageViewportStore.ActivePageIndex).ZoomFactor > 1.0;

    partial void OnHasOpenDocumentChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEmptyStateVisible));
        OnPropertyChanged(nameof(CanGoToPage));
        CloseCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        GoToPageCommand.NotifyCanExecuteChanged();
        ZoomInCommand.NotifyCanExecuteChanged();
        ZoomOutCommand.NotifyCanExecuteChanged();
        RotateLeftCommand.NotifyCanExecuteChanged();
        RotateRightCommand.NotifyCanExecuteChanged();
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

        await RenderCurrentPageAsync();
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

        await RenderCurrentPageAsync();
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

    private async Task OpenDocumentFromPathAsync(string filePath)
    {
        try
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

            BuildThumbnailPlaceholders();
            _ = GenerateThumbnailsAsync();

            AddCurrentDocumentToRecentFiles();

            UserMessage = null;
            StatusText = $"Opened {CurrentDocumentName}";

            await RenderCurrentPageAsync();
        }
        catch (FileNotFoundException)
        {
            UserMessage = "The selected file could not be found.";
            StatusText = "Open failed";
        }
        catch (DirectoryNotFoundException)
        {
            UserMessage = "The selected file location does not exist anymore.";
            StatusText = "Open failed";
        }
        catch (Exception ex)
        {
            UserMessage = ex.Message;
            StatusText = "Open failed";
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

    private async Task RenderCurrentPageAsync()
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

        _renderCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _renderCancellationTokenSource.Token;

        try
        {
            IsRendering = true;

            var result = await _renderPageUseCase.ExecuteAsync(
                new RenderPageRequest(
                    pageState.PageIndex,
                    pageState.ZoomFactor,
                    pageState.Rotation),
                cancellationToken);

            if (result.IsFailure)
            {
                UserMessage = result.Error?.Message ?? "Unable to render the current page.";
                StatusText = "Render failed";

                CurrentRenderedBitmap?.Dispose();
                CurrentRenderedBitmap = null;
                return;
            }

            if (result.Value is null)
            {
                UserMessage = "No rendered page was returned.";
                StatusText = "Render failed";

                CurrentRenderedBitmap?.Dispose();
                CurrentRenderedBitmap = null;
                return;
            }

            CurrentRenderedBitmap?.Dispose();
            CurrentRenderedBitmap = RenderedPageBitmapFactory.Create(result.Value);
            StatusText = $"Rendered page {CurrentPage}";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            UserMessage = ex.Message;
            StatusText = "Render failed";

            CurrentRenderedBitmap?.Dispose();
            CurrentRenderedBitmap = null;
        }
        finally
        {
            IsRendering = false;
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

        _thumbnailCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _thumbnailCancellationTokenSource.Token;

        try
        {
            IsGeneratingThumbnails = true;

            for (var i = 0; i < Thumbnails.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var thumbnailItem = Thumbnails[i];
                var thumbnailPageIndex = new PageIndex(i);
                var thumbnailPageState = _pageViewportStore.GetPageState(thumbnailPageIndex);

                try
                {
                    var result = await _renderPageUseCase.ExecuteAsync(
                        new RenderPageRequest(
                            thumbnailPageIndex,
                            ThumbnailZoomFactor,
                            thumbnailPageState.Rotation),
                        cancellationToken);

                    if (result.IsSuccess && result.Value is not null)
                    {
                        var bitmap = RenderedPageBitmapFactory.Create(result.Value);

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
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsGeneratingThumbnails = false;
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

            var result = await _renderPageUseCase.ExecuteAsync(
                new RenderPageRequest(
                    pageIndex,
                    ThumbnailZoomFactor,
                    pageState.Rotation));

            if (result.IsSuccess && result.Value is not null)
            {
                var bitmap = RenderedPageBitmapFactory.Create(result.Value);

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
        if (_renderCancellationTokenSource is null)
        {
            return;
        }

        _renderCancellationTokenSource.Cancel();
        _renderCancellationTokenSource.Dispose();
        _renderCancellationTokenSource = null;
    }

    private void CancelThumbnailGeneration()
    {
        if (_thumbnailCancellationTokenSource is null)
        {
            return;
        }

        _thumbnailCancellationTokenSource.Cancel();
        _thumbnailCancellationTokenSource.Dispose();
        _thumbnailCancellationTokenSource = null;
    }

    private void RefreshFromSession()
    {
        var session = _documentSessionStore.Current;
        if (session is null)
        {
            ResetDocumentState();
            return;
        }

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
        HasOpenDocument = false;
        CurrentDocumentName = null;
        CurrentDocumentType = null;
        CurrentDocumentPath = null;
        CurrentPage = 1;
        TotalPages = 0;
        CurrentZoom = "100%";
        CurrentRotation = "0°";
        GoToPageInput = "1";

        CurrentRenderedBitmap?.Dispose();
        CurrentRenderedBitmap = null;

        EmptyStateTitle = "Open a document";
        EmptyStateDescription = "Open a PDF or an image to start viewing it.";
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
            _renderCancellationTokenSource?.Cancel();
            _renderCancellationTokenSource?.Dispose();
            _renderCancellationTokenSource = null;

            _thumbnailCancellationTokenSource?.Cancel();
            _thumbnailCancellationTokenSource?.Dispose();
            _thumbnailCancellationTokenSource = null;

            CurrentRenderedBitmap?.Dispose();
            CurrentRenderedBitmap = null;

            ClearThumbnails();
        }

        _disposed = true;
    }

    public async Task NavigateToPreviousPageFromTrackpadAsync()
    {
        if (CanGoPreviousPage)
        {
            await PreviousPageAsync();
        }
    }

    public async Task NavigateToNextPageFromTrackpadAsync()
    {
        if (CanGoNextPage)
        {
            await NextPageAsync();
        }
    }
}
