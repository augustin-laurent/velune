using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Velune.Application.Abstractions;
using Velune.Application.Annotations;
using Velune.Application.Configuration;
using Velune.Application.Documents;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Application.Text;
using Velune.Application.UseCases;
using Velune.Domain.Abstractions;
using Velune.Domain.Annotations;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Windows.Services;
using Velune.Windows.ViewModels.UndoSystem;

namespace Velune.Windows.ViewModels;

/// <summary>
/// Primary view model for the workspace window, orchestrating documents, annotations, search, and preferences.
/// </summary>
public sealed partial class WindowsMainViewModel : ObservableObject, IDisposable
{
    private const string RenderFailed = "status.render.failed";

    private const int MaxOpenDocumentTabs = 8;
    private const double DefaultViewerZoom = 1.35;
    private const double ViewerHorizontalPadding = 80;
    private const double ViewerVerticalPadding = 40;
    private const double ThumbnailZoom = 0.20;
    private const int ThumbnailWidth = 260;
    private const int ThumbnailHeight = 340;
    private const double AnnotationDefaultWidthRatio = 0.24;
    private const double AnnotationDefaultHeightRatio = 0.12;
    private const double SignatureDefaultWidthRatio = 0.24;
    private const double SignatureDefaultHeightRatio = 0.10;
    private const double SignaturePadPreviewWidth = 248;
    private const double SignaturePadPreviewHeight = 124;

    private readonly OpenDocumentUseCase _openDocumentUseCase;
    private readonly CloseDocumentUseCase _closeDocumentUseCase;
    private readonly MergePdfDocumentsUseCase _mergePdfDocumentsUseCase;
    private readonly ExtractPdfPagesUseCase _extractPdfPagesUseCase;
    private readonly ReorderPdfPagesUseCase _reorderPdfPagesUseCase;
    private readonly RotatePdfPagesUseCase _rotatePdfPagesUseCase;
    private readonly LoadDocumentTextUseCase _loadDocumentTextUseCase;
    private readonly RunDocumentOcrUseCase _runDocumentOcrUseCase;
    private readonly SearchDocumentTextUseCase _searchDocumentTextUseCase;
    private readonly ResolveDocumentTextSelectionUseCase _resolveDocumentTextSelectionUseCase;
    private readonly IDocumentSessionStore _documentSessionStore;
    private readonly IRecentFilesService _recentFilesService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly IRenderOrchestrator _renderOrchestrator;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly IWindowsFileDialogService _fileDialogService;
    private readonly IWindowsPrintCoordinator _printCoordinator;
    private readonly IWindowsTextCatalog _textCatalog;
    private readonly List<NormalizedPoint> _activeAnnotationPoints = [];
    private readonly List<NormalizedPoint> _signatureCapturePoints = [];
    private readonly Dictionary<string, SignatureAsset> _signatureAssetLookup = new(StringComparer.Ordinal);
    private readonly ISignatureAssetStore _signatureAssetStore;
    private readonly IPdfMarkupService _pdfMarkupService;
    private readonly IPdfAnnotationStore _pdfAnnotationStore;
    private readonly SemaphoreSlim _documentOpenGate = new(1, 1);
    private readonly UndoRedoManager _undoStack;
    private double _documentViewerWidth;
    private double _documentViewerHeight;
    private bool _isUndoRedoInProgress;
    private NormalizedPoint? _activeAnnotationStartPoint;
    private Guid? _previewAnnotationId;
    private readonly bool _isApplyingThumbnailPreference;
    private bool _isApplyingPreferenceSelection;
    private Guid? _movingAnnotationId;
    private NormalizedPoint? _movingAnnotationStartPoint;
    private NormalizedTextRegion? _movingAnnotationOriginalBounds;
    private IReadOnlyList<NormalizedPoint>? _movingAnnotationOriginalPoints;
    private bool _editingExistingTextAnnotation;
    private Guid? _resizingAnnotationId;
    private NormalizedPoint? _resizingAnnotationStartPoint;
    private NormalizedTextRegion? _resizingAnnotationOriginalBounds;
    private IReadOnlyList<NormalizedPoint>? _resizingAnnotationOriginalPoints;
    private ResizeHandle _resizingHandle;
    private bool _isRotatingAnnotation;
    private double _rotatingAnnotationStartAngle;
    private double _rotatingAnnotationOriginalRotation;
    private double _rotatingAnnotationCenterX;
    private double _rotatingAnnotationCenterY;
    private DocumentAnnotation? _interactionAnnotationSnapshot;

    private enum ResizeHandle
    {
        None,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Rotate
    }

    private enum RightPanel
    {
        None,
        Search,
        Annotations,
        Info,
        Settings
    }

    private enum ThumbnailRenderOutcome
    {
        Rendered,
        Failed,
        Deferred
    }

    /// <summary>
    /// Initializes the main view model with all required application services.
    /// </summary>
    public WindowsMainViewModel(
        OpenDocumentUseCase openDocumentUseCase,
        CloseDocumentUseCase closeDocumentUseCase,
        MergePdfDocumentsUseCase mergePdfDocumentsUseCase,
        ExtractPdfPagesUseCase extractPdfPagesUseCase,
        ReorderPdfPagesUseCase reorderPdfPagesUseCase,
        RotatePdfPagesUseCase rotatePdfPagesUseCase,
        LoadDocumentTextUseCase loadDocumentTextUseCase,
        RunDocumentOcrUseCase runDocumentOcrUseCase,
        SearchDocumentTextUseCase searchDocumentTextUseCase,
        ResolveDocumentTextSelectionUseCase resolveDocumentTextSelectionUseCase,
        IDocumentSessionStore documentSessionStore,
        IRecentFilesService recentFilesService,
        IUserPreferencesService userPreferencesService,
        IRenderOrchestrator renderOrchestrator,
        IWindowsFileDialogService fileDialogService,
        IWindowsPrintCoordinator printCoordinator,
        ISignatureAssetStore signatureAssetStore,
        IPdfMarkupService pdfMarkupService,
        IPdfAnnotationStore pdfAnnotationStore,
        UndoRedoManager undoRedoManager,
        IWindowsTextCatalog textCatalog)
    {
        ArgumentNullException.ThrowIfNull(openDocumentUseCase);
        ArgumentNullException.ThrowIfNull(closeDocumentUseCase);
        ArgumentNullException.ThrowIfNull(mergePdfDocumentsUseCase);
        ArgumentNullException.ThrowIfNull(extractPdfPagesUseCase);
        ArgumentNullException.ThrowIfNull(reorderPdfPagesUseCase);
        ArgumentNullException.ThrowIfNull(rotatePdfPagesUseCase);
        ArgumentNullException.ThrowIfNull(loadDocumentTextUseCase);
        ArgumentNullException.ThrowIfNull(runDocumentOcrUseCase);
        ArgumentNullException.ThrowIfNull(searchDocumentTextUseCase);
        ArgumentNullException.ThrowIfNull(resolveDocumentTextSelectionUseCase);
        ArgumentNullException.ThrowIfNull(documentSessionStore);
        ArgumentNullException.ThrowIfNull(recentFilesService);
        ArgumentNullException.ThrowIfNull(userPreferencesService);
        ArgumentNullException.ThrowIfNull(renderOrchestrator);
        ArgumentNullException.ThrowIfNull(fileDialogService);
        ArgumentNullException.ThrowIfNull(printCoordinator);
        ArgumentNullException.ThrowIfNull(signatureAssetStore);
        ArgumentNullException.ThrowIfNull(pdfMarkupService);
        ArgumentNullException.ThrowIfNull(pdfAnnotationStore);
        ArgumentNullException.ThrowIfNull(undoRedoManager);
        ArgumentNullException.ThrowIfNull(textCatalog);

        _openDocumentUseCase = openDocumentUseCase;
        _closeDocumentUseCase = closeDocumentUseCase;
        _mergePdfDocumentsUseCase = mergePdfDocumentsUseCase;
        _extractPdfPagesUseCase = extractPdfPagesUseCase;
        _reorderPdfPagesUseCase = reorderPdfPagesUseCase;
        _rotatePdfPagesUseCase = rotatePdfPagesUseCase;
        _loadDocumentTextUseCase = loadDocumentTextUseCase;
        _runDocumentOcrUseCase = runDocumentOcrUseCase;
        _searchDocumentTextUseCase = searchDocumentTextUseCase;
        _resolveDocumentTextSelectionUseCase = resolveDocumentTextSelectionUseCase;
        _documentSessionStore = documentSessionStore;
        _recentFilesService = recentFilesService;
        _userPreferencesService = userPreferencesService;
        _renderOrchestrator = renderOrchestrator;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _fileDialogService = fileDialogService;
        _printCoordinator = printCoordinator;
        _signatureAssetStore = signatureAssetStore;
        _pdfMarkupService = pdfMarkupService;
        _pdfAnnotationStore = pdfAnnotationStore;
        _undoStack = undoRedoManager;
        _textCatalog = textCatalog;

        Labels = new WindowsLabels(textCatalog);
        AnnotationTools = CreateAnnotationTools(Labels);
        PreferenceLanguageOptions = [Labels.PreferencesSystem, Labels.PreferencesEnglish, Labels.PreferencesFrench, Labels.PreferencesSpanish];
        PreferenceThemeOptions = [Labels.PreferencesSystem, Labels.PreferencesLight, Labels.PreferencesDark];
        PreferenceZoomOptions = [Labels.PreferencesFitPage, Labels.PreferencesFitWidth, Labels.PreferencesActualSize];
        _isApplyingPreferenceSelection = true;
        SelectedPreferenceLanguage = MapLanguageToLabel(_userPreferencesService.Current.Language);
        SelectedPreferenceTheme = MapThemeToLabel(_userPreferencesService.Current.Theme);
        SelectedPreferenceZoom = MapZoomToLabel(_userPreferencesService.Current.DefaultZoom);
        _isApplyingPreferenceSelection = false;
        _isApplyingThumbnailPreference = true;
        ShowThumbnails = _userPreferencesService.Current.ShowThumbnailsPanel;
        _isApplyingThumbnailPreference = false;
        AnnotationTextDraft = textCatalog.GetString("annotation.default.note");
        SignatureAssetNameInput = textCatalog.GetString("annotation.default.signature_name");
        LoadSignatureAssets();
        StatusText = textCatalog.GetString("status.ready");
        RefreshRecentFiles();
    }

    public WindowsLabels Labels
    {
        get;
    }

    public ObservableCollection<WindowsDocumentTabViewModel> DocumentTabs
    {
        get;
    } = [];

    public ObservableCollection<RecentFileItem> RecentFiles
    {
        get;
    } = [];

    public ObservableCollection<WindowsAnnotationToolItem> AnnotationTools
    {
        get;
    }

    public ObservableCollection<SignatureAsset> SignatureAssets
    {
        get;
    } = [];

    public ObservableCollection<WindowsAnnotationColorItem> AnnotationColorOptions
    {
        get;
    } =
    [
        new WindowsAnnotationColorItem("#FFE600") { IsSelected = true },
        new WindowsAnnotationColorItem("#3B8CFF"),
        new WindowsAnnotationColorItem("#EF4444"),
        new WindowsAnnotationColorItem("#22C55E"),
        new WindowsAnnotationColorItem("#A855F7"),
        new WindowsAnnotationColorItem("#F97316"),
        new WindowsAnnotationColorItem("#DB2777"),
        new WindowsAnnotationColorItem("#22D3EE"),
        new WindowsAnnotationColorItem("#111827")
    ];

    public IReadOnlyList<double> AnnotationFontSizeOptions
    {
        get;
    } = [8, 10, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 64, 72];

    public IReadOnlyList<string> AnnotationFontFamilyOptions
    {
        get;
    } = ["Segoe UI", "Arial", "Times New Roman", "Courier New", "Georgia", "Verdana", "Calibri", "Consolas"];

    public IReadOnlyList<string> PreferenceLanguageOptions
    {
        get;
    }

    public IReadOnlyList<string> PreferenceThemeOptions
    {
        get;
    }

    public IReadOnlyList<string> PreferenceZoomOptions
    {
        get;
    }

    public string ActiveAnnotationToolLabel => GetAnnotationLabel(ActiveDocumentTab?.SelectedAnnotationTool ?? AnnotationTool.Select);

    public string ActiveAnnotationToolGlyph => AnnotationTools.FirstOrDefault(tool => tool.Tool == (ActiveDocumentTab?.SelectedAnnotationTool ?? AnnotationTool.Select))?.Glyph ?? "\uE8B3";

    public bool IsAnnotationToolsTabSelected => string.Equals(SelectedAnnotationPanelTab, "Tools", StringComparison.Ordinal);

    public bool IsAnnotationCommentsTabSelected => string.Equals(SelectedAnnotationPanelTab, "Comments", StringComparison.Ordinal);

    public bool IsAnnotationStyleTabSelected => string.Equals(SelectedAnnotationPanelTab, "Style", StringComparison.Ordinal);

    public bool IsAnnotationAppearancePanelVisible => ActiveDocumentTab?.SelectedAnnotationTool is not null;

    public bool IsAnnotationToolsAppearancePanelVisible =>
        (IsAnnotationToolsTabSelected && IsAnnotationAppearancePanelVisible) || ShowSelectionEditPanel;

    public bool IsAnnotationListPanelVisible => IsAnnotationToolsTabSelected || IsAnnotationCommentsTabSelected;

    public bool ShowAnnotationTextEditor =>
        IsAnnotationToolsTabSelected &&
        ActiveDocumentTab?.SelectedAnnotationTool is AnnotationTool.Note or AnnotationTool.Stamp;

    public bool ShowTextStyleControls =>
        (IsAnnotationToolsTabSelected &&
         ActiveDocumentTab?.SelectedAnnotationTool is AnnotationTool.Text) ||
        ShowSelectedTextStyleControls;

    public bool ShowSignatureControls =>
        IsAnnotationToolsTabSelected &&
        ActiveDocumentTab?.SelectedAnnotationTool is AnnotationTool.Signature;

    public bool ShowRectangleFillControls =>
        (IsAnnotationToolsTabSelected &&
         ActiveDocumentTab?.SelectedAnnotationTool is AnnotationTool.Rectangle) ||
        (ShowSelectionEditPanel && IsSelectedAnnotationRectangle);

    public bool IsSelectedAnnotationRectangle =>
        SelectedAnnotationId is { } id &&
        ActiveDocumentTab?.Annotations.FirstOrDefault(a => a.Id == id)?.Kind is DocumentAnnotationKind.Rectangle;

    public DocumentAnnotationKind? SelectedAnnotationKind =>
        SelectedAnnotationId is { } id
            ? ActiveDocumentTab?.Annotations.FirstOrDefault(a => a.Id == id)?.Kind
            : null;

    public bool ShowSelectionEditPanel =>
        SelectedAnnotationId is not null && ActiveDocumentTab?.SelectedAnnotationTool is AnnotationTool.Select;

    public bool ShowSelectedTextStyleControls =>
        ShowSelectionEditPanel && SelectedAnnotationKind is DocumentAnnotationKind.Text;

    public bool HasSignatureAssets => SignatureAssets.Count > 0;

    public bool CanDeleteSelectedSignatureAsset =>
        HasSignatureAssets && !string.IsNullOrWhiteSpace(SelectedSignatureAssetId);

    public bool CanSaveSignatureCapture =>
        _signatureCapturePoints.Count > 0 &&
        !string.IsNullOrWhiteSpace(SignatureAssetNameInput);

    public bool CanUseSignaturePlacement =>
        HasSignatureAssets && !string.IsNullOrWhiteSpace(SelectedSignatureAssetId);

    public string AnnotationOpacityText => $"{AnnotationOpacity:0}%";

