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
using Velune.Application.Text;
using Velune.Application.UseCases;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Presentation.FileSystem;
using Velune.Presentation.Imaging;
using Velune.Presentation.Search;

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
    private const double InlineHeaderSearchMinWidth = 1480;
    private const double HeaderTitleCompactThreshold = 1420;
    private const double HeaderTitleTightThreshold = 1220;
    private const string ThemeSystemLabel = "System";
    private const string ThemeLightLabel = "Light";
    private const string ThemeDarkLabel = "Dark";
    private const string DefaultZoomFitToPageLabel = "Fit to page";
    private const string DefaultZoomFitToWidthLabel = "Fit to width";
    private const string DefaultZoomActualSizeLabel = "100%";
    private const string PrintRangeAllPagesLabel = "All pages";
    private const string PrintRangeCurrentPageLabel = "Current page";
    private const string PrintRangeCustomLabel = "Custom range";
    private const string PrintOrientationAutomaticLabel = "Automatic";
    private const string PrintOrientationPortraitLabel = "Portrait";
    private const string PrintOrientationLandscapeLabel = "Landscape";

    private readonly IFilePickerService _filePickerService;
    private readonly IPrintService _printService;
    private readonly OpenDocumentUseCase _openDocumentUseCase;
    private readonly CloseDocumentUseCase _closeDocumentUseCase;
    private readonly PrintDocumentUseCase _printDocumentUseCase;
    private readonly ShowSystemPrintDialogUseCase _showSystemPrintDialogUseCase;
    private readonly LoadDocumentTextUseCase _loadDocumentTextUseCase;
    private readonly RunDocumentOcrUseCase _runDocumentOcrUseCase;
    private readonly CancelDocumentTextAnalysisUseCase _cancelDocumentTextAnalysisUseCase;
    private readonly SearchDocumentTextUseCase _searchDocumentTextUseCase;
    private readonly ResolveDocumentTextSelectionUseCase _resolveDocumentTextSelectionUseCase;
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
    private readonly IReadOnlyList<string> _printPageRangeOptions = [PrintRangeAllPagesLabel, PrintRangeCurrentPageLabel, PrintRangeCustomLabel];
    private readonly IReadOnlyList<string> _printOrientationOptions = [PrintOrientationAutomaticLabel, PrintOrientationPortraitLabel, PrintOrientationLandscapeLabel];
    private bool _disposed;
    private double _documentViewportWidth;
    private double _documentViewportHeight;
    private bool _isImageAutoFitEnabled;
    private bool _isApplyingPdfStructureOperation;
    private bool _isPrintingDocument;
    private bool _isLoadingPrintDestinations;
    private bool _isAnalyzingDocumentText;
    private bool _isApplyingPreferencesState;
    private bool _requiresSearchOcr;
    private NotificationKind _currentNotificationKind = NotificationKind.Info;
    private bool _isCurrentNotificationDismissible;
    private int _thumbnailGenerationVersion;
    private int _selectedSearchResultIndex = -1;
    private double _windowWidth = 1280;
    private Action? _notificationPrimaryAction;
    private Action? _notificationSecondaryAction;
    private DocumentTextIndex? _currentDocumentTextIndex;
    private DocumentTextSelectionResult? _currentDocumentTextSelection;
    private RenderJobHandle? _currentRenderJob;
    private DocumentTextJobHandle? _currentDocumentTextJob;
    private DocumentTextSelectionPoint? _documentTextSelectionAnchorPoint;

    public MainWindowViewModel(
        IFilePickerService filePickerService,
        IPrintService printService,
        OpenDocumentUseCase openDocumentUseCase,
        CloseDocumentUseCase closeDocumentUseCase,
        PrintDocumentUseCase printDocumentUseCase,
        ShowSystemPrintDialogUseCase showSystemPrintDialogUseCase,
        LoadDocumentTextUseCase loadDocumentTextUseCase,
        RunDocumentOcrUseCase runDocumentOcrUseCase,
        CancelDocumentTextAnalysisUseCase cancelDocumentTextAnalysisUseCase,
        SearchDocumentTextUseCase searchDocumentTextUseCase,
        ResolveDocumentTextSelectionUseCase resolveDocumentTextSelectionUseCase,
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
        ArgumentNullException.ThrowIfNull(printService);
        ArgumentNullException.ThrowIfNull(openDocumentUseCase);
        ArgumentNullException.ThrowIfNull(closeDocumentUseCase);
        ArgumentNullException.ThrowIfNull(printDocumentUseCase);
        ArgumentNullException.ThrowIfNull(showSystemPrintDialogUseCase);
        ArgumentNullException.ThrowIfNull(loadDocumentTextUseCase);
        ArgumentNullException.ThrowIfNull(runDocumentOcrUseCase);
        ArgumentNullException.ThrowIfNull(cancelDocumentTextAnalysisUseCase);
        ArgumentNullException.ThrowIfNull(searchDocumentTextUseCase);
        ArgumentNullException.ThrowIfNull(resolveDocumentTextSelectionUseCase);
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
        _printService = printService;
        _openDocumentUseCase = openDocumentUseCase;
        _closeDocumentUseCase = closeDocumentUseCase;
        _printDocumentUseCase = printDocumentUseCase;
        _showSystemPrintDialogUseCase = showSystemPrintDialogUseCase;
        _loadDocumentTextUseCase = loadDocumentTextUseCase;
        _runDocumentOcrUseCase = runDocumentOcrUseCase;
        _cancelDocumentTextAnalysisUseCase = cancelDocumentTextAnalysisUseCase;
        _searchDocumentTextUseCase = searchDocumentTextUseCase;
        _resolveDocumentTextSelectionUseCase = resolveDocumentTextSelectionUseCase;
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
        PrintDestinations = [];
        SearchResults = [];
        SearchHighlights = [];
        TextSelectionHighlights = [];
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
    private string _editableDocumentName = string.Empty;

    [ObservableProperty]
    private bool _isEditingDocumentName;

    [ObservableProperty]
    private bool _isInfoPanelVisible;

    [ObservableProperty]
    private bool _isSearchPanelVisible;

    [ObservableProperty]
    private bool _isPreferencesPanelVisible;

    [ObservableProperty]
    private bool _isPrintPanelVisible;

    [ObservableProperty]
    private string? _documentInfoWarning;

    [ObservableProperty]
    private string? _searchPanelNotice;

    [ObservableProperty]
    private string? _printPanelNotice;

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
    private string _searchQueryInput = string.Empty;

    [ObservableProperty]
    private string? _selectedDocumentText;

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

    [ObservableProperty]
    private PrintDestinationInfo? _selectedPrintDestination;

    [ObservableProperty]
    private string _selectedPrintPageRangeOption = PrintRangeAllPagesLabel;

    [ObservableProperty]
    private string _printCustomPageRange = string.Empty;

    [ObservableProperty]
    private string _printCopiesInput = "1";

    [ObservableProperty]
    private string _selectedPrintOrientationOption = PrintOrientationAutomaticLabel;

    [ObservableProperty]
    private bool _printFitToPage = true;

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
    public ObservableCollection<PrintDestinationInfo> PrintDestinations
    {
        get;
    }
    public ObservableCollection<SearchResultItemViewModel> SearchResults
    {
        get;
    }
    public ObservableCollection<SearchHighlightItem> SearchHighlights
    {
        get;
    }
    public ObservableCollection<SearchHighlightItem> TextSelectionHighlights
    {
        get;
    }
    public ObservableCollection<int> MemoryCacheEntryLimitOptions
    {
        get;
    }
    public IReadOnlyList<string> ThemePreferenceOptions => _themePreferenceOptions;
    public IReadOnlyList<string> DefaultZoomPreferenceOptions => _defaultZoomPreferenceOptions;
    public IReadOnlyList<string> PrintPageRangeOptions => _printPageRangeOptions;
    public IReadOnlyList<string> PrintOrientationOptions => _printOrientationOptions;

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
    public bool HasSearchResults => SearchResults.Count > 0;
    public bool HasSearchHighlights => SearchHighlights.Count > 0;
    public bool HasTextSelectionHighlights => TextSelectionHighlights.Count > 0;
    public bool HasSelectedDocumentText => !string.IsNullOrWhiteSpace(SelectedDocumentText);
    public string HeaderTitle => HasOpenDocument
        ? (string.IsNullOrWhiteSpace(EditableDocumentName)
            ? (CurrentDocumentName ?? ApplicationTitle)
            : EditableDocumentName)
        : ApplicationTitle;
    public string HeaderSubtitle => HasOpenDocument
        ? (CurrentDocumentType ?? "Document")
        : "PDF and image viewer";
    public bool ShouldEmphasizeOpenAction => !HasOpenDocument;
    public bool IsPdfDocument => HasOpenDocument && !IsCurrentImageDocument;
    public bool IsImageAutoFitActive => HasOpenDocument && IsCurrentImageDocument && _isImageAutoFitEnabled;
    public bool IsScrollableViewerVisible => !IsImageAutoFitActive;
    public bool ShowWindowMenuBar => !OperatingSystem.IsMacOS();
    public double HeaderTitleMaxWidth => WindowWidth >= InlineHeaderSearchMinWidth
        ? 320
        : WindowWidth >= HeaderTitleCompactThreshold
            ? 240
            : WindowWidth >= HeaderTitleTightThreshold
                ? 190
                : 150;
    public bool IsSidebarVisible => HasOpenDocument && !IsCurrentImageDocument && ShowThumbnailsPanelPreference;
    public bool CanToggleSidebar => HasOpenDocument && !IsCurrentImageDocument;
    public string SidebarToggleLabel => IsSidebarVisible ? "Hide pages" : "Show pages";
    public bool ShowEditableHeaderTitle => HasOpenDocument && !IsEditingDocumentName;
    public bool ShowHeaderTitleEditor => HasOpenDocument && IsEditingDocumentName;
    public bool UseInlineHeaderSearch => HasOpenDocument && WindowWidth >= InlineHeaderSearchMinWidth;
    public bool UseCollapsedHeaderSearchButton => HasOpenDocument && !UseInlineHeaderSearch;
    public bool IsPageNavigationVisible => !HasOpenDocument || !IsCurrentImageDocument;
    public bool IsPdfStructureActionsVisible => IsPdfDocument;
    public bool IsSidebarActionStripVisible => IsPdfDocument && (CanPersistCurrentPageRotation || HasPendingPageReorder);
    public bool IsInfoPanelOpen => HasOpenDocument && IsInfoPanelVisible;
    public bool IsSearchPanelOpen => HasOpenDocument && IsSearchPanelVisible;
    public bool IsPreferencesPanelOpen => IsPreferencesPanelVisible;
    public bool IsPrintPanelOpen => HasOpenDocument && IsPrintPanelVisible;
    public bool HasDocumentInfo => DocumentInfoItems.Count > 0;
    public bool HasDocumentInfoWarning => !string.IsNullOrWhiteSpace(DocumentInfoWarning);
    public bool HasSearchPanelNotice => !string.IsNullOrWhiteSpace(SearchPanelNotice);
    public bool HasPrintPanelNotice => !string.IsNullOrWhiteSpace(PrintPanelNotice);
    public bool IsPrintPageRangeVisible => IsPdfDocument;
    public bool IsCustomPrintRangeVisible => IsPrintPageRangeVisible && SelectedPrintPageRangeOption == PrintRangeCustomLabel;
    public GridLength SidebarColumnWidth => new(SidebarWidth);
    public double SidebarWidth => IsSidebarVisible ? SidebarExpandedWidth : 0;
    public double InfoPanelWidth => IsInfoPanelOpen ? InfoPanelExpandedWidth : 0;
    public double SearchPanelWidth => IsSearchPanelOpen ? InfoPanelExpandedWidth : 0;
    public double PreferencesPanelWidth => IsPreferencesPanelOpen ? InfoPanelExpandedWidth : 0;
    public double PrintPanelWidth => IsPrintPanelOpen ? InfoPanelExpandedWidth : 0;
    public double WindowWidth => _windowWidth;
    public string PageIndicator => TotalPages > 0 ? $"{CurrentPage} / {TotalPages}" : "-";
    public string SearchResultSummary => SearchResults.Count switch
    {
        0 when _requiresSearchOcr => "OCR required",
        0 when _isAnalyzingDocumentText => "Loading searchable text…",
        0 => "No results",
        1 => "1 result",
        _ => $"{SearchResults.Count} results"
    };
    public string SearchSelectionIndicator => _selectedSearchResultIndex < 0 || SearchResults.Count == 0
        ? "0 / 0"
        : $"{_selectedSearchResultIndex + 1} / {SearchResults.Count}";
    public bool CanSearchText => HasOpenDocument && !_isAnalyzingDocumentText && _currentDocumentTextIndex is not null && !string.IsNullOrWhiteSpace(SearchQueryInput);
    public bool CanRunDocumentOcr => HasOpenDocument && !_isAnalyzingDocumentText && _requiresSearchOcr;
    public bool CanCancelDocumentTextAnalysis => HasOpenDocument && _isAnalyzingDocumentText && _currentDocumentTextJob is not null;
    public bool CanNavigateSearchResults => SearchResults.Count > 1;
    public double DisplayedRenderedPageWidth => CurrentRenderedBitmap?.PixelSize.Width ?? 0;
    public double DisplayedRenderedPageHeight => CurrentRenderedBitmap?.PixelSize.Height ?? 0;
    public bool IsPdfStructureOperationInProgress => _isApplyingPdfStructureOperation;

    public bool CanGoPreviousPage => HasOpenDocument && CurrentPage > 1;
    public bool CanGoNextPage => HasOpenDocument && TotalPages > 0 && CurrentPage < TotalPages;
    public bool CanGoToPage => HasOpenDocument && TotalPages > 0;
    public bool CanSubmitPrintJob => HasOpenDocument && !_isPrintingDocument && !_isLoadingPrintDestinations;
    public bool CanUseFitCommands =>
        HasOpenDocument &&
        _documentViewportWidth > 0 &&
        _documentViewportHeight > 0 &&
        (HasRenderedPage || IsCurrentImageDocument);
    public bool ShouldUseTrackpadForPan =>
        HasOpenDocument &&
        (IsCurrentImageDocument ||
         GetCurrentZoomFactor() > 1.0);
    public bool CanPersistCurrentPageRotation =>
        IsPdfDocument &&
        !IsPdfStructureOperationInProgress &&
        GetPendingPdfRotationGroups().Count > 0;
    public bool CanSaveDocument =>
        IsPdfDocument &&
        !IsPdfStructureOperationInProgress &&
        !string.IsNullOrWhiteSpace(CurrentDocumentPath) &&
        (CanPersistCurrentPageRotation || HasPendingPageReorder || HasPendingRequestedSaveNameChange());
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
        OnPropertyChanged(nameof(HeaderSubtitle));
        OnPropertyChanged(nameof(ShouldEmphasizeOpenAction));
        OnPropertyChanged(nameof(CanGoToPage));
        OnPropertyChanged(nameof(IsPdfDocument));
        NotifyViewerModeChanged();
        NotifySidebarVisibilityChanged();
        OnPropertyChanged(nameof(CanToggleSidebar));
        OnPropertyChanged(nameof(SidebarToggleLabel));
        OnPropertyChanged(nameof(ShowEditableHeaderTitle));
        OnPropertyChanged(nameof(ShowHeaderTitleEditor));
        OnPropertyChanged(nameof(UseInlineHeaderSearch));
        OnPropertyChanged(nameof(UseCollapsedHeaderSearchButton));
        OnPropertyChanged(nameof(IsPageNavigationVisible));
        OnPropertyChanged(nameof(IsPdfStructureActionsVisible));
        OnPropertyChanged(nameof(CanPersistCurrentPageRotation));
        OnPropertyChanged(nameof(CanSaveDocument));
        OnPropertyChanged(nameof(IsSidebarActionStripVisible));
        OnPropertyChanged(nameof(IsInfoPanelOpen));
        OnPropertyChanged(nameof(InfoPanelWidth));
        OnPropertyChanged(nameof(IsSearchPanelOpen));
        OnPropertyChanged(nameof(SearchPanelWidth));
        OnPropertyChanged(nameof(IsPreferencesPanelOpen));
        OnPropertyChanged(nameof(PreferencesPanelWidth));
        OnPropertyChanged(nameof(IsPrintPanelOpen));
        OnPropertyChanged(nameof(PrintPanelWidth));
        OnPropertyChanged(nameof(IsPrintPageRangeVisible));
        OnPropertyChanged(nameof(IsCustomPrintRangeVisible));
        PrintDocumentCommand.NotifyCanExecuteChanged();
        SubmitPrintJobCommand.NotifyCanExecuteChanged();
        RefreshPrintDestinationsCommand.NotifyCanExecuteChanged();
        CloseCommand.NotifyCanExecuteChanged();
        ToggleInfoPanelCommand.NotifyCanExecuteChanged();
        ToggleSearchPanelCommand.NotifyCanExecuteChanged();
        OpenSearchCommand.NotifyCanExecuteChanged();
        TogglePreferencesPanelCommand.NotifyCanExecuteChanged();
        ToggleSidebarCommand.NotifyCanExecuteChanged();
        NotifySearchStateChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        GoToPageCommand.NotifyCanExecuteChanged();
        ZoomInCommand.NotifyCanExecuteChanged();
        ZoomOutCommand.NotifyCanExecuteChanged();
        FitToWidthCommand.NotifyCanExecuteChanged();
        FitToPageCommand.NotifyCanExecuteChanged();
        RotateLeftCommand.NotifyCanExecuteChanged();
        RotateRightCommand.NotifyCanExecuteChanged();
        SaveDocumentCommand.NotifyCanExecuteChanged();
        BeginDocumentNameEditCommand.NotifyCanExecuteChanged();
        CommitDocumentNameEditCommand.NotifyCanExecuteChanged();
        CancelDocumentNameEditCommand.NotifyCanExecuteChanged();
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
        OnPropertyChanged(nameof(CanToggleSidebar));
        OnPropertyChanged(nameof(SidebarToggleLabel));
        OnPropertyChanged(nameof(UseInlineHeaderSearch));
        OnPropertyChanged(nameof(UseCollapsedHeaderSearchButton));
        OnPropertyChanged(nameof(IsPageNavigationVisible));
        OnPropertyChanged(nameof(IsPdfStructureActionsVisible));
        OnPropertyChanged(nameof(CanPersistCurrentPageRotation));
        OnPropertyChanged(nameof(CanSaveDocument));
        OnPropertyChanged(nameof(IsSidebarActionStripVisible));
        OnPropertyChanged(nameof(IsPrintPageRangeVisible));
        OnPropertyChanged(nameof(IsCustomPrintRangeVisible));
        SaveDocumentCommand.NotifyCanExecuteChanged();
        PersistCurrentPageRotationCommand.NotifyCanExecuteChanged();
        ExtractCurrentPageCommand.NotifyCanExecuteChanged();
        DeleteCurrentPageCommand.NotifyCanExecuteChanged();
        SaveReorderedPdfCommand.NotifyCanExecuteChanged();
        ToggleSidebarCommand.NotifyCanExecuteChanged();
        MoveCurrentPageEarlierCommand.NotifyCanExecuteChanged();
        MoveCurrentPageLaterCommand.NotifyCanExecuteChanged();
    }

    partial void OnCurrentDocumentPathChanged(string? value)
    {
        OnPropertyChanged(nameof(CanSaveDocument));
        SaveDocumentCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsInfoPanelVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsInfoPanelOpen));
        OnPropertyChanged(nameof(InfoPanelWidth));
    }

    partial void OnIsSearchPanelVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsSearchPanelOpen));
        OnPropertyChanged(nameof(SearchPanelWidth));

        if (!value)
        {
            ClearSearchHighlights();
        }
        else
        {
            RefreshSearchHighlights();
        }
    }

    partial void OnIsPreferencesPanelVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPreferencesPanelOpen));
        OnPropertyChanged(nameof(PreferencesPanelWidth));
    }

    partial void OnIsPrintPanelVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPrintPanelOpen));
        OnPropertyChanged(nameof(PrintPanelWidth));
    }

    partial void OnDocumentInfoWarningChanged(string? value)
    {
        OnPropertyChanged(nameof(HasDocumentInfoWarning));
    }

    partial void OnPrintPanelNoticeChanged(string? value)
    {
        OnPropertyChanged(nameof(HasPrintPanelNotice));
    }

    partial void OnSearchPanelNoticeChanged(string? value)
    {
        OnPropertyChanged(nameof(HasSearchPanelNotice));
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
        OnPropertyChanged(nameof(DisplayedRenderedPageWidth));
        OnPropertyChanged(nameof(DisplayedRenderedPageHeight));
        FitToWidthCommand.NotifyCanExecuteChanged();
        FitToPageCommand.NotifyCanExecuteChanged();
        RefreshDocumentTextSelectionHighlights();
        RefreshSearchHighlights();
    }

    partial void OnCurrentDocumentNameChanged(string? value)
    {
        OnPropertyChanged(nameof(HeaderTitle));
    }

    partial void OnEditableDocumentNameChanged(string value)
    {
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(CanSaveDocument));
        SaveDocumentCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsEditingDocumentNameChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowEditableHeaderTitle));
        OnPropertyChanged(nameof(ShowHeaderTitleEditor));
    }

    partial void OnCurrentDocumentTypeChanged(string? value)
    {
        OnPropertyChanged(nameof(HeaderSubtitle));
    }

    partial void OnCurrentPageChanged(int value)
    {
        OnPropertyChanged(nameof(PageIndicator));
        OnPropertyChanged(nameof(CanPersistCurrentPageRotation));
        OnPropertyChanged(nameof(IsSidebarActionStripVisible));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        UpdateSelectedThumbnail();
        GoToPageInput = value.ToString();
        ClearDocumentTextSelection();
        PersistCurrentPageRotationCommand.NotifyCanExecuteChanged();
        ExtractCurrentPageCommand.NotifyCanExecuteChanged();
        DeleteCurrentPageCommand.NotifyCanExecuteChanged();
        MoveCurrentPageEarlierCommand.NotifyCanExecuteChanged();
        MoveCurrentPageLaterCommand.NotifyCanExecuteChanged();
        RefreshSearchHighlights();
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
        OnPropertyChanged(nameof(CanSaveDocument));
        OnPropertyChanged(nameof(IsSidebarActionStripVisible));
        SaveDocumentCommand.NotifyCanExecuteChanged();
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
        OnPropertyChanged(nameof(SidebarToggleLabel));

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

    partial void OnSelectedPrintPageRangeOptionChanged(string value)
    {
        OnPropertyChanged(nameof(IsCustomPrintRangeVisible));
    }

    partial void OnPrintCustomPageRangeChanged(string value)
    {
        SubmitPrintJobCommand.NotifyCanExecuteChanged();
    }

    partial void OnSearchQueryInputChanged(string value)
    {
        SearchTextCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedDocumentTextChanged(string? value)
    {
        OnPropertyChanged(nameof(HasSelectedDocumentText));
    }

    partial void OnPrintCopiesInputChanged(string value)
    {
        SubmitPrintJobCommand.NotifyCanExecuteChanged();
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
    private async Task CloseAsync()
    {
        await CloseCurrentDocumentStateAsync(clearNotifications: true);
        StatusText = "Document closed";
    }

    [RelayCommand(CanExecute = nameof(HasOpenDocument))]
    private async Task PrintDocumentAsync()
    {
        if (_printService.SupportsSystemPrintDialog)
        {
            await ShowSystemPrintDialogAsync();
            return;
        }

        if (IsPrintPanelVisible)
        {
            IsPrintPanelVisible = false;
            StatusText = "Print panel hidden";
            return;
        }

        IsInfoPanelVisible = false;
        IsSearchPanelVisible = false;
        IsPreferencesPanelVisible = false;
        IsPrintPanelVisible = true;
        StatusText = "Loading printers";

        await LoadPrintDestinationsAsync();

        StatusText = "Print panel shown";
    }

    private async Task ShowSystemPrintDialogAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentDocumentPath))
        {
            EnqueueWarning("No document to print", "Open a PDF or an image before printing.");
            StatusText = "Print unavailable";
            return;
        }

        IsInfoPanelVisible = false;
        IsSearchPanelVisible = false;
        IsPreferencesPanelVisible = false;
        IsPrintPanelVisible = false;
        StatusText = "Opening system print dialog";

        var result = await _showSystemPrintDialogUseCase.ExecuteAsync(CurrentDocumentPath);
        if (result.IsSuccess)
        {
            StatusText = "System print dialog shown";
            return;
        }

        if (string.Equals(result.Error?.Code, "print.cancelled", StringComparison.Ordinal))
        {
            StatusText = "Print cancelled";
            return;
        }

        EnqueueError(result.Error?.Message ?? "The system print dialog could not be opened.", "Print failed");
        StatusText = "Print failed";
    }

    [RelayCommand(CanExecute = nameof(HasOpenDocument))]
    private async Task RefreshPrintDestinationsAsync()
    {
        if (!HasOpenDocument)
        {
            return;
        }

        await LoadPrintDestinationsAsync();
        StatusText = "Printers refreshed";
    }

    [RelayCommand(CanExecute = nameof(CanSubmitPrintJob))]
    private async Task SubmitPrintJobAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentDocumentPath))
        {
            EnqueueWarning("No document to print", "Open a PDF or an image before printing.");
            StatusText = "Print unavailable";
            return;
        }

        if (!int.TryParse(PrintCopiesInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out var copies) ||
            copies <= 0)
        {
            EnqueueWarning("Invalid copy count", "Enter a valid number of copies.");
            StatusText = "Invalid print settings";
            return;
        }

        var pageRanges = BuildRequestedPrintPageRanges();
        if (pageRanges is null && IsCustomPrintRangeVisible)
        {
            EnqueueWarning("Invalid page range", "Enter a page range like 1-3,5.");
            StatusText = "Invalid print settings";
            return;
        }

        try
        {
            _isPrintingDocument = true;
            SubmitPrintJobCommand.NotifyCanExecuteChanged();
            PrintDocumentCommand.NotifyCanExecuteChanged();

            var result = await _printDocumentUseCase.ExecuteAsync(
                new PrintDocumentRequest(
                    CurrentDocumentPath,
                    SelectedPrintDestination?.Name,
                    copies,
                    pageRanges,
                    ParsePrintOrientationOption(SelectedPrintOrientationOption),
                    PrintFitToPage));

            if (result.IsFailure)
            {
                EnqueueError(result.Error?.Message ?? "The document could not be printed.", "Print failed");
                PrintPanelNotice = result.Error?.Message ?? "The document could not be printed.";
                StatusText = "Print failed";
                return;
            }

            PrintPanelNotice = null;
            IsPrintPanelVisible = false;
            EnqueueInfo("Print started", "The document was sent to the system print service.");
            StatusText = "Print started";
        }
        finally
        {
            _isPrintingDocument = false;
            SubmitPrintJobCommand.NotifyCanExecuteChanged();
            PrintDocumentCommand.NotifyCanExecuteChanged();
        }
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

        var nextZoom = Math.Min(MaxZoom, GetCurrentZoomFactor() + ZoomStep);

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

        var nextZoom = Math.Max(MinZoom, GetCurrentZoomFactor() - ZoomStep);

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
        await RotateActivePageAsync(rotation => rotation switch
        {
            Rotation.Deg0 => Rotation.Deg270,
            Rotation.Deg90 => Rotation.Deg0,
            Rotation.Deg180 => Rotation.Deg90,
            Rotation.Deg270 => Rotation.Deg180,
            _ => Rotation.Deg0
        });
    }

    [RelayCommand(CanExecute = nameof(HasOpenDocument))]
    private async Task RotateRightAsync()
    {
        await RotateActivePageAsync(rotation => rotation switch
        {
            Rotation.Deg0 => Rotation.Deg90,
            Rotation.Deg90 => Rotation.Deg180,
            Rotation.Deg180 => Rotation.Deg270,
            Rotation.Deg270 => Rotation.Deg0,
            _ => Rotation.Deg0
        });
    }

    private async Task RotateActivePageAsync(Func<Rotation, Rotation> rotationTransform)
    {
        ArgumentNullException.ThrowIfNull(rotationTransform);

        var activePageIndex = _pageViewportStore.ActivePageIndex;
        var nextRotation = rotationTransform(_pageViewportStore.GetRotation(activePageIndex));

        _pageViewportStore.SetRotation(activePageIndex, nextRotation);
        if (!TryUpdateRotation(nextRotation))
        {
            return;
        }

        RefreshPageViewState();

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

    [RelayCommand(CanExecute = nameof(CanSaveDocument))]
    private async Task SaveDocumentAsync()
    {
        var currentDocumentPath = CurrentDocumentPath;
        if (string.IsNullOrWhiteSpace(currentDocumentPath))
        {
            return;
        }

        var pendingRotations = GetPendingPdfRotationGroups();
        var orderedPages = Thumbnails
            .Select(thumbnail => thumbnail.SourcePageNumber)
            .ToArray();
        var hasPendingSaveNameChange = HasPendingRequestedSaveNameChange();

        if (pendingRotations.Count == 0 &&
            !HasPendingPageReorder &&
            !hasPendingSaveNameChange)
        {
            StatusText = "Nothing to save";
            return;
        }

        if (pendingRotations.Count == 0 && !HasPendingPageReorder)
        {
            await ExecutePdfStructureSaveInPlaceAsync(
                currentDocumentPath,
                _ => Task.FromResult(ResultFactory.Success(currentDocumentPath)),
                successStatus: "Document saved",
                failureTitle: "Unable to save document");
            return;
        }

        await ExecutePdfStructureSaveInPlaceAsync(
            currentDocumentPath,
            outputPath => SavePdfDocumentChangesAsync(
                currentDocumentPath,
                outputPath,
                pendingRotations,
                orderedPages,
                HasPendingPageReorder),
            successStatus: "Document saved",
            failureTitle: "Unable to save document");
    }

    [RelayCommand(CanExecute = nameof(CanExtractCurrentPage))]
    private async Task ExtractCurrentPageAsync()
    {
        await ExtractPageAsync(CurrentPage, CurrentPage.ToString(CultureInfo.InvariantCulture));
    }

    [RelayCommand(CanExecute = nameof(CanExtractCurrentPage))]
    private async Task ExtractThumbnailPageAsync(PageThumbnailItemViewModel? thumbnail)
    {
        if (thumbnail is null)
        {
            return;
        }

        await ExtractPageAsync(
            thumbnail.SourcePageNumber,
            thumbnail.DisplayPageNumber.ToString(CultureInfo.InvariantCulture));
    }

    [RelayCommand(CanExecute = nameof(CanDeleteCurrentPage))]
    private async Task DeleteCurrentPageAsync()
    {
        await DeletePageAsync(CurrentPage, CurrentPage.ToString(CultureInfo.InvariantCulture));
    }

    [RelayCommand(CanExecute = nameof(CanDeleteCurrentPage))]
    private async Task DeleteThumbnailPageAsync(PageThumbnailItemViewModel? thumbnail)
    {
        if (thumbnail is null)
        {
            return;
        }

        await DeletePageAsync(
            thumbnail.SourcePageNumber,
            thumbnail.DisplayPageNumber.ToString(CultureInfo.InvariantCulture));
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
    private void BeginDocumentNameEdit()
    {
        EditableDocumentName = ResolveRequestedDocumentFileName();
        IsEditingDocumentName = true;
        StatusText = "Editing save name";
    }

    [RelayCommand(CanExecute = nameof(HasOpenDocument))]
    private void CommitDocumentNameEdit()
    {
        if (!HasOpenDocument)
        {
            return;
        }

        var proposedName = EditableDocumentName.Trim();
        if (string.IsNullOrWhiteSpace(proposedName))
        {
            EditableDocumentName = CurrentDocumentName ?? string.Empty;
            IsEditingDocumentName = false;
            StatusText = "Save name unchanged";
            return;
        }

        if (!TryNormalizeRequestedDocumentFileName(proposedName, out var normalizedFileName, out var validationError))
        {
            EnqueueWarning("Invalid file name", validationError ?? "Enter a valid file name.");
            StatusText = "Invalid file name";
            return;
        }

        EditableDocumentName = normalizedFileName;
        IsEditingDocumentName = false;
        StatusText = "Save name updated";
    }

    [RelayCommand(CanExecute = nameof(HasOpenDocument))]
    private void CancelDocumentNameEdit()
    {
        EditableDocumentName = CurrentDocumentName ?? string.Empty;
        IsEditingDocumentName = false;
        StatusText = "Save name unchanged";
    }

    [RelayCommand(CanExecute = nameof(HasOpenDocument))]
    private async Task OpenSearchAsync()
    {
        IsInfoPanelVisible = false;
        IsPreferencesPanelVisible = false;
        IsPrintPanelVisible = false;
        IsSearchPanelVisible = true;

        if (string.IsNullOrWhiteSpace(SearchQueryInput))
        {
            StatusText = "Search shown";
            return;
        }

        if (_currentDocumentTextIndex is not null && !_isAnalyzingDocumentText)
        {
            await SearchTextAsync();
            return;
        }

        await EnsureDocumentTextAvailableAsync(forceOcr: false);

        if (!_isAnalyzingDocumentText &&
            _currentDocumentTextIndex is not null &&
            string.IsNullOrWhiteSpace(SearchQueryInput))
        {
            StatusText = "Search shown";
        }
    }

    [RelayCommand(CanExecute = nameof(CanToggleSidebar))]
    private void ToggleSidebar()
    {
        ShowThumbnailsPanelPreference = !ShowThumbnailsPanelPreference;
        StatusText = ShowThumbnailsPanelPreference
            ? "Pages panel shown"
            : "Pages panel hidden";
    }

    [RelayCommand(CanExecute = nameof(HasOpenDocument))]
    private async Task ToggleSearchPanelAsync()
    {
        if (IsSearchPanelVisible)
        {
            IsSearchPanelVisible = false;
            StatusText = "Search hidden";
            return;
        }

        IsInfoPanelVisible = false;
        IsPreferencesPanelVisible = false;
        IsPrintPanelVisible = false;
        IsSearchPanelVisible = true;
        StatusText = "Loading searchable text";

        await EnsureDocumentTextAvailableAsync(forceOcr: false);

        if (IsSearchPanelVisible &&
            !_requiresSearchOcr &&
            !_isAnalyzingDocumentText &&
            _currentDocumentTextIndex is not null)
        {
            StatusText = "Search shown";
        }
    }

    [RelayCommand(CanExecute = nameof(CanSearchText))]
    private async Task SearchTextAsync()
    {
        if (_currentDocumentTextIndex is null)
        {
            if (_requiresSearchOcr)
            {
                EnqueueInfo("Recognize text first", "Run OCR to search inside this document.");
                SearchPanelNotice = "This document has no searchable text yet. Recognize text to enable search.";
            }

            return;
        }

        var result = _searchDocumentTextUseCase.Execute(
            new SearchDocumentTextRequest(
                _currentDocumentTextIndex,
                new SearchQuery(SearchQueryInput)));

        if (result.IsFailure)
        {
            EnqueueWarning("Search unavailable", result.Error?.Message ?? "Search text is invalid.");
            return;
        }

        ApplySearchResults(result.Value ?? []);
        if (SearchResults.Count == 0)
        {
            SearchPanelNotice = $"No result for “{SearchQueryInput.Trim()}”.";
            ClearSearchHighlights();
            StatusText = "No search results";
            return;
        }

        SearchPanelNotice = null;
        await SelectSearchResultAsync(SearchResults[0], updateStatus: true);
    }

    [RelayCommand(CanExecute = nameof(CanRunDocumentOcr))]
    private async Task RunSearchOcrAsync()
    {
        await EnsureDocumentTextAvailableAsync(forceOcr: true);
    }

    [RelayCommand(CanExecute = nameof(CanCancelDocumentTextAnalysis))]
    private void CancelDocumentTextAnalysis()
    {
        if (_currentDocumentTextJob is null)
        {
            return;
        }

        _cancelDocumentTextAnalysisUseCase.Execute(_currentDocumentTextJob.JobId);
        SearchPanelNotice = "Text analysis cancelled.";
        StatusText = "Text analysis cancelled";
    }

    [RelayCommand(CanExecute = nameof(CanNavigateSearchResults))]
    private async Task PreviousSearchResultAsync()
    {
        if (SearchResults.Count == 0)
        {
            return;
        }

        var targetIndex = _selectedSearchResultIndex <= 0
            ? SearchResults.Count - 1
            : _selectedSearchResultIndex - 1;

        await SelectSearchResultAsync(SearchResults[targetIndex], updateStatus: true);
    }

    [RelayCommand(CanExecute = nameof(CanNavigateSearchResults))]
    private async Task NextSearchResultAsync()
    {
        if (SearchResults.Count == 0)
        {
            return;
        }

        var targetIndex = _selectedSearchResultIndex < 0 || _selectedSearchResultIndex >= SearchResults.Count - 1
            ? 0
            : _selectedSearchResultIndex + 1;

        await SelectSearchResultAsync(SearchResults[targetIndex], updateStatus: true);
    }

    [RelayCommand(CanExecute = nameof(HasSearchResults))]
    private async Task OpenSearchResultAsync(SearchResultItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        await SelectSearchResultAsync(item, updateStatus: true);
    }

    [RelayCommand(CanExecute = nameof(HasOpenDocument))]
    private void ToggleInfoPanel()
    {
        if (!IsInfoPanelVisible)
        {
            IsSearchPanelVisible = false;
            IsPreferencesPanelVisible = false;
            IsPrintPanelVisible = false;
        }

        IsInfoPanelVisible = !IsInfoPanelVisible;
        StatusText = IsInfoPanelVisible
            ? "File information shown"
            : "File information hidden";
    }

    [RelayCommand]
    private void TogglePreferencesPanel()
    {
        if (!IsPreferencesPanelVisible)
        {
            IsInfoPanelVisible = false;
            IsSearchPanelVisible = false;
            IsPrintPanelVisible = false;
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
            "Drag other thumbnails if needed, then save the document.",
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

    public void UpdateWindowWidth(double width)
    {
        if (width <= 0 || Math.Abs(_windowWidth - width) < 1)
        {
            return;
        }

        _windowWidth = width;
        OnPropertyChanged(nameof(WindowWidth));
        OnPropertyChanged(nameof(HeaderTitleMaxWidth));
        OnPropertyChanged(nameof(UseInlineHeaderSearch));
        OnPropertyChanged(nameof(UseCollapsedHeaderSearchButton));
    }

    private async Task OpenDocumentFromPathAsync(string filePath)
    {
        CancelCurrentTextAnalysis();
        ClearDocumentTextSelection();
        _currentDocumentTextIndex = null;
        _requiresSearchOcr = false;
        SearchQueryInput = string.Empty;
        SearchPanelNotice = null;
        ClearSearchResults();
        ClearSearchHighlights();

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

    private async Task LoadPrintDestinationsAsync()
    {
        if (_isLoadingPrintDestinations)
        {
            return;
        }

        try
        {
            _isLoadingPrintDestinations = true;
            SubmitPrintJobCommand.NotifyCanExecuteChanged();
            RefreshPrintDestinationsCommand.NotifyCanExecuteChanged();

            var result = await _printService.GetAvailablePrintersAsync();
            if (result.IsFailure)
            {
                PrintDestinations.Clear();
                SelectedPrintDestination = null;
                PrintPanelNotice = result.Error?.Message ?? "Printers could not be loaded.";
                return;
            }

            var currentSelection = SelectedPrintDestination?.Name;
            PrintDestinations.Clear();

            foreach (var printer in result.Value ?? [])
            {
                PrintDestinations.Add(printer);
            }

            SelectedPrintDestination = PrintDestinations.FirstOrDefault(
                item => string.Equals(item.Name, currentSelection, StringComparison.Ordinal)) ??
                PrintDestinations.FirstOrDefault(item => item.IsDefault) ??
                PrintDestinations.FirstOrDefault();

            PrintPanelNotice = PrintDestinations.Count == 0
                ? "No printers were detected. The system default printer may still be unavailable."
                : null;
        }
        finally
        {
            _isLoadingPrintDestinations = false;
            SubmitPrintJobCommand.NotifyCanExecuteChanged();
            RefreshPrintDestinationsCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task EnsureDocumentTextAvailableAsync(bool forceOcr)
    {
        if (!HasOpenDocument)
        {
            return;
        }

        CancelCurrentTextAnalysis();

        var handle = forceOcr
            ? _runDocumentOcrUseCase.Execute()
            : _loadDocumentTextUseCase.Execute();

        _currentDocumentTextJob = handle;
        _isAnalyzingDocumentText = true;
        NotifySearchStateChanged();

        SearchPanelNotice = forceOcr
            ? "Recognizing text locally…"
            : "Loading searchable text…";

        try
        {
            var result = await handle.Completion;
            if (_currentDocumentTextJob?.JobId != handle.JobId)
            {
                return;
            }

            if (result.IsCanceled)
            {
                SearchPanelNotice = "Text analysis cancelled.";
                StatusText = "Text analysis cancelled";
                return;
            }

            if (result.IsFailure)
            {
                _currentDocumentTextIndex = null;
                _requiresSearchOcr = !forceOcr;
                ClearSearchResults();
                ClearSearchHighlights();
                SearchPanelNotice = result.Error?.Message ?? "Searchable text could not be loaded.";
                EnqueueError(SearchPanelNotice, "Text analysis failed");
                StatusText = "Text analysis failed";
                return;
            }

            if (result.RequiresOcr)
            {
                _currentDocumentTextIndex = null;
                _requiresSearchOcr = true;
                ClearSearchResults();
                ClearSearchHighlights();
                SearchPanelNotice = "This document has no searchable text yet. Recognize text to enable search.";
                StatusText = "OCR required";
                return;
            }

            _currentDocumentTextIndex = result.Index;
            _requiresSearchOcr = false;
            SearchPanelNotice = null;
            NotifySearchStateChanged();

            if (!string.IsNullOrWhiteSpace(SearchQueryInput))
            {
                await SearchTextAsync();
            }
            else
            {
                ClearSearchResults();
                ClearSearchHighlights();
                StatusText = forceOcr
                    ? "Text recognized"
                    : "Searchable text loaded";
            }
        }
        finally
        {
            if (_currentDocumentTextJob?.JobId == handle.JobId)
            {
                _currentDocumentTextJob = null;
                _isAnalyzingDocumentText = false;
                NotifySearchStateChanged();
            }
        }
    }

    public async Task<bool> EnsureDocumentTextReadyForSelectionAsync()
    {
        if (!HasOpenDocument)
        {
            return false;
        }

        if (_currentDocumentTextIndex is not null)
        {
            return true;
        }

        if (_isAnalyzingDocumentText)
        {
            return false;
        }

        await EnsureDocumentTextAvailableAsync(forceOcr: false);

        if (_currentDocumentTextIndex is not null)
        {
            return true;
        }

        if (_requiresSearchOcr)
        {
            StatusText = "Recognize text to select text";
        }

        return false;
    }

    private async Task SelectSearchResultAsync(SearchResultItemViewModel item, bool updateStatus)
    {
        ArgumentNullException.ThrowIfNull(item);

        var targetPageNumber = item.PageNumber;
        if (targetPageNumber != CurrentPage)
        {
            await ChangeToPageAsync(targetPageNumber);
        }

        for (var i = 0; i < SearchResults.Count; i++)
        {
            SearchResults[i].IsSelected = ReferenceEquals(SearchResults[i], item);
            if (ReferenceEquals(SearchResults[i], item))
            {
                _selectedSearchResultIndex = i;
            }
        }

        RefreshSearchHighlights();
        OnPropertyChanged(nameof(SearchSelectionIndicator));

        if (updateStatus)
        {
            StatusText = $"Search result on page {item.PageNumber}";
        }
    }

    private void ApplySearchResults(IReadOnlyList<SearchHit> hits)
    {
        ClearSearchResults();

        foreach (var hit in hits)
        {
            SearchResults.Add(new SearchResultItemViewModel(hit, hit.PageIndex.Value + 1));
        }

        _selectedSearchResultIndex = SearchResults.Count > 0 ? 0 : -1;
        NotifySearchStateChanged();
        OnPropertyChanged(nameof(SearchSelectionIndicator));
    }

    private void ClearSearchResults()
    {
        SearchResults.Clear();
        _selectedSearchResultIndex = -1;
        NotifySearchStateChanged();
        OnPropertyChanged(nameof(SearchSelectionIndicator));
    }

    private void RefreshSearchHighlights()
    {
        ClearSearchHighlights();

        if (!IsSearchPanelOpen ||
            _selectedSearchResultIndex < 0 ||
            _selectedSearchResultIndex >= SearchResults.Count ||
            CurrentRenderedBitmap is null)
        {
            return;
        }

        var selected = SearchResults[_selectedSearchResultIndex];
        if (selected.PageNumber != CurrentPage)
        {
            return;
        }

        var pageContent = _currentDocumentTextIndex?.Pages
            .FirstOrDefault(page => page.PageIndex.Value == CurrentPage - 1);
        if (pageContent is null || selected.Hit.Regions.Count == 0)
        {
            return;
        }

        foreach (var region in selected.Hit.Regions)
        {
            if (!TryTransformRegionToRenderedPage(pageContent, region, out var left, out var top, out var width, out var height))
            {
                continue;
            }

            SearchHighlights.Add(new SearchHighlightItem(left, top, width, height, IsPrimary: true));
        }

        OnPropertyChanged(nameof(HasSearchHighlights));
    }

    private void ClearSearchHighlights()
    {
        SearchHighlights.Clear();
        OnPropertyChanged(nameof(HasSearchHighlights));
    }

    public bool BeginDocumentTextSelection(double x, double y)
    {
        var anchorPoint = new DocumentTextSelectionPoint(x, y);
        if (!TryResolveDocumentTextSelection(anchorPoint, anchorPoint, out var selection))
        {
            ClearDocumentTextSelection();
            return false;
        }

        _documentTextSelectionAnchorPoint = anchorPoint;
        ApplyDocumentTextSelection(selection);
        return true;
    }

    public void UpdateDocumentTextSelection(double x, double y)
    {
        var anchorPoint = _documentTextSelectionAnchorPoint;
        if (anchorPoint is null)
        {
            var began = BeginDocumentTextSelection(x, y);
            if (!began)
            {
                ClearDocumentTextSelection();
            }

            return;
        }

        var activePoint = new DocumentTextSelectionPoint(x, y);
        if (!TryResolveDocumentTextSelection(anchorPoint, activePoint, out var selection) &&
            !TryResolveDocumentTextSelection(anchorPoint, anchorPoint, out selection))
        {
            ClearDocumentTextSelection();
            return;
        }

        ApplyDocumentTextSelection(selection);
    }

    public void CompleteDocumentTextSelection()
    {
        if (HasSelectedDocumentText)
        {
            StatusText = "Text selected";
        }
    }

    public void ClearDocumentTextSelection()
    {
        if (TextSelectionHighlights.Count == 0 &&
            string.IsNullOrWhiteSpace(SelectedDocumentText) &&
            _documentTextSelectionAnchorPoint is null &&
            _currentDocumentTextSelection is null)
        {
            return;
        }

        _documentTextSelectionAnchorPoint = null;
        _currentDocumentTextSelection = null;
        TextSelectionHighlights.Clear();
        SelectedDocumentText = null;
        OnPropertyChanged(nameof(HasTextSelectionHighlights));
    }

    public bool TryMapViewerPointToDocumentTextSpace(
        double visualX,
        double visualY,
        double layerWidth,
        double layerHeight,
        out DocumentTextSelectionPoint point)
    {
        point = new DocumentTextSelectionPoint(0, 0);

        if (GetCurrentPageTextContent() is not { } pageContent)
        {
            return false;
        }

        var rotation = GetCurrentRotation();
        return DocumentTextSelectionCoordinateMapper.TryMapVisualToDocument(
            visualX,
            visualY,
            layerWidth,
            layerHeight,
            pageContent.SourceWidth,
            pageContent.SourceHeight,
            rotation,
            out point);
    }

    private bool TryResolveDocumentTextSelection(
        DocumentTextSelectionPoint anchorPoint,
        DocumentTextSelectionPoint activePoint,
        out DocumentTextSelectionResult selection)
    {
        if (_documentSessionStore.Current is not { } session ||
            _currentDocumentTextIndex is null ||
            GetCurrentPageTextContent() is not { } pageContent)
        {
            selection = new DocumentTextSelectionResult(
                new PageIndex(Math.Max(0, CurrentPage - 1)),
                null,
                [],
                TextSourceKind.Ocr);
            return false;
        }

        var result = _resolveDocumentTextSelectionUseCase.Execute(
            new DocumentTextSelectionRequest(
                session,
                _currentDocumentTextIndex,
                pageContent.PageIndex,
                anchorPoint,
                activePoint));

        if (result.IsFailure || result.Value is null || !result.Value.HasSelection)
        {
            selection = new DocumentTextSelectionResult(
                pageContent.PageIndex,
                null,
                [],
                pageContent.SourceKind);
            return false;
        }

        selection = result.Value;
        return true;
    }

    private void ApplyDocumentTextSelection(DocumentTextSelectionResult selection)
    {
        _currentDocumentTextSelection = selection;
        SelectedDocumentText = selection.SelectedText;
        RefreshDocumentTextSelectionHighlights();
    }

    private void RefreshDocumentTextSelectionHighlights()
    {
        TextSelectionHighlights.Clear();

        if (_currentDocumentTextSelection is null ||
            CurrentRenderedBitmap is null ||
            GetCurrentPageTextContent() is not { } pageContent ||
            _currentDocumentTextSelection.PageIndex != pageContent.PageIndex)
        {
            OnPropertyChanged(nameof(HasTextSelectionHighlights));
            return;
        }

        foreach (var region in _currentDocumentTextSelection.Regions)
        {
            if (!TryTransformRegionToRenderedPage(pageContent, region, out var left, out var top, out var width, out var height))
            {
                continue;
            }

            TextSelectionHighlights.Add(new SearchHighlightItem(left, top, width, height, IsPrimary: false));
        }

        OnPropertyChanged(nameof(HasTextSelectionHighlights));
    }

    private bool TryTransformRegionToRenderedPage(
        PageTextContent pageContent,
        NormalizedTextRegion region,
        out double left,
        out double top,
        out double width,
        out double height)
    {
        left = 0;
        top = 0;
        width = 0;
        height = 0;

        if (CurrentRenderedBitmap is null)
        {
            return false;
        }

        var sourceWidth = pageContent.SourceWidth;
        var sourceHeight = pageContent.SourceHeight;
        var sourceLeft = region.X * sourceWidth;
        var sourceTop = region.Y * sourceHeight;
        var sourceRight = sourceLeft + (region.Width * sourceWidth);
        var sourceBottom = sourceTop + (region.Height * sourceHeight);

        var rotation = GetCurrentRotation();
        var (rotatedLeft, rotatedTop, rotatedRight, rotatedBottom, rotatedWidth, rotatedHeight) =
            rotation switch
            {
                Rotation.Deg90 => (
                    sourceHeight - sourceBottom,
                    sourceLeft,
                    sourceHeight - sourceTop,
                    sourceRight,
                    sourceHeight,
                    sourceWidth),
                Rotation.Deg180 => (
                    sourceWidth - sourceRight,
                    sourceHeight - sourceBottom,
                    sourceWidth - sourceLeft,
                    sourceHeight - sourceTop,
                    sourceWidth,
                    sourceHeight),
                Rotation.Deg270 => (
                    sourceTop,
                    sourceWidth - sourceRight,
                    sourceBottom,
                    sourceWidth - sourceLeft,
                    sourceHeight,
                    sourceWidth),
                _ => (
                    sourceLeft,
                    sourceTop,
                    sourceRight,
                    sourceBottom,
                    sourceWidth,
                    sourceHeight)
            };

        left = rotatedLeft / rotatedWidth * CurrentRenderedBitmap.PixelSize.Width;
        top = rotatedTop / rotatedHeight * CurrentRenderedBitmap.PixelSize.Height;
        width = (rotatedRight - rotatedLeft) / rotatedWidth * CurrentRenderedBitmap.PixelSize.Width;
        height = (rotatedBottom - rotatedTop) / rotatedHeight * CurrentRenderedBitmap.PixelSize.Height;

        return width > 0 && height > 0;
    }

    private PageTextContent? GetCurrentPageTextContent()
    {
        return _currentDocumentTextIndex?.Pages
            .FirstOrDefault(page => page.PageIndex.Value == CurrentPage - 1);
    }

    private void NotifySearchStateChanged()
    {
        OnPropertyChanged(nameof(HasSearchResults));
        OnPropertyChanged(nameof(SearchResultSummary));
        OnPropertyChanged(nameof(CanSearchText));
        OnPropertyChanged(nameof(CanRunDocumentOcr));
        OnPropertyChanged(nameof(CanCancelDocumentTextAnalysis));
        OnPropertyChanged(nameof(CanNavigateSearchResults));
        SearchTextCommand.NotifyCanExecuteChanged();
        RunSearchOcrCommand.NotifyCanExecuteChanged();
        CancelDocumentTextAnalysisCommand.NotifyCanExecuteChanged();
        PreviousSearchResultCommand.NotifyCanExecuteChanged();
        NextSearchResultCommand.NotifyCanExecuteChanged();
        OpenSearchResultCommand.NotifyCanExecuteChanged();
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

        if (IsCurrentImageDocument)
        {
            _isImageAutoFitEnabled = zoomMode is ZoomMode.FitToPage;
            NotifyViewerModeChanged();
        }

        if (TryUpdateZoom(GetCurrentZoomFactor(), zoomMode) && CurrentRenderedBitmap is null)
        {
            await RenderCurrentPageAsync(preserveStatusText);
        }
    }

    private async Task ApplyExactZoomAsync(double zoomFactor, bool preserveStatusText)
    {
        DisableImageAutoFit();

        var clampedZoom = Math.Clamp(zoomFactor, MinZoom, MaxZoom);
        var zoomChanged = Math.Abs(GetCurrentZoomFactor() - clampedZoom) > ZoomComparisonTolerance;
        var zoomModeChanged = CurrentZoomMode != ZoomMode.Custom;

        if (zoomChanged || zoomModeChanged)
        {
            if (!TryUpdateZoom(clampedZoom, ZoomMode.Custom))
            {
                return;
            }

            RefreshPageViewState();
        }

        await RenderCurrentPageAsync(preserveStatusText);
    }

    private string? BuildRequestedPrintPageRanges()
    {
        if (!IsPrintPageRangeVisible || SelectedPrintPageRangeOption == PrintRangeAllPagesLabel)
        {
            return null;
        }

        if (SelectedPrintPageRangeOption == PrintRangeCurrentPageLabel)
        {
            return CurrentPage.ToString(CultureInfo.InvariantCulture);
        }

        if (SelectedPrintPageRangeOption != PrintRangeCustomLabel)
        {
            return null;
        }

        var trimmed = PrintCustomPageRange.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        foreach (var character in trimmed)
        {
            if (!char.IsDigit(character) &&
                character is not ',' &&
                character is not '-' &&
                !char.IsWhiteSpace(character))
            {
                return null;
            }
        }

        return trimmed;
    }

    private static PrintOrientationOption ParsePrintOrientationOption(string? value)
    {
        return value switch
        {
            PrintOrientationPortraitLabel => PrintOrientationOption.Portrait,
            PrintOrientationLandscapeLabel => PrintOrientationOption.Landscape,
            _ => PrintOrientationOption.Automatic
        };
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
        if (!TrySyncSessionToActivePageState(_pageViewportStore.ActivePageIndex))
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

        RefreshPageViewState();

        var renderJob = _renderOrchestrator.Submit(
            CreateViewerRenderRequest(
                _pageViewportStore.ActivePageIndex,
                GetCurrentZoomFactor(),
                GetCurrentRotation()));
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

                try
                {
                    var renderJob = _renderOrchestrator.Submit(
                        CreateThumbnailRenderRequest(thumbnailPageIndex, _pageViewportStore.GetRotation(thumbnailPageIndex)));

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

        try
        {
            thumbnailItem.IsLoading = true;

            var renderJob = _renderOrchestrator.Submit(
                CreateThumbnailRenderRequest(pageIndex, _pageViewportStore.GetRotation(pageIndex)));
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

    private void CancelCurrentTextAnalysis()
    {
        if (_currentDocumentTextJob is null)
        {
            return;
        }

        _cancelDocumentTextAnalysisUseCase.Execute(_currentDocumentTextJob.JobId);
        _currentDocumentTextJob = null;
        _isAnalyzingDocumentText = false;
        NotifySearchStateChanged();
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
        CurrentDocumentType = session.Metadata.FormatLabel ?? GetDocumentKindLabel(session.Metadata.DocumentType);
        CurrentDocumentPath = session.Metadata.FilePath;
        EditableDocumentName = session.Metadata.FileName;
        IsEditingDocumentName = false;
        CurrentPage = session.Viewport.CurrentPage.Value + 1;
        TotalPages = session.Metadata.PageCount ?? 1;
        RefreshDocumentInfo(session.Metadata);

        EmptyStateTitle = session.Metadata.FileName;
        EmptyStateDescription = $"Opened {session.Metadata.DocumentType} document.";
    }

    private void RefreshPageViewState()
    {
        CurrentZoom = $"{GetCurrentZoomFactor() * 100:0}%";
        CurrentRotation = $"{(int)GetCurrentRotation()}°";
        CurrentPage = _pageViewportStore.ActivePageIndex.Value + 1;
        OnPropertyChanged(nameof(CanPersistCurrentPageRotation));
        OnPropertyChanged(nameof(CanSaveDocument));
        OnPropertyChanged(nameof(IsSidebarActionStripVisible));
        SaveDocumentCommand.NotifyCanExecuteChanged();
        PersistCurrentPageRotationCommand.NotifyCanExecuteChanged();
    }

    private void ResetDocumentState()
    {
        _isImageAutoFitEnabled = false;
        _isApplyingPdfStructureOperation = false;
        _isPrintingDocument = false;
        _isAnalyzingDocumentText = false;
        _requiresSearchOcr = false;
        HasPendingPageReorder = false;
        HasOpenDocument = false;
        IsCurrentImageDocument = false;
        CurrentDocumentName = null;
        CurrentDocumentType = null;
        CurrentDocumentPath = null;
        EditableDocumentName = string.Empty;
        IsEditingDocumentName = false;
        IsInfoPanelVisible = false;
        IsSearchPanelVisible = false;
        IsPreferencesPanelVisible = false;
        IsPrintPanelVisible = false;
        _currentDocumentTextSelection = null;
        _documentTextSelectionAnchorPoint = null;
        _currentDocumentTextIndex = null;
        ClearDocumentInfo();
        SearchPanelNotice = null;
        PrintPanelNotice = null;
        PrintDestinations.Clear();
        SelectedPrintDestination = null;
        SelectedPrintPageRangeOption = PrintRangeAllPagesLabel;
        PrintCustomPageRange = string.Empty;
        PrintCopiesInput = "1";
        SelectedPrintOrientationOption = PrintOrientationAutomaticLabel;
        PrintFitToPage = true;
        CurrentPage = 1;
        TotalPages = 0;
        CurrentZoom = "100%";
        CurrentRotation = "0°";
        GoToPageInput = "1";
        SearchQueryInput = string.Empty;
        IsRendering = false;
        IsGeneratingThumbnails = false;
        PrintDocumentCommand.NotifyCanExecuteChanged();
        ClearSearchResults();
        ClearSearchHighlights();
        NotifySearchStateChanged();

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

        fitZoom = Math.Clamp(fitZoom, MinZoom, MaxZoom);

        var zoomChanged = Math.Abs(GetCurrentZoomFactor() - fitZoom) > ZoomComparisonTolerance;
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
        var rotation = GetCurrentRotation();
        var currentZoomFactor = GetCurrentZoomFactor();

        if (_documentSessionStore.Current is IImageDocumentSession imageSession)
        {
            fitZoom = zoomMode switch
            {
                ZoomMode.FitToWidth => ImageViewportCalculator.CalculateFitWidthZoom(
                    imageSession.ImageMetadata,
                    rotation,
                    availableWidth),
                ZoomMode.FitToPage => ImageViewportCalculator.CalculateFitZoom(
                    imageSession.ImageMetadata,
                    rotation,
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
                currentZoomFactor,
                availableWidth),
            ZoomMode.FitToPage => RenderedPageViewportCalculator.CalculateFitToPageZoom(
                CurrentRenderedBitmap.PixelSize.Width,
                CurrentRenderedBitmap.PixelSize.Height,
                currentZoomFactor,
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

    private bool TrySyncSessionToActivePageState(PageIndex pageIndex)
    {
        if (!TryUpdateRotation(_pageViewportStore.GetRotation(pageIndex)))
        {
            return false;
        }

        var zoomMode = CurrentZoomMode;
        var targetZoom = _documentSessionStore.CurrentViewport?.ZoomFactor ?? 1.0;

        return TryUpdateZoom(targetZoom, zoomMode);
    }

    private double GetCurrentZoomFactor() =>
        _documentSessionStore.CurrentViewport?.ZoomFactor ?? 1.0;

    private Rotation GetCurrentRotation() =>
        _pageViewportStore.GetRotation(_pageViewportStore.ActivePageIndex);

    private ZoomMode CurrentZoomMode =>
        _documentSessionStore.CurrentViewport?.ZoomMode ?? ZoomMode.Custom;

    private void NotifyViewerModeChanged()
    {
        OnPropertyChanged(nameof(IsImageAutoFitActive));
        OnPropertyChanged(nameof(IsScrollableViewerVisible));
    }

    private static RenderRequest CreateViewerRenderRequest(
        PageIndex pageIndex,
        double zoomFactor,
        Rotation rotation)
    {
        return new RenderRequest(
            ViewerRenderJobKey,
            pageIndex,
            zoomFactor,
            rotation,
            Priority: RenderPriority.Viewer);
    }

    private static RenderRequest CreateThumbnailRenderRequest(PageIndex pageIndex, Rotation rotation)
    {
        return new RenderRequest(
            CreateThumbnailJobKey(pageIndex),
            pageIndex,
            ThumbnailZoomFactor,
            rotation,
            ThumbnailRequestedWidth,
            ThumbnailRequestedHeight,
            RenderPriority.Thumbnail);
    }

    private static string CreateThumbnailJobKey(PageIndex pageIndex) =>
        $"{ThumbnailRenderJobPrefix}{pageIndex.Value}";

    private List<PendingPdfRotationGroup> GetPendingPdfRotationGroups()
    {
        var groups = Enumerable.Range(1, TotalPages)
            .Select(pageNumber => new
            {
                PageNumber = pageNumber,
                Rotation = _pageViewportStore.GetRotation(new PageIndex(pageNumber - 1))
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
            "Drag other thumbnails if needed, then save the document.",
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
        OnPropertyChanged(nameof(CanPersistCurrentPageRotation));
        OnPropertyChanged(nameof(CanSaveDocument));
        OnPropertyChanged(nameof(IsSidebarActionStripVisible));
        SaveDocumentCommand.NotifyCanExecuteChanged();
        PersistCurrentPageRotationCommand.NotifyCanExecuteChanged();
        ExtractCurrentPageCommand.NotifyCanExecuteChanged();
        DeleteCurrentPageCommand.NotifyCanExecuteChanged();
        SaveReorderedPdfCommand.NotifyCanExecuteChanged();
    }

    private async Task ExecutePdfStructureSaveInPlaceAsync(
        string currentDocumentPath,
        Func<string, Task<Result<string>>> executeOperation,
        string successStatus,
        string failureTitle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDocumentPath);
        ArgumentNullException.ThrowIfNull(executeOperation);
        ArgumentException.ThrowIfNullOrWhiteSpace(successStatus);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureTitle);

        if (!TryResolveRequestedSavePath(
                currentDocumentPath,
                out var targetDocumentPath,
                out var targetFileName,
                out var validationError))
        {
            EnqueueWarning("Invalid save name", validationError ?? "Enter a valid file name before saving.");
            StatusText = "Invalid save name";
            return;
        }

        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "velune-pdf-save",
            Guid.NewGuid().ToString("N"));
        var temporaryOutputPath = Path.Combine(temporaryDirectory, targetFileName);

        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            SetPdfStructureOperationState(true);

            var result = await executeOperation(temporaryOutputPath);
            if (result.IsFailure)
            {
                EnqueueError(result.Error?.Message ?? "The PDF update failed.", failureTitle);
                StatusText = failureTitle;
                return;
            }

            await CloseCurrentDocumentStateAsync(clearNotifications: false);

            try
            {
                File.Copy(
                    result.Value ?? temporaryOutputPath,
                    targetDocumentPath,
                    overwrite: PathsEqual(currentDocumentPath, targetDocumentPath));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                EnqueueError(ex.Message, failureTitle);
                StatusText = failureTitle;
                return;
            }

            await OpenDocumentFromPathAsync(targetDocumentPath);
            StatusText = successStatus;
        }
        finally
        {
            try
            {
                if (Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup for temporary save files.
            }

            SetPdfStructureOperationState(false);
        }
    }

    private async Task<Result<string>> SavePdfDocumentChangesAsync(
        string sourcePath,
        string outputPath,
        IReadOnlyList<PendingPdfRotationGroup> pendingRotations,
        IReadOnlyList<int> orderedPages,
        bool hasPendingReorder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(pendingRotations);
        ArgumentNullException.ThrowIfNull(orderedPages);

        var intermediateDirectory = Path.Combine(
            Path.GetTempPath(),
            "velune-pdf-save-steps",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(intermediateDirectory);

        var currentInputPath = sourcePath;

        try
        {
            if (pendingRotations.Count > 0)
            {
                for (var i = 0; i < pendingRotations.Count; i++)
                {
                    var group = pendingRotations[i];
                    var requiresAdditionalStep = hasPendingReorder || i < pendingRotations.Count - 1;
                    var currentOutputPath = requiresAdditionalStep
                        ? Path.Combine(intermediateDirectory, $"rotation-step-{i + 1}.pdf")
                        : outputPath;

                    var rotationResult = await _rotatePdfPagesUseCase.ExecuteAsync(
                        new RotatePdfPagesRequest(
                            currentInputPath,
                            currentOutputPath,
                            group.Pages,
                            group.Rotation));

                    if (rotationResult.IsFailure)
                    {
                        return rotationResult;
                    }

                    currentInputPath = rotationResult.Value ?? currentOutputPath;
                }
            }

            if (hasPendingReorder)
            {
                var reorderResult = await _reorderPdfPagesUseCase.ExecuteAsync(
                    new ReorderPdfPagesRequest(
                        currentInputPath,
                        outputPath,
                        orderedPages.ToArray()));

                if (reorderResult.IsFailure)
                {
                    return reorderResult;
                }

                return ResultFactory.Success(outputPath);
            }

            return ResultFactory.Success(currentInputPath);
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
                // Best-effort cleanup for temporary PDF structure files.
            }
        }
    }

    private async Task ExtractPageAsync(int sourcePageNumber, string pageLabel)
    {
        var currentDocumentPath = CurrentDocumentPath;
        if (string.IsNullOrWhiteSpace(currentDocumentPath))
        {
            return;
        }

        await ExecutePdfStructureOperationAsync(
            title: "Extract page",
            suggestedFileName: BuildSuggestedPdfFileName(),
            executeOperation: outputPath => _extractPdfPagesUseCase.ExecuteAsync(
                new ExtractPdfPagesRequest(
                    currentDocumentPath,
                    outputPath,
                    [sourcePageNumber])),
            successStatus: $"Extracted page {pageLabel}",
            failureTitle: "Unable to extract page");
    }

    private async Task DeletePageAsync(int sourcePageNumber, string pageLabel)
    {
        var currentDocumentPath = CurrentDocumentPath;
        if (string.IsNullOrWhiteSpace(currentDocumentPath))
        {
            return;
        }

        await ExecutePdfStructureOperationAsync(
            title: "Save PDF without page",
            suggestedFileName: BuildSuggestedPdfFileName(),
            executeOperation: outputPath => _deletePdfPagesUseCase.ExecuteAsync(
                new DeletePdfPagesRequest(
                    currentDocumentPath,
                    outputPath,
                    [sourcePageNumber])),
            successStatus: $"Saved PDF without page {pageLabel}",
            failureTitle: "Unable to delete page");
    }

    private string BuildSuggestedPdfFileName()
    {
        var resolvedFileName = ResolveRequestedDocumentFileName();
        return string.IsNullOrWhiteSpace(resolvedFileName)
            ? "document.pdf"
            : resolvedFileName;
    }

    private string ResolveRequestedDocumentFileName()
    {
        var candidate = string.IsNullOrWhiteSpace(EditableDocumentName)
            ? Path.GetFileName(CurrentDocumentName)
            : EditableDocumentName.Trim();

        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = "document";
        }

        if (!Path.HasExtension(candidate))
        {
            candidate = AppendCurrentDocumentExtension(candidate);
        }

        return candidate;
    }

    private bool TryNormalizeRequestedDocumentFileName(
        string candidate,
        out string normalizedFileName,
        out string? validationError)
    {
        normalizedFileName = string.Empty;
        validationError = null;

        if (string.IsNullOrWhiteSpace(candidate))
        {
            validationError = "The file name cannot be empty.";
            return false;
        }

        candidate = candidate.Trim();

        if (candidate.Contains(Path.DirectorySeparatorChar) ||
            candidate.Contains(Path.AltDirectorySeparatorChar))
        {
            validationError = "Only the file name can be edited here.";
            return false;
        }

        if (candidate.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            validationError = "The file name contains unsupported characters.";
            return false;
        }

        normalizedFileName = Path.HasExtension(candidate)
            ? candidate
            : AppendCurrentDocumentExtension(candidate);

        return true;
    }

    private bool HasPendingRequestedSaveNameChange()
    {
        var currentDocumentPath = CurrentDocumentPath;
        if (!IsPdfDocument ||
            string.IsNullOrWhiteSpace(currentDocumentPath) ||
            !TryResolveRequestedSavePath(
                currentDocumentPath,
                out var targetDocumentPath,
                out _,
                out _))
        {
            return false;
        }

        return !PathsEqual(currentDocumentPath, targetDocumentPath);
    }

    private bool TryResolveRequestedSavePath(
        string currentDocumentPath,
        out string targetDocumentPath,
        out string targetFileName,
        out string? validationError)
    {
        targetDocumentPath = string.Empty;
        targetFileName = string.Empty;

        if (!TryNormalizeRequestedDocumentFileName(
                ResolveRequestedDocumentFileName(),
                out targetFileName,
                out validationError))
        {
            return false;
        }

        var currentDirectory = Path.GetDirectoryName(currentDocumentPath);
        if (string.IsNullOrWhiteSpace(currentDirectory))
        {
            validationError = "The current document directory is unavailable.";
            return false;
        }

        targetDocumentPath = Path.Combine(currentDirectory, targetFileName);
        if (!PathsEqual(currentDocumentPath, targetDocumentPath) && File.Exists(targetDocumentPath))
        {
            validationError = $"A file named “{targetFileName}” already exists in this folder.";
            return false;
        }

        return true;
    }

    private string AppendCurrentDocumentExtension(string fileName)
    {
        var extension = Path.GetExtension(CurrentDocumentPath);
        return string.IsNullOrWhiteSpace(extension)
            ? $"{fileName}.pdf"
            : $"{fileName}{extension}";
    }

    private static bool PathsEqual(string leftPath, string rightPath)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(
            Path.GetFullPath(leftPath),
            Path.GetFullPath(rightPath),
            comparison);
    }

    private async Task CloseCurrentDocumentStateAsync(bool clearNotifications)
    {
        CancelCurrentRender();
        CancelCurrentTextAnalysis();
        CancelThumbnailGeneration();

        await _closeDocumentUseCase.ExecuteAsync();
        _pageViewportStore.Clear();
        ClearThumbnails();
        ResetDocumentState();

        if (clearNotifications)
        {
            ClearNotifications();
        }
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
        OnPropertyChanged(nameof(SidebarToggleLabel));
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
            CancelCurrentTextAnalysis();
            CancelThumbnailGeneration();

            CurrentRenderedBitmap?.Dispose();
            CurrentRenderedBitmap = null;

            ClearThumbnails();
        }

        _disposed = true;
    }

}
