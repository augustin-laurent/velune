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

public sealed partial class WindowsMainViewModel : ObservableObject, IDisposable
{
    private const int MaxOpenDocumentTabs = 8;
    private const double DefaultViewerZoom = 1.35;
    private const double ThumbnailZoom = 0.20;
    private const int ThumbnailWidth = 170;
    private const int ThumbnailHeight = 150;

    private readonly OpenDocumentUseCase _openDocumentUseCase;
    private readonly CloseDocumentUseCase _closeDocumentUseCase;
    private readonly MergePdfDocumentsUseCase _mergePdfDocumentsUseCase;
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
    private readonly SemaphoreSlim _documentOpenGate = new(1, 1);
    private NormalizedPoint? _activeAnnotationStartPoint;
    private Guid? _previewAnnotationId;
    private bool _isApplyingThumbnailPreference;

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

    public WindowsMainViewModel(
        OpenDocumentUseCase openDocumentUseCase,
        CloseDocumentUseCase closeDocumentUseCase,
        MergePdfDocumentsUseCase mergePdfDocumentsUseCase,
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
        IWindowsTextCatalog textCatalog)
    {
        ArgumentNullException.ThrowIfNull(openDocumentUseCase);
        ArgumentNullException.ThrowIfNull(closeDocumentUseCase);
        ArgumentNullException.ThrowIfNull(mergePdfDocumentsUseCase);
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
        ArgumentNullException.ThrowIfNull(textCatalog);

        _openDocumentUseCase = openDocumentUseCase;
        _closeDocumentUseCase = closeDocumentUseCase;
        _mergePdfDocumentsUseCase = mergePdfDocumentsUseCase;
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

    public void ResetForWelcome()
    {
        ActiveDocumentTab = null;
        DocumentTabs.Clear();
        RefreshRecentFiles();
        OnPropertyChanged(nameof(IsPagesPanelVisible));
        StatusText = _textCatalog.GetString("status.ready");
    }

    public void NotifyBindingsRefresh()
    {
        OnPropertyChanged(nameof(ActiveDocumentTab));
        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(IsPagesPanelVisible));
        NotifySelectedDocumentTextChanged();
    }

    public Task EnsureActiveTabHydratedAsync()
    {
        return HydrateActiveTabAsync();
    }

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