    public SolidColorBrush SelectedAnnotationColorBrush =>
        AnnotationColorOptions.FirstOrDefault(item => item.IsSelected)?.Brush
            ?? new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 255, 230, 0));

    public string CacheSizeText => $"{CacheSizeMegabytes:0} MB";

    [ObservableProperty]
    public partial WindowsDocumentTabViewModel? ActiveDocumentTab
    {
        get; set;
    }

    [ObservableProperty]
    public partial string StatusText
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool IsBusy
    {
        get; set;
    }

    [ObservableProperty]
    public partial string SelectedAnnotationPanelTab { get; set; } = "Tools";

    [ObservableProperty]
    public partial Guid? SelectedAnnotationId
    {
        get; set;
    }

    [ObservableProperty]
    public partial double AnnotationOpacity { get; set; } = 80;

    [ObservableProperty]
    public partial double AnnotationFontSize { get; set; } = 14;

    [ObservableProperty]
    public partial string AnnotationFontFamily { get; set; } = "Segoe UI";

    [ObservableProperty]
    public partial bool AnnotationFillEnabled
    {
        get; set;
    }

    [ObservableProperty]
    public partial string AnnotationFillHex
    {
        get; set;
    } = "#EEF1FF";

    public ObservableCollection<WindowsAnnotationColorItem> AnnotationFillColorOptions
    {
        get;
    } =
    [
        new WindowsAnnotationColorItem("#EEF1FF") { IsSelected = true },
        new WindowsAnnotationColorItem("#FFE600"),
        new WindowsAnnotationColorItem("#3B8CFF"),
        new WindowsAnnotationColorItem("#EF4444"),
        new WindowsAnnotationColorItem("#22C55E"),
        new WindowsAnnotationColorItem("#A855F7"),
        new WindowsAnnotationColorItem("#FFFFFF"),
        new WindowsAnnotationColorItem("#111827")
    ];

    [ObservableProperty]
    public partial string AnnotationTextDraft
    {
        get; set;
    }

    [ObservableProperty]
    public partial string SignatureAssetNameInput
    {
        get; set;
    }

    [ObservableProperty]
    public partial string? SelectedSignatureAssetId
    {
        get; set;
    }

    [ObservableProperty]
    public partial PointCollection SignaturePadPoints { get; set; } = new();

    [ObservableProperty]
    public partial string SelectedPreferenceLanguage
    {
        get; set;
    }

    [ObservableProperty]
    public partial string SelectedPreferenceTheme
    {
        get; set;
    }

    [ObservableProperty]
    public partial string SelectedPreferenceZoom
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool ShowThumbnails
    {
        get; set;
    }

    [ObservableProperty]
    public partial double CacheSizeMegabytes { get; set; } = 256;

    public bool HasDocument => ActiveDocumentTab is not null;

    public bool IsPagesPanelVisible => ActiveDocumentTab is not null && ShowThumbnails;

    public bool HasRecentFiles => RecentFiles.Count > 0;

    public bool ShowRecentFilesFooter => !HasDocument && HasRecentFiles;

    public bool ShowStatusFooter => !ShowRecentFilesFooter;

    public bool CanStartDocumentOperation => !IsBusy;

    public string? SelectedDocumentText => ActiveDocumentTab?.CurrentDocumentTextSelection?.SelectedText;

    public bool HasSelectedDocumentText => !string.IsNullOrWhiteSpace(SelectedDocumentText);

    /// <summary>
    /// Resets the view model state when returning to the welcome screen.
    /// </summary>
    public void ResetForWelcome()
    {
        ActiveDocumentTab = null;
        DocumentTabs.Clear();
        RefreshRecentFiles();
        OnPropertyChanged(nameof(IsPagesPanelVisible));
        StatusText = _textCatalog.GetString("status.ready");
    }

    /// <summary>
    /// Forces a property-changed notification refresh for key binding properties.
    /// </summary>
    public void NotifyBindingsRefresh()
    {
        OnPropertyChanged(nameof(ActiveDocumentTab));
        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(IsPagesPanelVisible));
        NotifySelectedDocumentTextChanged();
    }

    /// <summary>
    /// Ensures the active tab is fully rendered after the window loads.
    /// </summary>
    /// <returns>A task representing the render hydration.</returns>
    public Task EnsureActiveTabHydratedAsync()
    {
        return HydrateActiveTabAsync();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _documentOpenGate.Dispose();
    }

    [RelayCommand(CanExecute = nameof(CanStartDocumentOperation))]
    private async Task OpenAsync()
    {
        try
        {
            string? path = await _fileDialogService.PickOpenDocumentAsync();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            await HandleHomeFilesDroppedAsync([path]);
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartDocumentOperation))]
    private async Task MergeAsync()
    {
        try
        {
            IReadOnlyList<string> selectedPaths = await _fileDialogService.PickMergeDocumentsAsync();
            if (selectedPaths.Count == 0)
            {
                StatusText = _textCatalog.GetString("status.merge.cancelled");
                return;
            }

            string[] sourcePaths = (ActiveDocumentTab is null
                    ? selectedPaths
                    : new[] { ActiveDocumentTab.FilePath }.Concat(selectedPaths))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            await MergeDocumentSourcesAsync(sourcePaths);
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
    }

    public bool CanSaveDocument =>
        ActiveDocumentTab is not null &&
        !IsBusy &&
        (ActiveDocumentTab.IsDirty || ActiveDocumentTab.HasPendingPageEdits || ActiveDocumentTab.Annotations.Count > 0);

    [RelayCommand(CanExecute = nameof(CanSaveDocument))]
    private async Task SaveDocumentAsync()
    {
        if (ActiveDocumentTab is null)
        {
            return;
        }

        WindowsDocumentTabViewModel? tab = ActiveDocumentTab;
        try
        {
            bool hasPageEdits = tab.HasPendingPageEdits && tab.DocumentType is DocumentType.Pdf;
            bool hasAnnotations = tab.Annotations.Count > 0 && tab.DocumentType is DocumentType.Pdf;

            if (hasPageEdits || hasAnnotations)
            {
                await RunBusyAsync(async () =>
                {
                    string originalPath = tab.FilePath;
                    string currentPath = originalPath;

                    if (hasPageEdits)
                    {
                        string tempDir = Path.Combine(Path.GetTempPath(), "velune-op", Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(tempDir);

                        string? editedPath = await CreateEditedPdfAsync(
                            tab,
                            tempDir,
                            tab.Thumbnails.Select(thumbnail => thumbnail.PageNumber).ToArray(),
                            tab.GetPendingPageRotations());

                        if (string.IsNullOrWhiteSpace(editedPath))
                        {
                            return;
                        }

                        File.Copy(editedPath, originalPath, overwrite: true);
                        currentPath = originalPath;
                    }

                    if (hasAnnotations)
                    {
                        if (!hasPageEdits)
                        {
                            await ReleaseActiveSessionAsync(tab);
                        }

                        await _pdfAnnotationStore.SaveAsync(currentPath, tab.Annotations.ToList());
                    }

                    var savedAnnotations = tab.Annotations.ToList();
                    await ReloadActiveDocumentAsync(tab, originalPath);
                    await RunOnUiThreadAsync(() =>
                    {
                        tab.HasPendingPageReorder = false;
                        tab.ClearPendingPageRotations();
                        tab.IsDirty = false;
                        foreach (DocumentAnnotation annotation in savedAnnotations)
                        {
                            tab.Annotations.Add(annotation);
                        }

                        tab.RefreshAnnotationOverlays();
                        StatusText = _textCatalog.GetString("status.document.saved");
                        NotifySaveStateChanged();
                    });
                });
                return;
            }

            string suggestedName = string.IsNullOrWhiteSpace(tab.Title)
                ? "document.pdf"
                : tab.Title;
            string? outputPath = await _fileDialogService.PickSavePdfAsync(suggestedName);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                StatusText = _textCatalog.GetString("status.save.cancelled");
                return;
            }

            await ReleaseActiveSessionAsync(tab);
            File.Copy(tab.FilePath, outputPath, overwrite: true);
            await OpenPathAsync(outputPath);
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveDocument))]
    private async Task SaveDocumentAsAsync()
    {
        if (ActiveDocumentTab is null)
        {
            return;
        }

        WindowsDocumentTabViewModel? tab = ActiveDocumentTab;
        try
        {
            string suggestedName = string.IsNullOrWhiteSpace(tab.Title)
                ? "document.pdf"
                : tab.Title;
            string? outputPath = await _fileDialogService.PickSavePdfAsync(suggestedName);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                StatusText = _textCatalog.GetString("status.save.cancelled");
                return;
            }

            bool hasPageEdits = tab.HasPendingPageEdits && tab.DocumentType is DocumentType.Pdf;
            bool hasAnnotations = tab.Annotations.Count > 0 && tab.DocumentType is DocumentType.Pdf;

            if (hasPageEdits || hasAnnotations)
            {
                await RunBusyAsync(async () =>
                {
                    string tempDir = Path.Combine(Path.GetTempPath(), "velune-op", Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempDir);

                    IDocumentSession? session = _documentSessionStore.Sessions
                        .FirstOrDefault(s => s.Id == tab.SessionId);
                    string currentPath = tab.FilePath;

                    if (hasPageEdits)
                    {
                        string? editedPath = await CreateEditedPdfAsync(
                            tab,
                            tempDir,
                            tab.Thumbnails.Select(thumbnail => thumbnail.PageNumber).ToArray(),
                            tab.GetPendingPageRotations());

                        if (string.IsNullOrWhiteSpace(editedPath))
                        {
                            return;
                        }

                        currentPath = editedPath;
                    }

                    if (hasAnnotations)
                    {
                        if (session is null)
                        {
                            StatusText = _textCatalog.GetString("status.save.failed");
                            return;
                        }

                        if (!hasPageEdits)
                        {
                            await ReleaseActiveSessionAsync(tab);
                        }

                        string annotatedPath = Path.Combine(tempDir, "annotated.pdf");
                        Result<string> annotationResult = await _pdfMarkupService.ApplyAnnotationsAsync(
                            new ApplyPdfAnnotationsRequest(
                                session,
                                currentPath,
                                annotatedPath,
                                tab.Annotations.ToList()));

                        if (annotationResult.IsFailure)
                        {
                            await RunOnUiThreadAsync(() => StatusText = _textCatalog.GetString("status.save.failed"));
                            if (!hasPageEdits)
                            {
                                await ReopenAfterFailureAsync(tab);
                            }
                            return;
                        }

                        currentPath = annotationResult.Value ?? annotatedPath;
                    }

                    File.Copy(currentPath, outputPath, overwrite: true);
                    await _pdfAnnotationStore.RemoveAsync(outputPath);
                    await ReloadActiveDocumentAsync(tab, outputPath);
                    await RunOnUiThreadAsync(() =>
                    {
                        tab.HasPendingPageReorder = false;
                        tab.ClearPendingPageRotations();
                        tab.Annotations.Clear();
                        tab.IsDirty = false;
                        StatusText = _textCatalog.GetString("status.document.saved");
                        NotifySaveStateChanged();
                    });
                });
            }
            else
            {
                await ReleaseActiveSessionAsync(tab);
                File.Copy(tab.FilePath, outputPath, overwrite: true);
                await OpenPathAsync(outputPath);
            }
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
    }

    [RelayCommand]
    private async Task OpenRecentFileAsync(RecentFileItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.FilePath))
        {
            return;
        }

        try
        {
            await HandleHomeFilesDroppedAsync([item.FilePath]);
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(HasRecentFiles))]
    private void ClearRecentFiles()
    {
        _recentFilesService.Clear();
        RefreshRecentFiles();
        StatusText = _textCatalog.GetString("status.recent_files.cleared");
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        if (ActiveDocumentTab is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            Result result = await _printCoordinator.PrintAsync(ActiveDocumentTab);
            await RunOnUiThreadAsync(() =>
            {
                StatusText = result.IsSuccess
                    ? _textCatalog.GetString("status.print.started")
                    : FormatError("status.print.failed", result.Error);
            });
        });
    }

    [RelayCommand]
    private async Task ActivateTabAsync(WindowsDocumentTabViewModel? tab)
    {
        if (tab is null || ReferenceEquals(tab, ActiveDocumentTab))
        {
            return;
        }

        if (!_documentSessionStore.TryActivate(tab.SessionId))
        {
            await RunOnUiThreadAsync(() => StatusText = _textCatalog.GetString("status.open.failed"));
            return;
        }

        await RunOnUiThreadAsync(() =>
        {
            ActiveDocumentTab = tab;
            UpdateTabSelection();
        });
        await HydrateActiveTabAsync();
    }

    [RelayCommand]
    private async Task CloseTabAsync(WindowsDocumentTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        bool wasActive = ReferenceEquals(tab, ActiveDocumentTab);
        int tabIndex = DocumentTabs.IndexOf(tab);

        await _closeDocumentUseCase.ExecuteAsync(new CloseDocumentRequest(tab.SessionId));
        await _renderOrchestrator.CancelDocumentJobsAsync(tab.SessionId);
        await RunOnUiThreadAsync(() => DocumentTabs.Remove(tab));

        if (!wasActive)
        {
            return;
        }

        await RunOnUiThreadAsync(() => ActiveDocumentTab = ResolveNextTab(tabIndex));
        if (ActiveDocumentTab is not null)
        {
            _documentSessionStore.TryActivate(ActiveDocumentTab.SessionId);
            await RunOnUiThreadAsync(UpdateTabSelection);
            await HydrateActiveTabAsync();
            return;
        }

        await RunOnUiThreadAsync(() =>
        {
            UpdateTabSelection();
            StatusText = _textCatalog.GetString("status.document.closed");
        });
    }

    [RelayCommand]
    private void UndoAction()
    {
        if (!_undoStack.CanUndo)
        {
            return;
        }

        _isUndoRedoInProgress = true;
        try
        {
            _undoStack.Undo();
        }
        finally
        {
            _isUndoRedoInProgress = false;
        }

        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    [RelayCommand]
    private void RedoAction()
    {
        if (!_undoStack.CanRedo)
        {
            return;
        }

        _isUndoRedoInProgress = true;
        try
        {
            _undoStack.Redo();
        }
        finally
        {
            _isUndoRedoInProgress = false;
        }
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    public bool CanUndo => _undoStack.CanUndo;

    public bool CanRedo => _undoStack.CanRedo;

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (ActiveDocumentTab is null || ActiveDocumentTab.CurrentPage <= 1)
        {
            return;
        }

        int previous = ActiveDocumentTab.CurrentPage;
        await ChangePageAsync(ActiveDocumentTab.CurrentPage - 1);
        PushUndo(new NavigationAction(ActiveDocumentTab, previous, ActiveDocumentTab.CurrentPage, async p => await ChangePageAsync(p)));
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (ActiveDocumentTab is null || ActiveDocumentTab.CurrentPage >= ActiveDocumentTab.TotalPages)
        {
            return;
        }

        int previous = ActiveDocumentTab.CurrentPage;
        await ChangePageAsync(ActiveDocumentTab.CurrentPage + 1);
        PushUndo(new NavigationAction(ActiveDocumentTab, previous, ActiveDocumentTab.CurrentPage, async p => await ChangePageAsync(p)));
    }

    [RelayCommand]
    private async Task FitPageAsync()
    {
        if (ActiveDocumentTab is null)
        {
            return;
        }

        double availableWidth = _documentViewerWidth - ViewerHorizontalPadding;
        double availableHeight = _documentViewerHeight - ViewerVerticalPadding;

        if (availableWidth > 0 && availableHeight > 0 &&
            ActiveDocumentTab.CurrentPagePixelWidth > 0 && ActiveDocumentTab.CurrentPagePixelHeight > 0)
        {
            double zoom = RenderedPageViewportCalculator.CalculateFitToPageZoom(
                (int)ActiveDocumentTab.CurrentPagePixelWidth,
                (int)ActiveDocumentTab.CurrentPagePixelHeight,
                ActiveDocumentTab.ZoomFactor,
                availableWidth,
                availableHeight);

            ActiveDocumentTab.ZoomFactor = Math.Clamp(zoom, 0.2, 4.0);
        }
        else
        {
            ActiveDocumentTab.ZoomFactor = DefaultViewerZoom;
        }

        ActiveDocumentTab.ZoomText = $"{ActiveDocumentTab.ZoomFactor * 100:0}%";
        await RenderActivePageAsync(ActiveDocumentTab);
    }

    public void SetDocumentViewerSize(double width, double height)
    {
        _documentViewerWidth = width;
        _documentViewerHeight = height;
    }

    private double CalculateInitialFitZoom(int nativeWidth, int nativeHeight)
    {
        double availableWidth = _documentViewerWidth - ViewerHorizontalPadding;
        double availableHeight = _documentViewerHeight - ViewerVerticalPadding;

        if (availableWidth <= 0 || availableHeight <= 0)
        {
            return DefaultViewerZoom;
        }

        double zoom = Math.Min(
            availableWidth / Math.Max(1, nativeWidth),
            availableHeight / Math.Max(1, nativeHeight));

        return Math.Clamp(zoom, 0.2, 4.0);
    }

    [RelayCommand]
    private async Task ActualSizeAsync()
    {
        if (ActiveDocumentTab is null)
        {
            return;
        }

        ActiveDocumentTab.ZoomFactor = 1.0;
        ActiveDocumentTab.ZoomText = "100%";
        await RenderActivePageAsync(ActiveDocumentTab);
    }

    [RelayCommand]
    private void TogglePagesPanel()
    {
        if (ActiveDocumentTab is null)
        {
            return;
        }

        PushUndo(new PanelToggleAction("Pages", TogglePagesPanelCore));
        TogglePagesPanelCore();
    }

    private void TogglePagesPanelCore()
    {
        ShowThumbnails = !ShowThumbnails;
        StatusText = ShowThumbnails
            ? _textCatalog.GetString("status.pages_panel.shown")
            : _textCatalog.GetString("status.pages_panel.hidden");
    }

    [RelayCommand]
    private async Task OpenSearchAsync()
    {
        if (ActiveDocumentTab is not { } tab)
        {
            return;
        }

        await RunOnUiThreadAsync(() =>
        {
            SetRightPanel(tab, RightPanel.Search);
            StatusText = _textCatalog.GetString("status.search.loading");
        });

        if (tab.DocumentTextIndex is null && !tab.RequiresSearchOcr)
        {
            await EnsureDocumentTextAvailableAsync(tab, forceOcr: false);
        }

        if (!string.IsNullOrWhiteSpace(tab.SearchQuery))
        {
            await SearchTextAsync();
            return;
        }

        if (!tab.IsAnalyzingDocumentText && !tab.RequiresSearchOcr)
        {
            await RunOnUiThreadAsync(() => StatusText = _textCatalog.GetString("status.search.shown"));
        }
    }

    [RelayCommand]
    private async Task ToggleSearchPanelAsync()
    {
        if (ActiveDocumentTab is not { } tab)
        {
            return;
        }

        if (tab.IsSearchPanelOpen)
        {
            await RunOnUiThreadAsync(() =>
            {
                tab.IsSearchPanelOpen = false;
                StatusText = _textCatalog.GetString("status.search.hidden");
            });
            return;
        }

        await OpenSearchAsync();
    }

    [RelayCommand]
    private void ToggleAnnotationsPanel()
    {
        if (ActiveDocumentTab is not { } tab)
        {
            return;
        }

        PushUndo(new PanelToggleAction("Annotations", ToggleAnnotationsPanelCore));
        ToggleAnnotationsPanelCore();
    }

    private void ToggleAnnotationsPanelCore()
    {
        if (ActiveDocumentTab is not { } tab)
        {
            return;
        }

        if (tab.IsAnnotationsPanelOpen)
        {
            tab.IsAnnotationsPanelOpen = false;
            StatusText = _textCatalog.GetString("status.annotations.hidden");
            return;
        }

        SetRightPanel(tab, RightPanel.Annotations);
        StatusText = _textCatalog.GetString("status.annotations.shown");
    }

    [RelayCommand]
    private void ToggleInfoPanel()
    {
        if (ActiveDocumentTab is not { } tab)
        {
            return;
        }

        if (tab.IsInfoPanelOpen)
        {
            tab.IsInfoPanelOpen = false;
            StatusText = _textCatalog.GetString("status.info.hidden");
            return;
        }

        SetRightPanel(tab, RightPanel.Info);
        StatusText = _textCatalog.GetString("status.info.shown");
    }

    [RelayCommand]
    private void ToggleSettingsPanel()
    {
        if (ActiveDocumentTab is not { } tab)
        {
            return;
        }

        if (tab.IsSettingsPanelOpen)
        {
            tab.IsSettingsPanelOpen = false;
            StatusText = _textCatalog.GetString("status.preferences.hidden");
            return;
        }

        SetRightPanel(tab, RightPanel.Settings);
        StatusText = _textCatalog.GetString("status.preferences.shown");
    }

    [RelayCommand]
    private void SelectAnnotationTool(object? toolValue)
    {
        if (ActiveDocumentTab is null ||
            !TryResolveAnnotationTool(toolValue, out AnnotationTool tool))
        {
            return;
        }

        CommitInlineTextAnnotation();
        ActiveDocumentTab.SelectedAnnotationTool = tool;
        SelectedAnnotationPanelTab = "Tools";
        SetRightPanel(ActiveDocumentTab, RightPanel.Annotations);
        PrepareAnnotationTextDraftForTool(tool);
        if (tool is not AnnotationTool.Select)
        {
            SelectedAnnotationId = null;
        }

        if (tool is AnnotationTool.Select or AnnotationTool.Highlight)
        {
            _ = PrepareTextSelectionAsync(ActiveDocumentTab);
        }

        UpdateAnnotationToolSelection();
        NotifyAnnotationSelectionChanged();
        StatusText = tool is AnnotationTool.Select
            ? _textCatalog.GetString("status.annotation.tool.select")
            : _textCatalog.Format("status.annotation.tool.active", GetAnnotationLabel(tool));
    }

    [RelayCommand]
    private void SelectAnnotationColor(WindowsAnnotationColorItem? color)
    {
        if (color is null)
        {
            return;
        }

        foreach (WindowsAnnotationColorItem item in AnnotationColorOptions)
        {
            item.IsSelected = ReferenceEquals(item, color);
        }

        OnPropertyChanged(nameof(SelectedAnnotationColorBrush));
        ApplyColorToSelectedAnnotation(color.Hex);
    }

    private void ApplyColorToSelectedAnnotation(string hex)
    {
        if (SelectedAnnotationId is not { } id || ActiveDocumentTab is null)
        {
            return;
        }

        int index = ActiveDocumentTab.FindAnnotationIndex(id);
        if (index < 0)
        {
            return;
        }

        DocumentAnnotation annotation = ActiveDocumentTab.Annotations[index];
        AnnotationAppearance updated = new(
            hex,
            annotation.Appearance.FillHex,
            annotation.Appearance.StrokeThickness,
            annotation.Appearance.Opacity,
            annotation.Appearance.FontSize,
            annotation.Appearance.FontFamily,
            annotation.Appearance.RotationAngle);
        DocumentAnnotation newAnnotation = new(
            annotation.Id,
            annotation.Kind,
            annotation.PageIndex,
            updated,
            annotation.Bounds,
            annotation.Points,
            annotation.Text,
            annotation.AssetId,
            annotation.CreatedAt);
        ActiveDocumentTab.Annotations[index] = newAnnotation;
        PushUndo(new AnnotationMutationAction(ActiveDocumentTab, annotation, newAnnotation));
        ActiveDocumentTab.RefreshAnnotationOverlays(SelectedAnnotationId);
        NotifySaveStateChanged();
    }

    /// <summary>
    /// Selects the specified fill color for rectangle annotations.
    /// </summary>
    /// <param name="color">The color item to select.</param>
    public void SelectAnnotationFillColor(WindowsAnnotationColorItem? color)
    {
        if (color is null)
        {
            return;
        }

        foreach (WindowsAnnotationColorItem item in AnnotationFillColorOptions)
        {
            item.IsSelected = ReferenceEquals(item, color);
        }

        AnnotationFillHex = color.Hex;
        ApplyFillToSelectedAnnotation();
    }

    private void ApplyFillToSelectedAnnotation()
    {
        if (SelectedAnnotationId is not { } id || ActiveDocumentTab is null)
        {
            return;
        }

        int index = ActiveDocumentTab.FindAnnotationIndex(id);
        if (index < 0)
        {
            return;
        }

        DocumentAnnotation annotation = ActiveDocumentTab.Annotations[index];
        if (annotation.Kind is not DocumentAnnotationKind.Rectangle)
        {
            return;
        }

        string? fill = AnnotationFillEnabled ? AnnotationFillHex : null;
        AnnotationAppearance updated = new(
            annotation.Appearance.StrokeHex,
            fill,
            annotation.Appearance.StrokeThickness,
            annotation.Appearance.Opacity,
            annotation.Appearance.FontSize,
            annotation.Appearance.FontFamily,
            annotation.Appearance.RotationAngle);
        DocumentAnnotation newAnnotation = new(
            annotation.Id,
            annotation.Kind,
            annotation.PageIndex,
            updated,
            annotation.Bounds,
            annotation.Points,
            annotation.Text,
            annotation.AssetId,
            annotation.CreatedAt);
        ActiveDocumentTab.Annotations[index] = newAnnotation;
        PushUndo(new AnnotationMutationAction(ActiveDocumentTab, annotation, newAnnotation));
        ActiveDocumentTab.RefreshAnnotationOverlays(SelectedAnnotationId);
        NotifySaveStateChanged();
    }

    private void LoadSelectedAnnotationProperties()
    {
        if (SelectedAnnotationId is not { } id || ActiveDocumentTab is null)
        {
            return;
        }

        DocumentAnnotation? annotation = ActiveDocumentTab.Annotations.FirstOrDefault(a => a.Id == id);
        if (annotation is null)
        {
            return;
        }

        AnnotationOpacity = annotation.Appearance.Opacity * 100;
        AnnotationFontSize = annotation.Appearance.FontSize;
        AnnotationFontFamily = annotation.Appearance.FontFamily ?? "Segoe UI";
        AnnotationFillEnabled = annotation.Appearance.FillHex is not null;
        AnnotationFillHex = annotation.Appearance.FillHex ?? "#EEF1FF";

        string strokeHex = annotation.Appearance.StrokeHex;
        foreach (WindowsAnnotationColorItem item in AnnotationColorOptions)
        {
            item.IsSelected = string.Equals(item.Hex, strokeHex, StringComparison.OrdinalIgnoreCase);
        }

        OnPropertyChanged(nameof(SelectedAnnotationColorBrush));

        if (annotation.Appearance.FillHex is { } fillHex)
        {
            foreach (WindowsAnnotationColorItem item in AnnotationFillColorOptions)
            {
                item.IsSelected = string.Equals(item.Hex, fillHex, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private void ApplyOpacityToSelectedAnnotation()
    {
        if (SelectedAnnotationId is not { } id || ActiveDocumentTab is null)
        {
            return;
        }

        int index = ActiveDocumentTab.FindAnnotationIndex(id);
        if (index < 0)
        {
            return;
        }

        DocumentAnnotation annotation = ActiveDocumentTab.Annotations[index];
        double normalizedOpacity = Math.Clamp(AnnotationOpacity / 100.0, 0, 1);
        if (Math.Abs(annotation.Appearance.Opacity - normalizedOpacity) < 0.001)
        {
            return;
        }

        AnnotationAppearance updated = new(
            annotation.Appearance.StrokeHex,
            annotation.Appearance.FillHex,
            annotation.Appearance.StrokeThickness,
            normalizedOpacity,
            annotation.Appearance.FontSize,
            annotation.Appearance.FontFamily,
            annotation.Appearance.RotationAngle);
        DocumentAnnotation newAnnotation = new(
            annotation.Id,
            annotation.Kind,
            annotation.PageIndex,
            updated,
            annotation.Bounds,
            annotation.Points,
            annotation.Text,
            annotation.AssetId,
            annotation.CreatedAt);
        ActiveDocumentTab.Annotations[index] = newAnnotation;
        PushUndo(new AnnotationMutationAction(ActiveDocumentTab, annotation, newAnnotation));
        ActiveDocumentTab.RefreshAnnotationOverlays(SelectedAnnotationId);
        NotifySaveStateChanged();
    }

    private void ApplyFontToSelectedAnnotation()
    {
        if (SelectedAnnotationId is not { } id || ActiveDocumentTab is null)
        {
            return;
        }

        int index = ActiveDocumentTab.FindAnnotationIndex(id);
        if (index < 0)
        {
            return;
        }

        DocumentAnnotation annotation = ActiveDocumentTab.Annotations[index];
        if (annotation.Kind is not DocumentAnnotationKind.Text)
        {
            return;
        }

        AnnotationAppearance updated = new(
            annotation.Appearance.StrokeHex,
            annotation.Appearance.FillHex,
            annotation.Appearance.StrokeThickness,
            annotation.Appearance.Opacity,
            AnnotationFontSize,
            AnnotationFontFamily,
            annotation.Appearance.RotationAngle);
        DocumentAnnotation newAnnotation = new(
            annotation.Id,
            annotation.Kind,
            annotation.PageIndex,
            updated,
            annotation.Bounds,
            annotation.Points,
            annotation.Text,
            annotation.AssetId,
            annotation.CreatedAt);
        ActiveDocumentTab.Annotations[index] = newAnnotation;
        PushUndo(new AnnotationMutationAction(ActiveDocumentTab, annotation, newAnnotation));
        ActiveDocumentTab.RefreshAnnotationOverlays(SelectedAnnotationId);
        NotifySaveStateChanged();
    }

    public void DeleteSelectedAnnotation()
    {
        if (SelectedAnnotationId is not { } id)
        {
            return;
        }

        SelectedAnnotationId = null;
        DeleteAnnotationById(id);
    }

    public void RotateSelectedAnnotation90()
    {
        if (SelectedAnnotationId is not { } id || ActiveDocumentTab is null)
        {
            return;
        }

        int index = ActiveDocumentTab.FindAnnotationIndex(id);
        if (index < 0)
        {
            return;
        }

        DocumentAnnotation annotation = ActiveDocumentTab.Annotations[index];
        double newAngle = annotation.Appearance.RotationAngle + 90;
        if (newAngle >= 360)
        {
            newAngle -= 360;
        }

        AnnotationAppearance updated = new(
            annotation.Appearance.StrokeHex,
            annotation.Appearance.FillHex,
            annotation.Appearance.StrokeThickness,
            annotation.Appearance.Opacity,
            annotation.Appearance.FontSize,
            annotation.Appearance.FontFamily,
            newAngle);
        DocumentAnnotation newAnnotation = new(
            annotation.Id,
            annotation.Kind,
            annotation.PageIndex,
            updated,
            annotation.Bounds,
            annotation.Points,
            annotation.Text,
            annotation.AssetId,
            annotation.CreatedAt);
        ActiveDocumentTab.Annotations[index] = newAnnotation;
        PushUndo(new AnnotationMutationAction(ActiveDocumentTab, annotation, newAnnotation));
        ActiveDocumentTab.RefreshAnnotationOverlays(SelectedAnnotationId);
        NotifySaveStateChanged();
    }

    public void ResetSelectedAnnotationRotation()
    {
        if (SelectedAnnotationId is not { } id || ActiveDocumentTab is null)
        {
            return;
        }

        int index = ActiveDocumentTab.FindAnnotationIndex(id);
        if (index < 0)
        {
            return;
        }

        DocumentAnnotation annotation = ActiveDocumentTab.Annotations[index];
        double originalAngle = ActiveDocumentTab.GetOriginalRotation(id);
        if (Math.Abs(annotation.Appearance.RotationAngle - originalAngle) < 0.01)
        {
            return;
        }

        AnnotationAppearance updated = new(
            annotation.Appearance.StrokeHex,
            annotation.Appearance.FillHex,
            annotation.Appearance.StrokeThickness,
            annotation.Appearance.Opacity,
            annotation.Appearance.FontSize,
            annotation.Appearance.FontFamily,
            originalAngle);
        DocumentAnnotation newAnnotation = new(
            annotation.Id,
            annotation.Kind,
            annotation.PageIndex,
            updated,
            annotation.Bounds,
            annotation.Points,
            annotation.Text,
            annotation.AssetId,
            annotation.CreatedAt);
        ActiveDocumentTab.Annotations[index] = newAnnotation;
        PushUndo(new AnnotationMutationAction(ActiveDocumentTab, annotation, newAnnotation));
        ActiveDocumentTab.RefreshAnnotationOverlays(SelectedAnnotationId);
        NotifySaveStateChanged();
    }

    public void FlipSelectedAnnotationHorizontally()
    {
        if (SelectedAnnotationId is not { } id || ActiveDocumentTab is null)
        {
            return;
        }

        int index = ActiveDocumentTab.FindAnnotationIndex(id);
        if (index < 0)
        {
            return;
        }

        DocumentAnnotation annotation = ActiveDocumentTab.Annotations[index];
        if (annotation.Kind is DocumentAnnotationKind.Ink && annotation.Points.Count > 0)
        {
            double minX = annotation.Points.Min(p => p.X);
            double maxX = annotation.Points.Max(p => p.X);
            double cx = (minX + maxX) / 2;

            var flipped = annotation.Points
                .Select(p => new NormalizedPoint(Math.Clamp(cx + (cx - p.X), 0, 1), p.Y))
                .ToList();

            ActiveDocumentTab.Annotations[index] = new DocumentAnnotation(
                annotation.Id, annotation.Kind, annotation.PageIndex, annotation.Appearance,
                null, flipped, annotation.Text, annotation.AssetId, annotation.CreatedAt);
            ActiveDocumentTab.RefreshAnnotationOverlays(SelectedAnnotationId);
            NotifySaveStateChanged();
        }
    }

    public void FlipSelectedAnnotationVertically()
    {
        if (SelectedAnnotationId is not { } id || ActiveDocumentTab is null)
        {
            return;
        }

        int index = ActiveDocumentTab.FindAnnotationIndex(id);
        if (index < 0)
        {
            return;
        }

        DocumentAnnotation annotation = ActiveDocumentTab.Annotations[index];
        if (annotation.Kind is DocumentAnnotationKind.Ink && annotation.Points.Count > 0)
        {
            double minY = annotation.Points.Min(p => p.Y);
            double maxY = annotation.Points.Max(p => p.Y);
            double cy = (minY + maxY) / 2;

            var flipped = annotation.Points
                .Select(p => new NormalizedPoint(p.X, Math.Clamp(cy + (cy - p.Y), 0, 1)))
                .ToList();

            ActiveDocumentTab.Annotations[index] = new DocumentAnnotation(
                annotation.Id, annotation.Kind, annotation.PageIndex, annotation.Appearance,
                null, flipped, annotation.Text, annotation.AssetId, annotation.CreatedAt);
            ActiveDocumentTab.RefreshAnnotationOverlays(SelectedAnnotationId);
            NotifySaveStateChanged();
        }
    }

    [RelayCommand]
    private void SelectAnnotationPanelTab(string? tab)
    {
        if (string.IsNullOrWhiteSpace(tab))
        {
            return;
        }

        SelectedAnnotationPanelTab = tab;
    }

    [RelayCommand]
    private async Task ImportSignatureAssetAsync()
    {
        try
        {
            string? imagePath = await _fileDialogService.PickSignatureImageAsync();
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return;
            }

            if (!SupportedDocumentFormats.IsImage(Path.GetExtension(imagePath)))
            {
                StatusText = _textCatalog.GetString("error.signature.unsupported_image.message");
                return;
            }

            Result<SignatureAsset> result = _signatureAssetStore.Import(imagePath);
            if (result.IsFailure || result.Value is null)
            {
                StatusText = FormatError("error.signature.import_failed.message", result.Error);
                return;
            }

            LoadSignatureAssets();
            SelectedSignatureAssetId = result.Value.Id;
            StatusText = _textCatalog.Format("status.signature.imported", result.Value.DisplayName);
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelectedSignatureAsset))]
    private void DeleteSelectedSignatureAsset(string? assetId)
    {
        string? targetAssetId = string.IsNullOrWhiteSpace(assetId)
            ? SelectedSignatureAssetId
            : assetId;

        if (string.IsNullOrWhiteSpace(targetAssetId))
        {
            return;
        }

        if (DocumentTabs.Any(tab => tab.Annotations.Any(annotation =>
                annotation.Kind is DocumentAnnotationKind.Signature &&
                string.Equals(annotation.AssetId, targetAssetId, StringComparison.Ordinal))))
        {
            StatusText = _textCatalog.GetString("status.signature.in_use");
            return;
        }

        Result result = _signatureAssetStore.Delete(targetAssetId);
        if (result.IsFailure)
        {
            StatusText = FormatError("error.signature.delete_failed.message", result.Error);
            return;
        }

        LoadSignatureAssets();
        StatusText = _textCatalog.GetString("status.signature.deleted");
    }

    [RelayCommand(CanExecute = nameof(CanSaveSignatureCapture))]
    private void SaveDrawnSignatureAsset()
    {
        Result<SignatureAsset> result = _signatureAssetStore.SaveInkSignature(SignatureAssetNameInput.Trim(), _signatureCapturePoints);
        if (result.IsFailure || result.Value is null)
        {
            StatusText = FormatError("error.signature.save_failed.message", result.Error);
            return;
        }

        LoadSignatureAssets();
        SelectedSignatureAssetId = result.Value.Id;
        _signatureCapturePoints.Clear();
        RefreshSignaturePadPreview();
        StatusText = _textCatalog.Format("status.signature.saved", result.Value.DisplayName);
    }

    [RelayCommand]
    private void ClearSignatureCapture()
    {
        _signatureCapturePoints.Clear();
        RefreshSignaturePadPreview();
        StatusText = _textCatalog.GetString("status.signature.cleared");
    }

    [RelayCommand]
    private void SelectSignatureAsset(string? assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return;
        }

        SelectedSignatureAssetId = assetId;
        StatusText = _textCatalog.GetString("status.signature.selected");
    }

    [RelayCommand]
    private async Task SearchTextAsync()
    {
        if (ActiveDocumentTab is not { } tab)
        {
            return;
        }

        await RunOnUiThreadAsync(() => SetRightPanel(tab, RightPanel.Search));
        if (string.IsNullOrWhiteSpace(tab.SearchQuery))
        {
            await RunOnUiThreadAsync(() =>
            {
                tab.ClearSearchResults();
                tab.SearchPanelNotice = null;
                StatusText = _textCatalog.GetString("validation.search.query");
            });
            return;
        }

        if (tab.DocumentTextIndex is null)
        {
            await EnsureDocumentTextAvailableAsync(tab, forceOcr: false);
            if (tab.DocumentTextIndex is null)
            {
                return;
            }
        }

        Result<IReadOnlyList<SearchHit>> result = _searchDocumentTextUseCase.Execute(
            new SearchDocumentTextRequest(
                tab.DocumentTextIndex,
                new SearchQuery(tab.SearchQuery)));

        if (result.IsFailure)
        {
            await RunOnUiThreadAsync(() =>
            {
                tab.SearchPanelNotice = result.Error?.Message;
                StatusText = _textCatalog.GetString("error.search.unavailable.message");
            });
            return;
        }

        WindowsSearchResultItemViewModel? firstResult = await RunOnUiThreadAsync(() =>
        {
            tab.ApplySearchResults(result.Value ?? []);
            if (!tab.HasSearchResults)
            {
                tab.SearchPanelNotice = _textCatalog.Format("search.notice.no_match", tab.SearchQuery.Trim());
                StatusText = _textCatalog.GetString("status.search.none");
                return null;
            }

            tab.SearchPanelNotice = null;
            return tab.SearchResults[0];
        });

        if (firstResult is null)
        {
            return;
        }

        await SelectSearchResultAsync(firstResult, updateStatus: true);
    }

    [RelayCommand]
    private async Task RunSearchOcrAsync()
    {
        if (ActiveDocumentTab is not { } tab)
        {
            return;
        }

        await RunOnUiThreadAsync(() => SetRightPanel(tab, RightPanel.Search));
        await EnsureDocumentTextAvailableAsync(tab, forceOcr: true);
        if (tab.DocumentTextIndex is not null && !string.IsNullOrWhiteSpace(tab.SearchQuery))
        {
            await SearchTextAsync();
        }
    }

    [RelayCommand]
    private async Task PreviousSearchResultAsync()
    {
        if (ActiveDocumentTab?.PreviousSearchResult() is { } result)
        {
            await SelectSearchResultAsync(result, updateStatus: true);
        }
    }

    [RelayCommand]
    private async Task NextSearchResultAsync()
    {
        if (ActiveDocumentTab?.NextSearchResult() is { } result)
        {
            await SelectSearchResultAsync(result, updateStatus: true);
        }
    }

    [RelayCommand]
    private async Task OpenSearchResultAsync(WindowsSearchResultItemViewModel? result)
    {
        if (result is null)
        {
            return;
        }

        await SelectSearchResultAsync(result, updateStatus: true);
    }

    [RelayCommand]
    private async Task ZoomInAsync()
    {
        if (ActiveDocumentTab is null)
        {
            return;
        }

        double previous = ActiveDocumentTab.ZoomFactor;
        ActiveDocumentTab.ZoomFactor = Math.Min(4.0, ActiveDocumentTab.ZoomFactor + 0.1);
        ActiveDocumentTab.ZoomText = $"{ActiveDocumentTab.ZoomFactor * 100:0}%";
        PushUndo(new ZoomChangeAction(ActiveDocumentTab, previous, ActiveDocumentTab.ZoomFactor, tab => _ = RenderActivePageAsync(tab)));
        await RenderActivePageAsync(ActiveDocumentTab);
    }

    [RelayCommand]
    private async Task ZoomOutAsync()
    {
        if (ActiveDocumentTab is null)
        {
            return;
        }

        double previous = ActiveDocumentTab.ZoomFactor;
        ActiveDocumentTab.ZoomFactor = Math.Max(0.2, ActiveDocumentTab.ZoomFactor - 0.1);
        ActiveDocumentTab.ZoomText = $"{ActiveDocumentTab.ZoomFactor * 100:0}%";
        PushUndo(new ZoomChangeAction(ActiveDocumentTab, previous, ActiveDocumentTab.ZoomFactor, tab => _ = RenderActivePageAsync(tab)));
        await RenderActivePageAsync(ActiveDocumentTab);
    }

    [RelayCommand]
    private async Task SetZoomAsync(string factor)
    {
        if (ActiveDocumentTab is null || !double.TryParse(factor, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double zoom))
        {
            return;
        }

        double previous = ActiveDocumentTab.ZoomFactor;
        ActiveDocumentTab.ZoomFactor = Math.Clamp(zoom, 0.2, 4.0);
        ActiveDocumentTab.ZoomText = $"{ActiveDocumentTab.ZoomFactor * 100:0}%";
        PushUndo(new ZoomChangeAction(ActiveDocumentTab, previous, ActiveDocumentTab.ZoomFactor, tab => _ = RenderActivePageAsync(tab)));
        await RenderActivePageAsync(ActiveDocumentTab);
    }

    [RelayCommand]
    private void DeleteAnnotationById(Guid annotationId)
    {
        if (ActiveDocumentTab is null || annotationId == Guid.Empty)
        {
            return;
        }

        int index = ActiveDocumentTab.FindAnnotationIndex(annotationId);
        DocumentAnnotation? snapshot = index >= 0 ? ActiveDocumentTab.Annotations[index] : null;
        ActiveDocumentTab.DeleteAnnotationById(annotationId);
        if (snapshot is not null)
        {
            PushUndo(new AnnotationDeleteAction(ActiveDocumentTab, snapshot));
        }

        NotifySaveStateChanged();
        StatusText = _textCatalog.GetString("status.annotation.deleted");
    }

    /// <summary>
    /// Toggles the visibility of an annotation on the current page.
    /// </summary>
    /// <param name="annotationId">The annotation identifier.</param>
    public void ToggleAnnotationVisibility(Guid annotationId)
    {
        if (ActiveDocumentTab is null || annotationId == Guid.Empty)
        {
            return;
        }

        ActiveDocumentTab.ToggleAnnotationVisibility(annotationId);
    }

    /// <summary>
    /// Toggles the lock state of an annotation to prevent accidental moves.
    /// </summary>
    /// <param name="annotationId">The annotation identifier.</param>
    public void ToggleAnnotationLock(Guid annotationId)
    {
        if (ActiveDocumentTab is null || annotationId == Guid.Empty)
        {
            return;
        }

        ActiveDocumentTab.ToggleAnnotationLock(annotationId);
    }

    /// <summary>
    /// Opens a document from the specified file path in a new or existing tab.
    /// </summary>
    /// <param name="path">The file path to open.</param>
    public async Task OpenPathAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await _documentOpenGate.WaitAsync();
        try
        {
            await OpenPathCoreAsync(path);
        }
        finally
        {
            _documentOpenGate.Release();
        }
    }

    private async Task OpenPathCoreAsync(string path)
    {
        (WindowsDocumentTabViewModel? existingTab, bool canOpen) = await RunOnUiThreadAsync(() =>
        {
            WindowsDocumentTabViewModel? found = DocumentTabs.FirstOrDefault(tab => PathsEqual(tab.FilePath, path));
            if (found is not null || DocumentTabs.Count < MaxOpenDocumentTabs)
            {
                return (found, true);
            }

            StatusText = _textCatalog.Format("notification.tabs.limit.message", MaxOpenDocumentTabs);
            return (found, false);
        });

        if (existingTab is not null)
        {
            await ActivateTabAsync(existingTab);
            return;
        }

        if (!canOpen)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            Result<IDocumentSession> result = await _openDocumentUseCase.ExecuteAsync(
                new OpenDocumentRequest(path, DocumentOpenMode.AddToTabs));

            if (result.IsFailure || result.Value is null)
            {
                await RunOnUiThreadAsync(() => StatusText = FormatError("status.open.failed", result.Error));
                return;
            }

            WindowsDocumentTabViewModel tab = await RunOnUiThreadAsync(() =>
            {
                double initialZoom = CalculateInitialFitZoom(
                    result.Value.Metadata.PixelWidth ?? 900,
                    result.Value.Metadata.PixelHeight ?? 1200);

                var newTab = new WindowsDocumentTabViewModel(result.Value.Id, result.Value.Metadata, _textCatalog)
                {
                    IsActive = true,
                    IsPagesPanelOpen = ShowThumbnails,
                    ZoomFactor = initialZoom,
                    ZoomText = $"{initialZoom * 100:0}%"
                };
                newTab.UpdateSignatureAssets(_signatureAssetLookup);

                foreach (WindowsDocumentTabViewModel existing in DocumentTabs)
                {
                    existing.IsActive = false;
                }

                DocumentTabs.Add(newTab);
                ActiveDocumentTab = newTab;
                UpdateAnnotationToolSelection();
                return newTab;
            });

            await RunOnUiThreadAsync(() =>
            {
                AddToRecentFiles(tab);
            });
            await HydrateActiveTabAsync();
            await LoadAnnotationsFromAttachmentAsync(tab);
            await RunOnUiThreadAsync(() => StatusText = _textCatalog.Format("status.opened", tab.Title));
        });
    }

    private async Task LoadAnnotationsFromAttachmentAsync(WindowsDocumentTabViewModel tab)
    {
        if (tab.DocumentType is not DocumentType.Pdf || string.IsNullOrWhiteSpace(tab.FilePath))
        {
            return;
        }

        try
        {
            IReadOnlyList<DocumentAnnotation> annotations = await _pdfAnnotationStore.LoadAsync(tab.FilePath);
            if (annotations.Count == 0)
            {
                return;
            }

            await RunOnUiThreadAsync(() =>
            {
                foreach (DocumentAnnotation annotation in annotations)
                {
                    tab.Annotations.Add(annotation);
                }

                tab.RefreshAnnotationOverlays();
                NotifySaveStateChanged();
            });
        }
        catch
        {
            // Non-critical: if annotations can't be loaded, document still opens normally.
        }
    }

    /// <summary>
    /// Opens or merges files dropped onto the home area.
    /// </summary>
    /// <param name="filePaths">The dropped file paths.</param>
    public async Task HandleHomeFilesDroppedAsync(IReadOnlyList<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        string[] supportedPaths = filePaths
            .Where(IsSupportedDocumentPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        switch (supportedPaths.Length)
        {
            case 0:
                StatusText = _textCatalog.GetString("status.merge.drop_unsupported");
                return;
            case 1:
                await OpenPathAsync(supportedPaths[0]);
                return;
            default:
                await MergeDocumentSourcesAsync(supportedPaths);
                break;
        }
    }

    /// <summary>
    /// Handles a thumbnail reorder operation from drag-drop in the sidebar.
    /// </summary>
    /// <param name="sourcePageNumber">The 1-based source page number.</param>
    /// <param name="targetIndex">The target insertion index.</param>
    public async Task HandleThumbnailReorderAsync(int sourcePageNumber, int targetIndex)
    {
        if (ActiveDocumentTab is null || IsBusy)
        {
            return;
        }

        WindowsDocumentTabViewModel? tab = ActiveDocumentTab;
        await RunOnUiThreadAsync(() =>
        {
            tab.HasPendingPageReorder = true;
            tab.IsDirty = true;
            StatusText = _textCatalog.GetString("status.pages.reordered");
        });
    }

    /// <summary>
    /// Merges files dropped onto the thumbnail panel into the active document at the specified index.
    /// </summary>
    /// <param name="filePaths">The dropped file paths.</param>
    /// <param name="insertionIndex">The page index at which to insert.</param>
    public async Task HandleThumbnailFilesDroppedAsync(IReadOnlyList<string> filePaths, int insertionIndex)
    {
        if (ActiveDocumentTab is null || IsBusy)
        {
            return;
        }

        WindowsDocumentTabViewModel? tab = ActiveDocumentTab;
        string[] supportedPaths = filePaths
            .Where(IsSupportedDocumentPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (supportedPaths.Length == 0)
        {
            StatusText = _textCatalog.GetString("status.merge.drop_unsupported");
            return;
        }

        if (tab.DocumentType is not DocumentType.Pdf)
        {
            string[] sourcePaths = insertionIndex <= 0
                ? [.. supportedPaths, tab.FilePath]
                : [tab.FilePath, .. supportedPaths];
            await MergeInPlaceAsync(tab, sourcePaths);
            return;
        }

        string tempDir = Path.Combine(Path.GetTempPath(), "velune-drop-merge", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string outputPath = Path.Combine(tempDir, $"fused-{Guid.NewGuid():N}.pdf");

        await RunBusyAsync(async () =>
        {
            try
            {
                string currentSourcePath;
                if (tab.HasPendingPageEdits)
                {
                    string? editedPath = await CreateEditedPdfAsync(
                        tab,
                        tempDir,
                        tab.Thumbnails.Select(thumbnail => thumbnail.PageNumber).ToArray(),
                        tab.GetPendingPageRotations());
                    if (string.IsNullOrWhiteSpace(editedPath))
                    {
                        return;
                    }

                    currentSourcePath = editedPath;
                }
                else
                {
                    currentSourcePath = Path.Combine(tempDir, "source.pdf");
                    await ReleaseActiveSessionAsync(tab);
                    File.Copy(tab.FilePath, currentSourcePath, overwrite: true);
                }

                WindowsDroppedMergePlan plan = WindowsDroppedMergeSourcePlanner.Create(
                    currentSourcePath,
                    tab.DocumentType,
                    tab.TotalPages,
                    supportedPaths,
                    insertionIndex,
                    tempDir);

                foreach (WindowsDroppedMergeExtraction extraction in new[] { plan.Before, plan.After }.OfType<WindowsDroppedMergeExtraction>())
                {
                    Result<string> extractionResult = await _extractPdfPagesUseCase.ExecuteAsync(
                        new ExtractPdfPagesRequest(currentSourcePath, extraction.OutputPath, extraction.Pages));
                    if (!extractionResult.IsFailure)
                    {
                        continue;
                    }

                    await RunOnUiThreadAsync(() => StatusText = FormatError(RenderFailed, extractionResult.Error));
                    await ReopenAfterFailureAsync(tab);
                    return;
                }

                if (plan.SourcePaths.Count < 2)
                {
                    await ReopenAfterFailureAsync(tab);
                    return;
                }

                Result<string> mergeResult = await _mergePdfDocumentsUseCase.ExecuteAsync(
                    new MergePdfDocumentsRequest(plan.SourcePaths.ToArray(), outputPath));

                if (mergeResult.IsFailure)
                {
                    await RunOnUiThreadAsync(() => StatusText = FormatError(RenderFailed, mergeResult.Error));
                    await ReopenAfterFailureAsync(tab);
                    return;
                }

                Result<IDocumentSession> openResult = await _openDocumentUseCase.ExecuteAsync(
                    new OpenDocumentRequest(outputPath, DocumentOpenMode.AddToTabs));

                if (openResult.IsFailure || openResult.Value is null)
                {
                    await RunOnUiThreadAsync(() => StatusText = FormatError(RenderFailed, openResult.Error));
                    await ReopenAfterFailureAsync(tab);
                    return;
                }

                IDocumentSession? newSession = openResult.Value;
                await RunOnUiThreadAsync(() =>
                {
                    double reopenZoom = CalculateInitialFitZoom(
                        newSession.Metadata.PixelWidth ?? 900,
                        newSession.Metadata.PixelHeight ?? 1200);

                    var newTab = new WindowsDocumentTabViewModel(newSession.Id, newSession.Metadata, _textCatalog)
                    {
                        IsActive = true,
                        IsPagesPanelOpen = ShowThumbnails,
                        ZoomFactor = reopenZoom,
                        ZoomText = $"{reopenZoom * 100:0}%",
                        IsDirty = true
                    };
                    newTab.UpdateSignatureAssets(_signatureAssetLookup);

                    int tabIndex = DocumentTabs.IndexOf(tab);
                    DocumentTabs.Remove(tab);
                    DocumentTabs.Insert(Math.Max(0, tabIndex), newTab);

                    foreach (WindowsDocumentTabViewModel existing in DocumentTabs)
                    {
                        existing.IsActive = ReferenceEquals(existing, newTab);
                    }

                    ActiveDocumentTab = newTab;
                    UpdateAnnotationToolSelection();
                    StatusText = _textCatalog.GetString("status.merge.fused");
                });

                await HydrateActiveTabAsync();
            }
            catch (Exception exception)
            {
                await RunOnUiThreadAsync(() => StatusText = exception.Message);
                await ReopenAfterFailureAsync(tab);
            }
        });
    }

    private async Task MergeInPlaceAsync(WindowsDocumentTabViewModel tab, string[] sourcePaths)
    {
        if (sourcePaths.Length < 2)
        {
            StatusText = _textCatalog.GetString("status.merge.selection_invalid");
            return;
        }

        string tempDir = Path.Combine(Path.GetTempPath(), "velune-fuse", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string outputPath = Path.Combine(tempDir, $"fused-{Guid.NewGuid():N}.pdf");

        await RunBusyAsync(async () =>
        {
            try
            {
                await ReleaseActiveSessionAsync(tab);

                string[] resolvedPaths = new string[sourcePaths.Length];
                for (int i = 0; i < sourcePaths.Length; i++)
                {
                    if (string.Equals(sourcePaths[i], tab.FilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        string sourceExtension = Path.GetExtension(tab.FilePath);
                        string copy = Path.Combine(tempDir, $"source-{i}{sourceExtension}");
                        File.Copy(tab.FilePath, copy);
                        resolvedPaths[i] = copy;
                    }
                    else
                    {
                        resolvedPaths[i] = sourcePaths[i];
                    }
                }

                Result<string> mergeResult = await _mergePdfDocumentsUseCase.ExecuteAsync(
                    new MergePdfDocumentsRequest(resolvedPaths, outputPath));

                if (mergeResult.IsFailure)
                {
                    await RunOnUiThreadAsync(() => StatusText = FormatError(RenderFailed, mergeResult.Error));
                    await ReopenAfterFailureAsync(tab);
                    TryDeleteDirectory(tempDir);
                    return;
                }

                Result<IDocumentSession> openResult = await _openDocumentUseCase.ExecuteAsync(
                    new OpenDocumentRequest(outputPath, DocumentOpenMode.AddToTabs));

                if (openResult.IsFailure || openResult.Value is null)
                {
                    await RunOnUiThreadAsync(() => StatusText = FormatError(RenderFailed, openResult.Error));
                    await ReopenAfterFailureAsync(tab);
                    TryDeleteDirectory(tempDir);
                    return;
                }

                IDocumentSession? newSession = openResult.Value;
                await RunOnUiThreadAsync(() =>
                {
                    double reopenZoom = CalculateInitialFitZoom(
                        newSession.Metadata.PixelWidth ?? 900,
                        newSession.Metadata.PixelHeight ?? 1200);

                    var newTab = new WindowsDocumentTabViewModel(newSession.Id, newSession.Metadata, _textCatalog)
                    {
                        IsActive = true,
                        IsPagesPanelOpen = ShowThumbnails,
                        ZoomFactor = reopenZoom,
                        ZoomText = $"{reopenZoom * 100:0}%",
                        IsDirty = true
                    };
                    newTab.UpdateSignatureAssets(_signatureAssetLookup);

                    int tabIndex = DocumentTabs.IndexOf(tab);
                    DocumentTabs.Remove(tab);
                    DocumentTabs.Insert(Math.Max(0, tabIndex), newTab);

                    foreach (WindowsDocumentTabViewModel existing in DocumentTabs)
                    {
                        existing.IsActive = ReferenceEquals(existing, newTab);
                    }

                    ActiveDocumentTab = newTab;
                    UpdateAnnotationToolSelection();
                    StatusText = _textCatalog.GetString("status.merge.fused");
                });

                await HydrateActiveTabAsync();
            }
            catch (Exception exception)
            {
                await RunOnUiThreadAsync(() => StatusText = exception.Message);
                await ReopenAfterFailureAsync(tab);
            }
        });
    }

    private static void TryDeleteDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, true);
        }
        catch
        {
            // Do nothing
        }
    }

    /// <summary>
    /// Gets whether the thumbnail panel can accept external file drops.
    /// </summary>
    public bool CanAcceptThumbnailDrop =>
        ActiveDocumentTab is not null &&
        !IsBusy &&
        !string.IsNullOrWhiteSpace(ActiveDocumentTab.FilePath);

    /// <summary>
    /// Commits pending page reorder and rotation operations to the PDF file.
    /// </summary>
    public async Task CommitPageReorderAsync()
    {
        if (ActiveDocumentTab is null || !ActiveDocumentTab.HasPendingPageEdits || IsBusy)
        {
            return;
        }

        WindowsDocumentTabViewModel? tab = ActiveDocumentTab;
        if (tab.DocumentType is not DocumentType.Pdf)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await MaterializePageEditsAsync(
                tab,
                tab.Thumbnails.Select(thumbnail => thumbnail.PageNumber).ToArray(),
                tab.GetPendingPageRotations(),
                _textCatalog.GetString("status.pages.reorder_saved"));
        });
    }

    /// <summary>
    /// Applies page order and rotation changes from the page organizer window.
    /// </summary>
    /// <param name="finalPageOrder">The final order of original page numbers.</param>
    /// <param name="rotations">The rotation to apply per original page.</param>
    public async Task ApplyPageOrganizerResultAsync(
        IReadOnlyList<int> finalPageOrder,
        IReadOnlyList<(int OriginalPage, Rotation Rotation)> rotations)
    {
        ArgumentNullException.ThrowIfNull(finalPageOrder);
        ArgumentNullException.ThrowIfNull(rotations);

        if (ActiveDocumentTab is null || IsBusy)
        {
            return;
        }

        WindowsDocumentTabViewModel? tab = ActiveDocumentTab;
        if (tab.DocumentType is not DocumentType.Pdf)
        {
            return;
        }

        if (!RequiresPageReorder(finalPageOrder, tab.TotalPages) &&
            rotations.All(item => item.Rotation is Rotation.Deg0))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await MaterializePageEditsAsync(
                tab,
                finalPageOrder,
                rotations,
                _textCatalog.GetString("status.pages.reorder_saved"));
        });
    }

    private async Task<bool> MaterializePageEditsAsync(
        WindowsDocumentTabViewModel tab,
        IReadOnlyList<int> finalPageOrder,
        IReadOnlyList<(int OriginalPage, Rotation Rotation)> rotations,
        string successStatus)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "velune-op", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string? outputPath = await CreateEditedPdfAsync(tab, tempDir, finalPageOrder, rotations);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return false;
        }

        await ReloadActiveDocumentAsync(tab, outputPath);
        await RunOnUiThreadAsync(() =>
        {
            tab.HasPendingPageReorder = false;
            tab.ClearPendingPageRotations();
            tab.IsDirty = true;
            StatusText = successStatus;
        });

        return true;
    }

    private async Task<string?> CreateEditedPdfAsync(
        WindowsDocumentTabViewModel tab,
        string tempDir,
        IReadOnlyList<int> finalPageOrder,
        IReadOnlyList<(int OriginalPage, Rotation Rotation)> rotations)
    {
        string sourceCopy = Path.Combine(tempDir, "source.pdf");
        await ReleaseActiveSessionAsync(tab);
        try
        {
            File.Copy(tab.FilePath, sourceCopy, overwrite: true);
        }
        catch (Exception exception)
        {
            await RunOnUiThreadAsync(() => StatusText = exception.Message);
            await ReopenAfterFailureAsync(tab);
            return null;
        }

        string currentPath = sourceCopy;
        if (RequiresPageReorder(finalPageOrder, tab.TotalPages))
        {
            string reorderPath = Path.Combine(tempDir, "reordered.pdf");
            Result<string> reorderResult = IsExactPagePermutation(finalPageOrder, tab.TotalPages)
                ? await _reorderPdfPagesUseCase.ExecuteAsync(
                    new ReorderPdfPagesRequest(sourceCopy, reorderPath, finalPageOrder))
                : await _extractPdfPagesUseCase.ExecuteAsync(
                    new ExtractPdfPagesRequest(sourceCopy, reorderPath, finalPageOrder));

            if (reorderResult.IsFailure)
            {
                await RunOnUiThreadAsync(() => StatusText = FormatError("status.pages.reorder_failed", reorderResult.Error));
                await ReopenAfterFailureAsync(tab);
                return null;
            }

            currentPath = reorderPath;
        }

        foreach (IGrouping<Rotation, (int OriginalPage, Rotation Rotation)> group in rotations.Where(item => item.Rotation != Rotation.Deg0).GroupBy(item => item.Rotation))
        {
            int[] pages = ResolveRotatedOutputPages(finalPageOrder, group.Select(item => item.OriginalPage));
            if (pages.Length == 0)
            {
                continue;
            }

            string rotatedPath = Path.Combine(tempDir, $"rotated-{(int)group.Key}-{Guid.NewGuid():N}.pdf");
            Result<string> rotateResult = await _rotatePdfPagesUseCase.ExecuteAsync(
                new RotatePdfPagesRequest(currentPath, rotatedPath, pages, group.Key));

            if (rotateResult.IsFailure)
            {
                await RunOnUiThreadAsync(() => StatusText = FormatError("status.rotation.update_failed", rotateResult.Error));
                await ReopenAfterFailureAsync(tab);
                return null;
            }

            currentPath = rotatedPath;
        }

        return currentPath;
    }

    private static bool RequiresPageReorder(IReadOnlyList<int> finalPageOrder, int totalPages)
    {
        return finalPageOrder.Count != totalPages || finalPageOrder.Where((t, i) => t != i + 1).Any();
    }

    private static bool IsExactPagePermutation(IReadOnlyList<int> finalPageOrder, int totalPages)
    {
        return finalPageOrder.Count == totalPages &&
            finalPageOrder.Distinct().Count() == totalPages &&
            finalPageOrder.Min() == 1 &&
            finalPageOrder.Max() == totalPages;
    }

    private static int[] ResolveRotatedOutputPages(
        IReadOnlyList<int> finalPageOrder,
        IEnumerable<int> rotatedOriginalPages)
    {
        var originals = rotatedOriginalPages.ToHashSet();
        return originals.Count == 0
            ? []
            : finalPageOrder
            .Select((originalPage, index) => originals.Contains(originalPage) ? index + 1 : 0)
            .Where(pageNumber => pageNumber > 0)
            .ToArray();
    }

    /// <summary>
    /// Gets or sets the currently selected thumbnail page number for context menu operations.
    /// </summary>
    public int SelectedThumbnailPageNumber
    {
        get; set;
    }

    /// <summary>
    /// Rotates the currently selected page by 90 degrees.
    /// </summary>
    /// <param name="clockwise">True for clockwise, false for counter-clockwise.</param>
    public async Task RotateSelectedPageAsync(bool clockwise)
    {
        if (ActiveDocumentTab is null || IsBusy)
        {
            return;
        }

        if (SelectedThumbnailPageNumber < 1)
        {
            SelectedThumbnailPageNumber = ActiveDocumentTab.CurrentPage;
        }

        WindowsDocumentTabViewModel? tab = ActiveDocumentTab;
        if (tab.DocumentType is not DocumentType.Pdf)
        {
            return;
        }

        int pageNumber = SelectedThumbnailPageNumber;
        if (pageNumber < 1 || pageNumber > tab.TotalPages)
        {
            return;
        }

        Rotation previousRotation = tab.GetPageRotation(pageNumber);
        Rotation rotation = clockwise
            ? RotateRight(previousRotation)
            : RotateLeft(previousRotation);

        WindowsPageThumbnailViewModel? thumbnail = null;
        await RunOnUiThreadAsync(() =>
        {
            tab.SetPageRotation(pageNumber, rotation);
            if (tab.HasPendingPageEdits)
            {
                tab.IsDirty = true;
            }
            thumbnail = tab.Thumbnails.FirstOrDefault(item => item.PageNumber == pageNumber);
            StatusText = _textCatalog.Format("status.rotation.set", $"{(int)rotation}°");
        });

        if (tab.CurrentPage == pageNumber)
        {
            await RenderActivePageAsync(tab);
        }

        if (thumbnail is not null)
        {
            await RefreshThumbnailAsync(tab, thumbnail);
        }

        PushUndo(new PageOperationAction(
            "Rotate page",
            () => _ = ApplyPageRotationAsync(tab, pageNumber, rotation),
            () => _ = ApplyPageRotationAsync(tab, pageNumber, previousRotation)));
    }

    private async Task ApplyPageRotationAsync(WindowsDocumentTabViewModel tab, int pageNumber, Rotation rotation)
    {
        WindowsPageThumbnailViewModel? thumbnail = null;
        await RunOnUiThreadAsync(() =>
        {
            tab.SetPageRotation(pageNumber, rotation);
            if (tab.HasPendingPageEdits)
            {
                tab.IsDirty = true;
            }

            thumbnail = tab.Thumbnails.FirstOrDefault(item => item.PageNumber == pageNumber);
            StatusText = _textCatalog.Format("status.rotation.set", $"{(int)rotation}°");
        });

        if (tab.CurrentPage == pageNumber)
        {
            await RenderActivePageAsync(tab);
        }

        if (thumbnail is not null)
        {
            await RefreshThumbnailAsync(tab, thumbnail);
        }
    }

    private static Rotation RotateRight(Rotation current) => current switch
    {
        Rotation.Deg0 => Rotation.Deg90,
        Rotation.Deg90 => Rotation.Deg180,
        Rotation.Deg180 => Rotation.Deg270,
        Rotation.Deg270 => Rotation.Deg0,
        _ => Rotation.Deg0
    };

    private static Rotation RotateLeft(Rotation current) => current switch
    {
        Rotation.Deg0 => Rotation.Deg270,
        Rotation.Deg90 => Rotation.Deg0,
        Rotation.Deg180 => Rotation.Deg90,
        Rotation.Deg270 => Rotation.Deg180,
        _ => Rotation.Deg0
    };

    private async Task RefreshThumbnailAsync(
        WindowsDocumentTabViewModel tab,
        WindowsPageThumbnailViewModel thumbnail)
    {
        await RunOnUiThreadAsync(() =>
        {
            thumbnail.Image = null;
            thumbnail.BeginRender();
        });

        ThumbnailRenderOutcome outcome = await RenderThumbnailWithRetryAsync(tab, thumbnail);
        await RunOnUiThreadAsync(() =>
        {
            if (outcome == ThumbnailRenderOutcome.Failed && thumbnail.Image is null)
            {
                thumbnail.MarkRenderFailed(_textCatalog.GetString("windows.thumbnail.unavailable"));
                return;
            }

            thumbnail.IsLoading = false;
        });
    }

    /// <summary>
    /// Moves the selected page up or down by one position.
    /// </summary>
    /// <param name="direction">-1 to move up, +1 to move down.</param>
    public async Task MoveSelectedPageAsync(int direction)
    {
        if (ActiveDocumentTab is null || IsBusy)
        {
            return;
        }

        if (SelectedThumbnailPageNumber < 1)
        {
            SelectedThumbnailPageNumber = ActiveDocumentTab.CurrentPage;
        }

        WindowsDocumentTabViewModel? tab = ActiveDocumentTab;
        if (tab.DocumentType is not DocumentType.Pdf)
        {
            return;
        }

        int pageIndex = SelectedThumbnailPageNumber - 1;
        int targetIndex = pageIndex + direction;

        if (targetIndex < 0 || targetIndex >= tab.TotalPages)
        {
            return;
        }

        var pages = Enumerable.Range(1, tab.TotalPages).ToList();
        (pages[pageIndex], pages[targetIndex]) = (pages[targetIndex], pages[pageIndex]);

        await RunBusyAsync(async () =>
        {
            if (await MaterializePageEditsAsync(
                    tab,
                    pages,
                    tab.GetPendingPageRotations(),
                    _textCatalog.GetString("status.pages.reordered")))
            {
                await RunOnUiThreadAsync(() => SelectedThumbnailPageNumber = targetIndex + 1);
            }
        });
    }

    /// <summary>
    /// Deletes the currently selected page from the active PDF document.
    /// </summary>
    public async Task DeleteSelectedPageAsync()
    {
        if (ActiveDocumentTab is null || IsBusy)
        {
            return;
        }

        if (SelectedThumbnailPageNumber < 1)
        {
            SelectedThumbnailPageNumber = ActiveDocumentTab.CurrentPage;
        }

        WindowsDocumentTabViewModel? tab = ActiveDocumentTab;
        if (tab.DocumentType is not DocumentType.Pdf || tab.TotalPages <= 1)
        {
            return;
        }

        int pageToDelete = SelectedThumbnailPageNumber;
        int[] remainingPages = Enumerable.Range(1, tab.TotalPages)
            .Where(p => p != pageToDelete)
            .ToArray();

        await RunBusyAsync(async () =>
        {
            if (await MaterializePageEditsAsync(
                    tab,
                    remainingPages,
                    tab.GetPendingPageRotations(),
                    _textCatalog.GetString("status.pages.reordered")))
            {
                await RunOnUiThreadAsync(() => SelectedThumbnailPageNumber = Math.Min(pageToDelete, tab.TotalPages));
            }
        });
    }

    /// <summary>
    /// Navigates to the specified page number in the active document.
    /// </summary>
    /// <param name="pageNumber">The 1-based page number to navigate to.</param>
    public async Task ChangePageAsync(int pageNumber)
    {
        if (ActiveDocumentTab is null ||
            pageNumber < 1 ||
            pageNumber > ActiveDocumentTab.TotalPages)
        {
            return;
        }

        CommitInlineTextAnnotation();
        WindowsDocumentTabViewModel? tab = ActiveDocumentTab;
        await RunOnUiThreadAsync(() =>
        {
            tab.CurrentPage = pageNumber;
            UpdateSelectedThumbnail(tab);
        });
        await RenderActivePageAsync(tab);
    }

    /// <summary>
    /// Begins an annotation drawing interaction at the specified pointer position.
    /// </summary>
    /// <param name="x">Pointer X in pixels.</param>
    /// <param name="y">Pointer Y in pixels.</param>
    /// <param name="width">Page layer width in pixels.</param>
    /// <param name="height">Page layer height in pixels.</param>
    /// <returns>True if the interaction was started.</returns>
    public bool BeginAnnotationInteraction(double x, double y, double width, double height)
    {
        if (ActiveDocumentTab is null ||
            ActiveDocumentTab.SelectedAnnotationTool is AnnotationTool.Select ||
            width <= 0 ||
            height <= 0)
        {
            return false;
        }

        if (ActiveDocumentTab.SelectedAnnotationTool is AnnotationTool.Signature && !CanUseSignaturePlacement)
        {
            StatusText = _textCatalog.GetString("prompt.signature.choose_first.message");
            return false;
        }

        NormalizedPoint point = DocumentAnnotationCoordinateMapper.MapVisualPointToNormalized(
            x,
            y,
            width,
            height,
            ActiveDocumentTab.Rotation);

        if (ActiveDocumentTab.SelectedAnnotationTool is AnnotationTool.Text)
        {
            CommitInlineTextAnnotation();
            DocumentAnnotation? existingText = ActiveDocumentTab.FindTextAnnotationAtPoint(point);
            if (existingText is not null)
            {
                ActiveDocumentTab.BeginInlineTextEdit(existingText);
                _editingExistingTextAnnotation = true;
                return true;
            }
        }

        _activeAnnotationStartPoint = point;
        _activeAnnotationPoints.Clear();
        _activeAnnotationPoints.Add(point);
        return true;
    }

    /// <summary>
    /// Updates an in-progress annotation drawing with the current pointer position.
    /// </summary>
    /// <param name="x">Pointer X in pixels.</param>
    /// <param name="y">Pointer Y in pixels.</param>
    /// <param name="width">Page layer width in pixels.</param>
    /// <param name="height">Page layer height in pixels.</param>
    public void UpdateAnnotationInteraction(double x, double y, double width, double height)
    {
        if (ActiveDocumentTab is null ||
            _activeAnnotationStartPoint is null ||
            width <= 0 ||
            height <= 0)
        {
            return;
        }

        NormalizedPoint point = DocumentAnnotationCoordinateMapper.MapVisualPointToNormalized(
            x,
            y,
            width,
            height,
            ActiveDocumentTab.Rotation);

        if (ActiveDocumentTab.SelectedAnnotationTool is AnnotationTool.Ink)
        {
            _activeAnnotationPoints.Add(point);
            ReplacePreviewAnnotation(CreateInkAnnotation(_activeAnnotationPoints));
            return;
        }

        if (CreateBoxAnnotation(point) is { } annotation)
        {
            ReplacePreviewAnnotation(annotation);
        }
    }

    /// <summary>
    /// Completes an annotation drawing interaction and creates the annotation.
    /// </summary>
    /// <param name="x">Pointer X in pixels.</param>
    /// <param name="y">Pointer Y in pixels.</param>
    /// <param name="width">Page layer width in pixels.</param>
    /// <param name="height">Page layer height in pixels.</param>
    public void CompleteAnnotationInteraction(double x, double y, double width, double height)
    {
        if (_editingExistingTextAnnotation)
        {
            _editingExistingTextAnnotation = false;
            return;
        }

        if (ActiveDocumentTab is null || _activeAnnotationStartPoint is null)
        {
            CancelAnnotationInteraction();
            return;
        }

        UpdateAnnotationInteraction(x, y, width, height);
        ClearPreviewAnnotation();
        DocumentAnnotation? annotation = ActiveDocumentTab.SelectedAnnotationTool is AnnotationTool.Ink
            ? CreateInkAnnotation(_activeAnnotationPoints)
            : CreateBoxAnnotation(DocumentAnnotationCoordinateMapper.MapVisualPointToNormalized(
                x,
                y,
                width,
                height,
                ActiveDocumentTab.Rotation));

        if (annotation is null)
        {
            CancelAnnotationInteraction();
            return;
        }

        ActiveDocumentTab.AddAnnotation(annotation);
        PushUndo(new AnnotationAddAction(ActiveDocumentTab, annotation));
        NotifySaveStateChanged();
        switch (annotation.Kind)
        {
            case DocumentAnnotationKind.Text:
                ActiveDocumentTab.BeginInlineTextEdit(annotation);
                break;
            case DocumentAnnotationKind.Note:
                {
                    SelectedAnnotationPanelTab = "Comments";
                    WindowsCommentOverlayViewModel? commentVm = ActiveDocumentTab.CurrentPageCommentOverlays
                        .FirstOrDefault(c => c.Id == annotation.Id);
                    if (commentVm is not null)
                    {
                        BeginCommentEdit(commentVm);
                    }

                    break;
                }
        }

        StatusText = _textCatalog.Format("status.annotation.added", GetAnnotationLabel(ActiveDocumentTab.SelectedAnnotationTool));
        CancelAnnotationInteraction();
    }

    /// <summary>
    /// Cancels the current annotation drawing interaction.
    /// </summary>
    public void CancelAnnotationInteraction()
    {
        ClearPreviewAnnotation();
        _activeAnnotationStartPoint = null;
        _activeAnnotationPoints.Clear();
    }

    /// <summary>
    /// Begins a drag-move operation on an existing annotation at the pointer position.
    /// </summary>
    /// <param name="x">Pointer X in pixels.</param>
    /// <param name="y">Pointer Y in pixels.</param>
    /// <param name="width">Page layer width in pixels.</param>
    /// <param name="height">Page layer height in pixels.</param>
    /// <returns>True if an annotation was found and the move started.</returns>
    public bool BeginAnnotationMove(double x, double y, double width, double height)
    {
        if (ActiveDocumentTab is null || width <= 0 || height <= 0)
        {
            return false;
        }

        NormalizedPoint point = DocumentAnnotationCoordinateMapper.MapVisualPointToNormalized(
            x, y, width, height, ActiveDocumentTab.Rotation);

        // Check handles on currently selected annotation first (handles may be outside bounds)
        if (SelectedAnnotationId is not null)
        {
            DocumentAnnotation? selected = ActiveDocumentTab.Annotations
                .FirstOrDefault(a => a.Id == SelectedAnnotationId);
            if (selected is not null)
            {
                NormalizedTextRegion selectedBounds = selected.Bounds ?? ComputeInkBounds(selected);
                NormalizedPoint selectedHandlePoint = UnrotatePoint(point, selectedBounds, selected.Appearance.RotationAngle, width, height);
                ResizeHandle selectedHandle = DetectResizeHandle(selectedHandlePoint, selectedBounds, width, height);
                if (selectedHandle is ResizeHandle.Rotate)
                {
                    double cxPixel = (selectedBounds.X + selectedBounds.Width / 2) * width;
                    double cyPixel = (selectedBounds.Y + selectedBounds.Height / 2) * height;
                    _rotatingAnnotationCenterX = cxPixel;
                    _rotatingAnnotationCenterY = cyPixel;
                    _rotatingAnnotationStartAngle = Math.Atan2(y - cyPixel, x - cxPixel) * 180 / Math.PI;
                    _rotatingAnnotationOriginalRotation = selected.Appearance.RotationAngle;
                    _isRotatingAnnotation = true;
                    _resizingAnnotationId = selected.Id;
                    _resizingAnnotationOriginalBounds = selectedBounds;
                    _resizingHandle = ResizeHandle.Rotate;
                    _interactionAnnotationSnapshot = selected;
                    return true;
                }

                if (selectedHandle is not ResizeHandle.None)
                {
                    IReadOnlyList<NormalizedPoint>? selectedInkPoints = selected.Kind is DocumentAnnotationKind.Ink
                        ? selected.Points.ToList()
                        : null;
                    _resizingAnnotationId = selected.Id;
                    _resizingAnnotationStartPoint = point;
                    _resizingAnnotationOriginalBounds = selectedBounds;
                    _resizingAnnotationOriginalPoints = selectedInkPoints;
                    _resizingHandle = selectedHandle;
                    _interactionAnnotationSnapshot = selected;
                    return true;
                }
            }
        }

        DocumentAnnotation? annotation = ActiveDocumentTab.FindAnnotationAtPoint(point, width, height);
        if (annotation is null)
        {
            SelectedAnnotationId = null;
            return false;
        }

        SelectedAnnotationId = annotation.Id;

        NormalizedTextRegion effectiveBounds = annotation.Bounds ?? ComputeInkBounds(annotation);
        IReadOnlyList<NormalizedPoint>? inkPoints = annotation.Kind is DocumentAnnotationKind.Ink
            ? annotation.Points.ToList()
            : null;

        NormalizedPoint handlePoint = UnrotatePoint(point, effectiveBounds, annotation.Appearance.RotationAngle, width, height);
        ResizeHandle handle = DetectResizeHandle(handlePoint, effectiveBounds, width, height);
        if (handle is ResizeHandle.Rotate)
        {
            double cxPixel = (effectiveBounds.X + effectiveBounds.Width / 2) * width;
            double cyPixel = (effectiveBounds.Y + effectiveBounds.Height / 2) * height;
            _rotatingAnnotationCenterX = cxPixel;
            _rotatingAnnotationCenterY = cyPixel;
            _rotatingAnnotationStartAngle = Math.Atan2(y - cyPixel, x - cxPixel) * 180 / Math.PI;
            _rotatingAnnotationOriginalRotation = annotation.Appearance.RotationAngle;
            _isRotatingAnnotation = true;
            _resizingAnnotationId = annotation.Id;
            _resizingAnnotationOriginalBounds = effectiveBounds;
            _resizingHandle = ResizeHandle.Rotate;
            _interactionAnnotationSnapshot = annotation;
            return true;
        }

        if (handle is not ResizeHandle.None)
        {
            _resizingAnnotationId = annotation.Id;
            _resizingAnnotationStartPoint = point;
            _resizingAnnotationOriginalBounds = effectiveBounds;
            _resizingAnnotationOriginalPoints = inkPoints;
            _resizingHandle = handle;
            _interactionAnnotationSnapshot = annotation;
            return true;
        }

        _movingAnnotationId = annotation.Id;
        _movingAnnotationStartPoint = point;
        _movingAnnotationOriginalBounds = effectiveBounds;
        _movingAnnotationOriginalPoints = inkPoints;
        _interactionAnnotationSnapshot = annotation;
        return true;
    }

    private static NormalizedTextRegion ComputeInkBounds(DocumentAnnotation annotation)
    {
        if (annotation.Points.Count == 0)
        {
            return new NormalizedTextRegion(0, 0, 1, 1);
        }

        double minX = annotation.Points.Min(p => p.X);
        double maxX = annotation.Points.Max(p => p.X);
        double minY = annotation.Points.Min(p => p.Y);
        double maxY = annotation.Points.Max(p => p.Y);

        double x = Math.Max(0, minX);
        double y = Math.Max(0, minY);
        double w = Math.Max(0.01, maxX - minX);
        double h = Math.Max(0.01, maxY - minY);

        return new NormalizedTextRegion(x, y, Math.Min(w, 1 - x), Math.Min(h, 1 - y));
    }

    private static NormalizedPoint UnrotatePoint(NormalizedPoint point, NormalizedTextRegion bounds, double angleDeg, double pageWidth, double pageHeight)
    {
        if (Math.Abs(angleDeg) < 0.01)
        {
            return point;
        }

        double cx = (bounds.X + bounds.Width / 2) * pageWidth;
        double cy = (bounds.Y + bounds.Height / 2) * pageHeight;
        double px = point.X * pageWidth;
        double py = point.Y * pageHeight;
        double rad = -angleDeg * Math.PI / 180;
        double cos = Math.Cos(rad);
        double sin = Math.Sin(rad);
        double dx = px - cx;
        double dy = py - cy;
        double rx = cx + dx * cos - dy * sin;
        double ry = cy + dx * sin + dy * cos;
        return new NormalizedPoint(rx / pageWidth, ry / pageHeight);
    }

    private static ResizeHandle DetectResizeHandle(NormalizedPoint point, NormalizedTextRegion bounds, double pageWidth, double pageHeight)
    {
        double threshold = 0.015;
        double left = bounds.X;
        double top = bounds.Y;
        double right = bounds.X + bounds.Width;
        double bottom = bounds.Y + bounds.Height;

        double rotateCenterX = (left + right) / 2;
        double rotateCenterY = top - 20 / pageHeight;
        if (Math.Abs(point.X - rotateCenterX) < threshold && Math.Abs(point.Y - rotateCenterY) < threshold)
        {
            return ResizeHandle.Rotate;
        }

        if (Math.Abs(point.X - left) < threshold && Math.Abs(point.Y - top) < threshold)
        {
            return ResizeHandle.TopLeft;
        }

        if (Math.Abs(point.X - right) < threshold && Math.Abs(point.Y - top) < threshold)
        {
            return ResizeHandle.TopRight;
        }

        if (Math.Abs(point.X - left) < threshold && Math.Abs(point.Y - bottom) < threshold)
        {
            return ResizeHandle.BottomLeft;
        }

        if (Math.Abs(point.X - right) < threshold && Math.Abs(point.Y - bottom) < threshold)
        {
            return ResizeHandle.BottomRight;
        }

        return ResizeHandle.None;
    }

    /// <summary>
    /// Updates the position of the annotation being moved.
    /// </summary>
    /// <param name="x">Pointer X in pixels.</param>
    /// <param name="y">Pointer Y in pixels.</param>
    /// <param name="width">Page layer width in pixels.</param>
    /// <param name="height">Page layer height in pixels.</param>
    public void UpdateAnnotationMove(double x, double y, double width, double height)
    {
        if (_resizingAnnotationId is not null)
        {
            UpdateAnnotationResize(x, y, width, height);
            return;
        }

        if (ActiveDocumentTab is null ||
            _movingAnnotationId is null ||
            _movingAnnotationStartPoint is null ||
            _movingAnnotationOriginalBounds is null ||
            width <= 0 || height <= 0)
        {
            return;
        }

        NormalizedPoint point = DocumentAnnotationCoordinateMapper.MapVisualPointToNormalized(
            x, y, width, height, ActiveDocumentTab.Rotation);

        double dx = point.X - _movingAnnotationStartPoint.X;
        double dy = point.Y - _movingAnnotationStartPoint.Y;
        NormalizedTextRegion bounds = _movingAnnotationOriginalBounds;

        double newX = Math.Clamp(bounds.X + dx, 0, 1 - bounds.Width);
        double newY = Math.Clamp(bounds.Y + dy, 0, 1 - bounds.Height);
        var newBounds = new NormalizedTextRegion(newX, newY, bounds.Width, bounds.Height);

        if (_movingAnnotationOriginalPoints is not null)
        {
            ActiveDocumentTab.TransformInkAnnotation(_movingAnnotationId.Value, bounds, newBounds, _movingAnnotationOriginalPoints);
        }
        else
        {
            ActiveDocumentTab.MoveAnnotation(_movingAnnotationId.Value, newBounds);
        }
    }

    /// <summary>
    /// Completes the annotation move and commits the final position.
    /// </summary>
    /// <param name="x">Pointer X in pixels.</param>
    /// <param name="y">Pointer Y in pixels.</param>
    /// <param name="width">Page layer width in pixels.</param>
    /// <param name="height">Page layer height in pixels.</param>
    public void CompleteAnnotationMove(double x, double y, double width, double height)
    {
        if (_resizingAnnotationId is not null)
        {
            Guid completedId = _resizingAnnotationId.Value;
            UpdateAnnotationResize(x, y, width, height);
            PushInteractionUndo(completedId);
            _resizingAnnotationId = null;
            _resizingAnnotationStartPoint = null;
            _resizingAnnotationOriginalBounds = null;
            _resizingAnnotationOriginalPoints = null;
            _resizingHandle = ResizeHandle.None;
            _isRotatingAnnotation = false;
            return;
        }

        Guid movedId = _movingAnnotationId ?? Guid.Empty;
        UpdateAnnotationMove(x, y, width, height);
        if (movedId != Guid.Empty)
        {
            PushInteractionUndo(movedId);
        }

        _movingAnnotationId = null;
        _movingAnnotationStartPoint = null;
        _movingAnnotationOriginalBounds = null;
        _movingAnnotationOriginalPoints = null;
    }

    public void UpdateAnnotationResize(double x, double y, double width, double height)
    {
        if (_isRotatingAnnotation)
        {
            UpdateAnnotationRotation(x, y, width, height);
            return;
        }

        if (ActiveDocumentTab is null ||
            _resizingAnnotationId is null ||
            _resizingAnnotationStartPoint is null ||
            _resizingAnnotationOriginalBounds is null ||
            width <= 0 || height <= 0)
        {
            return;
        }

        NormalizedPoint point = DocumentAnnotationCoordinateMapper.MapVisualPointToNormalized(
            x, y, width, height, ActiveDocumentTab.Rotation);

        NormalizedTextRegion orig = _resizingAnnotationOriginalBounds;
        double dx = point.X - _resizingAnnotationStartPoint.X;
        double dy = point.Y - _resizingAnnotationStartPoint.Y;

        double newX = orig.X;
        double newY = orig.Y;
        double newW = orig.Width;
        double newH = orig.Height;

        switch (_resizingHandle)
        {
            case ResizeHandle.TopLeft:
                newX = Math.Clamp(orig.X + dx, 0, orig.X + orig.Width - 0.01);
                newY = Math.Clamp(orig.Y + dy, 0, orig.Y + orig.Height - 0.01);
                newW = orig.Width - (newX - orig.X);
                newH = orig.Height - (newY - orig.Y);
                break;
            case ResizeHandle.TopRight:
                newY = Math.Clamp(orig.Y + dy, 0, orig.Y + orig.Height - 0.01);
                newW = Math.Clamp(orig.Width + dx, 0.01, 1 - orig.X);
                newH = orig.Height - (newY - orig.Y);
                break;
            case ResizeHandle.BottomLeft:
                newX = Math.Clamp(orig.X + dx, 0, orig.X + orig.Width - 0.01);
                newW = orig.Width - (newX - orig.X);
                newH = Math.Clamp(orig.Height + dy, 0.01, 1 - orig.Y);
                break;
            case ResizeHandle.BottomRight:
                newW = Math.Clamp(orig.Width + dx, 0.01, 1 - orig.X);
                newH = Math.Clamp(orig.Height + dy, 0.01, 1 - orig.Y);
                break;
        }

        newW = Math.Clamp(newW, 0.01, 1 - newX);
        newH = Math.Clamp(newH, 0.01, 1 - newY);
        var newBounds = new NormalizedTextRegion(newX, newY, newW, newH);

        if (_resizingAnnotationOriginalPoints is not null)
        {
            ActiveDocumentTab.TransformInkAnnotation(_resizingAnnotationId.Value, orig, newBounds, _resizingAnnotationOriginalPoints);
        }
        else
        {
            ActiveDocumentTab.MoveAnnotation(_resizingAnnotationId.Value, newBounds);
        }
    }

    private void UpdateAnnotationRotation(double x, double y, double width, double height)
    {
        if (ActiveDocumentTab is null ||
            _resizingAnnotationId is null ||
            _resizingAnnotationOriginalBounds is null ||
            width <= 0 || height <= 0)
        {
            return;
        }

        double currentAngle = Math.Atan2(y - _rotatingAnnotationCenterY, x - _rotatingAnnotationCenterX) * 180 / Math.PI;
        double deltaAngle = currentAngle - _rotatingAnnotationStartAngle;
        double newRotation = _rotatingAnnotationOriginalRotation + deltaAngle;

        int index = ActiveDocumentTab.FindAnnotationIndex(_resizingAnnotationId.Value);
        if (index < 0)
        {
            return;
        }

        DocumentAnnotation annotation = ActiveDocumentTab.Annotations[index];
        AnnotationAppearance updated = new(
            annotation.Appearance.StrokeHex,
            annotation.Appearance.FillHex,
            annotation.Appearance.StrokeThickness,
            annotation.Appearance.Opacity,
            annotation.Appearance.FontSize,
            annotation.Appearance.FontFamily,
            newRotation);
        ActiveDocumentTab.Annotations[index] = new DocumentAnnotation(
            annotation.Id,
            annotation.Kind,
            annotation.PageIndex,
            updated,
            annotation.Bounds,
            annotation.Points,
            annotation.Text,
            annotation.AssetId,
            annotation.CreatedAt);
        ActiveDocumentTab.RefreshAnnotationOverlays(SelectedAnnotationId);
    }

    /// <summary>
    /// Cancels the annotation move and restores the original position.
    /// </summary>
    public void CancelAnnotationMove()
    {
        if (_resizingAnnotationId is not null && _resizingAnnotationOriginalBounds is not null && ActiveDocumentTab is not null)
        {
            if (_resizingAnnotationOriginalPoints is not null)
            {
                ActiveDocumentTab.TransformInkAnnotation(
                    _resizingAnnotationId.Value, _resizingAnnotationOriginalBounds, _resizingAnnotationOriginalBounds, _resizingAnnotationOriginalPoints);
            }
            else
            {
                ActiveDocumentTab.MoveAnnotation(_resizingAnnotationId.Value, _resizingAnnotationOriginalBounds);
            }
        }

        _resizingAnnotationId = null;
        _resizingAnnotationStartPoint = null;
        _resizingAnnotationOriginalBounds = null;
        _resizingAnnotationOriginalPoints = null;
        _resizingHandle = ResizeHandle.None;
        _isRotatingAnnotation = false;

        if (_movingAnnotationId is not null && _movingAnnotationOriginalBounds is not null && ActiveDocumentTab is not null)
        {
            if (_movingAnnotationOriginalPoints is not null)
            {
                ActiveDocumentTab.TransformInkAnnotation(
                    _movingAnnotationId.Value, _movingAnnotationOriginalBounds, _movingAnnotationOriginalBounds, _movingAnnotationOriginalPoints);
            }
            else
            {
                ActiveDocumentTab.MoveAnnotation(_movingAnnotationId.Value, _movingAnnotationOriginalBounds);
            }
        }

        _movingAnnotationId = null;
        _movingAnnotationStartPoint = null;
        _movingAnnotationOriginalBounds = null;
        _movingAnnotationOriginalPoints = null;
    }

    /// <summary>
    /// Begins capturing a freehand signature stroke.
    /// </summary>
    /// <param name="x">Pointer X in pixels.</param>
    /// <param name="y">Pointer Y in pixels.</param>
    /// <param name="width">Signature pad width in pixels.</param>
    /// <param name="height">Signature pad height in pixels.</param>
    public void BeginSignatureCapture(double x, double y, double width, double height)
    {
        _signatureCapturePoints.Clear();
        UpdateSignatureCapture(x, y, width, height);
    }

    /// <summary>
    /// Adds a point to the in-progress signature stroke.
    /// </summary>
    /// <param name="x">Pointer X in pixels.</param>
    /// <param name="y">Pointer Y in pixels.</param>
    /// <param name="width">Signature pad width in pixels.</param>
    /// <param name="height">Signature pad height in pixels.</param>
    public void UpdateSignatureCapture(double x, double y, double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        NormalizedPoint point = DocumentAnnotationCoordinateMapper.MapVisualPointToNormalized(
            x,
            y,
            width,
            height,
            Rotation.Deg0);
        _signatureCapturePoints.Add(point);
        RefreshSignaturePadPreview();
    }

    /// <summary>
    /// Completes the signature capture stroke.
    /// </summary>
    public void CompleteSignatureCapture()
    {
        RefreshSignaturePadPreview();
    }

    /// <summary>
    /// Opens the inline editor for the annotation with the specified identifier.
    /// </summary>
    /// <param name="annotationId">The annotation to edit.</param>
    public void BeginEditAnnotationById(Guid annotationId)
    {
        if (ActiveDocumentTab is null)
        {
            return;
        }

        CommitInlineTextAnnotation();
        DocumentAnnotation? annotation = ActiveDocumentTab.Annotations.FirstOrDefault(a => a.Id == annotationId);
        if (annotation is null)
        {
            return;
        }

        ActiveDocumentTab.BeginInlineTextEdit(annotation);
    }

    /// <summary>
    /// Begins editing a comment annotation's text.
    /// </summary>
    /// <param name="comment">The comment overlay to edit.</param>
    public void BeginCommentEdit(WindowsCommentOverlayViewModel comment)
    {
        ArgumentNullException.ThrowIfNull(comment);
        comment.EditText = comment.Text;
        comment.IsEditing = true;
    }

    /// <summary>
    /// Commits the edited comment text to the underlying annotation.
    /// </summary>
    /// <param name="comment">The comment overlay being edited.</param>
    public void CommitCommentEdit(WindowsCommentOverlayViewModel comment)
    {
        ArgumentNullException.ThrowIfNull(comment);

        if (!comment.IsEditing)
        {
            return;
        }

        comment.IsEditing = false;
        string text = comment.EditText.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        ActiveDocumentTab?.UpdateAnnotationText(comment.Id, text);
        ActiveDocumentTab?.RefreshAnnotationOverlays(SelectedAnnotationId);
    }

    /// <summary>
    /// Updates the text content of the annotation currently being edited inline.
    /// </summary>
    /// <param name="text">The new text content.</param>
    public void UpdateInlineTextAnnotation(string? text)
    {
        if (ActiveDocumentTab?.InlineTextEditor is not { } editor)
        {
            return;
        }

        ActiveDocumentTab.UpdateAnnotationText(editor.AnnotationId, text);
    }

    /// <summary>
    /// Commits the inline text editor content and closes the editor.
    /// </summary>
    public void CommitInlineTextAnnotation()
    {
        if (ActiveDocumentTab?.InlineTextEditor is not { } editor)
        {
            return;
        }

        DocumentAnnotation? annotation = ActiveDocumentTab.Annotations.FirstOrDefault(a => a.Id == editor.AnnotationId);
        string text = editor.Text.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            if (annotation?.Kind is DocumentAnnotationKind.Note or DocumentAnnotationKind.Stamp)
            {
                ActiveDocumentTab.EndInlineTextEdit();
                return;
            }

            ActiveDocumentTab.DeleteAnnotationById(editor.AnnotationId);
            NotifySaveStateChanged();
            StatusText = _textCatalog.GetString("status.annotation.deleted");
            return;
        }

        ActiveDocumentTab.UpdateAnnotationText(editor.AnnotationId, text);
        ActiveDocumentTab.EndInlineTextEdit();
    }

    /// <summary>
    /// Cancels the inline text editor and removes the annotation if it has no content.
    /// </summary>
    public void CancelInlineTextAnnotation()
    {
        if (ActiveDocumentTab?.InlineTextEditor is not { } editor)
        {
            return;
        }

        DocumentAnnotation? annotation = ActiveDocumentTab.Annotations.FirstOrDefault(a => a.Id == editor.AnnotationId);
        if (annotation?.Kind is DocumentAnnotationKind.Note or DocumentAnnotationKind.Stamp)
        {
            ActiveDocumentTab.EndInlineTextEdit();
            return;
        }

        ActiveDocumentTab.DeleteAnnotationById(editor.AnnotationId);
        NotifySaveStateChanged();
        StatusText = _textCatalog.GetString("status.annotation.deleted");
    }

    /// <summary>
    /// Begins a text selection interaction on the document page.
    /// </summary>
    /// <param name="x">Pointer X in pixels.</param>
    /// <param name="y">Pointer Y in pixels.</param>
    /// <param name="width">Page layer width in pixels.</param>
    /// <param name="height">Page layer height in pixels.</param>
    /// <returns>True if text selection started successfully.</returns>
    public bool BeginDocumentTextSelection(double x, double y, double width, double height)
    {
        if (ActiveDocumentTab is not { } tab)
        {
            ClearDocumentTextSelection();
            return false;
        }

        if (tab.DocumentTextIndex is null)
        {
            _ = PrepareTextSelectionAsync(tab);
            ClearDocumentTextSelection();
            return false;
        }

        if (!TryResolveDocumentTextPoint(x, y, width, height, out DocumentTextSelectionPoint anchorPoint))
        {
            ClearDocumentTextSelection();
            return false;
        }

        tab.DocumentTextSelectionAnchorPoint = anchorPoint;
        return UpdateDocumentTextSelection(x, y, width, height);
    }

    /// <summary>
    /// Updates the active text selection range based on the current pointer position.
    /// </summary>
    /// <param name="x">Pointer X in pixels.</param>
    /// <param name="y">Pointer Y in pixels.</param>
    /// <param name="width">Page layer width in pixels.</param>
    /// <param name="height">Page layer height in pixels.</param>
    /// <returns>True if the selection has content.</returns>
    public bool UpdateDocumentTextSelection(double x, double y, double width, double height)
    {
        if (ActiveDocumentTab is not { } tab ||
            tab.DocumentTextIndex is null ||
            tab.DocumentTextSelectionAnchorPoint is not { } anchorPoint ||
            !TryResolveDocumentTextPoint(x, y, width, height, out DocumentTextSelectionPoint? activePoint) ||
            _documentSessionStore.Current is not { } session)
        {
            return false;
        }

        Result<DocumentTextSelectionResult> result = _resolveDocumentTextSelectionUseCase.Execute(
            new DocumentTextSelectionRequest(
                session,
                tab.DocumentTextIndex,
                new PageIndex(Math.Max(0, tab.CurrentPage - 1)),
                anchorPoint,
                activePoint));

        if (result.IsFailure || result.Value is not { } selection)
        {
            ClearDocumentTextSelection();
            return false;
        }

        tab.CurrentDocumentTextSelection = selection;
        tab.RefreshDocumentTextSelectionHighlights();
        NotifySelectedDocumentTextChanged();
        return selection.HasSelection;
    }

    /// <summary>
    /// Completes the text selection interaction.
    /// </summary>
    public void CompleteDocumentTextSelection()
    {
        if (ActiveDocumentTab is not null)
        {
            ActiveDocumentTab.DocumentTextSelectionAnchorPoint = null;
        }

        if (HasSelectedDocumentText)
        {
            StatusText = _textCatalog.GetString("status.text.selected");
        }
    }

    /// <summary>
    /// Creates highlight annotations from the current text selection.
    /// </summary>
    public void CreateHighlightFromTextSelection()
    {
        if (ActiveDocumentTab?.CurrentDocumentTextSelection is not { HasSelection: true } selection)
        {
            ClearDocumentTextSelection();
            return;
        }

        AnnotationAppearance appearance = CurrentAnnotationAppearance();
        foreach (NormalizedTextRegion region in selection.Regions)
        {
            var annotation = new DocumentAnnotation(
                Guid.NewGuid(),
                DocumentAnnotationKind.Highlight,
                selection.PageIndex,
                appearance,
                region);
            ActiveDocumentTab.AddAnnotation(annotation);
            PushUndo(new AnnotationAddAction(ActiveDocumentTab, annotation));
        }

        NotifySaveStateChanged();
        StatusText = _textCatalog.Format("status.annotation.added", _textCatalog.GetString("annotation.kind.highlight"));
        ClearDocumentTextSelection();
    }

    /// <summary>
    /// Clears the current document text selection and its visual highlights.
    /// </summary>
    public void ClearDocumentTextSelection()
    {
        if (ActiveDocumentTab is null)
        {
            return;
        }

        ActiveDocumentTab.DocumentTextSelectionAnchorPoint = null;
        ActiveDocumentTab.CurrentDocumentTextSelection = null;
        ActiveDocumentTab.RefreshDocumentTextSelectionHighlights();
        NotifySelectedDocumentTextChanged();
    }

    /// <summary>
    /// Adjusts the current text selection by the given character delta.
    /// Positive delta extends selection forward, negative shrinks it.
    /// </summary>
    public void AdjustDocumentTextSelection(int delta)
    {
        if (ActiveDocumentTab is not { } tab ||
            tab.DocumentTextIndex is null ||
            tab.CurrentDocumentTextSelection is not { HasSelection: true } current ||
            current.StartCharacterIndex < 0 || current.EndCharacterIndex < 0)
        {
            return;
        }

        int newEnd = current.EndCharacterIndex + delta;
        if (newEnd < current.StartCharacterIndex)
        {
            newEnd = current.StartCharacterIndex;
        }

        Result<DocumentTextSelectionResult> result = _resolveDocumentTextSelectionUseCase.ExecuteByRange(
            tab.DocumentTextIndex,
            new PageIndex(Math.Max(0, tab.CurrentPage - 1)),
            current.StartCharacterIndex,
            newEnd);

        if (result is { IsSuccess: true, Value: { HasSelection: true } selection })
        {
            tab.CurrentDocumentTextSelection = selection;
            tab.RefreshDocumentTextSelectionHighlights();
            NotifySelectedDocumentTextChanged();
        }
    }

    /// <summary>
    /// Adjusts the current text selection by one word boundary.
    /// Positive direction extends to next word end, negative shrinks to previous word start.
    /// </summary>
    public void AdjustDocumentTextSelectionByWord(int direction)
    {
        if (ActiveDocumentTab is not { } tab ||
            tab.DocumentTextIndex is null ||
            tab.CurrentDocumentTextSelection is not { HasSelection: true } current ||
            current.StartCharacterIndex < 0 || current.EndCharacterIndex < 0)
        {
            return;
        }

        PageIndex pageIndex = new(Math.Max(0, tab.CurrentPage - 1));
        PageTextContent? pageContent = tab.DocumentTextIndex.Pages
            .FirstOrDefault(p => p.PageIndex == pageIndex);
        if (pageContent is null || pageContent.Text.Length == 0)
        {
            return;
        }

        string text = pageContent.Text;
        int newEnd;

        if (direction > 0)
        {
            newEnd = current.EndCharacterIndex + 1;
            while (newEnd < text.Length && !char.IsWhiteSpace(text[newEnd]))
            {
                newEnd++;
            }

            while (newEnd < text.Length && char.IsWhiteSpace(text[newEnd]))
            {
                newEnd++;
            }

            newEnd = Math.Max(current.EndCharacterIndex, newEnd - 1);
        }
        else
        {
            newEnd = current.EndCharacterIndex - 1;
            while (newEnd > current.StartCharacterIndex && char.IsWhiteSpace(text[newEnd]))
            {
                newEnd--;
            }

            while (newEnd > current.StartCharacterIndex && !char.IsWhiteSpace(text[newEnd - 1]))
            {
                newEnd--;
            }

            newEnd = Math.Max(current.StartCharacterIndex, newEnd);
        }

        Result<DocumentTextSelectionResult> result = _resolveDocumentTextSelectionUseCase.ExecuteByRange(
            tab.DocumentTextIndex, pageIndex, current.StartCharacterIndex, newEnd);

        if (result is { IsSuccess: true, Value: { HasSelection: true } selection })
        {
            tab.CurrentDocumentTextSelection = selection;
            tab.RefreshDocumentTextSelectionHighlights();
            NotifySelectedDocumentTextChanged();
        }
    }

    /// <summary>
    /// Notifies the user that the selected text was copied to the clipboard.
    /// </summary>
    public void NotifySelectedDocumentTextCopied()
    {
        StatusText = _textCatalog.GetString("status.clipboard.copied");
    }

    private async Task ReloadActiveDocumentAsync(WindowsDocumentTabViewModel tab, string? newFilePath = null)
    {
        await _renderOrchestrator.CancelDocumentJobsAsync(tab.SessionId);

        string pathToOpen = newFilePath ?? tab.FilePath;
        Result<IDocumentSession> openResult = await _openDocumentUseCase.ExecuteAsync(
            new OpenDocumentRequest(pathToOpen, DocumentOpenMode.ReplaceCurrent));

        if (openResult.IsFailure || openResult.Value is null)
        {
            return;
        }

        IDocumentSession? newSession = openResult.Value;
        int pageCount = newSession.Metadata.PageCount ?? tab.TotalPages;

        await RunOnUiThreadAsync(() =>
        {
            tab.SessionId = newSession.Id;
            if (newFilePath is not null)
            {
                tab.FilePath = newFilePath;
            }

            tab.TotalPages = pageCount;
            tab.CurrentPage = Math.Min(tab.CurrentPage, pageCount);
            tab.CurrentPageImage = null;
            tab.Thumbnails.Clear();
            for (int page = 1; page <= pageCount; page++)
            {
                tab.Thumbnails.Add(new WindowsPageThumbnailViewModel(
                    page,
                    _textCatalog.Format("windows.thumbnail.page", page),
                    _textCatalog.GetString("windows.thumbnail.loading")));
            }
        });

        await HydrateActiveTabAsync();
    }

    private async Task ReleaseActiveSessionAsync(WindowsDocumentTabViewModel tab)
    {
        await _renderOrchestrator.CancelDocumentJobsAsync(tab.SessionId);

        IReadOnlyList<IDocumentSession> sessions = _documentSessionStore.Sessions;
        IDocumentSession? session = sessions.FirstOrDefault(s => s.Id == tab.SessionId);
        if (session is IReleasableDocumentSession releasable)
        {
            releasable.ReleaseResources();
        }

        _documentSessionStore.Remove(tab.SessionId);
    }

    private async Task ReopenAfterFailureAsync(WindowsDocumentTabViewModel tab)
    {
        Result<IDocumentSession> openResult = await _openDocumentUseCase.ExecuteAsync(
            new OpenDocumentRequest(tab.FilePath, DocumentOpenMode.AddToTabs));

        if (openResult is { IsSuccess: true, Value: not null })
        {
            await RunOnUiThreadAsync(() =>
            {
                tab.SessionId = openResult.Value.Id;
                _documentSessionStore.TryActivate(tab.SessionId);
            });
        }
    }

    private async Task HydrateActiveTabAsync()
    {
        WindowsDocumentTabViewModel? tab = ActiveDocumentTab;
        if (tab is null)
        {
            await RunOnUiThreadAsync(() =>
            {
                OnPropertyChanged(nameof(HasDocument));
                OnPropertyChanged(nameof(IsPagesPanelVisible));
            });
            return;
        }

        await RunOnUiThreadAsync(() =>
        {
            OnPropertyChanged(nameof(HasDocument));
            OnPropertyChanged(nameof(IsPagesPanelVisible));
            UpdateSelectedThumbnail(tab);
            UpdateAnnotationToolSelection();
        });

        if (tab.CurrentPageImage is null)
        {
            await RenderActivePageAsync(tab);
        }

        if (tab.HasMissingThumbnails)
        {
            QueueMissingThumbnailGeneration(tab);
        }
    }

    private async Task RenderActivePageAsync(WindowsDocumentTabViewModel tab)
    {
        if (!_documentSessionStore.TryActivate(tab.SessionId))
        {
            return;
        }

        tab.SyncRotationToCurrentPage();

        await RunOnUiThreadAsync(() => tab.IsRendering = true);
        try
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var pageIndex = new PageIndex(Math.Max(0, tab.CurrentPage - 1));
                RenderJobHandle handle = _renderOrchestrator.Submit(
                    new RenderRequest(
                        $"windows-viewer:{tab.SessionId.Value}",
                        pageIndex,
                        tab.ZoomFactor,
                        tab.GetPageRotation(tab.CurrentPage),
                        Priority: RenderPriority.Viewer));

                RenderResult result = await handle.Completion;
                if (result is { IsSuccess: true, Page: not null })
                {
                    await RunOnUiThreadAsync(() =>
                    {
                        tab.CurrentPageImage = WindowsBitmapFactory.Create(result.Page);
                        tab.SetCurrentPagePixels(result.Page.Width, result.Page.Height);
                    });
                    return;
                }

                if (result.Error is not null)
                {
                    await RunOnUiThreadAsync(() => StatusText = FormatError(RenderFailed, result.Error));
                    return;
                }

                if (result is { IsCanceled: false, IsObsolete: false })
                {
                    return;
                }
            }
        }
        finally
        {
            await RunOnUiThreadAsync(() => tab.IsRendering = false);
        }
    }

    private async Task LoadDocumentTextIndexAsync(WindowsDocumentTabViewModel tab)
    {
        try
        {
            if (!_documentSessionStore.TryActivate(tab.SessionId))
            {
                return;
            }

            DocumentTextJobHandle handle = _loadDocumentTextUseCase.Execute();
            DocumentTextAnalysisResult result = await handle.Completion;
            if (result is { IsSuccess: true, Index: not null } && ReferenceEquals(tab, ActiveDocumentTab))
            {
                tab.DocumentTextIndex = result.Index;
            }
        }
        catch (Exception exception)
        {
            await RunOnUiThreadAsync(() => StatusText = $"{_textCatalog.GetString(RenderFailed)}: {exception.Message}");
        }
    }

    private async Task EnsureDocumentTextAvailableAsync(WindowsDocumentTabViewModel tab, bool forceOcr)
    {
        if (!_documentSessionStore.TryActivate(tab.SessionId))
        {
            return;
        }

        await RunOnUiThreadAsync(() =>
        {
            tab.IsAnalyzingDocumentText = true;
            tab.RequiresSearchOcr = false;
            tab.SearchPanelNotice = _textCatalog.GetString(forceOcr ? "search.notice.recognizing" : "search.notice.loading");
            tab.NotifySearchStateChanged();
        });

        try
        {
            DocumentTextJobHandle handle = forceOcr
                ? _runDocumentOcrUseCase.Execute()
                : _loadDocumentTextUseCase.Execute();

            DocumentTextAnalysisResult result = await handle.Completion;
            await RunOnUiThreadAsync(() =>
            {
                if (result.IsCanceled)
                {
                    tab.SearchPanelNotice = _textCatalog.GetString("search.notice.analysis_cancelled");
                    StatusText = _textCatalog.GetString("status.analysis.cancelled");
                    return;
                }

                if (result.IsFailure)
                {
                    tab.DocumentTextIndex = null;
                    tab.RequiresSearchOcr = !forceOcr;
                    tab.ClearSearchResults();
                    tab.SearchPanelNotice = result.Error?.Message ?? _textCatalog.GetString("error.text_analysis.failed.message");
                    StatusText = _textCatalog.GetString("status.analysis.failed");
                    return;
                }

                if (result.RequiresOcr)
                {
                    tab.DocumentTextIndex = null;
                    tab.RequiresSearchOcr = true;
                    tab.ClearSearchResults();
                    tab.SearchPanelNotice = _textCatalog.GetString("search.notice.no_text");
                    StatusText = _textCatalog.GetString("status.ocr.required");
                    return;
                }

                tab.DocumentTextIndex = result.Index;
                tab.RequiresSearchOcr = false;
                tab.SearchPanelNotice = null;
                StatusText = forceOcr
                    ? _textCatalog.GetString("status.text.recognized")
                    : _textCatalog.GetString("status.text.loaded");
            });

        }
        catch (Exception exception)
        {
            await RunOnUiThreadAsync(() =>
            {
                tab.SearchPanelNotice = exception.Message;
                StatusText = $"{_textCatalog.GetString("status.analysis.failed")}: {exception.Message}";
            });
        }
        finally
        {
            await RunOnUiThreadAsync(() =>
            {
                tab.IsAnalyzingDocumentText = false;
                tab.NotifySearchStateChanged();
            });
        }
    }

    private async Task PrepareTextSelectionAsync(WindowsDocumentTabViewModel tab)
    {
        if (tab.DocumentTextIndex is not null || tab.IsAnalyzingDocumentText)
        {
            return;
        }

        await EnsureDocumentTextAvailableAsync(tab, forceOcr: false);
        if (tab.DocumentTextIndex is not null)
        {
            await RunOnUiThreadAsync(() => StatusText = _textCatalog.GetString("status.text.loaded"));
            return;
        }

        if (tab.RequiresSearchOcr)
        {
            await RunOnUiThreadAsync(() => StatusText = _textCatalog.GetString("search.status.recognize_first"));
        }
    }

    private async Task SelectSearchResultAsync(WindowsSearchResultItemViewModel result, bool updateStatus)
    {
        if (ActiveDocumentTab is not { } tab ||
            !tab.SearchResults.Any(item => ReferenceEquals(item, result)))
        {
            return;
        }

        if (result.PageNumber != tab.CurrentPage)
        {
            await ChangePageAsync(result.PageNumber);
        }

        await RunOnUiThreadAsync(() =>
        {
            if (!ReferenceEquals(tab, ActiveDocumentTab) ||
                !tab.SearchResults.Any(item => ReferenceEquals(item, result)))
            {
                return;
            }

            tab.SelectSearchResult(result);
            if (updateStatus)
            {
                StatusText = _textCatalog.Format("status.search.result", result.PageNumber);
            }
        });
    }

    private static void SetRightPanel(WindowsDocumentTabViewModel tab, RightPanel panel)
    {
        tab.IsSearchPanelOpen = panel is RightPanel.Search;
        tab.IsAnnotationsPanelOpen = panel is RightPanel.Annotations;
        tab.IsInfoPanelOpen = panel is RightPanel.Info;
        tab.IsSettingsPanelOpen = panel is RightPanel.Settings;
    }

    private async Task GenerateMissingThumbnailsAsync(WindowsDocumentTabViewModel tab)
    {
        if (tab.IsGeneratingThumbnails || !SessionExists(tab.SessionId))
        {
            return;
        }

        await RunOnUiThreadAsync(() => tab.IsGeneratingThumbnails = true);
        try
        {
            foreach (WindowsPageThumbnailViewModel thumbnail in tab.Thumbnails.Where(item => item.Image is null).ToArray())
            {
                if (!SessionExists(tab.SessionId))
                {
                    return;
                }

                await RunOnUiThreadAsync(() => thumbnail.BeginRender());
                ThumbnailRenderOutcome outcome = await RenderThumbnailWithRetryAsync(tab, thumbnail);
                await RunOnUiThreadAsync(() =>
                {
                    if (outcome == ThumbnailRenderOutcome.Failed && thumbnail.Image is null)
                    {
                        thumbnail.MarkRenderFailed(_textCatalog.GetString("windows.thumbnail.unavailable"));
                        return;
                    }

                    thumbnail.IsLoading = false;
                });
            }
        }
        catch (Exception exception)
        {
            await RunOnUiThreadAsync(() => StatusText = $"{_textCatalog.GetString(RenderFailed)}: {exception.Message}");
        }
        finally
        {
            await RunOnUiThreadAsync(() =>
            {
                foreach (WindowsPageThumbnailViewModel thumbnail in tab.Thumbnails)
                {
                    thumbnail.IsLoading = false;
                }

                tab.IsGeneratingThumbnails = false;
                tab.NotifyThumbnailStatusChanged();
            });
        }
    }

    private void QueueMissingThumbnailGeneration(WindowsDocumentTabViewModel tab)
    {
        _ = GenerateMissingThumbnailsSafelyAsync(tab);
    }

    private async Task GenerateMissingThumbnailsSafelyAsync(WindowsDocumentTabViewModel tab)
    {
        try
        {
            await GenerateMissingThumbnailsAsync(tab);
        }
        catch (Exception exception)
        {
            await RunOnUiThreadAsync(() => StatusText = $"{_textCatalog.GetString(RenderFailed)}: {exception.Message}");
        }
    }

    private async Task<ThumbnailRenderOutcome> RenderThumbnailWithRetryAsync(
        WindowsDocumentTabViewModel tab,
        WindowsPageThumbnailViewModel thumbnail)
    {
        const int maxAttempts = 2;
        var pageIndex = new PageIndex(thumbnail.PageNumber - 1);

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (!SessionExists(tab.SessionId))
            {
                return ThumbnailRenderOutcome.Deferred;
            }

            RenderJobHandle? handle = SubmitForTab(
                tab,
                new RenderRequest(
                    JobKey: $"windows-thumbnail:{tab.SessionId.Value}:{pageIndex.Value}",
                    PageIndex: pageIndex,
                    ZoomFactor: ThumbnailZoom,
                    Rotation: thumbnail.Rotation,
                    RequestedWidth: ThumbnailWidth,
                    RequestedHeight: ThumbnailHeight,
                    Priority: RenderPriority.Thumbnail));

            if (handle is null)
            {
                return ThumbnailRenderOutcome.Deferred;
            }

            RenderResult result = await handle.Completion;
            switch (result)
            {
                case { IsSuccess: true, Page: not null }:
                    {
                        RenderedPage? page = result.Page;
                        await SetThumbnailImageAsync(thumbnail, tab, page);
                        return ThumbnailRenderOutcome.Rendered;
                    }
                case { IsCanceled: false, IsObsolete: false }:
                    {
                        if (result.Error is not null)
                        {
                            await RunOnUiThreadAsync(() => StatusText = FormatError(RenderFailed, result.Error));
                        }

                        return ThumbnailRenderOutcome.Failed;
                    }
            }
        }

        return ThumbnailRenderOutcome.Deferred;
    }

    private async Task SetThumbnailImageAsync(
        WindowsPageThumbnailViewModel thumbnail,
        WindowsDocumentTabViewModel tab,
        RenderedPage page)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            thumbnail.Image = WindowsBitmapFactory.Create(page);
            tab.NotifyThumbnailStatusChanged();
        }
        else
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    thumbnail.Image = WindowsBitmapFactory.Create(page);
                    tab.NotifyThumbnailStatusChanged();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            await tcs.Task;
        }
    }

    private RenderJobHandle? SubmitForTab(WindowsDocumentTabViewModel tab, RenderRequest request)
    {
        DocumentId? previousActiveSessionId = _documentSessionStore.ActiveSessionId;
        if (!_documentSessionStore.TryActivate(tab.SessionId))
        {
            return null;
        }

        RenderJobHandle handle = _renderOrchestrator.Submit(request);
        RestoreActiveSession(previousActiveSessionId);
        return handle;
    }

    private void RestoreActiveSession(DocumentId? documentId)
    {
        if (documentId is { } activeSessionId &&
            SessionExists(activeSessionId))
        {
            _documentSessionStore.TryActivate(activeSessionId);
        }
    }

    private bool SessionExists(DocumentId documentId)
    {
        return _documentSessionStore.Sessions.Any(session => session.Id == documentId);
    }

    private WindowsDocumentTabViewModel? ResolveNextTab(int closedTabIndex)
    {
        if (DocumentTabs.Count == 0)
        {
            return null;
        }

        if (closedTabIndex >= 0 && closedTabIndex < DocumentTabs.Count)
        {
            return DocumentTabs[closedTabIndex];
        }

        int leftIndex = Math.Min(DocumentTabs.Count - 1, Math.Max(0, closedTabIndex - 1));
        return DocumentTabs[leftIndex];
    }

    private void UpdateTabSelection()
    {
        foreach (WindowsDocumentTabViewModel tab in DocumentTabs)
        {
            tab.IsActive = ReferenceEquals(tab, ActiveDocumentTab);
            tab.IsPagesPanelOpen = ShowThumbnails;
        }

        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(IsPagesPanelVisible));
        OnPropertyChanged(nameof(ShowRecentFilesFooter));
        OnPropertyChanged(nameof(ShowStatusFooter));
        UpdateAnnotationToolSelection();
    }

    private void ApplyShowThumbnailsToTabs(bool value)
    {
        foreach (WindowsDocumentTabViewModel tab in DocumentTabs)
        {
            tab.IsPagesPanelOpen = value;
        }
    }

    private async Task SaveThumbnailPreferenceAsync(bool value)
    {
        try
        {
            await _userPreferencesService.SaveAsync(_userPreferencesService.Current with
            {
                ShowThumbnailsPanel = value
            });
        }
        catch (Exception exception)
        {
            await RunOnUiThreadAsync(() => StatusText = exception.Message);
        }
    }

    private async Task SavePreferenceAsync(Func<UserPreferences, UserPreferences> transform)
    {
        try
        {
            UserPreferences updated = transform(_userPreferencesService.Current);
            await _userPreferencesService.SaveAsync(updated);
        }
        catch (Exception exception)
        {
            await RunOnUiThreadAsync(() => StatusText = exception.Message);
        }
    }

    private string MapLanguageToLabel(AppLanguagePreference language)
    {
        return language switch
        {
            AppLanguagePreference.English => Labels.PreferencesEnglish,
            AppLanguagePreference.French => Labels.PreferencesFrench,
            AppLanguagePreference.Spanish => Labels.PreferencesSpanish,
            _ => Labels.PreferencesSystem
        };
    }

    private AppLanguagePreference MapLabelToLanguage(string label)
    {
        if (string.Equals(label, Labels.PreferencesEnglish, StringComparison.Ordinal))
        {
            return AppLanguagePreference.English;
        }

        if (string.Equals(label, Labels.PreferencesFrench, StringComparison.Ordinal))
        {
            return AppLanguagePreference.French;
        }

        if (string.Equals(label, Labels.PreferencesSpanish, StringComparison.Ordinal))
        {
            return AppLanguagePreference.Spanish;
        }

        return AppLanguagePreference.System;
    }

    private string MapThemeToLabel(AppThemePreference theme)
    {
        return theme switch
        {
            AppThemePreference.Light => Labels.PreferencesLight,
            AppThemePreference.Dark => Labels.PreferencesDark,
            _ => Labels.PreferencesSystem
        };
    }

    private AppThemePreference MapLabelToTheme(string label)
    {
        if (string.Equals(label, Labels.PreferencesLight, StringComparison.Ordinal))
        {
            return AppThemePreference.Light;
        }

        if (string.Equals(label, Labels.PreferencesDark, StringComparison.Ordinal))
        {
            return AppThemePreference.Dark;
        }

        return AppThemePreference.System;
    }

    private string MapZoomToLabel(DefaultZoomPreference zoom)
    {
        return zoom switch
        {
            DefaultZoomPreference.FitToWidth => Labels.PreferencesFitWidth,
            DefaultZoomPreference.ActualSize => Labels.PreferencesActualSize,
            _ => Labels.PreferencesFitPage
        };
    }

    private DefaultZoomPreference MapLabelToZoom(string label)
    {
        if (string.Equals(label, Labels.PreferencesFitWidth, StringComparison.Ordinal))
        {
            return DefaultZoomPreference.FitToWidth;
        }

        if (string.Equals(label, Labels.PreferencesActualSize, StringComparison.Ordinal))
        {
            return DefaultZoomPreference.ActualSize;
        }

        return DefaultZoomPreference.FitToPage;
    }

    private async Task MergeDocumentSourcesAsync(string[] sourcePaths)
    {
        if (sourcePaths.Length < 2)
        {
            StatusText = _textCatalog.GetString("status.merge.selection_invalid");
            return;
        }

        string? outputPath = await _fileDialogService.PickSavePdfAsync("Velune merged.pdf");
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            StatusText = _textCatalog.GetString("status.merge.cancelled");
            return;
        }

        await RunBusyAsync(async () =>
        {
            Result<string> result = await _mergePdfDocumentsUseCase.ExecuteAsync(
                new MergePdfDocumentsRequest(sourcePaths, outputPath));

            if (result.IsFailure)
            {
                await RunOnUiThreadAsync(() => StatusText = FormatError(RenderFailed, result.Error));
                return;
            }

            await RunOnUiThreadAsync(() => StatusText = _textCatalog.GetString("status.merge.saved"));
            await OpenPathAsync(outputPath);
        });
    }

    private void AddToRecentFiles(WindowsDocumentTabViewModel tab)
    {
        if (string.IsNullOrWhiteSpace(tab.Title) ||
            string.IsNullOrWhiteSpace(tab.FilePath))
        {
            return;
        }

        _recentFilesService.Add(new RecentFileItem(
            tab.Title,
            tab.FilePath,
            GetLocalizedDocumentTypeLabel(tab)));

        RefreshRecentFiles();
    }

    private void RefreshRecentFiles()
    {
        RecentFiles.Clear();

        foreach (RecentFileItem item in _recentFilesService.GetAll())
        {
            RecentFiles.Add(item);
        }

        OnPropertyChanged(nameof(HasRecentFiles));
        OnPropertyChanged(nameof(ShowRecentFilesFooter));
        OnPropertyChanged(nameof(ShowStatusFooter));
        ClearRecentFilesCommand.NotifyCanExecuteChanged();
    }

    private string GetLocalizedDocumentTypeLabel(WindowsDocumentTabViewModel tab)
    {
        if (tab.DocumentType is DocumentType.Pdf)
        {
            return _textCatalog.GetString("document.type.pdf");
        }

        if (tab.DocumentType is DocumentType.Image)
        {
            return Path.GetExtension(tab.FilePath).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => _textCatalog.GetString("document.type.jpeg"),
                ".png" => _textCatalog.GetString("document.type.png"),
                ".webp" => _textCatalog.GetString("document.type.webp"),
                _ => _textCatalog.GetString("document.type.image")
            };
        }

        return _textCatalog.GetString("document.type.document");
    }

    private static bool IsSupportedDocumentPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string extension = Path.GetExtension(path);
        return !string.IsNullOrWhiteSpace(extension) &&
            SupportedDocumentFormats.IsSupported(extension);
    }

    private void UpdateSelectedThumbnail(WindowsDocumentTabViewModel tab)
    {
        foreach (WindowsPageThumbnailViewModel thumbnail in tab.Thumbnails)
        {
            thumbnail.IsSelected = thumbnail.PageNumber == tab.CurrentPage;
        }
    }

    private void UpdateAnnotationToolSelection()
    {
        AnnotationTool selected = ActiveDocumentTab?.SelectedAnnotationTool ?? AnnotationTool.Select;
        foreach (WindowsAnnotationToolItem tool in AnnotationTools)
        {
            tool.IsSelected = tool.Tool == selected;
        }

        NotifyAnnotationSelectionChanged();
    }

    private void NotifyAnnotationSelectionChanged()
    {
        OnPropertyChanged(nameof(ActiveAnnotationToolLabel));
        OnPropertyChanged(nameof(ActiveAnnotationToolGlyph));
        OnPropertyChanged(nameof(IsAnnotationAppearancePanelVisible));
        OnPropertyChanged(nameof(IsAnnotationToolsAppearancePanelVisible));
        OnPropertyChanged(nameof(ShowAnnotationTextEditor));
        OnPropertyChanged(nameof(ShowTextStyleControls));
        OnPropertyChanged(nameof(ShowSignatureControls));
        OnPropertyChanged(nameof(ShowRectangleFillControls));
        OnPropertyChanged(nameof(ShowSelectionEditPanel));
        OnPropertyChanged(nameof(ShowSelectedTextStyleControls));
        OnPropertyChanged(nameof(SelectedAnnotationKind));
        OnPropertyChanged(nameof(CanUseSignaturePlacement));
    }

    private void NotifySelectedDocumentTextChanged()
    {
        OnPropertyChanged(nameof(SelectedDocumentText));
        OnPropertyChanged(nameof(HasSelectedDocumentText));
    }

    private DocumentAnnotation CreateInkAnnotation(IReadOnlyList<NormalizedPoint> points)
    {
        return new DocumentAnnotation(
            Guid.NewGuid(),
            DocumentAnnotationKind.Ink,
            CurrentPageIndex(),
            CurrentAnnotationAppearance(),
            points: [.. points]);
    }

    private DocumentAnnotation? CreateBoxAnnotation(NormalizedPoint activePoint)
    {
        AnnotationTool tool = ActiveDocumentTab?.SelectedAnnotationTool ?? AnnotationTool.Rectangle;
        DocumentAnnotationKind kind = tool switch
        {
            AnnotationTool.Highlight => DocumentAnnotationKind.Highlight,
            AnnotationTool.Text => DocumentAnnotationKind.Text,
            AnnotationTool.Note => DocumentAnnotationKind.Note,
            AnnotationTool.Stamp => DocumentAnnotationKind.Stamp,
            AnnotationTool.Signature => DocumentAnnotationKind.Signature,
            _ => DocumentAnnotationKind.Rectangle
        };

        if (kind is DocumentAnnotationKind.Signature && string.IsNullOrWhiteSpace(SelectedSignatureAssetId))
        {
            return null;
        }

        NormalizedPoint startPoint = _activeAnnotationStartPoint ?? activePoint;
        NormalizedTextRegion bounds = BuildAnnotationBounds(kind, startPoint, activePoint);

        return new DocumentAnnotation(
            Guid.NewGuid(),
            kind,
            CurrentPageIndex(),
            CurrentAnnotationAppearance(),
            bounds,
            text: ResolveAnnotationText(kind),
            assetId: kind is DocumentAnnotationKind.Signature ? SelectedSignatureAssetId : null);
    }

    private static NormalizedTextRegion BuildAnnotationBounds(
        DocumentAnnotationKind kind,
        NormalizedPoint start,
        NormalizedPoint end)
    {
        NormalizedTextRegion bounds = DocumentAnnotationCoordinateMapper.CreateBounds(start, end);
        bool hasMeaningfulDrag = bounds.Width > 0.01 || bounds.Height > 0.01;
        if (hasMeaningfulDrag)
        {
            return bounds;
        }

        return kind is DocumentAnnotationKind.Signature
            ? DocumentAnnotationCoordinateMapper.InflatePoint(start, SignatureDefaultWidthRatio, SignatureDefaultHeightRatio)
            : DocumentAnnotationCoordinateMapper.InflatePoint(start, AnnotationDefaultWidthRatio, AnnotationDefaultHeightRatio);
    }

    private AnnotationAppearance CurrentAnnotationAppearance()
    {
        string color = AnnotationColorOptions.FirstOrDefault(item => item.IsSelected)?.Hex ?? "#FFE600";
        string? fill = AnnotationFillEnabled ? AnnotationFillHex : null;
        return new AnnotationAppearance(
            color,
            fill,
            3,
            Math.Clamp(AnnotationOpacity / 100d, 0.05, 1),
            AnnotationFontSize,
            string.IsNullOrWhiteSpace(AnnotationFontFamily) ? null : AnnotationFontFamily);
    }

    private PageIndex CurrentPageIndex()
    {
        return new PageIndex(Math.Max(0, (ActiveDocumentTab?.CurrentPage ?? 1) - 1));
    }

    private bool TryResolveDocumentTextPoint(
        double x,
        double y,
        double width,
        double height,
        out DocumentTextSelectionPoint point)
    {
        point = new DocumentTextSelectionPoint(0, 0);

        if (ActiveDocumentTab?.DocumentTextIndex is not { } index)
        {
            return false;
        }

        var pageIndex = new PageIndex(Math.Max(0, ActiveDocumentTab.CurrentPage - 1));
        PageTextContent? page = index.Pages.FirstOrDefault(item => item.PageIndex == pageIndex);
        if (page is null)
        {
            return false;
        }

        return DocumentTextSelectionCoordinateMapper.TryMapVisualToDocument(
            x,
            y,
            width,
            height,
            page.SourceWidth,
            page.SourceHeight,
            ActiveDocumentTab.Rotation,
            out point);
    }

    private string? ResolveAnnotationText(DocumentAnnotationKind kind)
    {
        if (kind is DocumentAnnotationKind.Text)
        {
            return string.Empty;
        }

        if (kind is DocumentAnnotationKind.Note or DocumentAnnotationKind.Stamp &&
            !string.IsNullOrWhiteSpace(AnnotationTextDraft))
        {
            return AnnotationTextDraft.Trim();
        }

        if (kind is DocumentAnnotationKind.Signature &&
            !string.IsNullOrWhiteSpace(SelectedSignatureAssetId) &&
            _signatureAssetLookup.TryGetValue(SelectedSignatureAssetId, out SignatureAsset? signatureAsset))
        {
            return signatureAsset.DisplayName;
        }

        return kind switch
        {
            DocumentAnnotationKind.Text => _textCatalog.GetString("annotation.default.text"),
            DocumentAnnotationKind.Note => _textCatalog.GetString("annotation.default.note_label"),
            DocumentAnnotationKind.Stamp => _textCatalog.GetString("annotation.default.stamp"),
            DocumentAnnotationKind.Signature => _textCatalog.GetString("annotation.default.signature_name"),
            _ => null
        };
    }

    private void PrepareAnnotationTextDraftForTool(AnnotationTool tool)
    {
        if (tool is not (AnnotationTool.Note or AnnotationTool.Stamp))
        {
            return;
        }

        string[] defaultValues = new[]
        {
            _textCatalog.GetString("annotation.default.text"),
            _textCatalog.GetString("annotation.default.note"),
            _textCatalog.GetString("annotation.default.note_label"),
            _textCatalog.GetString("annotation.default.stamp")
        };

        if (!string.IsNullOrWhiteSpace(AnnotationTextDraft) &&
            !defaultValues.Any(value => string.Equals(AnnotationTextDraft, value, StringComparison.Ordinal)))
        {
            return;
        }

        AnnotationTextDraft = tool switch
        {
            AnnotationTool.Stamp => _textCatalog.GetString("annotation.default.stamp"),
            _ => _textCatalog.GetString("annotation.default.note")
        };
    }

    private void ReplacePreviewAnnotation(DocumentAnnotation annotation)
    {
        if (ActiveDocumentTab is null)
        {
            return;
        }

        ClearPreviewAnnotation();
        _previewAnnotationId = annotation.Id;
        ActiveDocumentTab.AddAnnotation(annotation);
        ActiveDocumentTab.IsDirty = false;
    }

    private void ClearPreviewAnnotation()
    {
        if (ActiveDocumentTab is null || _previewAnnotationId is not { } previewAnnotationId)
        {
            return;
        }

        ActiveDocumentTab.DeleteAnnotationById(previewAnnotationId);
        ActiveDocumentTab.IsDirty = false;
        _previewAnnotationId = null;
    }

    private void LoadSignatureAssets()
    {
        string? previousSelectedAssetId = SelectedSignatureAssetId;
        SignatureAssets.Clear();
        _signatureAssetLookup.Clear();

        foreach (SignatureAsset? asset in _signatureAssetStore.GetAll().OrderByDescending(asset => asset.CreatedAt))
        {
            SignatureAssets.Add(asset);
            _signatureAssetLookup[asset.Id] = asset;
        }

        SelectedSignatureAssetId = SignatureAssets.FirstOrDefault(asset =>
            string.Equals(asset.Id, previousSelectedAssetId, StringComparison.Ordinal))?.Id
            ?? SignatureAssets.FirstOrDefault()?.Id;

        RefreshSignatureAssetsOnTabs();
        NotifySignatureStateChanged();
    }

    private void RefreshSignatureAssetsOnTabs()
    {
        foreach (WindowsDocumentTabViewModel tab in DocumentTabs)
        {
            tab.UpdateSignatureAssets(_signatureAssetLookup);
        }
    }

    private void RefreshSignaturePadPreview()
    {
        var points = new PointCollection();
        foreach (NormalizedPoint point in _signatureCapturePoints)
        {
            points.Add(new global::Windows.Foundation.Point(
                point.X * SignaturePadPreviewWidth,
                point.Y * SignaturePadPreviewHeight));
        }

        SignaturePadPoints = points;
        OnPropertyChanged(nameof(CanSaveSignatureCapture));
        SaveDrawnSignatureAssetCommand.NotifyCanExecuteChanged();
    }

    private void NotifySignatureStateChanged()
    {
        OnPropertyChanged(nameof(HasSignatureAssets));
        OnPropertyChanged(nameof(CanDeleteSelectedSignatureAsset));
        OnPropertyChanged(nameof(CanSaveSignatureCapture));
        OnPropertyChanged(nameof(CanUseSignaturePlacement));
        DeleteSelectedSignatureAssetCommand.NotifyCanExecuteChanged();
        SaveDrawnSignatureAssetCommand.NotifyCanExecuteChanged();
    }

    private async Task RunBusyAsync(Func<Task> operation)
    {
        await RunOnUiThreadAsync(() => IsBusy = true);
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            await RunOnUiThreadAsync(() => StatusText = exception.Message);
        }
        finally
        {
            await RunOnUiThreadAsync(() => IsBusy = false);
        }
    }

    private Task RunOnUiThreadAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcherQueue.TryEnqueue(() =>
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

    private Task<T> RunOnUiThreadAsync<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        if (_dispatcherQueue.HasThreadAccess)
        {
            return Task.FromResult(func());
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                completion.SetResult(func());
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

    private string FormatError(string statusKey, AppError? error)
    {
        string message = _textCatalog.GetString(statusKey);
        return error is null ? message : $"{message}: {error.Message}";
    }

    private string GetAnnotationLabel(AnnotationTool tool)
    {
        return AnnotationTools.FirstOrDefault(item => item.Tool == tool)?.Label ?? tool.ToString();
    }

    private static bool TryResolveAnnotationTool(object? value, out AnnotationTool tool)
    {
        switch (value)
        {
            case AnnotationTool annotationTool:
                tool = annotationTool;
                return true;
            case string text when Enum.TryParse(text, ignoreCase: true, out AnnotationTool parsed):
                tool = parsed;
                return true;
            default:
                tool = AnnotationTool.Select;
                return false;
        }
    }

    private static ObservableCollection<WindowsAnnotationToolItem> CreateAnnotationTools(WindowsLabels labels)
    {
        return
        [
            new WindowsAnnotationToolItem(AnnotationTool.Select, labels.Select, "\uE8B3") { IsSelected = true },
            new WindowsAnnotationToolItem(AnnotationTool.Highlight, labels.Highlight, "\uE891"),
            new WindowsAnnotationToolItem(AnnotationTool.Ink, labels.Ink, "\uED5F"),
            new WindowsAnnotationToolItem(AnnotationTool.Text, labels.Text, "\uE8D2"),
            new WindowsAnnotationToolItem(AnnotationTool.Note, labels.Note, "\uE90A"),
            new WindowsAnnotationToolItem(AnnotationTool.Rectangle, labels.Rectangle, "\uE003"),
            new WindowsAnnotationToolItem(AnnotationTool.Signature, labels.Signature, "\uEE56"),
            new WindowsAnnotationToolItem(AnnotationTool.Stamp, labels.Stamp, "\uE7C1")
        ];
    }

    private static bool PathsEqual(string first, string second)
    {
        return string.Equals(
            Path.GetFullPath(first),
            Path.GetFullPath(second),
            StringComparison.OrdinalIgnoreCase);
    }

    partial void OnActiveDocumentTabChanged(WindowsDocumentTabViewModel? value)
    {
        if (value is not null)
        {
            value.IsPagesPanelOpen = ShowThumbnails;
        }

        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(IsPagesPanelVisible));
        OnPropertyChanged(nameof(ShowRecentFilesFooter));
        OnPropertyChanged(nameof(ShowStatusFooter));
        OnPropertyChanged(nameof(CanSaveDocument));
        SaveDocumentCommand.NotifyCanExecuteChanged();
        NotifySelectedDocumentTextChanged();
        value?.UpdateSignatureAssets(_signatureAssetLookup);
        UpdateAnnotationToolSelection();
    }

    partial void OnShowThumbnailsChanged(bool value)
    {
        ApplyShowThumbnailsToTabs(value);
        OnPropertyChanged(nameof(IsPagesPanelVisible));

        if (!_isApplyingThumbnailPreference)
        {
            _ = SaveThumbnailPreferenceAsync(value);
        }
    }

    partial void OnSelectedPreferenceLanguageChanged(string value)
    {
        if (!_isApplyingPreferenceSelection)
        {
            _ = SavePreferenceAsync(p => p with { Language = MapLabelToLanguage(value) });
        }
    }

    partial void OnSelectedPreferenceThemeChanged(string value)
    {
        if (!_isApplyingPreferenceSelection)
        {
            _ = SavePreferenceAsync(p => p with { Theme = MapLabelToTheme(value) });
        }
    }

    partial void OnSelectedPreferenceZoomChanged(string value)
    {
        if (!_isApplyingPreferenceSelection)
        {
            _ = SavePreferenceAsync(p => p with { DefaultZoom = MapLabelToZoom(value) });
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartDocumentOperation));
        OnPropertyChanged(nameof(CanSaveDocument));
        OpenCommand.NotifyCanExecuteChanged();
        MergeCommand.NotifyCanExecuteChanged();
        SaveDocumentCommand.NotifyCanExecuteChanged();
        SaveDocumentAsCommand.NotifyCanExecuteChanged();
    }

    private void PushUndo(IUndoableAction action)
    {
        if (_isUndoRedoInProgress)
        {
            return;
        }

        _undoStack.Push(action);
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private void PushInteractionUndo(Guid annotationId)
    {
        if (_interactionAnnotationSnapshot is null || ActiveDocumentTab is null)
        {
            _interactionAnnotationSnapshot = null;
            return;
        }

        int index = ActiveDocumentTab.FindAnnotationIndex(annotationId);
        if (index >= 0)
        {
            DocumentAnnotation after = ActiveDocumentTab.Annotations[index];
            PushUndo(new AnnotationMutationAction(ActiveDocumentTab, _interactionAnnotationSnapshot, after));
        }

        _interactionAnnotationSnapshot = null;
    }

    private void NotifySaveStateChanged()
    {
        OnPropertyChanged(nameof(CanSaveDocument));
        SaveDocumentCommand.NotifyCanExecuteChanged();
        SaveDocumentAsCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedAnnotationPanelTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsAnnotationToolsTabSelected));
        OnPropertyChanged(nameof(IsAnnotationCommentsTabSelected));
        OnPropertyChanged(nameof(IsAnnotationStyleTabSelected));
        OnPropertyChanged(nameof(IsAnnotationToolsAppearancePanelVisible));
        OnPropertyChanged(nameof(IsAnnotationListPanelVisible));
        OnPropertyChanged(nameof(ShowAnnotationTextEditor));
        OnPropertyChanged(nameof(ShowTextStyleControls));
        OnPropertyChanged(nameof(ShowSignatureControls));
        OnPropertyChanged(nameof(ShowRectangleFillControls));
    }

    partial void OnAnnotationOpacityChanged(double value)
    {
        OnPropertyChanged(nameof(AnnotationOpacityText));
        ApplyOpacityToSelectedAnnotation();
    }

    partial void OnAnnotationFillEnabledChanged(bool value)
    {
        ApplyFillToSelectedAnnotation();
    }

    partial void OnAnnotationFontSizeChanged(double value)
    {
        ApplyFontToSelectedAnnotation();
    }

    partial void OnAnnotationFontFamilyChanged(string value)
    {
        ApplyFontToSelectedAnnotation();
    }

    partial void OnSelectedAnnotationIdChanged(Guid? value)
    {
        OnPropertyChanged(nameof(IsSelectedAnnotationRectangle));
        OnPropertyChanged(nameof(ShowRectangleFillControls));
        OnPropertyChanged(nameof(ShowSelectionEditPanel));
        OnPropertyChanged(nameof(ShowSelectedTextStyleControls));
        OnPropertyChanged(nameof(SelectedAnnotationKind));
        OnPropertyChanged(nameof(ShowTextStyleControls));
        OnPropertyChanged(nameof(IsAnnotationToolsAppearancePanelVisible));
        ActiveDocumentTab?.RefreshAnnotationOverlays(value);
        LoadSelectedAnnotationProperties();

        if (value is not null && ActiveDocumentTab is not null)
        {
            SetRightPanel(ActiveDocumentTab, RightPanel.Annotations);
            SelectedAnnotationPanelTab = "Tools";
        }
    }

    partial void OnSignatureAssetNameInputChanged(string value)
    {
        OnPropertyChanged(nameof(CanSaveSignatureCapture));
        SaveDrawnSignatureAssetCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedSignatureAssetIdChanged(string? value)
    {
        OnPropertyChanged(nameof(CanDeleteSelectedSignatureAsset));
        OnPropertyChanged(nameof(CanUseSignaturePlacement));
        DeleteSelectedSignatureAssetCommand.NotifyCanExecuteChanged();
    }

    partial void OnCacheSizeMegabytesChanged(double value)
    {
        OnPropertyChanged(nameof(CacheSizeText));
    }
}
