using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Application.DTOs;
using Velune.Application.Results;
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
    private const double InfoPanelExpandedWidth = 300;
    private const double ViewportResizeThreshold = 1.0;
    private const double ZoomComparisonTolerance = 0.001;
    private const string ThemeSystemLabel = "System";
    private const string ThemeLightLabel = "Light";
    private const string ThemeDarkLabel = "Dark";
    private const string DefaultZoomFitToPageLabel = "Fit to page";
    private const string DefaultZoomFitToWidthLabel = "Fit to width";
    private const string DefaultZoomActualSizeLabel = "100%";

    private readonly IFilePickerService _filePickerService;
    private readonly OpenDocumentUseCase _openDocumentUseCase;
    private readonly CloseDocumentUseCase _closeDocumentUseCase;
    private readonly ChangePageUseCase _changePageUseCase;
    private readonly ChangeZoomUseCase _changeZoomUseCase;
    private readonly RotateDocumentUseCase _rotateDocumentUseCase;
    private readonly RotatePdfPagesUseCase _rotatePdfPagesUseCase;
    private readonly DeletePdfPagesUseCase _deletePdfPagesUseCase;
    private readonly ExtractPdfPagesUseCase _extractPdfPagesUseCase;
    private readonly ReorderPdfPagesUseCase _reorderPdfPagesUseCase;
    private readonly IRenderOrchestrator _renderOrchestrator;
    private readonly IDocumentSessionStore _documentSessionStore;
    private readonly IRecentFilesService _recentFilesService;
    private readonly IPageViewportStore _pageViewportStore;
    private readonly IUserPreferencesService _userPreferencesService;

    private readonly Queue<NotificationEntry> _notificationQueue = [];
    private readonly Dictionary<int, RenderJobHandle> _thumbnailRenderJobs = [];
    private readonly IReadOnlyList<string> _themePreferenceOptions = [ThemeSystemLabel, ThemeLightLabel, ThemeDarkLabel];
    private readonly IReadOnlyList<string> _defaultZoomPreferenceOptions = [DefaultZoomFitToPageLabel, DefaultZoomFitToWidthLabel, DefaultZoomActualSizeLabel];
    private bool _disposed;
    private double _documentViewportWidth;
    private double _documentViewportHeight;
    private bool _isImageAutoFitEnabled;
    private bool _isApplyingPdfStructureOperation;
    private bool _isApplyingPreferencesState;
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
        RotatePdfPagesUseCase rotatePdfPagesUseCase,
        DeletePdfPagesUseCase deletePdfPagesUseCase,
        ExtractPdfPagesUseCase extractPdfPagesUseCase,
        ReorderPdfPagesUseCase reorderPdfPagesUseCase,
        IRenderOrchestrator renderOrchestrator,
        IDocumentSessionStore documentSessionStore,
        IRecentFilesService recentFilesService,
        IPageViewportStore pageViewportStore,
        IUserPreferencesService userPreferencesService)
    {
        ArgumentNullException.ThrowIfNull(filePickerService);
        ArgumentNullException.ThrowIfNull(openDocumentUseCase);
        ArgumentNullException.ThrowIfNull(closeDocumentUseCase);
        ArgumentNullException.ThrowIfNull(changePageUseCase);
        ArgumentNullException.ThrowIfNull(changeZoomUseCase);
        ArgumentNullException.ThrowIfNull(rotateDocumentUseCase);
        ArgumentNullException.ThrowIfNull(rotatePdfPagesUseCase);
        ArgumentNullException.ThrowIfNull(deletePdfPagesUseCase);
        ArgumentNullException.ThrowIfNull(extractPdfPagesUseCase);
        ArgumentNullException.ThrowIfNull(reorderPdfPagesUseCase);
        ArgumentNullException.ThrowIfNull(renderOrchestrator);
        ArgumentNullException.ThrowIfNull(documentSessionStore);
        ArgumentNullException.ThrowIfNull(recentFilesService);
        ArgumentNullException.ThrowIfNull(pageViewportStore);
        ArgumentNullException.ThrowIfNull(userPreferencesService);

        _filePickerService = filePickerService;
        _openDocumentUseCase = openDocumentUseCase;
        _closeDocumentUseCase = closeDocumentUseCase;
        _changePageUseCase = changePageUseCase;
        _changeZoomUseCase = changeZoomUseCase;
        _rotateDocumentUseCase = rotateDocumentUseCase;
        _rotatePdfPagesUseCase = rotatePdfPagesUseCase;
        _deletePdfPagesUseCase = deletePdfPagesUseCase;
        _extractPdfPagesUseCase = extractPdfPagesUseCase;
        _reorderPdfPagesUseCase = reorderPdfPagesUseCase;
        _renderOrchestrator = renderOrchestrator;
        _documentSessionStore = documentSessionStore;
        _recentFilesService = recentFilesService;
        _pageViewportStore = pageViewportStore;
        _userPreferencesService = userPreferencesService;

        RecentFiles = [];
        Thumbnails = [];
        DocumentInfoItems = [];
        MemoryCacheEntryLimitOptions = new ObservableCollection<int> { 0, 32, 64, 128, 256 };
        ApplyPreferencesToUi(_userPreferencesService.Current);
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
    private bool _isInfoPanelVisible;

    [ObservableProperty]
    private bool _isPreferencesPanelVisible;

    [ObservableProperty]
    private string? _documentInfoWarning;

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

    [ObservableProperty]
    private bool _hasPendingPageReorder;

    [ObservableProperty]
    private string _selectedThemePreference = ThemeSystemLabel;

    [ObservableProperty]
    private string _selectedDefaultZoomPreference = DefaultZoomFitToPageLabel;

    [ObservableProperty]
    private bool _showThumbnailsPanelPreference = true;

    [ObservableProperty]
    private int _selectedMemoryCacheEntryLimit = 64;

    public ObservableCollection<RecentFileItem> RecentFiles
    {
        get;
    }
    public ObservableCollection<PageThumbnailItemViewModel> Thumbnails
    {
        get;
    }
    public ObservableCollection<DocumentInfoItem> DocumentInfoItems
    {
        get;
    }
    public ObservableCollection<int> MemoryCacheEntryLimitOptions
    {
        get;
    }
    public IReadOnlyList<string> ThemePreferenceOptions => _themePreferenceOptions;
    public IReadOnlyList<string> DefaultZoomPreferenceOptions => _defaultZoomPreferenceOptions;

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
    public bool IsPdfDocument => HasOpenDocument && !IsCurrentImageDocument;
    public bool IsImageAutoFitActive => HasOpenDocument && IsCurrentImageDocument && _isImageAutoFitEnabled;
    public bool IsScrollableViewerVisible => !IsImageAutoFitActive;
    public bool IsSidebarVisible => HasOpenDocument && !IsCurrentImageDocument && ShowThumbnailsPanelPreference;
    public bool IsPageNavigationVisible => !HasOpenDocument || !IsCurrentImageDocument;
    public bool IsPdfStructureActionsVisible => IsPdfDocument;
    public bool IsInfoPanelOpen => HasOpenDocument && IsInfoPanelVisible;
    public bool IsPreferencesPanelOpen => HasOpenDocument && IsPreferencesPanelVisible;
    public bool HasDocumentInfo => DocumentInfoItems.Count > 0;
    public bool HasDocumentInfoWarning => !string.IsNullOrWhiteSpace(DocumentInfoWarning);
    public GridLength SidebarColumnWidth => new(SidebarWidth);
    public double SidebarWidth => IsSidebarVisible ? SidebarExpandedWidth : 0;
    public double InfoPanelWidth => IsInfoPanelOpen ? InfoPanelExpandedWidth : 0;
    public double PreferencesPanelWidth => IsPreferencesPanelOpen ? InfoPanelExpandedWidth : 0;
    public string PageIndicator => TotalPages > 0 ? $"{CurrentPage} / {TotalPages}" : "-";
    public bool IsPdfStructureOperationInProgress => _isApplyingPdfStructureOperation;

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
    public bool CanPersistCurrentPageRotation =>
        IsPdfDocument &&
        !IsPdfStructureOperationInProgress &&
        GetPendingPdfRotationGroups().Count > 0;
    public bool CanExtractCurrentPage =>
        IsPdfDocument &&
        !IsPdfStructureOperationInProgress &&
        TotalPages > 0;
    public bool CanDeleteCurrentPage =>
        IsPdfDocument &&
        !IsPdfStructureOperationInProgress &&
        TotalPages > 1;
    public bool CanSaveReorderedPdf =>
        IsPdfDocument &&
        !IsPdfStructureOperationInProgress &&
        HasPendingPageReorder;
    public bool CanMoveCurrentPageEarlier =>
        IsPdfDocument &&
        !IsPdfStructureOperationInProgress &&
        GetCurrentThumbnailIndex() > 0;
    public bool CanMoveCurrentPageLater =>
        IsPdfDocument &&
        !IsPdfStructureOperationInProgress &&
        GetCurrentThumbnailIndex() >= 0 &&
        GetCurrentThumbnailIndex() < Thumbnails.Count - 1;

    partial void OnHasOpenDocumentChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEmptyStateVisible));
        OnPropertyChanged(nameof(CanGoToPage));
        OnPropertyChanged(nameof(IsPdfDocument));
        NotifyViewerModeChanged();
        NotifySidebarVisibilityChanged();
        OnPropertyChanged(nameof(IsPageNavigationVisible));
        OnPropertyChanged(nameof(IsPdfStructureActionsVisible));
        OnPropertyChanged(nameof(IsInfoPanelOpen));
        OnPropertyChanged(nameof(InfoPanelWidth));
        OnPropertyChanged(nameof(IsPreferencesPanelOpen));
        OnPropertyChanged(nameof(PreferencesPanelWidth));
        CloseCommand.NotifyCanExecuteChanged();
        ToggleInfoPanelCommand.NotifyCanExecuteChanged();
        TogglePreferencesPanelCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        GoToPageCommand.NotifyCanExecuteChanged();
        ZoomInCommand.NotifyCanExecuteChanged();
        ZoomOutCommand.NotifyCanExecuteChanged();
        FitToWidthCommand.NotifyCanExecuteChanged();
        FitToPageCommand.NotifyCanExecuteChanged();
        RotateLeftCommand.NotifyCanExecuteChanged();
        RotateRightCommand.NotifyCanExecuteChanged();
        PersistCurrentPageRotationCommand.NotifyCanExecuteChanged();
        ExtractCurrentPageCommand.NotifyCanExecuteChanged();
        DeleteCurrentPageCommand.NotifyCanExecuteChanged();
        SaveReorderedPdfCommand.NotifyCanExecuteChanged();
        MoveCurrentPageEarlierCommand.NotifyCanExecuteChanged();
        MoveCurrentPageLaterCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsCurrentImageDocumentChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPdfDocument));
        NotifyViewerModeChanged();
        NotifySidebarVisibilityChanged();
        OnPropertyChanged(nameof(IsPageNavigationVisible));
        OnPropertyChanged(nameof(IsPdfStructureActionsVisible));
        PersistCurrentPageRotationCommand.NotifyCanExecuteChanged();
        ExtractCurrentPageCommand.NotifyCanExecuteChanged();
        DeleteCurrentPageCommand.NotifyCanExecuteChanged();
        SaveReorderedPdfCommand.NotifyCanExecuteChanged();
        MoveCurrentPageEarlierCommand.NotifyCanExecuteChanged();
        MoveCurrentPageLaterCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsInfoPanelVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsInfoPanelOpen));
        OnPropertyChanged(nameof(InfoPanelWidth));
    }

    partial void OnIsPreferencesPanelVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPreferencesPanelOpen));
        OnPropertyChanged(nameof(PreferencesPanelWidth));
    }

    partial void OnDocumentInfoWarningChanged(string? value)
    {
        OnPropertyChanged(nameof(HasDocumentInfoWarning));
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
        PersistCurrentPageRotationCommand.NotifyCanExecuteChanged();
        ExtractCurrentPageCommand.NotifyCanExecuteChanged();
        DeleteCurrentPageCommand.NotifyCanExecuteChanged();
        MoveCurrentPageEarlierCommand.NotifyCanExecuteChanged();
        MoveCurrentPageLaterCommand.NotifyCanExecuteChanged();
    }

    partial void OnTotalPagesChanged(int value)
    {
        OnPropertyChanged(nameof(HasMultiplePages));
        OnPropertyChanged(nameof(PageIndicator));
        OnPropertyChanged(nameof(CanGoToPage));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        GoToPageCommand.NotifyCanExecuteChanged();
        ExtractCurrentPageCommand.NotifyCanExecuteChanged();
        DeleteCurrentPageCommand.NotifyCanExecuteChanged();
        MoveCurrentPageEarlierCommand.NotifyCanExecuteChanged();
        MoveCurrentPageLaterCommand.NotifyCanExecuteChanged();
    }

    partial void OnHasPendingPageReorderChanged(bool value)
    {
        SaveReorderedPdfCommand.NotifyCanExecuteChanged();
        MoveCurrentPageEarlierCommand.NotifyCanExecuteChanged();
        MoveCurrentPageLaterCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedThemePreferenceChanged(string value)
    {
        if (_isApplyingPreferencesState)
        {
            return;
        }

        _ = PersistPreferencesFromUiAsync(applyDefaultZoomToOpenDocument: false);
    }

    partial void OnSelectedDefaultZoomPreferenceChanged(string value)
    {
        if (_isApplyingPreferencesState)
        {
            return;
        }

        _ = PersistPreferencesFromUiAsync(applyDefaultZoomToOpenDocument: true);
    }

    partial void OnShowThumbnailsPanelPreferenceChanged(bool value)
    {
        NotifySidebarVisibilityChanged();

        if (_isApplyingPreferencesState)
        {
            return;
        }

        _ = PersistPreferencesFromUiAsync(applyDefaultZoomToOpenDocument: false);
    }

    partial void OnSelectedMemoryCacheEntryLimitChanged(int value)
    {
        if (_isApplyingPreferencesState)
        {
            return;
        }

        _ = PersistPreferencesFromUiAsync(applyDefaultZoomToOpenDocument: false);
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

        await ChangeToPageAsync(thumbnail.SourcePageNumber);
    }

    [RelayCommand(CanExecute = nameof(CanMoveCurrentPageEarlier))]
    private async Task MoveCurrentPageEarlierAsync()
    {
        var currentIndex = GetCurrentThumbnailIndex();
        if (currentIndex <= 0)
        {
            return;
        }

        await HandleThumbnailReorderAsync(
            CurrentPage,
            Thumbnails[currentIndex - 1].SourcePageNumber);

        StatusText = $"Moved page {CurrentPage} earlier";
    }

    [RelayCommand(CanExecute = nameof(CanMoveCurrentPageLater))]
    private async Task MoveCurrentPageLaterAsync()
    {
        var currentIndex = GetCurrentThumbnailIndex();
        if (currentIndex < 0 || currentIndex >= Thumbnails.Count - 1)
        {
            return;
        }

        await HandleThumbnailReorderAsync(
            CurrentPage,
            Thumbnails[currentIndex + 1].SourcePageNumber);

        StatusText = $"Moved page {CurrentPage} later";
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

    [RelayCommand(CanExecute = nameof(CanPersistCurrentPageRotation))]
    private async Task PersistCurrentPageRotationAsync()
    {
        var currentDocumentPath = CurrentDocumentPath;
        var pendingRotations = GetPendingPdfRotationGroups();

        if (string.IsNullOrWhiteSpace(currentDocumentPath) || pendingRotations.Count == 0)
        {
            return;
        }

        await ExecutePdfStructureOperationAsync(
            title: "Save PDF with page rotations",
            suggestedFileName: BuildSuggestedPdfFileName(),
            executeOperation: outputPath => PersistPendingPdfRotationsAsync(
                currentDocumentPath,
                outputPath,
                pendingRotations),
            successStatus: pendingRotations.Count == 1
                ? $"Saved rotated PDF copy for page {pendingRotations[0].Pages[0]}"
                : "Saved rotated PDF copy",
            failureTitle: "Unable to save rotated PDF");
    }

    [RelayCommand(CanExecute = nameof(CanExtractCurrentPage))]
    private async Task ExtractCurrentPageAsync()
    {
        var currentDocumentPath = CurrentDocumentPath;
        if (string.IsNullOrWhiteSpace(currentDocumentPath))
        {
            return;
        }

        await ExecutePdfStructureOperationAsync(
            title: "Extract current page",
            suggestedFileName: BuildSuggestedPdfFileName(),
            executeOperation: outputPath => _extractPdfPagesUseCase.ExecuteAsync(
                new ExtractPdfPagesRequest(
                    currentDocumentPath,
                    outputPath,
                    [CurrentPage])),
            successStatus: $"Extracted page {CurrentPage}",
            failureTitle: "Unable to extract page");
    }

    [RelayCommand(CanExecute = nameof(CanDeleteCurrentPage))]
    private async Task DeleteCurrentPageAsync()
    {
        var currentDocumentPath = CurrentDocumentPath;
        if (string.IsNullOrWhiteSpace(currentDocumentPath))
        {
            return;
        }

        await ExecutePdfStructureOperationAsync(
            title: "Save PDF without current page",
            suggestedFileName: BuildSuggestedPdfFileName(),
            executeOperation: outputPath => _deletePdfPagesUseCase.ExecuteAsync(
                new DeletePdfPagesRequest(
                    currentDocumentPath,
                    outputPath,
                    [CurrentPage])),
            successStatus: $"Saved PDF without page {CurrentPage}",
            failureTitle: "Unable to delete page");
    }

    [RelayCommand(CanExecute = nameof(CanSaveReorderedPdf))]
    private async Task SaveReorderedPdfAsync()
    {
        var currentDocumentPath = CurrentDocumentPath;
        if (string.IsNullOrWhiteSpace(currentDocumentPath))
        {
            return;
        }

        var orderedPages = Thumbnails
            .Select(thumbnail => thumbnail.SourcePageNumber)
            .ToArray();

        await ExecutePdfStructureOperationAsync(
            title: "Save reordered PDF",
            suggestedFileName: BuildSuggestedPdfFileName(),
            executeOperation: outputPath => _reorderPdfPagesUseCase.ExecuteAsync(
                new ReorderPdfPagesRequest(
                    currentDocumentPath,
                    outputPath,
                    orderedPages)),
            successStatus: "Saved reordered PDF copy",
            failureTitle: "Unable to save reordered PDF");
    }

    [RelayCommand(CanExecute = nameof(HasOpenDocument))]
    private void ToggleInfoPanel()
    {
        if (!IsInfoPanelVisible)
        {
            IsPreferencesPanelVisible = false;
        }

        IsInfoPanelVisible = !IsInfoPanelVisible;
        StatusText = IsInfoPanelVisible
            ? "File information shown"
            : "File information hidden";
    }

    [RelayCommand(CanExecute = nameof(HasOpenDocument))]
    private void TogglePreferencesPanel()
    {
        if (!IsPreferencesPanelVisible)
        {
            IsInfoPanelVisible = false;
        }

        IsPreferencesPanelVisible = !IsPreferencesPanelVisible;
        StatusText = IsPreferencesPanelVisible
            ? "Preferences shown"
            : "Preferences hidden";
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

    public async Task HandleThumbnailActivatedAsync(int sourcePageNumber)
    {
        if (!HasOpenDocument || sourcePageNumber <= 0)
        {
            return;
        }

        await ChangeToPageAsync(sourcePageNumber);
    }

    public async Task HandleThumbnailReorderAsync(int sourcePageNumber, int targetPageNumber)
    {
        if (!IsPdfDocument ||
            IsPdfStructureOperationInProgress ||
            sourcePageNumber <= 0 ||
            targetPageNumber <= 0 ||
            sourcePageNumber == targetPageNumber)
        {
            return;
        }

        var sourceThumbnail = Thumbnails.FirstOrDefault(item => item.SourcePageNumber == sourcePageNumber);
        var targetThumbnail = Thumbnails.FirstOrDefault(item => item.SourcePageNumber == targetPageNumber);

        if (sourceThumbnail is null || targetThumbnail is null)
        {
            return;
        }

        var sourceIndex = Thumbnails.IndexOf(sourceThumbnail);
        var targetIndex = Thumbnails.IndexOf(targetThumbnail);
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
        {
            return;
        }

        MoveThumbnailToIndex(sourceIndex, targetIndex, announce: true);

        await Task.CompletedTask;
    }

    public async Task HandleThumbnailReorderToIndexAsync(int sourcePageNumber, int targetIndex)
    {
        if (!IsPdfDocument ||
            IsPdfStructureOperationInProgress ||
            sourcePageNumber <= 0 ||
            targetIndex < 0 ||
            targetIndex >= Thumbnails.Count)
        {
            return;
        }

        var sourceThumbnail = Thumbnails.FirstOrDefault(item => item.SourcePageNumber == sourcePageNumber);
        if (sourceThumbnail is null)
        {
            return;
        }

        var sourceIndex = Thumbnails.IndexOf(sourceThumbnail);
        if (sourceIndex < 0 || sourceIndex == targetIndex)
        {
            return;
        }

        MoveThumbnailToIndex(sourceIndex, targetIndex, announce: false);
        await Task.CompletedTask;
    }

    public void CompleteThumbnailReorderDrag()
    {
        if (!HasPendingPageReorder)
        {
            return;
        }

        EnqueueInfo(
            "Page order updated",
            "Drag other thumbnails if needed, then save a reordered PDF copy.",
            replaceCurrent: true);
        StatusText = "Page order updated";
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
        HasPendingPageReorder = false;

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
        var detailsWarning = _documentSessionStore.Current?.Metadata.DetailsWarning;
        if (!string.IsNullOrWhiteSpace(detailsWarning))
        {
            EnqueueInfo("Some file details are unavailable", detailsWarning);
        }

        StatusText = $"Opened {CurrentDocumentName}";

        await ApplyPreferredDefaultZoomAsync();

        if (!IsCurrentImageDocument)
        {
            _ = GenerateThumbnailsAsync();
        }
    }

    private async Task ApplyPreferredDefaultZoomAsync(bool preserveStatusText = false)
    {
        if (!HasOpenDocument)
        {
            return;
        }

        var defaultZoomPreference = ParseDefaultZoomPreference(SelectedDefaultZoomPreference);
        switch (defaultZoomPreference)
        {
            case DefaultZoomPreference.FitToWidth:
                await ApplyPreferredFitZoomAsync(ZoomMode.FitToWidth, preserveStatusText);
                return;
            case DefaultZoomPreference.ActualSize:
                await ApplyExactZoomAsync(1.0, preserveStatusText);
                return;
            default:
                await ApplyPreferredFitZoomAsync(ZoomMode.FitToPage, preserveStatusText);
                return;
        }
    }

    private async Task ApplyPreferredFitZoomAsync(ZoomMode zoomMode, bool preserveStatusText)
    {
        if (!IsCurrentImageDocument && CurrentRenderedBitmap is null)
        {
            await RenderCurrentPageAsync(preserveStatusText: true);
        }

        if (await TryApplyViewportFitAsync(zoomMode, preserveStatusText, forceRender: true))
        {
            return;
        }

        var pageState = _pageViewportStore.GetPageState(_pageViewportStore.ActivePageIndex);
        if (IsCurrentImageDocument)
        {
            _isImageAutoFitEnabled = zoomMode is ZoomMode.FitToPage;
            NotifyViewerModeChanged();
        }

        if (TryUpdateZoom(pageState.ZoomFactor, zoomMode) && CurrentRenderedBitmap is null)
        {
            await RenderCurrentPageAsync(preserveStatusText);
        }
    }

    private async Task ApplyExactZoomAsync(double zoomFactor, bool preserveStatusText)
    {
        DisableImageAutoFit();

        var pageState = _pageViewportStore.GetPageState(_pageViewportStore.ActivePageIndex);
        var clampedZoom = Math.Clamp(zoomFactor, MinZoom, MaxZoom);
        var zoomChanged = Math.Abs(pageState.ZoomFactor - clampedZoom) > ZoomComparisonTolerance;
        var zoomModeChanged = CurrentZoomMode != ZoomMode.Custom;

        if (zoomChanged || zoomModeChanged)
        {
            _pageViewportStore.SetPageState(pageState.WithZoom(clampedZoom));
            if (!TryUpdateZoom(clampedZoom, ZoomMode.Custom))
            {
                return;
            }

            RefreshPageViewState();
        }

        await RenderCurrentPageAsync(preserveStatusText);
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
                var thumbnailPageIndex = new PageIndex(thumbnailItem.SourcePageNumber - 1);
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

        var thumbnailItem = Thumbnails.FirstOrDefault(item => item.SourcePageNumber == CurrentPage);
        if (thumbnailItem is null)
        {
            return;
        }

        var pageIndex = new PageIndex(thumbnailItem.SourcePageNumber - 1);
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

        RefreshThumbnailDisplayNumbers();
        NotifyThumbnailOrderCommandsChanged();
        OnPropertyChanged(nameof(HasThumbnails));
    }

    private void UpdateSelectedThumbnail()
    {
        foreach (var item in Thumbnails)
        {
            item.IsSelected = item.SourcePageNumber == CurrentPage;
        }
    }

    private void ClearThumbnails()
    {
        foreach (var item in Thumbnails)
        {
            item.Dispose();
        }

        Thumbnails.Clear();
        NotifyThumbnailOrderCommandsChanged();
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
        RefreshDocumentInfo(session.Metadata);

        EmptyStateTitle = session.Metadata.FileName;
        EmptyStateDescription = $"Opened {session.Metadata.DocumentType} document.";
    }

    private void RefreshPageViewState()
    {
        var pageState = _pageViewportStore.GetPageState(_pageViewportStore.ActivePageIndex);
        CurrentZoom = $"{pageState.ZoomFactor * 100:0}%";
        CurrentRotation = $"{(int)pageState.Rotation}°";
        CurrentPage = pageState.PageIndex.Value + 1;
        PersistCurrentPageRotationCommand.NotifyCanExecuteChanged();
    }

    private void ResetDocumentState()
    {
        _isImageAutoFitEnabled = false;
        _isApplyingPdfStructureOperation = false;
        HasPendingPageReorder = false;
        HasOpenDocument = false;
        IsCurrentImageDocument = false;
        CurrentDocumentName = null;
        CurrentDocumentType = null;
        CurrentDocumentPath = null;
        IsInfoPanelVisible = false;
        IsPreferencesPanelVisible = false;
        ClearDocumentInfo();
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

    private List<PendingPdfRotationGroup> GetPendingPdfRotationGroups()
    {
        var groups = Enumerable.Range(1, TotalPages)
            .Select(pageNumber => new
            {
                PageNumber = pageNumber,
                Rotation = _pageViewportStore.GetPageState(new PageIndex(pageNumber - 1)).Rotation
            })
            .Where(item => item.Rotation != Rotation.Deg0)
            .GroupBy(item => item.Rotation)
            .OrderBy(group => group.Key)
            .Select(group => new PendingPdfRotationGroup(
                group.Key,
                group.Select(item => item.PageNumber).OrderBy(pageNumber => pageNumber).ToArray()))
            .ToList();

        return groups;
    }

    private void MoveThumbnailToIndex(int sourceIndex, int targetIndex, bool announce)
    {
        if (sourceIndex < 0 ||
            sourceIndex >= Thumbnails.Count ||
            targetIndex < 0 ||
            targetIndex >= Thumbnails.Count ||
            sourceIndex == targetIndex)
        {
            return;
        }

        Thumbnails.Move(sourceIndex, targetIndex);
        RefreshThumbnailDisplayNumbers();
        HasPendingPageReorder = true;
        NotifyThumbnailOrderCommandsChanged();

        if (!announce)
        {
            return;
        }

        EnqueueInfo(
            "Page order updated",
            "Drag other thumbnails if needed, then save a reordered PDF copy.",
            replaceCurrent: true);
        StatusText = "Page order updated";
    }

    private void RefreshThumbnailDisplayNumbers()
    {
        for (var i = 0; i < Thumbnails.Count; i++)
        {
            Thumbnails[i].DisplayPageNumber = i + 1;
        }
    }

    private int GetCurrentThumbnailIndex()
    {
        for (var i = 0; i < Thumbnails.Count; i++)
        {
            if (Thumbnails[i].SourcePageNumber == CurrentPage)
            {
                return i;
            }
        }

        return -1;
    }

    private void NotifyThumbnailOrderCommandsChanged()
    {
        MoveCurrentPageEarlierCommand.NotifyCanExecuteChanged();
        MoveCurrentPageLaterCommand.NotifyCanExecuteChanged();
    }

    private async Task ExecutePdfStructureOperationAsync(
        string title,
        string suggestedFileName,
        Func<string, Task<Result<string>>> executeOperation,
        string successStatus,
        string failureTitle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);
        ArgumentNullException.ThrowIfNull(executeOperation);
        ArgumentException.ThrowIfNullOrWhiteSpace(successStatus);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureTitle);

        var outputPath = await _filePickerService.PickSavePdfFileAsync(title, suggestedFileName);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            StatusText = "Save cancelled";
            return;
        }

        try
        {
            SetPdfStructureOperationState(true);

            var result = await executeOperation(outputPath);
            if (result.IsFailure)
            {
                EnqueueError(result.Error?.Message ?? "The PDF update failed.", failureTitle);
                StatusText = failureTitle;
                return;
            }

            await OpenDocumentFromPathAsync(result.Value ?? outputPath);
            StatusText = successStatus;
        }
        finally
        {
            SetPdfStructureOperationState(false);
        }
    }

    private async Task<Result<string>> PersistPendingPdfRotationsAsync(
        string sourcePath,
        string outputPath,
        IReadOnlyList<PendingPdfRotationGroup> pendingRotations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(pendingRotations);

        if (pendingRotations.Count == 0)
        {
            return ResultFactory.Success(outputPath);
        }

        var intermediateDirectory = Path.Combine(
            Path.GetTempPath(),
            "velune-pdf-rotations",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(intermediateDirectory);

        var currentInputPath = sourcePath;

        try
        {
            for (var i = 0; i < pendingRotations.Count; i++)
            {
                var group = pendingRotations[i];
                var isLastGroup = i == pendingRotations.Count - 1;
                var currentOutputPath = isLastGroup
                    ? outputPath
                    : Path.Combine(intermediateDirectory, $"rotation-step-{i + 1}.pdf");

                var result = await _rotatePdfPagesUseCase.ExecuteAsync(
                    new RotatePdfPagesRequest(
                        currentInputPath,
                        currentOutputPath,
                        group.Pages,
                        group.Rotation));

                if (result.IsFailure)
                {
                    return result;
                }

                currentInputPath = result.Value ?? currentOutputPath;
            }

            return ResultFactory.Success(outputPath);
        }
        finally
        {
            try
            {
                if (Directory.Exists(intermediateDirectory))
                {
                    Directory.Delete(intermediateDirectory, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup for temporary rotation files.
            }
        }
    }

    private void SetPdfStructureOperationState(bool isBusy)
    {
        if (_isApplyingPdfStructureOperation == isBusy)
        {
            return;
        }

        _isApplyingPdfStructureOperation = isBusy;
        OnPropertyChanged(nameof(IsPdfStructureOperationInProgress));
        PersistCurrentPageRotationCommand.NotifyCanExecuteChanged();
        ExtractCurrentPageCommand.NotifyCanExecuteChanged();
        DeleteCurrentPageCommand.NotifyCanExecuteChanged();
        SaveReorderedPdfCommand.NotifyCanExecuteChanged();
    }

    private string BuildSuggestedPdfFileName()
    {
        var fileName = Path.GetFileName(CurrentDocumentName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "document.pdf";
        }

        return Path.HasExtension(fileName)
            ? fileName
            : $"{fileName}.pdf";
    }

    private async Task PersistPreferencesFromUiAsync(bool applyDefaultZoomToOpenDocument)
    {
        if (_isApplyingPreferencesState)
        {
            return;
        }

        try
        {
            await _userPreferencesService.SaveAsync(BuildUserPreferencesFromUi());

            if (applyDefaultZoomToOpenDocument && HasOpenDocument)
            {
                await ApplyPreferredDefaultZoomAsync(preserveStatusText: true);
            }

            StatusText = "Preferences updated";
        }
        catch
        {
            ApplyPreferencesToUi(_userPreferencesService.Current);
            EnqueueError("Unable to save your preferences right now.", "Preferences not saved");
            StatusText = "Preferences not saved";
        }
    }

    private UserPreferences BuildUserPreferencesFromUi()
    {
        return new UserPreferences
        {
            Theme = ParseThemePreference(SelectedThemePreference),
            DefaultZoom = ParseDefaultZoomPreference(SelectedDefaultZoomPreference),
            ShowThumbnailsPanel = ShowThumbnailsPanelPreference,
            MemoryCacheEntryLimit = SelectedMemoryCacheEntryLimit
        };
    }

    private void ApplyPreferencesToUi(UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        EnsureMemoryCacheOptionExists(preferences.MemoryCacheEntryLimit);

        _isApplyingPreferencesState = true;
        try
        {
            SelectedThemePreference = ToThemePreferenceLabel(preferences.Theme);
            SelectedDefaultZoomPreference = ToDefaultZoomPreferenceLabel(preferences.DefaultZoom);
            ShowThumbnailsPanelPreference = preferences.ShowThumbnailsPanel;
            SelectedMemoryCacheEntryLimit = preferences.MemoryCacheEntryLimit;
        }
        finally
        {
            _isApplyingPreferencesState = false;
        }

        NotifySidebarVisibilityChanged();
    }

    private void EnsureMemoryCacheOptionExists(int entryLimit)
    {
        if (MemoryCacheEntryLimitOptions.Contains(entryLimit))
        {
            return;
        }

        var insertionIndex = 0;
        while (insertionIndex < MemoryCacheEntryLimitOptions.Count &&
               MemoryCacheEntryLimitOptions[insertionIndex] < entryLimit)
        {
            insertionIndex++;
        }

        MemoryCacheEntryLimitOptions.Insert(insertionIndex, entryLimit);
    }

    private static AppThemePreference ParseThemePreference(string? value)
    {
        return value switch
        {
            ThemeLightLabel => AppThemePreference.Light,
            ThemeDarkLabel => AppThemePreference.Dark,
            _ => AppThemePreference.System
        };
    }

    private static string ToThemePreferenceLabel(AppThemePreference value)
    {
        return value switch
        {
            AppThemePreference.Light => ThemeLightLabel,
            AppThemePreference.Dark => ThemeDarkLabel,
            _ => ThemeSystemLabel
        };
    }

    private static DefaultZoomPreference ParseDefaultZoomPreference(string? value)
    {
        return value switch
        {
            DefaultZoomFitToWidthLabel => DefaultZoomPreference.FitToWidth,
            DefaultZoomActualSizeLabel => DefaultZoomPreference.ActualSize,
            _ => DefaultZoomPreference.FitToPage
        };
    }

    private static string ToDefaultZoomPreferenceLabel(DefaultZoomPreference value)
    {
        return value switch
        {
            DefaultZoomPreference.FitToWidth => DefaultZoomFitToWidthLabel,
            DefaultZoomPreference.ActualSize => DefaultZoomActualSizeLabel,
            _ => DefaultZoomFitToPageLabel
        };
    }

    private sealed record PendingPdfRotationGroup(
        Rotation Rotation,
        IReadOnlyList<int> Pages);

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

    private void NotifySidebarVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsSidebarVisible));
        OnPropertyChanged(nameof(SidebarColumnWidth));
        OnPropertyChanged(nameof(SidebarWidth));
    }

    private void RefreshDocumentInfo(DocumentMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        DocumentInfoItems.Clear();

        AddDocumentInfo("Kind", metadata.FormatLabel ?? GetDocumentKindLabel(metadata.DocumentType));
        AddDocumentInfo("Size", FormatFileSize(metadata.FileSizeInBytes));

        if (metadata.PageCount is > 0)
        {
            AddDocumentInfo("Pages", metadata.PageCount.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (metadata.PixelWidth is > 0 && metadata.PixelHeight is > 0)
        {
            AddDocumentInfo("Dimensions", $"{metadata.PixelWidth} × {metadata.PixelHeight} px");
        }

        if (metadata.CreatedAt is not null)
        {
            AddDocumentInfo("Created", FormatDate(metadata.CreatedAt.Value));
        }

        if (metadata.ModifiedAt is not null)
        {
            AddDocumentInfo("Modified", FormatDate(metadata.ModifiedAt.Value));
        }

        AddDocumentInfo("Title", metadata.DocumentTitle);
        AddDocumentInfo("Author", metadata.Author);
        AddDocumentInfo("Creator", metadata.Creator);
        AddDocumentInfo("Producer", metadata.Producer);
        AddDocumentInfo("Location", Path.GetDirectoryName(metadata.FilePath));

        DocumentInfoWarning = metadata.DetailsWarning;
        OnPropertyChanged(nameof(HasDocumentInfo));
    }

    private void ClearDocumentInfo()
    {
        DocumentInfoItems.Clear();
        DocumentInfoWarning = null;
        OnPropertyChanged(nameof(HasDocumentInfo));
    }

    private void AddDocumentInfo(string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        DocumentInfoItems.Add(new DocumentInfoItem(label, value));
    }

    private static string GetDocumentKindLabel(DocumentType documentType)
    {
        return documentType switch
        {
            DocumentType.Pdf => "PDF document",
            DocumentType.Image => "Image",
            _ => "Document"
        };
    }

    private static string FormatDate(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("f", CultureInfo.CurrentCulture);
    }

    private static string FormatFileSize(long fileSizeInBytes)
    {
        string[] units = ["bytes", "KB", "MB", "GB", "TB"];
        var size = (double)fileSizeInBytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        var format = unitIndex == 0 ? "0" : "0.#";
        return $"{size.ToString(format, CultureInfo.InvariantCulture)} {units[unitIndex]}";
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
