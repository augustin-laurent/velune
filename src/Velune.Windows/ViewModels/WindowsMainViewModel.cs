using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Velune.Application.Abstractions;
using Velune.Application.Annotations;
using Velune.Application.Documents;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Application.Text;
using Velune.Application.UseCases;
using Velune.Domain.Annotations;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Windows.Services;

namespace Velune.Windows.ViewModels;

/// <summary>
/// Primary view model for the workspace window, orchestrating documents, annotations, search, and preferences.
/// </summary>
public sealed partial class WindowsMainViewModel : ObservableObject, IDisposable
{
    private const int MaxOpenDocumentTabs = 8;
    private const double DefaultViewerZoom = 1.35;
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
    private NormalizedPoint? _activeAnnotationStartPoint;
    private Guid? _previewAnnotationId;
    private bool _isApplyingThumbnailPreference;
    private Guid? _movingAnnotationId;
    private NormalizedPoint? _movingAnnotationStartPoint;
    private NormalizedTextRegion? _movingAnnotationOriginalBounds;
    private bool _editingExistingTextAnnotation;

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
        _textCatalog = textCatalog;

        Labels = new WindowsLabels(textCatalog);
        AnnotationTools = CreateAnnotationTools(Labels);
        PreferenceLanguageOptions = [Labels.PreferencesSystem, Labels.PreferencesEnglish, Labels.PreferencesFrench, Labels.PreferencesSpanish];
        PreferenceThemeOptions = [Labels.PreferencesSystem, Labels.PreferencesLight, Labels.PreferencesDark];
        PreferenceZoomOptions = [Labels.PreferencesFitPage, Labels.PreferencesFitWidth, Labels.PreferencesActualSize];
        SelectedPreferenceLanguage = Labels.PreferencesSystem;
        SelectedPreferenceTheme = Labels.PreferencesSystem;
        SelectedPreferenceZoom = Labels.PreferencesFitPage;
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

    public bool IsAnnotationAppearancePanelVisible => ActiveDocumentTab?.SelectedAnnotationTool is not null and not AnnotationTool.Select;

    public bool IsAnnotationToolsAppearancePanelVisible => IsAnnotationToolsTabSelected && IsAnnotationAppearancePanelVisible;

    public bool IsAnnotationListPanelVisible => IsAnnotationToolsTabSelected || IsAnnotationCommentsTabSelected;

    public bool ShowAnnotationTextEditor =>
        IsAnnotationToolsTabSelected &&
        ActiveDocumentTab?.SelectedAnnotationTool is AnnotationTool.Note or AnnotationTool.Stamp;

    public bool ShowTextStyleControls =>
        IsAnnotationToolsTabSelected &&
        ActiveDocumentTab?.SelectedAnnotationTool is AnnotationTool.Text;

    public bool ShowSignatureControls =>
        IsAnnotationToolsTabSelected &&
        ActiveDocumentTab?.SelectedAnnotationTool is AnnotationTool.Signature;

    public bool ShowRectangleFillControls =>
        IsAnnotationToolsTabSelected &&
        ActiveDocumentTab?.SelectedAnnotationTool is AnnotationTool.Rectangle;

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
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy
    {
        get; set;
    }

