using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Application.Documents;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Application.Text;
using Velune.Application.UseCases;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Presentation.FileSystem;
using Velune.Presentation.Imaging;
using Velune.Presentation.Localization;
using Velune.Presentation.Platform;
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
    private static readonly TimeSpan ViewportFitDebounceDelay = TimeSpan.FromMilliseconds(110);
    private const double ZoomComparisonTolerance = 0.001;
    private const double InlineHeaderSearchMinWidth = 1480;
    private const double HeaderTitleCompactThreshold = 1420;
    private const double HeaderTitleTightThreshold = 1220;

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
    private readonly MergePdfDocumentsUseCase _mergePdfDocumentsUseCase;
    private readonly IPdfMarkupService _pdfMarkupService;
    private readonly IImageMarkupService _imageMarkupService;
    private readonly ISignatureAssetStore _signatureAssetStore;
    private readonly IRenderOrchestrator _renderOrchestrator;
    private readonly IDocumentSessionStore _documentSessionStore;
    private readonly IRecentFilesService _recentFilesService;
    private readonly IPageViewportStore _pageViewportStore;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly ILocalizationService _localizationService;
    private readonly ILocalizedErrorFormatter _localizedErrorFormatter;

    private readonly Queue<NotificationEntry> _notificationQueue = [];
    private readonly Dictionary<int, RenderJobHandle> _thumbnailRenderJobs = [];
    private IReadOnlyList<LocalizedOption<AppLanguagePreference>> _languagePreferenceOptions = [];
    private IReadOnlyList<LocalizedOption<AppThemePreference>> _themePreferenceOptions = [];
    private IReadOnlyList<LocalizedOption<DefaultZoomPreference>> _defaultZoomPreferenceOptions = [];
    private IReadOnlyList<LocalizedOption<PrintPageRangeChoice>> _printPageRangeOptions = [];
    private IReadOnlyList<LocalizedOption<PrintOrientationOption>> _printOrientationOptions = [];
    private bool _disposed;
    private double _documentViewportWidth;
    private double _documentViewportHeight;
    private bool _isImageAutoFitEnabled;
    private bool _isApplyingPdfStructureOperation;
    private bool _isPrintingDocument;
    private bool _isLoadingPrintDestinations;
    private bool _isAnalyzingDocumentText;
    private bool _isApplyingPreferencesState;
    private bool _hasPendingMergedDocumentSave;
    private bool _requiresSearchOcr;
    private NotificationKind _currentNotificationKind = NotificationKind.Info;
    private bool _isCurrentNotificationDismissible;
    private int _thumbnailGenerationVersion;
    private int _selectedSearchResultIndex = -1;
    private double _windowWidth = 1280;
    private Action? _notificationPrimaryAction;
    private Action? _notificationSecondaryAction;
    private string? _pendingMergedDocumentTemporaryDirectory;
    private string? _pendingMergedDocumentSuggestedFileName;
    private string? _pendingMergedDocumentTargetPath;
    private string _pendingMergedDocumentSaveSuccessStatusKey = "status.save.merged_pdf";
    private bool _pendingMergedDocumentRequiresSavePicker = true;
    private DocumentTextIndex? _currentDocumentTextIndex;
    private DocumentTextSelectionResult? _currentDocumentTextSelection;
    private RenderJobHandle? _currentRenderJob;
    private DocumentTextJobHandle? _currentDocumentTextJob;
    private DocumentTextSelectionPoint? _documentTextSelectionAnchorPoint;
    private CancellationTokenSource? _pendingViewportFitUpdateCancellation;

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
        MergePdfDocumentsUseCase mergePdfDocumentsUseCase,
        IPdfMarkupService pdfMarkupService,
        IImageMarkupService imageMarkupService,
        ISignatureAssetStore signatureAssetStore,
        IRenderOrchestrator renderOrchestrator,
        IDocumentSessionStore documentSessionStore,
        IRecentFilesService recentFilesService,
        IPageViewportStore pageViewportStore,
        IUserPreferencesService userPreferencesService,
        ILocalizationService localizationService,
        ILocalizedErrorFormatter localizedErrorFormatter)
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
        ArgumentNullException.ThrowIfNull(mergePdfDocumentsUseCase);
        ArgumentNullException.ThrowIfNull(pdfMarkupService);
        ArgumentNullException.ThrowIfNull(imageMarkupService);
        ArgumentNullException.ThrowIfNull(signatureAssetStore);
        ArgumentNullException.ThrowIfNull(renderOrchestrator);
        ArgumentNullException.ThrowIfNull(documentSessionStore);
        ArgumentNullException.ThrowIfNull(recentFilesService);
        ArgumentNullException.ThrowIfNull(pageViewportStore);
        ArgumentNullException.ThrowIfNull(userPreferencesService);
        ArgumentNullException.ThrowIfNull(localizationService);
        ArgumentNullException.ThrowIfNull(localizedErrorFormatter);

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
        _mergePdfDocumentsUseCase = mergePdfDocumentsUseCase;
        _pdfMarkupService = pdfMarkupService;
        _imageMarkupService = imageMarkupService;
        _signatureAssetStore = signatureAssetStore;
        _renderOrchestrator = renderOrchestrator;
        _documentSessionStore = documentSessionStore;
        _recentFilesService = recentFilesService;
        _pageViewportStore = pageViewportStore;
        _userPreferencesService = userPreferencesService;
        _localizationService = localizationService;
        _localizedErrorFormatter = localizedErrorFormatter;

        RecentFiles = [];
        Thumbnails = [];
        DocumentInfoItems = [];
        PrintDestinations = [];
        SearchResults = [];
        SearchHighlights = [];
        TextSelectionHighlights = [];
        MemoryCacheEntryLimitOptions = new ObservableCollection<int> { 0, 32, 64, 128, 256 };
        RebuildLocalizedOptions();
        ApplyLocalizedShellText();
        ApplyPreferencesToUi(_userPreferencesService.Current);
        _localizationService.LanguageChanged += OnLanguageChanged;
        RefreshRecentFiles();
        InitializeAnnotationWorkspace();
    }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _applicationTitle = string.Empty;

    [ObservableProperty]
    private string _sidebarTitle = string.Empty;

    [ObservableProperty]
    private string _emptyStateTitle = string.Empty;

    [ObservableProperty]
    private string _emptyStateDescription = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

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
    private LocalizedOption<AppLanguagePreference>? _selectedLanguagePreference;

    [ObservableProperty]
    private LocalizedOption<AppThemePreference>? _selectedThemePreference;

    [ObservableProperty]
    private LocalizedOption<DefaultZoomPreference>? _selectedDefaultZoomPreference;

    [ObservableProperty]
    private bool _showThumbnailsPanelPreference = true;

    [ObservableProperty]
    private int _selectedMemoryCacheEntryLimit = 64;

    [ObservableProperty]
    private PrintDestinationInfo? _selectedPrintDestination;

    [ObservableProperty]
    private LocalizedOption<PrintPageRangeChoice>? _selectedPrintPageRangeOption;

    [ObservableProperty]
    private string _printCustomPageRange = string.Empty;

    [ObservableProperty]
    private string _printCopiesInput = "1";

    [ObservableProperty]
    private LocalizedOption<PrintOrientationOption>? _selectedPrintOrientationOption;

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
    public IReadOnlyList<LocalizedOption<AppLanguagePreference>> LanguagePreferenceOptions => _languagePreferenceOptions;
    public IReadOnlyList<LocalizedOption<AppThemePreference>> ThemePreferenceOptions => _themePreferenceOptions;
    public IReadOnlyList<LocalizedOption<DefaultZoomPreference>> DefaultZoomPreferenceOptions => _defaultZoomPreferenceOptions;
    public IReadOnlyList<LocalizedOption<PrintPageRangeChoice>> PrintPageRangeOptions => _printPageRangeOptions;
    public IReadOnlyList<LocalizedOption<PrintOrientationOption>> PrintOrientationOptions => _printOrientationOptions;

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
    public bool ShowRenderingBanner => IsRendering && CurrentRenderedBitmap is null;
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
        ? (CurrentDocumentType ?? L("document.type.document"))
        : L("app.window.subtitle");
    public bool ShouldEmphasizeOpenAction => !HasOpenDocument;
    public bool IsPdfDocument => HasOpenDocument && !IsCurrentImageDocument;
    public bool IsImageAutoFitActive => HasOpenDocument && IsCurrentImageDocument && _isImageAutoFitEnabled;
    public bool IsScrollableViewerVisible => !IsImageAutoFitActive;
    public bool ShowWindowMenuBar => !OperatingSystem.IsMacOS();
    public double HeaderTitleMaxWidth => WindowWidth >= InlineHeaderSearchMinWidth
        ? 280
        : WindowWidth >= HeaderTitleCompactThreshold
            ? 220
            : WindowWidth >= HeaderTitleTightThreshold
                ? 180
                : 140;
    public bool IsSidebarVisible => HasOpenDocument && ShowThumbnailsPanelPreference;
    public bool CanToggleSidebar => HasOpenDocument;
    public string SidebarToggleLabel => IsSidebarVisible ? L("toolbar.sidebar.hide") : L("toolbar.sidebar.show");
    public bool ShowEditableHeaderTitle => HasOpenDocument && !IsEditingDocumentName;
    public bool ShowHeaderTitleEditor => HasOpenDocument && IsEditingDocumentName;
    public bool UseInlineHeaderSearch => HasOpenDocument && IsSearchAvailableForCurrentDocument && WindowWidth >= InlineHeaderSearchMinWidth;
    public bool UseCollapsedHeaderSearchButton => HasOpenDocument && IsSearchAvailableForCurrentDocument && !UseInlineHeaderSearch;
    public bool IsPageNavigationVisible => HasOpenDocument && !IsCurrentImageDocument;
    public bool IsPdfStructureActionsVisible => IsPdfDocument;
    public bool IsSidebarActionStripVisible => IsPdfDocument && (CanPersistCurrentPageRotation || HasPendingPageReorder);
    public bool IsInfoPanelOpen => HasOpenDocument && IsInfoPanelVisible;
    public bool IsSearchPanelOpen => HasOpenDocument && IsSearchAvailableForCurrentDocument && IsSearchPanelVisible;
    public bool IsPreferencesPanelOpen => IsPreferencesPanelVisible;
    public bool IsPrintPanelOpen => HasOpenDocument && IsPrintPanelVisible;
    public bool HasDocumentInfo => DocumentInfoItems.Count > 0;
    public bool HasDocumentInfoWarning => !string.IsNullOrWhiteSpace(DocumentInfoWarning);
    public bool HasSearchPanelNotice => !string.IsNullOrWhiteSpace(SearchPanelNotice);
    public bool HasPrintPanelNotice => !string.IsNullOrWhiteSpace(PrintPanelNotice);
    public bool IsPrintPageRangeVisible => IsPdfDocument;
    public bool IsCustomPrintRangeVisible => IsPrintPageRangeVisible && SelectedPrintPageRangeOption?.Value is PrintPageRangeChoice.CustomRange;
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
        0 when _requiresSearchOcr => L("search.summary.ocr_required"),
        0 when _isAnalyzingDocumentText => L("search.summary.loading"),
        0 => L("search.summary.none"),
        1 => L("search.summary.one"),
        _ => L("search.summary.many", SearchResults.Count)
    };
    public string SearchSelectionIndicator => _selectedSearchResultIndex < 0 || SearchResults.Count == 0
        ? L("search.selection.none")
        : L("search.selection.current", _selectedSearchResultIndex + 1, SearchResults.Count);
    public bool CanSearchText => IsSearchAvailableForCurrentDocument && !_isAnalyzingDocumentText && _currentDocumentTextIndex is not null && !string.IsNullOrWhiteSpace(SearchQueryInput);
    public bool CanRunDocumentOcr => IsSearchAvailableForCurrentDocument && !_isAnalyzingDocumentText && _requiresSearchOcr;
    public bool CanCancelDocumentTextAnalysis => IsSearchAvailableForCurrentDocument && _isAnalyzingDocumentText && _currentDocumentTextJob is not null;
    public bool CanNavigateSearchResults => SearchResults.Count > 1;
    public double DisplayedRenderedPageWidth => CurrentRenderedBitmap?.PixelSize.Width ?? 0;
    public double DisplayedRenderedPageHeight => CurrentRenderedBitmap?.PixelSize.Height ?? 0;
    public HorizontalAlignment ScrollableDocumentHorizontalAlignment =>
        HasRenderedPage && DisplayedRenderedPageWidth > _documentViewportWidth + 1
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Center;
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
        HasOpenDocument &&
        !IsPdfStructureOperationInProgress &&
        !string.IsNullOrWhiteSpace(CurrentDocumentPath) &&
        ((IsPdfDocument && (_hasPendingMergedDocumentSave || CanPersistCurrentPageRotation || HasPendingPageReorder || HasPendingRequestedSaveNameChange() || HasPendingAnnotationChanges)) ||
         (IsCurrentImageDocument && (HasPendingRequestedSaveNameChange() || HasPendingAnnotationChanges)));
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
    public bool CanMergePdfDocuments => !IsPdfStructureOperationInProgress;
    public bool CanAcceptThumbnailDocumentDrop =>
        HasOpenDocument &&
        !IsPdfStructureOperationInProgress &&
        !string.IsNullOrWhiteSpace(CurrentDocumentPath);
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
        OnPropertyChanged(nameof(SidebarHostVisible));
        OnPropertyChanged(nameof(SidebarOpacity));
        OnPropertyChanged(nameof(IsSidebarInteractive));
        OnPropertyChanged(nameof(CanToggleSidebar));
        OnPropertyChanged(nameof(CanAcceptThumbnailDocumentDrop));
        OnPropertyChanged(nameof(SidebarToggleLabel));
        OnPropertyChanged(nameof(ShowEditableHeaderTitle));
        OnPropertyChanged(nameof(ShowHeaderTitleEditor));
        OnPropertyChanged(nameof(IsSearchAvailableForCurrentDocument));
        OnPropertyChanged(nameof(UseInlineHeaderSearch));
        OnPropertyChanged(nameof(UseCollapsedHeaderSearchButton));
        OnPropertyChanged(nameof(IsPageNavigationVisible));
        OnPropertyChanged(nameof(IsPdfStructureActionsVisible));
        OnPropertyChanged(nameof(CanPersistCurrentPageRotation));
        OnPropertyChanged(nameof(CanSaveDocument));
        OnPropertyChanged(nameof(CanAcceptThumbnailDocumentDrop));
        OnPropertyChanged(nameof(IsSidebarActionStripVisible));
        OnPropertyChanged(nameof(IsInfoPanelOpen));
        OnPropertyChanged(nameof(InfoPanelWidth));
        OnPropertyChanged(nameof(InfoPanelOpacity));
        OnPropertyChanged(nameof(IsInfoPanelInteractive));
        OnPropertyChanged(nameof(IsSearchPanelOpen));
        OnPropertyChanged(nameof(SearchPanelWidth));
        OnPropertyChanged(nameof(SearchPanelOpacity));
        OnPropertyChanged(nameof(IsSearchPanelInteractive));
        OnPropertyChanged(nameof(IsAnnotationsPanelOpen));
        OnPropertyChanged(nameof(AnnotationsPanelWidth));
        OnPropertyChanged(nameof(AnnotationsPanelOpacity));
        OnPropertyChanged(nameof(IsAnnotationsPanelInteractive));
        OnPropertyChanged(nameof(IsPreferencesPanelOpen));
        OnPropertyChanged(nameof(PreferencesPanelWidth));
        OnPropertyChanged(nameof(PreferencesPanelOpacity));
        OnPropertyChanged(nameof(IsPreferencesPanelInteractive));
        OnPropertyChanged(nameof(IsPrintPanelOpen));
        OnPropertyChanged(nameof(PrintPanelWidth));
        OnPropertyChanged(nameof(PrintPanelOpacity));
        OnPropertyChanged(nameof(IsPrintPanelInteractive));
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
        ToggleAnnotationsPanelCommand.NotifyCanExecuteChanged();
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
        AddHighlightAnnotationFromSelectionCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsCurrentImageDocumentChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPdfDocument));
        NotifyViewerModeChanged();
        NotifySidebarVisibilityChanged();
        OnPropertyChanged(nameof(SidebarHostVisible));
        OnPropertyChanged(nameof(SidebarOpacity));
        OnPropertyChanged(nameof(IsSidebarInteractive));
        OnPropertyChanged(nameof(CanToggleSidebar));
        OnPropertyChanged(nameof(CanAcceptThumbnailDocumentDrop));
        OnPropertyChanged(nameof(SidebarToggleLabel));
        OnPropertyChanged(nameof(IsSearchAvailableForCurrentDocument));
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
        ToggleAnnotationsPanelCommand.NotifyCanExecuteChanged();
        ToggleSidebarCommand.NotifyCanExecuteChanged();
        MoveCurrentPageEarlierCommand.NotifyCanExecuteChanged();
        MoveCurrentPageLaterCommand.NotifyCanExecuteChanged();
        AddHighlightAnnotationFromSelectionCommand.NotifyCanExecuteChanged();
    }

    partial void OnCurrentDocumentPathChanged(string? value)
    {
        OnPropertyChanged(nameof(CanSaveDocument));
        OnPropertyChanged(nameof(CanAcceptThumbnailDocumentDrop));
        SaveDocumentCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsInfoPanelVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsInfoPanelOpen));
        OnPropertyChanged(nameof(InfoPanelWidth));
        OnPropertyChanged(nameof(InfoPanelOpacity));
        OnPropertyChanged(nameof(IsInfoPanelInteractive));
    }

    partial void OnIsSearchPanelVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsSearchPanelOpen));
        OnPropertyChanged(nameof(SearchPanelWidth));
        OnPropertyChanged(nameof(SearchPanelOpacity));
        OnPropertyChanged(nameof(IsSearchPanelInteractive));

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
        OnPropertyChanged(nameof(PreferencesPanelOpacity));
        OnPropertyChanged(nameof(IsPreferencesPanelInteractive));
    }

    partial void OnIsPrintPanelVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPrintPanelOpen));
        OnPropertyChanged(nameof(PrintPanelWidth));
        OnPropertyChanged(nameof(PrintPanelOpacity));
        OnPropertyChanged(nameof(IsPrintPanelInteractive));
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
        OnPropertyChanged(nameof(ShowRenderingBanner));
        OnPropertyChanged(nameof(ScrollableDocumentHorizontalAlignment));
        FitToWidthCommand.NotifyCanExecuteChanged();
        FitToPageCommand.NotifyCanExecuteChanged();
        RefreshDocumentTextSelectionHighlights();
        RefreshSearchHighlights();
        RefreshAnnotationOverlay();
    }

    partial void OnIsRenderingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowRenderingBanner));
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

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        ApplyLocalizedShellText();
        ApplyPreferencesToUi(_userPreferencesService.Current);
        RefreshRecentFiles();
        RefreshSearchResultLocalization();
        RefreshThumbnailLocalization();
        RefreshAnnotationLocalization();

        if (_documentSessionStore.Current is { } session)
        {
            CurrentDocumentType = GetLocalizedDocumentFormatLabel(session.Metadata);
            RefreshDocumentInfo(session.Metadata);
            EmptyStateTitle = session.Metadata.FileName;
            EmptyStateDescription = L("document.empty.opened", GetDocumentKindLabel(session.Metadata.DocumentType));
        }

        OnPropertyChanged(nameof(HeaderSubtitle));
        OnPropertyChanged(nameof(SidebarToggleLabel));
        OnPropertyChanged(nameof(SearchResultSummary));
        OnPropertyChanged(nameof(SearchSelectionIndicator));
    }

    private void ApplyLocalizedShellText()
    {
        Title = L("app.name");
        ApplicationTitle = L("app.name");
        SidebarTitle = L("sidebar.pages.title");

        if (!HasOpenDocument)
        {
            EmptyStateTitle = L("app.empty.title");
            EmptyStateDescription = L("app.empty.description");
        }

        if (string.IsNullOrWhiteSpace(StatusText))
        {
            StatusText = L("app.ready");
        }
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
        RefreshAnnotationWorkspaceState();
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

    partial void OnSelectedLanguagePreferenceChanged(LocalizedOption<AppLanguagePreference>? value)
    {
        if (_isApplyingPreferencesState)
        {
            return;
        }

        _ = PersistPreferencesFromUiAsync(applyDefaultZoomToOpenDocument: false);
    }

    partial void OnSelectedThemePreferenceChanged(LocalizedOption<AppThemePreference>? value)
    {
        if (_isApplyingPreferencesState)
        {
            return;
        }

        _ = PersistPreferencesFromUiAsync(applyDefaultZoomToOpenDocument: false);
    }

    partial void OnSelectedDefaultZoomPreferenceChanged(LocalizedOption<DefaultZoomPreference>? value)
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
        OnPropertyChanged(nameof(SidebarOpacity));
        OnPropertyChanged(nameof(IsSidebarInteractive));

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

    partial void OnSelectedPrintPageRangeOptionChanged(LocalizedOption<PrintPageRangeChoice>? value)
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
        OnPropertyChanged(nameof(CanCreateHighlightAnnotation));
        AddHighlightAnnotationFromSelectionCommand.NotifyCanExecuteChanged();
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
            StatusText = L("status.open.cancelled");
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
        StatusText = L("status.document.closed");
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
            StatusText = L("status.print.panel_hidden");
            return;
        }

        IsInfoPanelVisible = false;
        IsSearchPanelVisible = false;
        IsPreferencesPanelVisible = false;
        IsPrintPanelVisible = true;
        StatusText = L("status.print.loading_printers");

        await LoadPrintDestinationsAsync();

        StatusText = L("status.print.panel_shown");
    }

    private async Task ShowSystemPrintDialogAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentDocumentPath))
        {
            EnqueueLocalizedError(null, "error.print.no_document.title", "error.print.no_document.message");
            StatusText = L("status.print.unavailable");
            return;
        }

        IsInfoPanelVisible = false;
        IsSearchPanelVisible = false;
        IsPreferencesPanelVisible = false;
        IsPrintPanelVisible = false;
        StatusText = L("status.print.opening_dialog");

        var result = await _showSystemPrintDialogUseCase.ExecuteAsync(CurrentDocumentPath);
        if (result.IsSuccess)
        {
            StatusText = L("status.print.dialog_shown");
            return;
        }

        if (string.Equals(result.Error?.Code, "print.cancelled", StringComparison.Ordinal))
        {
            StatusText = L("status.print.cancelled");
            return;
        }

        EnqueueLocalizedError(result.Error, "error.print.dialog_failed.title", "error.print.dialog_failed.message");
        StatusText = L("status.print.failed");
    }

    [RelayCommand(CanExecute = nameof(HasOpenDocument))]
    private async Task RefreshPrintDestinationsAsync()
    {
        if (!HasOpenDocument)
        {
            return;
        }

        await LoadPrintDestinationsAsync();
        StatusText = L("status.print.refreshed");
    }

    [RelayCommand(CanExecute = nameof(CanSubmitPrintJob))]
    private async Task SubmitPrintJobAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentDocumentPath))
        {
            EnqueueLocalizedError(null, "error.print.no_document.title", "error.print.no_document.message");
            StatusText = L("status.print.unavailable");
            return;
        }

        if (!int.TryParse(PrintCopiesInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out var copies) ||
            copies <= 0)
        {
            EnqueueWarning(L("validation.print.title"), L("validation.print.copies"));
            StatusText = L("validation.print.invalid_status");
            return;
        }

        var pageRanges = BuildRequestedPrintPageRanges();
        if (pageRanges is null && IsCustomPrintRangeVisible)
        {
            EnqueueWarning(L("validation.print.title"), L("validation.print.range"));
            StatusText = L("validation.print.invalid_status");
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
                    SelectedPrintOrientationOption?.Value ?? PrintOrientationOption.Automatic,
                    PrintFitToPage));

            if (result.IsFailure)
            {
                var presentation = _localizedErrorFormatter.Format(
                    result.Error,
                    "error.print.failed.title",
                    "error.print.failed.message");
                EnqueueError(presentation.Message, presentation.Title);
                PrintPanelNotice = presentation.Message;
                StatusText = L("status.print.failed");
                return;
            }

            PrintPanelNotice = null;
            IsPrintPanelVisible = false;
            EnqueueLocalizedInfo("notification.print.started.title", "notification.print.started.message");
            StatusText = L("status.print.started");
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
            L("notification.clear_recent.title"),
            L("notification.clear_recent.message"),
            L("notification.clear_recent.confirm"),
            ConfirmClearRecentFiles,
            L("notification.clear_recent.keep"));

        StatusText = L("status.confirmation.required");
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
            EnqueueWarning(L("status.page.invalid"), L("validation.page.enter"));
            StatusText = L("status.page.invalid");
            return;
        }

        if (!int.TryParse(GoToPageInput, out var pageNumber))
        {
            EnqueueWarning(L("status.page.invalid"), L("validation.page.numeric"));
            StatusText = L("status.page.invalid");
            return;
        }

        if (pageNumber < 1 || pageNumber > TotalPages)
        {
            EnqueueWarning(L("status.page.invalid"), L("validation.page.range", TotalPages));
            StatusText = L("status.page.invalid");
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

    public Task OpenThumbnailContextMenuActionAsync(PageThumbnailItemViewModel? thumbnail)
    {
        return SelectThumbnailAsync(thumbnail);
    }

    public Task ExtractThumbnailContextMenuActionAsync(PageThumbnailItemViewModel? thumbnail)
    {
        return CanExtractCurrentPage
            ? ExtractThumbnailPageAsync(thumbnail)
            : Task.CompletedTask;
    }

    public Task DeleteThumbnailContextMenuActionAsync(PageThumbnailItemViewModel? thumbnail)
    {
        return CanDeleteCurrentPage
            ? DeleteThumbnailPageAsync(thumbnail)
            : Task.CompletedTask;
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

        StatusText = L("status.page.moved_earlier", CurrentPage);
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

        StatusText = L("status.page.moved_later", CurrentPage);
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
        StatusText = L("status.zoom.set", CurrentZoom);
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
        StatusText = L("status.zoom.set", CurrentZoom);
    }

    [RelayCommand(CanExecute = nameof(CanUseFitCommands))]
    private async Task FitToWidthAsync()
    {
        if (!await TryApplyViewportFitAsync(ZoomMode.FitToWidth, forceRender: true))
        {
            return;
        }

        StatusText = L("status.zoom.fit_width");
    }

    [RelayCommand(CanExecute = nameof(CanUseFitCommands))]
    private async Task FitToPageAsync()
    {
        if (!await TryApplyViewportFitAsync(ZoomMode.FitToPage, forceRender: true))
        {
            return;
        }

        StatusText = L("status.zoom.fit_page");
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
        StatusText = L("status.rotation.set", CurrentRotation);
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
            title: L("dialog.save.rotated_pdf"),
            suggestedFileName: BuildSuggestedPdfFileName(),
            executeOperation: outputPath => PersistPendingPdfRotationsAsync(
                currentDocumentPath,
                outputPath,
                pendingRotations),
            successStatus: pendingRotations.Count == 1
                ? L("status.save.rotated_pdf.single", pendingRotations[0].Pages[0])
                : L("status.save.rotated_pdf"),
            failureTitle: L("error.save.rotated_pdf.title"));
    }

    [RelayCommand(CanExecute = nameof(CanSaveDocument))]
    private async Task SaveDocumentAsync()
    {
        var currentDocumentPath = CurrentDocumentPath;
        if (string.IsNullOrWhiteSpace(currentDocumentPath))
        {
            return;
        }

        if (IsCurrentImageDocument)
        {
            if (!HasPendingRequestedSaveNameChange() && !HasPendingAnnotationChanges)
            {
                StatusText = L("status.nothing_to_save");
                return;
            }

            await SaveImageDocumentInPlaceAsync(currentDocumentPath);
            return;
        }

        if (_hasPendingMergedDocumentSave)
        {
            await SavePendingMergedDocumentAsync(currentDocumentPath);
            return;
        }

        var pendingRotations = GetPendingPdfRotationGroups();
        var orderedPages = Thumbnails
            .Select(thumbnail => thumbnail.SourcePageNumber)
            .ToArray();
        var hasPendingSaveNameChange = HasPendingRequestedSaveNameChange();

        if (pendingRotations.Count == 0 &&
            !HasPendingPageReorder &&
            !hasPendingSaveNameChange &&
            !HasPendingAnnotationChanges)
        {
            StatusText = L("status.nothing_to_save");
            return;
        }

        if (HasPendingAnnotationChanges)
        {
            await SaveAnnotatedPdfInPlaceAsync(currentDocumentPath);
            return;
        }

        if (pendingRotations.Count == 0 && !HasPendingPageReorder)
        {
            await ExecutePdfStructureSaveInPlaceAsync(
                currentDocumentPath,
                _ => Task.FromResult(ResultFactory.Success(currentDocumentPath)),
                successStatus: L("status.document.saved"),
                failureTitle: L("error.save.document.title"));
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
            successStatus: L("status.document.saved"),
            failureTitle: L("error.save.document.title"));
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
            title: L("dialog.save.reordered_pdf"),
            suggestedFileName: BuildSuggestedPdfFileName(),
            executeOperation: outputPath => _reorderPdfPagesUseCase.ExecuteAsync(
                new ReorderPdfPagesRequest(
                    currentDocumentPath,
                    outputPath,
                    orderedPages)),
            successStatus: L("status.save.reordered_pdf"),
            failureTitle: L("error.save.reordered_pdf.title"));
    }

    [RelayCommand(CanExecute = nameof(CanMergePdfDocuments))]
    private async Task MergePdfDocumentsAsync()
    {
        var sourcePaths = await _filePickerService.PickOpenMergeSourceFilesAsync(
            L("dialog.open.merge_pdf_sources"));
        if (sourcePaths.Count == 0)
        {
            StatusText = L("status.merge.cancelled");
            return;
        }

        await MergeDocumentSourcesAsync(sourcePaths);
    }

    public async Task HandleThumbnailFilesDroppedAsync(IReadOnlyList<string> filePaths, int insertionIndex)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        if (!CanAcceptThumbnailDocumentDrop ||
            string.IsNullOrWhiteSpace(CurrentDocumentPath))
        {
            return;
        }

        var supportedDroppedPaths = filePaths
            .Where(IsSupportedMergeSourcePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (supportedDroppedPaths.Length == 0)
        {
            EnqueueLocalizedWarning(
                "notification.merge.drop_unsupported.title",
                "notification.merge.drop_unsupported.message",
                replaceCurrent: true);
            StatusText = L("status.merge.drop_unsupported");
            return;
        }

        var sourcePaths = new[] { CurrentDocumentPath }
            .Concat(supportedDroppedPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await MergeDroppedDocumentSourcesAsync(sourcePaths, insertionIndex);
    }

    private async Task MergeDocumentSourcesAsync(IReadOnlyList<string> sourcePaths)
    {
        if (sourcePaths.Count < 2)
        {
            EnqueueLocalizedWarning(
                "notification.merge.selection_invalid.title",
                "notification.merge.selection_invalid.message",
                replaceCurrent: true);
            StatusText = L("status.merge.selection_invalid");
            return;
        }

        await CreateMergedDocumentPreviewAsync(
            BuildSuggestedMergedPdfFileName(sourcePaths),
            _ => Task.FromResult<(string[]? SourcePaths, AppError? Error)>((sourcePaths.ToArray(), null)));
    }

    private async Task MergeDroppedDocumentSourcesAsync(string[] sourcePaths, int insertionIndex)
    {
        if (sourcePaths.Length < 2)
        {
            EnqueueLocalizedWarning(
                "notification.merge.selection_invalid.title",
                "notification.merge.selection_invalid.message",
                replaceCurrent: true);
            StatusText = L("status.merge.selection_invalid");
            return;
        }

        await CreateMergedDocumentPreviewAsync(
            BuildSuggestedMergedPdfFileName(sourcePaths),
            getTemporaryDirectory => BuildDroppedMergeSourcePathsAsync(
                sourcePaths,
                insertionIndex,
                getTemporaryDirectory));
    }

    private async Task CreateMergedDocumentPreviewAsync(
        string suggestedFileName,
        Func<Func<string>, Task<(string[]? SourcePaths, AppError? Error)>> resolveMergeSourcePathsAsync)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);
        ArgumentNullException.ThrowIfNull(resolveMergeSourcePathsAsync);

        string? sourceTemporaryDirectory = null;
        var previewDirectory = CreateMergeTemporaryDirectory();
        var previewOutputPath = Path.Combine(previewDirectory, suggestedFileName);
        var keepPreviewDirectory = false;

        try
        {
            SetPdfStructureOperationState(true);

            var mergeSourcePaths = await resolveMergeSourcePathsAsync(() =>
            {
                sourceTemporaryDirectory ??= CreateMergeTemporaryDirectory();
                return sourceTemporaryDirectory;
            });
            if (mergeSourcePaths.Error is not null)
            {
                EnqueueLocalizedError(mergeSourcePaths.Error, "error.save.merged_pdf.title", "error.save.document.message");
                StatusText = L("error.save.merged_pdf.title");
                return;
            }

            var result = await _mergePdfDocumentsUseCase.ExecuteAsync(
                new MergePdfDocumentsRequest(mergeSourcePaths.SourcePaths!, previewOutputPath));
            if (result.IsFailure)
            {
                EnqueueLocalizedError(result.Error, "error.save.merged_pdf.title", "error.save.document.message");
                StatusText = L("error.save.merged_pdf.title");
                return;
            }

            var previewPath = result.Value ?? previewOutputPath;
            await OpenDocumentFromPathAsync(
                previewPath,
                addToRecentFiles: false,
                displayFileName: suggestedFileName);

            if (string.IsNullOrWhiteSpace(CurrentDocumentPath) ||
                !PathsEqual(CurrentDocumentPath, previewPath))
            {
                return;
            }

            SetPendingMergedDocumentSave(previewDirectory, suggestedFileName);
            keepPreviewDirectory = true;
            StatusText = L("status.merge.ready_to_save");
        }
        finally
        {
            TryDeleteDirectory(sourceTemporaryDirectory);

            if (!keepPreviewDirectory)
            {
                TryDeleteDirectory(previewDirectory);
            }

            SetPdfStructureOperationState(false);
        }
    }

    private async Task SavePendingMergedDocumentAsync(string currentDocumentPath)
    {
        var suggestedFileName = string.IsNullOrWhiteSpace(EditableDocumentName)
            ? _pendingMergedDocumentSuggestedFileName ?? BuildSuggestedPdfFileName()
            : BuildSuggestedPdfFileName();

        var outputPath = _pendingMergedDocumentRequiresSavePicker
            ? await _filePickerService.PickSavePdfFileAsync(
                L("dialog.save.merged_pdf"),
                suggestedFileName)
            : _pendingMergedDocumentTargetPath;
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            StatusText = L("status.save.cancelled");
            return;
        }

        string? temporaryDirectory = null;
        var successStatusKey = _pendingMergedDocumentSaveSuccessStatusKey;

        try
        {
            SetPdfStructureOperationState(true);

            var outputSourcePath = currentDocumentPath;
            var pendingRotations = GetPendingPdfRotationGroups();
            var hasPendingReorder = HasPendingPageReorder;
            var hasPendingAnnotations = HasPendingAnnotationChanges;

            if (pendingRotations.Count > 0 || hasPendingReorder || hasPendingAnnotations)
            {
                temporaryDirectory = Path.Combine(
                    Path.GetTempPath(),
                    "velune-merged-pdf-save",
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temporaryDirectory);
            }

            if (pendingRotations.Count > 0 || hasPendingReorder)
            {
                var orderedPages = Thumbnails
                    .Select(thumbnail => thumbnail.SourcePageNumber)
                    .ToArray();
                var structuredPath = Path.Combine(temporaryDirectory!, $"structured-{suggestedFileName}");
                var structureResult = await SavePdfDocumentChangesAsync(
                    currentDocumentPath,
                    structuredPath,
                    pendingRotations,
                    orderedPages,
                    hasPendingReorder);
                if (structureResult.IsFailure)
                {
                    EnqueueLocalizedError(structureResult.Error, "error.save.document.title", "error.save.document.message");
                    StatusText = L("error.save.document.title");
                    return;
                }

                outputSourcePath = structureResult.Value ?? structuredPath;
            }

            if (hasPendingAnnotations)
            {
                if (_documentSessionStore.Current is not { } session)
                {
                    return;
                }

                var annotatedPath = Path.Combine(temporaryDirectory!, suggestedFileName);
                var annotationResult = await _pdfMarkupService.ApplyAnnotationsAsync(
                    new ApplyPdfAnnotationsRequest(
                        session,
                        outputSourcePath,
                        annotatedPath,
                        CloneAnnotations(_annotations)));
                if (annotationResult.IsFailure)
                {
                    EnqueueLocalizedError(annotationResult.Error, "error.save.document.title", "error.save.document.message");
                    StatusText = L("error.save.document.title");
                    return;
                }

                outputSourcePath = annotationResult.Value ?? annotatedPath;
            }

            if (!PathsEqual(outputSourcePath, outputPath))
            {
                File.Copy(outputSourcePath, outputPath, overwrite: true);
            }

            var previewDirectory = _pendingMergedDocumentTemporaryDirectory;
            ClearPendingMergedDocumentSave(deleteTemporaryDirectory: false);
            await OpenDocumentFromPathAsync(outputPath);

            if (!string.IsNullOrWhiteSpace(CurrentDocumentPath) &&
                PathsEqual(CurrentDocumentPath, outputPath))
            {
                TryDeleteDirectory(previewDirectory);
                StatusText = L(successStatusKey);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            EnqueueLocalizedError(AppError.Infrastructure("document.save.copy_failed", exception.Message), "error.save.document.title", "error.save.document.message");
            StatusText = L("error.save.document.title");
        }
        finally
        {
            TryDeleteDirectory(temporaryDirectory);
            SetPdfStructureOperationState(false);
        }
    }

    private void SetPendingMergedDocumentSave(
        string temporaryDirectory,
        string suggestedFileName,
        bool requiresSavePicker = true,
        string? targetPath = null,
        string saveSuccessStatusKey = "status.save.merged_pdf")
    {
        _pendingMergedDocumentTemporaryDirectory = temporaryDirectory;
        _pendingMergedDocumentSuggestedFileName = suggestedFileName;
        _pendingMergedDocumentTargetPath = targetPath;
        _pendingMergedDocumentRequiresSavePicker = requiresSavePicker;
        _pendingMergedDocumentSaveSuccessStatusKey = saveSuccessStatusKey;
        _hasPendingMergedDocumentSave = true;
        NotifySaveDocumentStateChanged();
    }

    private void ClearPendingMergedDocumentSave(bool deleteTemporaryDirectory)
    {
        var temporaryDirectory = _pendingMergedDocumentTemporaryDirectory;
        var hadPendingSave = _hasPendingMergedDocumentSave;

        _pendingMergedDocumentTemporaryDirectory = null;
        _pendingMergedDocumentSuggestedFileName = null;
        _pendingMergedDocumentTargetPath = null;
        _pendingMergedDocumentRequiresSavePicker = true;
        _pendingMergedDocumentSaveSuccessStatusKey = "status.save.merged_pdf";
        _hasPendingMergedDocumentSave = false;

        if (hadPendingSave)
        {
            NotifySaveDocumentStateChanged();
        }

        if (deleteTemporaryDirectory)
        {
            TryDeleteDirectory(temporaryDirectory);
        }
    }

    private void NotifySaveDocumentStateChanged()
    {
        OnPropertyChanged(nameof(CanSaveDocument));
        SaveDocumentCommand.NotifyCanExecuteChanged();
    }

    private async Task<(string[]? SourcePaths, AppError? Error)> BuildDroppedMergeSourcePathsAsync(
        string[] sourcePaths,
        int insertionIndex,
        Func<string> getTemporaryDirectory)
    {
        var currentDocumentPath = sourcePaths[0];
        var droppedSourcePaths = sourcePaths.Skip(1).ToArray();

        if (!IsPdfDocument || TotalPages <= 1)
        {
            return insertionIndex <= 0
                ? ([.. droppedSourcePaths, currentDocumentPath], null)
                : ([currentDocumentPath, .. droppedSourcePaths], null);
        }

        var normalizedInsertionIndex = Math.Clamp(insertionIndex, 0, TotalPages);
        if (normalizedInsertionIndex <= 0)
        {
            return ([.. droppedSourcePaths, currentDocumentPath], null);
        }

        if (normalizedInsertionIndex >= TotalPages)
        {
            return ([currentDocumentPath, .. droppedSourcePaths], null);
        }

        var temporaryDirectory = getTemporaryDirectory();
        var beforePath = Path.Combine(temporaryDirectory, "current-before-drop.pdf");
        var afterPath = Path.Combine(temporaryDirectory, "current-after-drop.pdf");

        var beforeResult = await _extractPdfPagesUseCase.ExecuteAsync(
            new ExtractPdfPagesRequest(
                currentDocumentPath,
                beforePath,
                Enumerable.Range(1, normalizedInsertionIndex).ToArray()));
        if (beforeResult.IsFailure)
        {
            return (null, beforeResult.Error);
        }

        var afterResult = await _extractPdfPagesUseCase.ExecuteAsync(
            new ExtractPdfPagesRequest(
                currentDocumentPath,
                afterPath,
                Enumerable.Range(
                    normalizedInsertionIndex + 1,
                    TotalPages - normalizedInsertionIndex).ToArray()));
        if (afterResult.IsFailure)
        {
            return (null, afterResult.Error);
        }

        return ([beforePath, .. droppedSourcePaths, afterPath], null);
    }

    private static bool IsSupportedMergeSourcePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        return !string.IsNullOrWhiteSpace(extension) &&
            SupportedDocumentFormats.IsSupported(extension);
    }

    private static string CreateMergeTemporaryDirectory()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            "velune-drop-merge",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    private static void TryDeleteDirectory(string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) ||
            !Directory.Exists(directoryPath))
        {
            return;
        }

        try
        {
            Directory.Delete(directoryPath, recursive: true);
        }
        catch
        {
            // Best-effort cleanup for temporary current-document page slices.
        }
    }

    [RelayCommand(CanExecute = nameof(HasOpenDocument))]
    private void BeginDocumentNameEdit()
    {
        EditableDocumentName = ResolveRequestedDocumentFileName();
        IsEditingDocumentName = true;
        StatusText = L("status.save_name.editing");
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
            StatusText = L("status.save_name.unchanged");
            return;
        }

        if (!TryNormalizeRequestedDocumentFileName(proposedName, out var normalizedFileName, out var validationError))
        {
            EnqueueWarning(L("status.save_name.invalid"), validationError ?? L("validation.file_name.enter"));
            StatusText = L("status.save_name.invalid");
            return;
        }

        EditableDocumentName = normalizedFileName;
        IsEditingDocumentName = false;
        StatusText = L("status.save_name.updated");
    }

    [RelayCommand(CanExecute = nameof(HasOpenDocument))]
    private void CancelDocumentNameEdit()
    {
        EditableDocumentName = CurrentDocumentName ?? string.Empty;
        IsEditingDocumentName = false;
        StatusText = L("status.save_name.unchanged");
    }

    [RelayCommand(CanExecute = nameof(IsSearchAvailableForCurrentDocument))]
    private async Task OpenSearchAsync()
    {
        if (!IsSearchAvailableForCurrentDocument)
        {
            return;
        }

        IsInfoPanelVisible = false;
        IsAnnotationsPanelVisible = false;
        IsPreferencesPanelVisible = false;
        IsPrintPanelVisible = false;
        IsSearchPanelVisible = true;

        if (string.IsNullOrWhiteSpace(SearchQueryInput))
        {
            StatusText = L("status.search.shown");
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
            StatusText = L("status.search.shown");
        }
    }

    [RelayCommand(CanExecute = nameof(CanToggleSidebar))]
    private void ToggleSidebar()
    {
        ShowThumbnailsPanelPreference = !ShowThumbnailsPanelPreference;
        StatusText = ShowThumbnailsPanelPreference
            ? L("status.pages_panel.shown")
            : L("status.pages_panel.hidden");
    }

    [RelayCommand(CanExecute = nameof(IsSearchAvailableForCurrentDocument))]
    private async Task ToggleSearchPanelAsync()
    {
        if (!IsSearchAvailableForCurrentDocument)
        {
            return;
        }

        if (IsSearchPanelVisible)
        {
            IsSearchPanelVisible = false;
            StatusText = L("status.search.hidden");
            return;
        }

        IsInfoPanelVisible = false;
        IsAnnotationsPanelVisible = false;
        IsPreferencesPanelVisible = false;
        IsPrintPanelVisible = false;
        IsSearchPanelVisible = true;
        StatusText = L("status.search.loading");

        await EnsureDocumentTextAvailableAsync(forceOcr: false);

        if (IsSearchPanelVisible &&
            !_requiresSearchOcr &&
            !_isAnalyzingDocumentText &&
            _currentDocumentTextIndex is not null)
        {
            StatusText = L("status.search.shown");
        }
    }

    [RelayCommand(CanExecute = nameof(CanSearchText))]
    private async Task SearchTextAsync()
    {
        if (_currentDocumentTextIndex is null)
        {
            if (_requiresSearchOcr)
            {
                EnqueueLocalizedInfo("search.prompt.recognize_first.title", "search.prompt.recognize_first.message");
                SearchPanelNotice = L("search.notice.no_text");
            }

            return;
        }

        var result = _searchDocumentTextUseCase.Execute(
            new SearchDocumentTextRequest(
                _currentDocumentTextIndex,
                new SearchQuery(SearchQueryInput)));

        if (result.IsFailure)
        {
            EnqueueLocalizedError(result.Error, "error.search.unavailable.title", "error.search.unavailable.message");
            return;
        }

        ApplySearchResults(result.Value ?? []);
        if (SearchResults.Count == 0)
        {
            SearchPanelNotice = L("search.notice.no_match", SearchQueryInput.Trim());
            ClearSearchHighlights();
            StatusText = L("status.search.none");
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
        SearchPanelNotice = L("search.notice.analysis_cancelled");
        StatusText = L("status.analysis.cancelled");
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
            ? L("status.info.shown")
            : L("status.info.hidden");
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
            ? L("status.preferences.shown")
            : L("status.preferences.hidden");
    }

    [RelayCommand(CanExecute = nameof(CanDismissUserMessage))]
    private void DismissMessage()
    {
        AdvanceNotificationQueue();
        StatusText = L("status.notification.dismissed");
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
            L("notification.page_order_updated.title"),
            L("notification.page_order_updated.message"),
            replaceCurrent: true);
        StatusText = L("status.page_order.updated");
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
        OnPropertyChanged(nameof(ScrollableDocumentHorizontalAlignment));

        if (!widthChanged && !heightChanged)
        {
            return;
        }

        if (CurrentZoomMode is ZoomMode.Custom)
        {
            return;
        }

        if (CurrentRenderedBitmap is null)
        {
            await TryApplyViewportFitAsync(CurrentZoomMode, preserveStatusText: true);
            return;
        }

        await DebounceViewportFitUpdateAsync();
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

    private async Task OpenDocumentFromPathAsync(
        string filePath,
        bool addToRecentFiles = true,
        string? displayFileName = null)
    {
        if (_hasPendingMergedDocumentSave &&
            !string.IsNullOrWhiteSpace(CurrentDocumentPath) &&
            !PathsEqual(CurrentDocumentPath, filePath))
        {
            ClearPendingMergedDocumentSave(deleteTemporaryDirectory: true);
        }

        CancelPendingViewportFitUpdate();
        CancelCurrentTextAnalysis();
        ClearDocumentTextSelection();
        ResetAnnotationWorkspace();
        _currentDocumentTextIndex = null;
        _requiresSearchOcr = false;
        SearchQueryInput = string.Empty;
        SearchPanelNotice = null;
        ClearSearchResults();
        ClearSearchHighlights();
        IsSearchPanelVisible = false;

        var result = await _openDocumentUseCase.ExecuteAsync(new OpenDocumentRequest(filePath));

        if (result.IsFailure)
        {
            EnqueueLocalizedError(result.Error, "error.open.failed.title", "error.open.failed.message");
            StatusText = L("status.open.failed");
            return;
        }

        RefreshFromSession();
        if (!string.IsNullOrWhiteSpace(displayFileName))
        {
            ApplyCurrentDocumentDisplayName(displayFileName);
        }

        HasPendingPageReorder = false;

        _pageViewportStore.Initialize(TotalPages > 0 ? TotalPages : 1);
        _pageViewportStore.SetActivePage(new PageIndex(0));
        RefreshPageViewState();

        ClearThumbnails();
        BuildThumbnailPlaceholders();

        if (addToRecentFiles)
        {
            AddCurrentDocumentToRecentFiles();
        }

        ClearNotifications();
        var detailsWarning = _documentSessionStore.Current?.Metadata.DetailsWarning;
        if (!string.IsNullOrWhiteSpace(detailsWarning))
        {
            EnqueueInfo(L("notification.document.details_unavailable.title"), detailsWarning);
        }

        StatusText = L("status.opened", CurrentDocumentName ?? string.Empty);

        await ApplyPreferredDefaultZoomAsync();

        _ = GenerateThumbnailsAsync();
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
                PrintPanelNotice = _localizedErrorFormatter
                    .Format(result.Error, "error.print.failed.title", "error.print.failed.message")
                    .Message;
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
                ? L("error.print.no_printers")
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
        if (!HasOpenDocument || !IsSearchAvailableForCurrentDocument)
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
            ? L("search.notice.recognizing")
            : L("search.notice.loading");

        try
        {
            var result = await handle.Completion;
            if (_currentDocumentTextJob?.JobId != handle.JobId)
            {
                return;
            }

            if (result.IsCanceled)
            {
                SearchPanelNotice = L("search.notice.analysis_cancelled");
                StatusText = L("status.analysis.cancelled");
                return;
            }

            if (result.IsFailure)
            {
                _currentDocumentTextIndex = null;
                _requiresSearchOcr = !forceOcr;
                ClearSearchResults();
                ClearSearchHighlights();
                var presentation = _localizedErrorFormatter.Format(
                    result.Error,
                    "error.text_analysis.failed.title",
                    "error.text_analysis.failed.message");
                SearchPanelNotice = presentation.Message;
                EnqueueError(presentation.Message, presentation.Title);
                StatusText = L("status.analysis.failed");
                return;
            }

            if (result.RequiresOcr)
            {
                _currentDocumentTextIndex = null;
                _requiresSearchOcr = true;
                ClearSearchResults();
                ClearSearchHighlights();
                SearchPanelNotice = L("search.notice.no_text");
                StatusText = L("status.ocr.required");
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
                    ? L("status.text.recognized")
                    : L("status.text.loaded");
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
        if (!HasOpenDocument || !IsSearchAvailableForCurrentDocument)
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
            StatusText = L("search.status.recognize_first");
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
            StatusText = L("status.search.result", item.PageNumber);
        }
    }

    private void ApplySearchResults(IReadOnlyList<SearchHit> hits)
    {
        ClearSearchResults();

        foreach (var hit in hits)
        {
            SearchResults.Add(new SearchResultItemViewModel(hit, hit.PageIndex.Value + 1, _localizationService));
        }

        _selectedSearchResultIndex = SearchResults.Count > 0 ? 0 : -1;
        NotifySearchStateChanged();
        OnPropertyChanged(nameof(SearchSelectionIndicator));
    }

    private void RefreshSearchResultLocalization()
    {
        foreach (var result in SearchResults)
        {
            result.UpdateLocalization(_localizationService);
        }
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
            StatusText = L("status.text.selected");
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

        var defaultZoomPreference = SelectedDefaultZoomPreference?.Value ?? DefaultZoomPreference.FitToPage;
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
        if (!IsPrintPageRangeVisible || SelectedPrintPageRangeOption?.Value is PrintPageRangeChoice.AllPages)
        {
            return null;
        }

        if (SelectedPrintPageRangeOption?.Value is PrintPageRangeChoice.CurrentPage)
        {
            return CurrentPage.ToString(CultureInfo.InvariantCulture);
        }

        if (SelectedPrintPageRangeOption?.Value is not PrintPageRangeChoice.CustomRange)
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

    private async Task ChangeToPageAsync(int pageNumber)
    {
        if (pageNumber < 1 || (TotalPages > 0 && pageNumber > TotalPages))
        {
            EnqueueWarning(L("status.page.invalid"), L("validation.page.range", TotalPages));
            StatusText = L("status.page.invalid");
            return;
        }

        var result = _changePageUseCase.Execute(
            new ChangePageRequest(new PageIndex(pageNumber - 1)));

        if (result.IsFailure)
        {
            EnqueueLocalizedError(result.Error, "error.page_change.failed.title", "error.page_change.failed.message");
            StatusText = L("status.page.change_failed");
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

        StatusText = L("status.page.current", CurrentPage);
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
                EnqueueLocalizedError(result.Error, "error.render.failed.title", "error.render.failed.message");
                StatusText = L("status.render.failed");
                return;
            }

            if (result.Page is null)
            {
                EnqueueLocalizedError(null, "error.render.empty.title", "error.render.empty.message");
                StatusText = L("status.render.failed");
                return;
            }

            CurrentRenderedBitmap?.Dispose();
            CurrentRenderedBitmap = RenderedPageBitmapFactory.Create(result.Page);
            renderSucceeded = true;
            StatusText = preserveStatusText ? previousStatusText : L("status.page.rendered", CurrentPage);
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
        RefreshThumbnailLocalization();
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

    private void RefreshThumbnailLocalization()
    {
        foreach (var item in Thumbnails)
        {
            item.UpdateLocalization(_localizationService);
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
        CurrentDocumentType = GetLocalizedDocumentFormatLabel(session.Metadata);
        CurrentDocumentPath = session.Metadata.FilePath;
        EditableDocumentName = session.Metadata.FileName;
        IsEditingDocumentName = false;
        CurrentPage = session.Viewport.CurrentPage.Value + 1;
        TotalPages = session.Metadata.PageCount ?? 1;
        RefreshDocumentInfo(session.Metadata);

        EmptyStateTitle = session.Metadata.FileName;
        EmptyStateDescription = L("document.empty.opened", GetDocumentKindLabel(session.Metadata.DocumentType));
    }

    private void ApplyCurrentDocumentDisplayName(string fileName)
    {
        CurrentDocumentName = fileName;
        EditableDocumentName = fileName;
        EmptyStateTitle = fileName;
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
        ClearPendingMergedDocumentSave(deleteTemporaryDirectory: true);
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
        IsAnnotationsPanelVisible = false;
        _currentDocumentTextSelection = null;
        _documentTextSelectionAnchorPoint = null;
        _currentDocumentTextIndex = null;
        ResetAnnotationWorkspace();
        ClearDocumentInfo();
        SearchPanelNotice = null;
        PrintPanelNotice = null;
        PrintDestinations.Clear();
        SelectedPrintDestination = null;
        SelectedPrintPageRangeOption = FindOption(_printPageRangeOptions, PrintPageRangeChoice.AllPages);
        PrintCustomPageRange = string.Empty;
        PrintCopiesInput = "1";
        SelectedPrintOrientationOption = FindOption(_printOrientationOptions, PrintOrientationOption.Automatic);
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

        EmptyStateTitle = L("app.empty.title");
        EmptyStateDescription = L("app.empty.description");
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
            EnqueueLocalizedError(result.Error, "error.zoom_update.failed.title", "error.zoom_update.failed.message");
            StatusText = L("status.zoom.update_failed");
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
            EnqueueLocalizedError(result.Error, "error.rotation_update.failed.title", "error.rotation_update.failed.message");
            StatusText = L("status.rotation.update_failed");
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
            L("notification.page_order_updated.title"),
            L("notification.page_order_updated.message"),
            replaceCurrent: true);
        StatusText = L("status.page_order.updated");
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
            StatusText = L("status.save.cancelled");
            return;
        }

        try
        {
            SetPdfStructureOperationState(true);

            var result = await executeOperation(outputPath);
            if (result.IsFailure)
            {
                EnqueueLocalizedError(result.Error, "error.save.document.title", "error.save.document.message");
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
        MergePdfDocumentsCommand.NotifyCanExecuteChanged();
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
            EnqueueWarning(L("status.save_name.invalid"), validationError ?? L("validation.file_name.save_default"));
            StatusText = L("status.save_name.invalid");
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
                EnqueueLocalizedError(result.Error, "error.save.document.title", "error.save.document.message");
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
                EnqueueLocalizedError(AppError.Infrastructure("document.save.copy_failed", ex.Message), "error.save.document.title", "error.save.document.message");
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
            title: L("dialog.extract_page"),
            suggestedFileName: BuildSuggestedPdfFileName(),
            executeOperation: outputPath => _extractPdfPagesUseCase.ExecuteAsync(
                new ExtractPdfPagesRequest(
                    currentDocumentPath,
                    outputPath,
                    [sourcePageNumber])),
            successStatus: L("status.page.extracted", pageLabel),
            failureTitle: L("error.page.extract_failed.title"));
    }

    private async Task DeletePageAsync(int sourcePageNumber, string pageLabel)
    {
        var currentDocumentPath = CurrentDocumentPath;
        if (string.IsNullOrWhiteSpace(currentDocumentPath))
        {
            return;
        }

        var hasExistingPendingSave = _hasPendingMergedDocumentSave;
        var requiresSavePicker = hasExistingPendingSave && _pendingMergedDocumentRequiresSavePicker;
        var targetDocumentPath = requiresSavePicker
            ? null
            : _pendingMergedDocumentTargetPath ?? currentDocumentPath;
        var suggestedFileName = BuildSuggestedPdfFileName();
        var previewDirectory = CreateMergeTemporaryDirectory();
        var previewOutputPath = Path.Combine(previewDirectory, suggestedFileName);
        var keepPreviewDirectory = false;

        try
        {
            SetPdfStructureOperationState(true);

            var result = await _deletePdfPagesUseCase.ExecuteAsync(
                new DeletePdfPagesRequest(
                    currentDocumentPath,
                    previewOutputPath,
                    [sourcePageNumber]));
            if (result.IsFailure)
            {
                EnqueueLocalizedError(result.Error, "error.page.delete_failed.title", "error.save.document.message");
                StatusText = L("error.page.delete_failed.title");
                return;
            }

            var displayFileName = CurrentDocumentName ?? suggestedFileName;
            var previewPath = result.Value ?? previewOutputPath;
            await OpenDocumentFromPathAsync(
                previewPath,
                addToRecentFiles: false,
                displayFileName: displayFileName);

            if (string.IsNullOrWhiteSpace(CurrentDocumentPath) ||
                !PathsEqual(CurrentDocumentPath, previewPath))
            {
                return;
            }

            SetPendingMergedDocumentSave(
                previewDirectory,
                suggestedFileName,
                requiresSavePicker: requiresSavePicker,
                targetPath: targetDocumentPath,
                saveSuccessStatusKey: requiresSavePicker ? "status.save.merged_pdf" : "status.document.saved");
            keepPreviewDirectory = true;
            StatusText = L("status.page.deleted_from_pdf", pageLabel);
        }
        finally
        {
            if (!keepPreviewDirectory)
            {
                TryDeleteDirectory(previewDirectory);
            }

            SetPdfStructureOperationState(false);
        }
    }

    private string BuildSuggestedPdfFileName()
    {
        var resolvedFileName = ResolveRequestedDocumentFileName();
        return string.IsNullOrWhiteSpace(resolvedFileName)
            ? L("document.file_name.default")
            : resolvedFileName;
    }

    private string BuildSuggestedMergedPdfFileName(IReadOnlyList<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);

        var firstSourcePath = sourcePaths.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
        var baseFileName = string.IsNullOrWhiteSpace(firstSourcePath)
            ? Path.GetFileNameWithoutExtension(L("document.file_name.default"))
            : Path.GetFileNameWithoutExtension(firstSourcePath);

        if (string.IsNullOrWhiteSpace(baseFileName))
        {
            baseFileName = Path.GetFileNameWithoutExtension(L("document.file_name.default"));
        }

        return $"{baseFileName}-merged.pdf";
    }

    private string ResolveRequestedDocumentFileName()
    {
        var candidate = string.IsNullOrWhiteSpace(EditableDocumentName)
            ? Path.GetFileName(CurrentDocumentName)
            : EditableDocumentName.Trim();

        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = L("document.file_name.fallback");
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
            validationError = L("validation.file_name.empty");
            return false;
        }

        candidate = candidate.Trim();

        if (candidate.Contains(Path.DirectorySeparatorChar) ||
            candidate.Contains(Path.AltDirectorySeparatorChar))
        {
            validationError = L("validation.file_name.name_only");
            return false;
        }

        if (candidate.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            validationError = L("validation.file_name.unsupported");
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
            validationError = L("validation.file_name.directory_missing");
            return false;
        }

        targetDocumentPath = Path.Combine(currentDirectory, targetFileName);
        if (!PathsEqual(currentDocumentPath, targetDocumentPath) && File.Exists(targetDocumentPath))
        {
            validationError = L("validation.file_name.exists", targetFileName);
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
        CancelPendingViewportFitUpdate();
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
            var updatedPreferences = BuildUserPreferencesFromUi();
            var languageChanged = _userPreferencesService.Current.Language != updatedPreferences.Language;

            await _userPreferencesService.SaveAsync(updatedPreferences);

            if (applyDefaultZoomToOpenDocument && HasOpenDocument)
            {
                await ApplyPreferredDefaultZoomAsync(preserveStatusText: true);
            }

            if (languageChanged && PresentationPlatform.IsMacOS)
            {
                EnqueueLocalizedInfo(
                    "notification.language.restart_required.title",
                    "notification.language.restart_required.message",
                    replaceCurrent: true);
            }

            StatusText = L("status.preferences.updated");
        }
        catch
        {
            ApplyPreferencesToUi(_userPreferencesService.Current);
            EnqueueLocalizedError(null, "error.preferences.not_saved.title", "error.preferences.not_saved.message");
            StatusText = L("status.preferences.not_saved");
        }
    }

    private UserPreferences BuildUserPreferencesFromUi()
    {
        return new UserPreferences
        {
            Language = SelectedLanguagePreference?.Value ?? AppLanguagePreference.System,
            Theme = SelectedThemePreference?.Value ?? AppThemePreference.System,
            DefaultZoom = SelectedDefaultZoomPreference?.Value ?? DefaultZoomPreference.FitToPage,
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
            RebuildLocalizedOptions();
            SelectedLanguagePreference = FindOption(_languagePreferenceOptions, preferences.Language);
            SelectedThemePreference = FindOption(_themePreferenceOptions, preferences.Theme);
            SelectedDefaultZoomPreference = FindOption(_defaultZoomPreferenceOptions, preferences.DefaultZoom);
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

    private static LocalizedOption<TValue>? FindOption<TValue>(
        IReadOnlyList<LocalizedOption<TValue>> options,
        TValue value)
        where TValue : struct
    {
        return options.FirstOrDefault(option => EqualityComparer<TValue>.Default.Equals(option.Value, value));
    }

    private void RebuildLocalizedOptions()
    {
        _languagePreferenceOptions =
        [
            new LocalizedOption<AppLanguagePreference>(AppLanguagePreference.System, L("preferences.language.system")),
            new LocalizedOption<AppLanguagePreference>(AppLanguagePreference.English, L("preferences.language.english")),
            new LocalizedOption<AppLanguagePreference>(AppLanguagePreference.French, L("preferences.language.french")),
            new LocalizedOption<AppLanguagePreference>(AppLanguagePreference.Spanish, L("preferences.language.spanish"))
        ];
        _themePreferenceOptions =
        [
            new LocalizedOption<AppThemePreference>(AppThemePreference.System, L("preferences.theme.system")),
            new LocalizedOption<AppThemePreference>(AppThemePreference.Light, L("preferences.theme.light")),
            new LocalizedOption<AppThemePreference>(AppThemePreference.Dark, L("preferences.theme.dark"))
        ];
        _defaultZoomPreferenceOptions =
        [
            new LocalizedOption<DefaultZoomPreference>(DefaultZoomPreference.FitToPage, L("preferences.zoom.fit_page")),
            new LocalizedOption<DefaultZoomPreference>(DefaultZoomPreference.FitToWidth, L("preferences.zoom.fit_width")),
            new LocalizedOption<DefaultZoomPreference>(DefaultZoomPreference.ActualSize, L("preferences.zoom.actual_size"))
        ];
        _printPageRangeOptions =
        [
            new LocalizedOption<PrintPageRangeChoice>(PrintPageRangeChoice.AllPages, L("preferences.print.pages.all")),
            new LocalizedOption<PrintPageRangeChoice>(PrintPageRangeChoice.CurrentPage, L("preferences.print.pages.current")),
            new LocalizedOption<PrintPageRangeChoice>(PrintPageRangeChoice.CustomRange, L("preferences.print.pages.custom"))
        ];
        _printOrientationOptions =
        [
            new LocalizedOption<PrintOrientationOption>(PrintOrientationOption.Automatic, L("preferences.print.orientation.automatic")),
            new LocalizedOption<PrintOrientationOption>(PrintOrientationOption.Portrait, L("preferences.print.orientation.portrait")),
            new LocalizedOption<PrintOrientationOption>(PrintOrientationOption.Landscape, L("preferences.print.orientation.landscape"))
        ];

        OnPropertyChanged(nameof(LanguagePreferenceOptions));
        OnPropertyChanged(nameof(ThemePreferenceOptions));
        OnPropertyChanged(nameof(DefaultZoomPreferenceOptions));
        OnPropertyChanged(nameof(PrintPageRangeOptions));
        OnPropertyChanged(nameof(PrintOrientationOptions));
    }

    private sealed record PendingPdfRotationGroup(
        Rotation Rotation,
        IReadOnlyList<int> Pages);

    private void ConfirmClearRecentFiles()
    {
        _recentFilesService.Clear();
        RefreshRecentFiles();
        EnqueueLocalizedInfo("notification.recent_cleared.title", "notification.recent_cleared.message");
        StatusText = L("status.recent_files.cleared");
    }

    private string L(string key) => _localizationService.GetString(key);

    private string L(string key, params object?[] arguments) => _localizationService.GetString(key, arguments);

    private void EnqueueLocalizedInfo(string titleKey, string messageKey, bool replaceCurrent = false, params object?[] messageArguments)
    {
        EnqueueInfo(L(titleKey), L(messageKey, messageArguments), replaceCurrent);
    }

    private void EnqueueLocalizedWarning(string titleKey, string messageKey, bool replaceCurrent = false, params object?[] messageArguments)
    {
        EnqueueWarning(L(titleKey), L(messageKey, messageArguments), replaceCurrent);
    }

    private void EnqueueLocalizedError(
        AppError? error,
        string fallbackTitleKey,
        string fallbackMessageKey,
        bool replaceCurrent = false,
        params object?[] fallbackArguments)
    {
        var presentation = _localizedErrorFormatter.Format(error, fallbackTitleKey, fallbackMessageKey, fallbackArguments);
        EnqueueError(presentation.Message, presentation.Title, replaceCurrent);
    }

    private void EnqueueInfo(string title, string message, bool replaceCurrent = false)
    {
        EnqueueNotification(new NotificationEntry(title, message, NotificationKind.Info), replaceCurrent);
    }

    private void EnqueueWarning(string title, string message, bool replaceCurrent = false)
    {
        EnqueueNotification(new NotificationEntry(title, message, NotificationKind.Warning), replaceCurrent);
    }

    private void EnqueueError(string message, string? title = null, bool replaceCurrent = false)
    {
        EnqueueNotification(new NotificationEntry(title ?? L("error.non_fatal.title"), message, NotificationKind.Error), replaceCurrent);
    }

    private void EnqueueConfirmation(
        string title,
        string message,
        string confirmLabel,
        Action onConfirm,
        string? cancelLabel = null)
    {
        EnqueueNotification(
            new NotificationEntry(
                title,
                message,
                NotificationKind.Confirmation,
                confirmLabel,
                onConfirm,
                cancelLabel ?? L("app.cancel"),
                () => StatusText = L("notification.action.cancelled"),
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
            RecentFiles.Add(item with
            {
                DocumentType = GetLocalizedRecentFileType(item)
            });
        }

        OnPropertyChanged(nameof(HasRecentFiles));
        ClearRecentFilesCommand.NotifyCanExecuteChanged();
    }

    private string GetLocalizedRecentFileType(RecentFileItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var extension = Path.GetExtension(item.FilePath);
        if (Velune.Application.Documents.SupportedDocumentFormats.IsPdf(extension))
        {
            return L("document.type.pdf");
        }

        if (Velune.Application.Documents.SupportedDocumentFormats.IsImage(extension))
        {
            return GetImageFormatLabel(extension);
        }

        return L("document.type.document");
    }

    private void NotifySidebarVisibilityChanged()
    {
        OnPropertyChanged(nameof(SidebarHostVisible));
        OnPropertyChanged(nameof(IsSidebarVisible));
        OnPropertyChanged(nameof(SidebarColumnWidth));
        OnPropertyChanged(nameof(SidebarWidth));
        OnPropertyChanged(nameof(SidebarOpacity));
        OnPropertyChanged(nameof(IsSidebarInteractive));
        OnPropertyChanged(nameof(SidebarToggleLabel));
    }

    private async Task DebounceViewportFitUpdateAsync()
    {
        CancelPendingViewportFitUpdate();

        var cancellationSource = new CancellationTokenSource();
        _pendingViewportFitUpdateCancellation = cancellationSource;

        try
        {
            await Task.Delay(ViewportFitDebounceDelay, cancellationSource.Token);

            if (_disposed || _pendingViewportFitUpdateCancellation != cancellationSource)
            {
                return;
            }

            await TryApplyViewportFitAsync(CurrentZoomMode, preserveStatusText: true);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (_pendingViewportFitUpdateCancellation == cancellationSource)
            {
                _pendingViewportFitUpdateCancellation = null;
            }

            cancellationSource.Dispose();
        }
    }

    private void CancelPendingViewportFitUpdate()
    {
        if (_pendingViewportFitUpdateCancellation is null)
        {
            return;
        }

        _pendingViewportFitUpdateCancellation.Cancel();
        _pendingViewportFitUpdateCancellation.Dispose();
        _pendingViewportFitUpdateCancellation = null;
    }

    private void RefreshDocumentInfo(DocumentMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        DocumentInfoItems.Clear();

        AddDocumentInfo(L("document.kind.label"), GetLocalizedDocumentFormatLabel(metadata));
        AddDocumentInfo(L("document.size.label"), FormatFileSize(metadata.FileSizeInBytes));

        if (metadata.PageCount is > 0)
        {
            AddDocumentInfo(L("document.pages.label"), metadata.PageCount.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (metadata.PixelWidth is > 0 && metadata.PixelHeight is > 0)
        {
            AddDocumentInfo(L("document.dimensions.label"), $"{metadata.PixelWidth} × {metadata.PixelHeight} px");
        }

        if (metadata.CreatedAt is not null)
        {
            AddDocumentInfo(L("document.created.label"), FormatDate(metadata.CreatedAt.Value));
        }

        if (metadata.ModifiedAt is not null)
        {
            AddDocumentInfo(L("document.modified.label"), FormatDate(metadata.ModifiedAt.Value));
        }

        AddDocumentInfo(L("document.title.label"), metadata.DocumentTitle);
        AddDocumentInfo(L("document.author.label"), metadata.Author);
        AddDocumentInfo(L("document.creator.label"), metadata.Creator);
        AddDocumentInfo(L("document.producer.label"), metadata.Producer);
        AddDocumentInfo(L("document.location.label"), Path.GetDirectoryName(metadata.FilePath));

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

    private string GetLocalizedDocumentFormatLabel(DocumentMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return metadata.DocumentType switch
        {
            DocumentType.Pdf => L("document.type.pdf"),
            DocumentType.Image => GetImageFormatLabel(Path.GetExtension(metadata.FilePath)),
            _ => L("document.type.document")
        };
    }

    private string GetImageFormatLabel(string extension)
    {
        return Path.GetExtension(extension).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => L("document.type.jpeg"),
            ".png" => L("document.type.png"),
            ".webp" => L("document.type.webp"),
            _ => L("document.type.image")
        };
    }

    private string GetDocumentKindLabel(DocumentType documentType)
    {
        return documentType switch
        {
            DocumentType.Pdf => L("document.type.pdf"),
            DocumentType.Image => L("document.type.image"),
            _ => L("document.type.document")
        };
    }

    private static string FormatDate(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("f", CultureInfo.CurrentCulture);
    }

    private string FormatFileSize(long fileSizeInBytes)
    {
        string[] units =
        [
            L("document.file_size.bytes"),
            L("document.file_size.kb"),
            L("document.file_size.mb"),
            L("document.file_size.gb"),
            L("document.file_size.tb")
        ];
        var size = (double)fileSizeInBytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        var format = unitIndex == 0 ? "0" : "0.#";
        return $"{size.ToString(format, CultureInfo.CurrentCulture)} {units[unitIndex]}";
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
            _localizationService.LanguageChanged -= OnLanguageChanged;
            CancelPendingViewportFitUpdate();
            CancelCurrentRender();
            CancelCurrentTextAnalysis();
            CancelThumbnailGeneration();
            ClearPendingMergedDocumentSave(deleteTemporaryDirectory: true);

            CurrentRenderedBitmap?.Dispose();
            CurrentRenderedBitmap = null;

            ClearThumbnails();
        }

        _disposed = true;
    }

}