        ActiveDocumentTab.SelectedAnnotationTool = tool;
        SelectedAnnotationPanelTab = "Tools";
        SetRightPanel(ActiveDocumentTab, RightPanel.Annotations);
        if (tool is AnnotationTool.Select)
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
        StatusText = _textCatalog.GetString("status.annotation.deleted");
    }

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
            await RunOnUiThreadAsync(() => StatusText = _textCatalog.Format("status.opened", tab.Title));
        });
    }

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

    public async Task ChangePageAsync(int pageNumber)
    {
        if (ActiveDocumentTab is null ||
            pageNumber < 1 ||
            pageNumber > ActiveDocumentTab.TotalPages)
        {
            return;
        }

        var tab = ActiveDocumentTab;
        await RunOnUiThreadAsync(() =>
        {
            tab.CurrentPage = pageNumber;
            UpdateSelectedThumbnail(tab);
        });
        await RenderActivePageAsync(tab);
    }

    public bool BeginAnnotationInteraction(double x, double y, double width, double height)
    {
        if (ActiveDocumentTab is null ||
            ActiveDocumentTab.SelectedAnnotationTool is AnnotationTool.Select ||
            width <= 0 ||
            height <= 0)
        {
            return false;
        }

        var point = DocumentAnnotationCoordinateMapper.MapVisualPointToNormalized(
            x,
            y,
            width,
            height,
            ActiveDocumentTab.Rotation);

        _activeAnnotationStartPoint = point;
        _activeAnnotationPoints.Clear();
        _activeAnnotationPoints.Add(point);
        return true;
    }

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

        ReplacePreviewAnnotation(CreateBoxAnnotation(point));
    }

    public void CompleteAnnotationInteraction(double x, double y, double width, double height)
    {
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

        ActiveDocumentTab.AddAnnotation(annotation);
        StatusText = _textCatalog.Format("status.annotation.added", GetAnnotationLabel(ActiveDocumentTab.SelectedAnnotationTool));
        CancelAnnotationInteraction();
    }

    public void CancelAnnotationInteraction()
    {
        ClearPreviewAnnotation();
        _activeAnnotationStartPoint = null;
        _activeAnnotationPoints.Clear();
    }

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

    public void NotifySelectedDocumentTextCopied()
    {
        StatusText = _textCatalog.GetString("status.clipboard.copied");
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
                        tab.Rotation,
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
                    Rotation: tab.Rotation,
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

    private DocumentAnnotation CreateBoxAnnotation(NormalizedPoint activePoint)
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

        var bounds = kind is DocumentAnnotationKind.Note or DocumentAnnotationKind.Stamp or DocumentAnnotationKind.Signature
            ? DocumentAnnotationCoordinateMapper.InflatePoint(_activeAnnotationStartPoint ?? activePoint, 0.18, 0.08)
            : DocumentAnnotationCoordinateMapper.CreateBounds(_activeAnnotationStartPoint ?? activePoint, activePoint);

        return new DocumentAnnotation(
            Guid.NewGuid(),
            kind,
            CurrentPageIndex(),
            CurrentAnnotationAppearance(),
            bounds,
            text: ResolveDefaultAnnotationText(kind));
    }

    private AnnotationAppearance CurrentAnnotationAppearance()
    {
        var color = AnnotationColorOptions.FirstOrDefault(item => item.IsSelected)?.Hex ?? "#FFE600";
        return new AnnotationAppearance(
            color,
            null,
            3,
            Math.Clamp(AnnotationOpacity / 100d, 0.05, 1));
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

    private string? ResolveDefaultAnnotationText(DocumentAnnotationKind kind)
    {
        return kind switch
        {
            DocumentAnnotationKind.Text => _textCatalog.GetString("annotation.default.text"),
            DocumentAnnotationKind.Note => _textCatalog.GetString("annotation.default.note"),
            DocumentAnnotationKind.Stamp => _textCatalog.GetString("annotation.default.stamp"),
            DocumentAnnotationKind.Signature => _textCatalog.GetString("annotation.default.signature_name"),
            _ => null
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
            new WindowsAnnotationToolItem(AnnotationTool.Highlight, labels.Highlight, "\uE7FB"),
            new WindowsAnnotationToolItem(AnnotationTool.Ink, labels.Ink, "\uED5F"),
            new WindowsAnnotationToolItem(AnnotationTool.Text, labels.Text, "\uE8D2"),
            new WindowsAnnotationToolItem(AnnotationTool.Note, labels.Note, "\uE90A"),
            new WindowsAnnotationToolItem(AnnotationTool.Rectangle, labels.Rectangle, "\uE9F5"),
            new WindowsAnnotationToolItem(AnnotationTool.Signature, labels.Signature, "\uED5F"),
            new WindowsAnnotationToolItem(AnnotationTool.Stamp, labels.Stamp, "\uE8B7")
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
        NotifySelectedDocumentTextChanged();
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
        OpenCommand.NotifyCanExecuteChanged();
        MergeCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedAnnotationPanelTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsAnnotationToolsTabSelected));
        OnPropertyChanged(nameof(IsAnnotationCommentsTabSelected));
        OnPropertyChanged(nameof(IsAnnotationStyleTabSelected));
        OnPropertyChanged(nameof(IsAnnotationToolsAppearancePanelVisible));
        OnPropertyChanged(nameof(IsAnnotationListPanelVisible));
    }

    partial void OnAnnotationOpacityChanged(double value)
    {
        OnPropertyChanged(nameof(AnnotationOpacityText));
    }

    partial void OnCacheSizeMegabytesChanged(double value)
    {
        OnPropertyChanged(nameof(CacheSizeText));
    }
}