    [ObservableProperty]
    public partial string SelectedAnnotationPanelTab { get; set; } = "Tools";

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
    public partial string AnnotationTextDraft { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SignatureAssetNameInput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? SelectedSignatureAssetId
    {
        get; set;
    }

    [ObservableProperty]
    public partial PointCollection SignaturePadPoints { get; set; } = new();

    [ObservableProperty]
    public partial string SelectedPreferenceLanguage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedPreferenceTheme { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedPreferenceZoom { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowThumbnails { get; set; } = true;

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
        _renderOrchestrator.Dispose();
        _documentOpenGate.Dispose();
    }

    [RelayCommand(CanExecute = nameof(CanStartDocumentOperation))]
    private async Task OpenAsync()
    {
        try
        {
            var path = await _fileDialogService.PickOpenDocumentAsync();
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
            var selectedPaths = await _fileDialogService.PickMergeDocumentsAsync();
            if (selectedPaths.Count == 0)
            {
                StatusText = _textCatalog.GetString("status.merge.cancelled");
                return;
            }

            var sourcePaths = (ActiveDocumentTab is null
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

        var tab = ActiveDocumentTab;
        try
        {
            var hasPageEdits = tab.HasPendingPageEdits && tab.DocumentType is DocumentType.Pdf;
            var hasAnnotations = tab.Annotations.Count > 0 && tab.DocumentType is DocumentType.Pdf;

            if (hasPageEdits || hasAnnotations)
            {
                await RunBusyAsync(async () =>
                {
                    var originalPath = tab.FilePath;
                    var currentPath = originalPath;

                    if (hasPageEdits)
                    {
                        var tempDir = Path.Combine(Path.GetTempPath(), "velune-op", Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(tempDir);

                        var editedPath = await CreateEditedPdfAsync(
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
                        foreach (var annotation in savedAnnotations)
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

            var suggestedName = string.IsNullOrWhiteSpace(tab.Title)
                ? "document.pdf"
                : tab.Title;
            var outputPath = await _fileDialogService.PickSavePdfAsync(suggestedName);
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

        var tab = ActiveDocumentTab;
        try
        {
            var suggestedName = string.IsNullOrWhiteSpace(tab.Title)
                ? "document.pdf"
                : tab.Title;
            var outputPath = await _fileDialogService.PickSavePdfAsync(suggestedName);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                StatusText = _textCatalog.GetString("status.save.cancelled");
                return;
            }

            var hasPageEdits = tab.HasPendingPageEdits && tab.DocumentType is DocumentType.Pdf;
            var hasAnnotations = tab.Annotations.Count > 0 && tab.DocumentType is DocumentType.Pdf;

            if (hasPageEdits || hasAnnotations)
            {
                await RunBusyAsync(async () =>
                {
                    var tempDir = Path.Combine(Path.GetTempPath(), "velune-op", Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempDir);

                    var session = _documentSessionStore.Sessions
                        .FirstOrDefault(s => s.Id == tab.SessionId);
                    var currentPath = tab.FilePath;

                    if (hasPageEdits)
                    {
                        var editedPath = await CreateEditedPdfAsync(
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

                        var annotatedPath = Path.Combine(tempDir, "annotated.pdf");
                        var annotationResult = await _pdfMarkupService.ApplyAnnotationsAsync(
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
            var result = await _printCoordinator.PrintAsync(ActiveDocumentTab);
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

        var wasActive = ReferenceEquals(tab, ActiveDocumentTab);
        var tabIndex = DocumentTabs.IndexOf(tab);

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
    private async Task PreviousPageAsync()
    {
        if (ActiveDocumentTab is null || ActiveDocumentTab.CurrentPage <= 1)
        {
            return;
        }

        await ChangePageAsync(ActiveDocumentTab.CurrentPage - 1);
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (ActiveDocumentTab is null || ActiveDocumentTab.CurrentPage >= ActiveDocumentTab.TotalPages)
        {
            return;
        }

        await ChangePageAsync(ActiveDocumentTab.CurrentPage + 1);
    }

    [RelayCommand]
    private async Task FitPageAsync()
    {
        if (ActiveDocumentTab is null)
        {
            return;
        }

        ActiveDocumentTab.ZoomFactor = DefaultViewerZoom;
        ActiveDocumentTab.ZoomText = $"{ActiveDocumentTab.ZoomFactor * 100:0}%";
        await RenderActivePageAsync(ActiveDocumentTab);
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
        if (ActiveDocumentTab is not null)
        {
            ShowThumbnails = !ShowThumbnails;
            StatusText = ShowThumbnails
                ? _textCatalog.GetString("status.pages_panel.shown")
                : _textCatalog.GetString("status.pages_panel.hidden");
        }
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
            !TryResolveAnnotationTool(toolValue, out var tool))
        {
            return;
        }

        CommitInlineTextAnnotation();
        ActiveDocumentTab.SelectedAnnotationTool = tool;
        SelectedAnnotationPanelTab = "Tools";
        SetRightPanel(ActiveDocumentTab, RightPanel.Annotations);
        PrepareAnnotationTextDraftForTool(tool);
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

        foreach (var item in AnnotationColorOptions)
        {
            item.IsSelected = ReferenceEquals(item, color);
        }

        OnPropertyChanged(nameof(SelectedAnnotationColorBrush));
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

        foreach (var item in AnnotationFillColorOptions)
        {
            item.IsSelected = ReferenceEquals(item, color);
        }

        AnnotationFillHex = color.Hex;
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
            var imagePath = await _fileDialogService.PickSignatureImageAsync();
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return;
            }

            if (!SupportedDocumentFormats.IsImage(Path.GetExtension(imagePath)))
            {
                StatusText = _textCatalog.GetString("error.signature.unsupported_image.message");
                return;
            }

            var result = _signatureAssetStore.Import(imagePath);
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
        var targetAssetId = string.IsNullOrWhiteSpace(assetId)
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

        var result = _signatureAssetStore.Delete(targetAssetId);
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
        var result = _signatureAssetStore.SaveInkSignature(SignatureAssetNameInput.Trim(), _signatureCapturePoints);
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

        var result = _searchDocumentTextUseCase.Execute(
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

        WindowsSearchResultItemViewModel? firstResult = null;
        await RunOnUiThreadAsync(() =>
        {
            tab.ApplySearchResults(result.Value ?? []);
            if (!tab.HasSearchResults)
            {
                tab.SearchPanelNotice = _textCatalog.Format("search.notice.no_match", tab.SearchQuery.Trim());
                StatusText = _textCatalog.GetString("status.search.none");
                return;
            }

            tab.SearchPanelNotice = null;
            firstResult = tab.SearchResults[0];
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

        ActiveDocumentTab.ZoomFactor = Math.Min(4.0, ActiveDocumentTab.ZoomFactor + 0.1);
        ActiveDocumentTab.ZoomText = $"{ActiveDocumentTab.ZoomFactor * 100:0}%";
        await RenderActivePageAsync(ActiveDocumentTab);
    }

    [RelayCommand]
    private async Task ZoomOutAsync()
    {
        if (ActiveDocumentTab is null)
        {
            return;
        }

        ActiveDocumentTab.ZoomFactor = Math.Max(0.2, ActiveDocumentTab.ZoomFactor - 0.1);
        ActiveDocumentTab.ZoomText = $"{ActiveDocumentTab.ZoomFactor * 100:0}%";
        await RenderActivePageAsync(ActiveDocumentTab);
    }

    [RelayCommand]
    private async Task SetZoomAsync(string factor)
    {
        if (ActiveDocumentTab is null || !double.TryParse(factor, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var zoom))
        {
            return;
        }

        ActiveDocumentTab.ZoomFactor = Math.Clamp(zoom, 0.2, 4.0);
        ActiveDocumentTab.ZoomText = $"{ActiveDocumentTab.ZoomFactor * 100:0}%";
        await RenderActivePageAsync(ActiveDocumentTab);
    }

    [RelayCommand]
    private void DeleteAnnotationById(Guid annotationId)
    {
        if (ActiveDocumentTab is null || annotationId == Guid.Empty)
        {
            return;
        }

        ActiveDocumentTab.DeleteAnnotationById(annotationId);
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
        WindowsDocumentTabViewModel? existingTab = null;
        var canOpen = true;
        await RunOnUiThreadAsync(() =>
        {
            existingTab = DocumentTabs.FirstOrDefault(tab => PathsEqual(tab.FilePath, path));
            if (existingTab is null && DocumentTabs.Count >= MaxOpenDocumentTabs)
            {
                StatusText = _textCatalog.Format("notification.tabs.limit.message", MaxOpenDocumentTabs);
                canOpen = false;
            }
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
            var result = await _openDocumentUseCase.ExecuteAsync(
                new OpenDocumentRequest(path, DocumentOpenMode.AddToTabs));

            if (result.IsFailure || result.Value is null)
            {
                await RunOnUiThreadAsync(() => StatusText = FormatError("status.open.failed", result.Error));
                return;
            }

            WindowsDocumentTabViewModel? tab = null;
            await RunOnUiThreadAsync(() =>
            {
                tab = new WindowsDocumentTabViewModel(result.Value.Id, result.Value.Metadata, _textCatalog)
                {
                    IsActive = true,
                    IsPagesPanelOpen = ShowThumbnails,
                    ZoomFactor = DefaultViewerZoom,
                    ZoomText = $"{DefaultViewerZoom * 100:0}%"
                };
                tab.UpdateSignatureAssets(_signatureAssetLookup);

                foreach (var existing in DocumentTabs)
                {
                    existing.IsActive = false;
                }

                DocumentTabs.Add(tab);
                ActiveDocumentTab = tab;
                UpdateAnnotationToolSelection();
            });

            if (tab is null)
            {
                return;
            }

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
            var annotations = await _pdfAnnotationStore.LoadAsync(tab.FilePath);
            if (annotations.Count == 0)
            {
                return;
            }

            await RunOnUiThreadAsync(() =>
            {
                foreach (var annotation in annotations)
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
    /// Opens or merges files that were dropped onto the home area.
    /// </summary>
    /// <param name="filePaths">The dropped file paths.</param>
    public async Task HandleHomeFilesDroppedAsync(IReadOnlyList<string> filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        var supportedPaths = filePaths
            .Where(IsSupportedDocumentPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (supportedPaths.Length == 0)
        {
            StatusText = _textCatalog.GetString("status.merge.drop_unsupported");
            return;
        }

        if (supportedPaths.Length == 1)
        {
            await OpenPathAsync(supportedPaths[0]);
            return;
        }

        await MergeDocumentSourcesAsync(supportedPaths);
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

        var tab = ActiveDocumentTab;
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

        var tab = ActiveDocumentTab;
        var supportedPaths = filePaths
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

        var tempDir = Path.Combine(Path.GetTempPath(), "velune-drop-merge", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var outputPath = Path.Combine(tempDir, $"fused-{Guid.NewGuid():N}.pdf");

        await RunBusyAsync(async () =>
        {
            try
            {
                string currentSourcePath;
                if (tab.HasPendingPageEdits)
                {
                    var editedPath = await CreateEditedPdfAsync(
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

                var plan = WindowsDroppedMergeSourcePlanner.Create(
                    currentSourcePath,
                    tab.DocumentType,
                    tab.TotalPages,
                    supportedPaths,
                    insertionIndex,
                    tempDir);

                foreach (var extraction in new[] { plan.Before, plan.After }.OfType<WindowsDroppedMergeExtraction>())
                {
                    var extractionResult = await _extractPdfPagesUseCase.ExecuteAsync(
                        new ExtractPdfPagesRequest(currentSourcePath, extraction.OutputPath, extraction.Pages));
                    if (extractionResult.IsFailure)
                    {
                        await RunOnUiThreadAsync(() => StatusText = FormatError("status.merge.failed", extractionResult.Error));
                        await ReopenAfterFailureAsync(tab);
                        return;
                    }
                }

                if (plan.SourcePaths.Count < 2)
                {
                    await ReopenAfterFailureAsync(tab);
                    return;
                }

                var mergeResult = await _mergePdfDocumentsUseCase.ExecuteAsync(
                    new MergePdfDocumentsRequest(plan.SourcePaths.ToArray(), outputPath));

                if (mergeResult.IsFailure)
                {
                    await RunOnUiThreadAsync(() => StatusText = FormatError("status.merge.failed", mergeResult.Error));
                    await ReopenAfterFailureAsync(tab);
                    return;
                }

                var openResult = await _openDocumentUseCase.ExecuteAsync(
                    new OpenDocumentRequest(outputPath, DocumentOpenMode.AddToTabs));

                if (openResult.IsFailure || openResult.Value is null)
                {
                    await RunOnUiThreadAsync(() => StatusText = FormatError("status.merge.failed", openResult.Error));
                    await ReopenAfterFailureAsync(tab);
                    return;
                }

                var newSession = openResult.Value;
                await RunOnUiThreadAsync(() =>
                {
                    var newTab = new WindowsDocumentTabViewModel(newSession.Id, newSession.Metadata, _textCatalog)
                    {
                        IsActive = true,
                        IsPagesPanelOpen = ShowThumbnails,
                        ZoomFactor = DefaultViewerZoom,
                        ZoomText = $"{DefaultViewerZoom * 100:0}%",
                        IsDirty = true
                    };
                    newTab.UpdateSignatureAssets(_signatureAssetLookup);

                    var tabIndex = DocumentTabs.IndexOf(tab);
                    DocumentTabs.Remove(tab);
                    DocumentTabs.Insert(Math.Max(0, tabIndex), newTab);

                    foreach (var existing in DocumentTabs)
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

        var tempDir = Path.Combine(Path.GetTempPath(), "velune-fuse", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var outputPath = Path.Combine(tempDir, $"fused-{Guid.NewGuid():N}.pdf");

        await RunBusyAsync(async () =>
        {
            try
            {
                await ReleaseActiveSessionAsync(tab);

                var resolvedPaths = new string[sourcePaths.Length];
                for (var i = 0; i < sourcePaths.Length; i++)
                {
                    if (string.Equals(sourcePaths[i], tab.FilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        var sourceExtension = Path.GetExtension(tab.FilePath);
                        var copy = Path.Combine(tempDir, $"source-{i}{sourceExtension}");
                        File.Copy(tab.FilePath, copy);
                        resolvedPaths[i] = copy;
                    }
                    else
                    {
                        resolvedPaths[i] = sourcePaths[i];
                    }
                }

                var mergeResult = await _mergePdfDocumentsUseCase.ExecuteAsync(
                    new MergePdfDocumentsRequest(resolvedPaths, outputPath));

                if (mergeResult.IsFailure)
                {
                    await RunOnUiThreadAsync(() => StatusText = FormatError("status.merge.failed", mergeResult.Error));
                    await ReopenAfterFailureAsync(tab);
                    TryDeleteDirectory(tempDir);
                    return;
                }

                var openResult = await _openDocumentUseCase.ExecuteAsync(
                    new OpenDocumentRequest(outputPath, DocumentOpenMode.AddToTabs));

                if (openResult.IsFailure || openResult.Value is null)
                {
                    await RunOnUiThreadAsync(() => StatusText = FormatError("status.merge.failed", openResult.Error));
                    await ReopenAfterFailureAsync(tab);
                    TryDeleteDirectory(tempDir);
                    return;
                }

                var newSession = openResult.Value;
                await RunOnUiThreadAsync(() =>
                {
                    var newTab = new WindowsDocumentTabViewModel(newSession.Id, newSession.Metadata, _textCatalog)
                    {
                        IsActive = true,
                        IsPagesPanelOpen = ShowThumbnails,
                        ZoomFactor = DefaultViewerZoom,
                        ZoomText = $"{DefaultViewerZoom * 100:0}%",
                        IsDirty = true
                    };
                    newTab.UpdateSignatureAssets(_signatureAssetLookup);

                    var tabIndex = DocumentTabs.IndexOf(tab);
                    DocumentTabs.Remove(tab);
                    DocumentTabs.Insert(Math.Max(0, tabIndex), newTab);

                    foreach (var existing in DocumentTabs)
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

        var tab = ActiveDocumentTab;
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
        IReadOnlyList<(int OriginalPage, Domain.ValueObjects.Rotation Rotation)> rotations)
    {
        ArgumentNullException.ThrowIfNull(finalPageOrder);
        ArgumentNullException.ThrowIfNull(rotations);

        if (ActiveDocumentTab is null || IsBusy)
        {
            return;
        }

        var tab = ActiveDocumentTab;
        if (tab.DocumentType is not Domain.Documents.DocumentType.Pdf)
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
        var tempDir = Path.Combine(Path.GetTempPath(), "velune-op", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var outputPath = await CreateEditedPdfAsync(tab, tempDir, finalPageOrder, rotations);
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
        var sourceCopy = Path.Combine(tempDir, "source.pdf");
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

        var currentPath = sourceCopy;
        if (RequiresPageReorder(finalPageOrder, tab.TotalPages))
        {
            var reorderPath = Path.Combine(tempDir, "reordered.pdf");
            var reorderResult = IsExactPagePermutation(finalPageOrder, tab.TotalPages)
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

        foreach (var group in rotations.Where(item => item.Rotation != Rotation.Deg0).GroupBy(item => item.Rotation))
        {
            var pages = ResolveRotatedOutputPages(finalPageOrder, group.Select(item => item.OriginalPage));
            if (pages.Length == 0)
            {
                continue;
            }

            var rotatedPath = Path.Combine(tempDir, $"rotated-{(int)group.Key}-{Guid.NewGuid():N}.pdf");
            var rotateResult = await _rotatePdfPagesUseCase.ExecuteAsync(
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
        if (finalPageOrder.Count != totalPages)
        {
            return true;
        }

        for (var i = 0; i < finalPageOrder.Count; i++)
        {
            if (finalPageOrder[i] != i + 1)
            {
                return true;
            }
        }

        return false;
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
        if (originals.Count == 0)
        {
            return [];
        }

        return finalPageOrder
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

        var tab = ActiveDocumentTab;
        if (tab.DocumentType is not Domain.Documents.DocumentType.Pdf)
        {
            return;
        }

        var pageNumber = SelectedThumbnailPageNumber;
        if (pageNumber < 1 || pageNumber > tab.TotalPages)
        {
            return;
        }

        var rotation = clockwise
            ? RotateRight(tab.GetPageRotation(pageNumber))
            : RotateLeft(tab.GetPageRotation(pageNumber));

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

        var outcome = await RenderThumbnailWithRetryAsync(tab, thumbnail);
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

        var tab = ActiveDocumentTab;
        if (tab.DocumentType is not Domain.Documents.DocumentType.Pdf)
        {
            return;
        }

        var pageIndex = SelectedThumbnailPageNumber - 1;
        var targetIndex = pageIndex + direction;

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

        var tab = ActiveDocumentTab;
        if (tab.DocumentType is not Domain.Documents.DocumentType.Pdf || tab.TotalPages <= 1)
        {
            return;
        }

        var pageToDelete = SelectedThumbnailPageNumber;
        var remainingPages = Enumerable.Range(1, tab.TotalPages)
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
        var tab = ActiveDocumentTab;
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

        var point = DocumentAnnotationCoordinateMapper.MapVisualPointToNormalized(
            x,
            y,
            width,
            height,
            ActiveDocumentTab.Rotation);

        if (ActiveDocumentTab.SelectedAnnotationTool is AnnotationTool.Text)
        {
            CommitInlineTextAnnotation();
            var existingText = ActiveDocumentTab.FindTextAnnotationAtPoint(point);
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

        var point = DocumentAnnotationCoordinateMapper.MapVisualPointToNormalized(
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
        var annotation = ActiveDocumentTab.SelectedAnnotationTool is AnnotationTool.Ink
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
        NotifySaveStateChanged();
        if (annotation.Kind is DocumentAnnotationKind.Text)
        {
            ActiveDocumentTab.BeginInlineTextEdit(annotation);
        }

        if (annotation.Kind is DocumentAnnotationKind.Note)
        {
            SelectedAnnotationPanelTab = "Comments";
            var commentVm = ActiveDocumentTab.CurrentPageCommentOverlays
                .FirstOrDefault(c => c.Id == annotation.Id);
            if (commentVm is not null)
            {
                BeginCommentEdit(commentVm);
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

        var point = DocumentAnnotationCoordinateMapper.MapVisualPointToNormalized(
            x, y, width, height, ActiveDocumentTab.Rotation);

        var annotation = ActiveDocumentTab.FindAnnotationAtPoint(point);
        if (annotation?.Bounds is null)
        {
            return false;
        }

        _movingAnnotationId = annotation.Id;
        _movingAnnotationStartPoint = point;
        _movingAnnotationOriginalBounds = annotation.Bounds;
        return true;
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
        if (ActiveDocumentTab is null ||
            _movingAnnotationId is null ||
            _movingAnnotationStartPoint is null ||
            _movingAnnotationOriginalBounds is null ||
            width <= 0 || height <= 0)
        {
            return;
        }

        var point = DocumentAnnotationCoordinateMapper.MapVisualPointToNormalized(
            x, y, width, height, ActiveDocumentTab.Rotation);

        var dx = point.X - _movingAnnotationStartPoint.X;
        var dy = point.Y - _movingAnnotationStartPoint.Y;
        var bounds = _movingAnnotationOriginalBounds;

        var newX = Math.Clamp(bounds.X + dx, 0, 1 - bounds.Width);
        var newY = Math.Clamp(bounds.Y + dy, 0, 1 - bounds.Height);

        ActiveDocumentTab.MoveAnnotation(
            _movingAnnotationId.Value,
            new NormalizedTextRegion(newX, newY, bounds.Width, bounds.Height));
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
        UpdateAnnotationMove(x, y, width, height);
        _movingAnnotationId = null;
        _movingAnnotationStartPoint = null;
        _movingAnnotationOriginalBounds = null;
    }

    /// <summary>
    /// Cancels the annotation move and restores the original position.
    /// </summary>
    public void CancelAnnotationMove()
    {
        if (_movingAnnotationId is not null && _movingAnnotationOriginalBounds is not null && ActiveDocumentTab is not null)
        {
            ActiveDocumentTab.MoveAnnotation(_movingAnnotationId.Value, _movingAnnotationOriginalBounds);
        }

        _movingAnnotationId = null;
        _movingAnnotationStartPoint = null;
        _movingAnnotationOriginalBounds = null;
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

        var point = DocumentAnnotationCoordinateMapper.MapVisualPointToNormalized(
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
        var annotation = ActiveDocumentTab.Annotations.FirstOrDefault(a => a.Id == annotationId);
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
        var text = comment.EditText.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        ActiveDocumentTab?.UpdateAnnotationText(comment.Id, text);
        ActiveDocumentTab?.RefreshAnnotationOverlays();
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

        var annotation = ActiveDocumentTab.Annotations.FirstOrDefault(a => a.Id == editor.AnnotationId);
        var text = editor.Text.Trim();

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

        var annotation = ActiveDocumentTab.Annotations.FirstOrDefault(a => a.Id == editor.AnnotationId);
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

        if (!TryResolveDocumentTextPoint(x, y, width, height, out var anchorPoint))
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
            !TryResolveDocumentTextPoint(x, y, width, height, out var activePoint) ||
            _documentSessionStore.Current is not { } session)
        {
            return false;
        }

        var result = _resolveDocumentTextSelectionUseCase.Execute(
            new DocumentTextSelectionRequest(
                session,
                tab.DocumentTextIndex,
                new PageIndex(Math.Max(0, tab.CurrentPage - 1)),
                anchorPoint,
                activePoint));

        if (result.IsFailure)
        {
            ClearDocumentTextSelection();
            return false;
        }

        if (result.Value is not { } selection)
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

        var appearance = CurrentAnnotationAppearance();
        foreach (var region in selection.Regions)
        {
            var annotation = new DocumentAnnotation(
                Guid.NewGuid(),
                DocumentAnnotationKind.Highlight,
                selection.PageIndex,
                appearance,
                region);
            ActiveDocumentTab.AddAnnotation(annotation);
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
    /// Notifies the user that selected text was copied to the clipboard.
    /// </summary>
    public void NotifySelectedDocumentTextCopied()
    {
        StatusText = _textCatalog.GetString("status.clipboard.copied");
    }

    private async Task ReloadActiveDocumentAsync(WindowsDocumentTabViewModel tab, string? newFilePath = null)
    {
        await _renderOrchestrator.CancelDocumentJobsAsync(tab.SessionId);

        var pathToOpen = newFilePath ?? tab.FilePath;
        var openResult = await _openDocumentUseCase.ExecuteAsync(
            new OpenDocumentRequest(pathToOpen, DocumentOpenMode.ReplaceCurrent));

        if (openResult.IsFailure || openResult.Value is null)
        {
            return;
        }

        var newSession = openResult.Value;
        var pageCount = newSession.Metadata.PageCount ?? tab.TotalPages;

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
            for (var page = 1; page <= pageCount; page++)
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

        var sessions = _documentSessionStore.Sessions;
        var session = sessions.FirstOrDefault(s => s.Id == tab.SessionId);
        if (session is IReleasableDocumentSession releasable)
        {
            releasable.ReleaseResources();
        }

        _documentSessionStore.Remove(tab.SessionId);
    }

    private async Task ReopenAfterFailureAsync(WindowsDocumentTabViewModel tab)
    {
        var openResult = await _openDocumentUseCase.ExecuteAsync(
            new OpenDocumentRequest(tab.FilePath, DocumentOpenMode.AddToTabs));

        if (openResult.IsSuccess && openResult.Value is not null)
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
        var tab = ActiveDocumentTab;
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
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var pageIndex = new PageIndex(Math.Max(0, tab.CurrentPage - 1));
                var handle = _renderOrchestrator.Submit(
                    new RenderRequest(
                        $"windows-viewer:{tab.SessionId.Value}",
                        pageIndex,
                        tab.ZoomFactor,
                        tab.GetPageRotation(tab.CurrentPage),
                        Priority: RenderPriority.Viewer));

                var result = await handle.Completion;
                if (result.IsSuccess && result.Page is not null)
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
                    await RunOnUiThreadAsync(() => StatusText = FormatError("status.render.failed", result.Error));
                    return;
                }

                if (!result.IsCanceled && !result.IsObsolete)
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

            var handle = _loadDocumentTextUseCase.Execute();
            var result = await handle.Completion;
            if (result.IsSuccess && result.Index is not null && ReferenceEquals(tab, ActiveDocumentTab))
            {
                tab.DocumentTextIndex = result.Index;
            }
        }
        catch (Exception exception)
        {
            await RunOnUiThreadAsync(() => StatusText = $"{_textCatalog.GetString("status.render.failed")}: {exception.Message}");
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
            var handle = forceOcr
                ? _runDocumentOcrUseCase.Execute()
                : _loadDocumentTextUseCase.Execute();

            var result = await handle.Completion;
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
            foreach (var thumbnail in tab.Thumbnails.Where(item => item.Image is null).ToArray())
            {
                if (!SessionExists(tab.SessionId))
                {
                    return;
                }

                await RunOnUiThreadAsync(() => thumbnail.BeginRender());
                var outcome = await RenderThumbnailWithRetryAsync(tab, thumbnail);
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
            await RunOnUiThreadAsync(() => StatusText = $"{_textCatalog.GetString("status.render.failed")}: {exception.Message}");
        }
        finally
        {
            await RunOnUiThreadAsync(() =>
            {
                foreach (var thumbnail in tab.Thumbnails)
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
            await RunOnUiThreadAsync(() => StatusText = $"{_textCatalog.GetString("status.render.failed")}: {exception.Message}");
        }
    }

    private async Task<ThumbnailRenderOutcome> RenderThumbnailWithRetryAsync(
        WindowsDocumentTabViewModel tab,
        WindowsPageThumbnailViewModel thumbnail)
    {
        const int maxAttempts = 2;
        var pageIndex = new PageIndex(thumbnail.PageNumber - 1);

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (!SessionExists(tab.SessionId))
            {
                return ThumbnailRenderOutcome.Deferred;
            }

            var handle = SubmitForTab(
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

            var result = await handle.Completion;
            if (result.IsSuccess && result.Page is not null)
            {
                var page = result.Page;
                await SetThumbnailImageAsync(thumbnail, tab, page);
                return ThumbnailRenderOutcome.Rendered;
            }

            if (!result.IsCanceled && !result.IsObsolete)
            {
                if (result.Error is not null)
                {
                    await RunOnUiThreadAsync(() => StatusText = FormatError("status.render.failed", result.Error));
                }

                return ThumbnailRenderOutcome.Failed;
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
        var previousActiveSessionId = _documentSessionStore.ActiveSessionId;
        if (!_documentSessionStore.TryActivate(tab.SessionId))
        {
            return null;
        }

        var handle = _renderOrchestrator.Submit(request);
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

        var leftIndex = Math.Min(DocumentTabs.Count - 1, Math.Max(0, closedTabIndex - 1));
        return DocumentTabs[leftIndex];
    }

    private void UpdateTabSelection()
    {
        foreach (var tab in DocumentTabs)
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
        foreach (var tab in DocumentTabs)
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

    private async Task MergeDocumentSourcesAsync(string[] sourcePaths)
    {
        if (sourcePaths.Length < 2)
        {
            StatusText = _textCatalog.GetString("status.merge.selection_invalid");
            return;
        }

        var outputPath = await _fileDialogService.PickSavePdfAsync("Velune merged.pdf");
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            StatusText = _textCatalog.GetString("status.merge.cancelled");
            return;
        }

        await RunBusyAsync(async () =>
        {
            var result = await _mergePdfDocumentsUseCase.ExecuteAsync(
                new MergePdfDocumentsRequest(sourcePaths, outputPath));

            if (result.IsFailure)
            {
                await RunOnUiThreadAsync(() => StatusText = FormatError("status.merge.failed", result.Error));
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

        foreach (var item in _recentFilesService.GetAll())
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

        var extension = Path.GetExtension(path);
        return !string.IsNullOrWhiteSpace(extension) &&
            SupportedDocumentFormats.IsSupported(extension);
    }

    private void UpdateSelectedThumbnail(WindowsDocumentTabViewModel tab)
    {
        foreach (var thumbnail in tab.Thumbnails)
        {
            thumbnail.IsSelected = thumbnail.PageNumber == tab.CurrentPage;
        }
    }

    private void UpdateAnnotationToolSelection()
    {
        var selected = ActiveDocumentTab?.SelectedAnnotationTool ?? AnnotationTool.Select;
        foreach (var tool in AnnotationTools)
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
        var tool = ActiveDocumentTab?.SelectedAnnotationTool ?? AnnotationTool.Rectangle;
        var kind = tool switch
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

        var startPoint = _activeAnnotationStartPoint ?? activePoint;
        var bounds = BuildAnnotationBounds(kind, startPoint, activePoint);

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
        var bounds = DocumentAnnotationCoordinateMapper.CreateBounds(start, end);
        var hasMeaningfulDrag = bounds.Width > 0.01 || bounds.Height > 0.01;
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
        var color = AnnotationColorOptions.FirstOrDefault(item => item.IsSelected)?.Hex ?? "#FFE600";
        var fill = AnnotationFillEnabled ? AnnotationFillHex : null;
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
        var page = index.Pages.FirstOrDefault(item => item.PageIndex == pageIndex);
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
            _signatureAssetLookup.TryGetValue(SelectedSignatureAssetId, out var signatureAsset))
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

        var defaultValues = new[]
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
        var previousSelectedAssetId = SelectedSignatureAssetId;
        SignatureAssets.Clear();
        _signatureAssetLookup.Clear();

        foreach (var asset in _signatureAssetStore.GetAll().OrderByDescending(asset => asset.CreatedAt))
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
        foreach (var tab in DocumentTabs)
        {
            tab.UpdateSignatureAssets(_signatureAssetLookup);
        }
    }

    private void RefreshSignaturePadPreview()
    {
        var points = new PointCollection();
        foreach (var point in _signatureCapturePoints)
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

    private string FormatError(string statusKey, AppError? error)
    {
        var message = _textCatalog.GetString(statusKey);
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

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartDocumentOperation));
        OnPropertyChanged(nameof(CanSaveDocument));
        OpenCommand.NotifyCanExecuteChanged();
        MergeCommand.NotifyCanExecuteChanged();
        SaveDocumentCommand.NotifyCanExecuteChanged();
        SaveDocumentAsCommand.NotifyCanExecuteChanged();
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
