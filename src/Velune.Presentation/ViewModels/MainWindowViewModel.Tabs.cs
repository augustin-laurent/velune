using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Velune.Application.DTOs;
using Velune.Application.Text;
using Velune.Domain.Annotations;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Presentation.Localization;
using Velune.Presentation.Platform;
using Velune.Presentation.Search;

namespace Velune.Presentation.ViewModels;

public partial class MainWindowViewModel
{
    private enum WindowsRibbonSection
    {
        File,
        Home,
        View,
        Annotate,
        Help
    }

    private const int MaxOpenDocumentTabs = 8;

    private readonly Dictionary<DocumentId, DocumentTabState> _documentTabStates = [];
    private DocumentTabViewModel? _activeDocumentTab;
    private WindowsRibbonSection _selectedWindowsRibbonSection = WindowsRibbonSection.Home;
    private bool _isRestoringDocumentTab;

    public ObservableCollection<DocumentTabViewModel> DocumentTabs
    {
        get;
    } = [];

    public DocumentTabViewModel? ActiveDocumentTab
    {
        get => _activeDocumentTab;
        private set
        {
            if (ReferenceEquals(_activeDocumentTab, value))
            {
                return;
            }

            SetProperty(ref _activeDocumentTab, value);
            OnPropertyChanged(nameof(HasDocumentTabs));
        }
    }

    public bool IsWindowsShellVisible => PresentationPlatform.IsWindows;
    public bool IsClassicShellVisible => !IsWindowsShellVisible;
    public bool ShowClassicHeader => HasOpenDocument && IsClassicShellVisible;
    public bool HasDocumentTabs => DocumentTabs.Count > 0;
    public bool IsWindowsRibbonFileSelected => _selectedWindowsRibbonSection is WindowsRibbonSection.File;
    public bool IsWindowsRibbonHomeSelected => _selectedWindowsRibbonSection is WindowsRibbonSection.Home;
    public bool IsWindowsRibbonViewSelected => _selectedWindowsRibbonSection is WindowsRibbonSection.View;
    public bool IsWindowsRibbonAnnotateSelected => _selectedWindowsRibbonSection is WindowsRibbonSection.Annotate;
    public bool IsWindowsRibbonHelpSelected => _selectedWindowsRibbonSection is WindowsRibbonSection.Help;
    public bool ShowWindowsRibbonFileCommands => _selectedWindowsRibbonSection is WindowsRibbonSection.File or WindowsRibbonSection.Home;
    public bool ShowWindowsRibbonNavigationCommands =>
        IsPageNavigationVisible &&
        (_selectedWindowsRibbonSection is WindowsRibbonSection.Home or WindowsRibbonSection.View);
    public bool ShowWindowsRibbonZoomCommands =>
        HasOpenDocument &&
        (_selectedWindowsRibbonSection is WindowsRibbonSection.Home or WindowsRibbonSection.View);
    public bool ShowWindowsRibbonSearchCommands =>
        IsSearchAvailableForCurrentDocument &&
        (_selectedWindowsRibbonSection is WindowsRibbonSection.Home or WindowsRibbonSection.View);
    public bool ShowWindowsRibbonViewCommands => _selectedWindowsRibbonSection is WindowsRibbonSection.Home or WindowsRibbonSection.View or WindowsRibbonSection.Help;
    public bool ShowWindowsRibbonAnnotationCommands =>
        HasOpenDocument &&
        (_selectedWindowsRibbonSection is WindowsRibbonSection.Home or WindowsRibbonSection.Annotate);

    public IRelayCommand<string?> SelectWindowsRibbonSectionCommand
    {
        get;
    }

    public IAsyncRelayCommand<DocumentTabViewModel?> ActivateDocumentTabCommand
    {
        get;
    }

    public IAsyncRelayCommand<DocumentTabViewModel?> CloseDocumentTabCommand
    {
        get;
    }

    private DocumentOpenMode DefaultOpenMode => IsWindowsShellVisible
        ? DocumentOpenMode.AddToTabs
        : DocumentOpenMode.ReplaceCurrent;

    private void SelectWindowsRibbonSection(string? sectionValue)
    {
        if (!Enum.TryParse<WindowsRibbonSection>(sectionValue, ignoreCase: true, out var section) ||
            _selectedWindowsRibbonSection == section)
        {
            return;
        }

        _selectedWindowsRibbonSection = section;
        NotifyWindowsRibbonSectionChanged();
    }

    private async Task ActivateDocumentTabAsync(DocumentTabViewModel? tab)
    {
        if (tab is null || ReferenceEquals(tab, ActiveDocumentTab))
        {
            return;
        }

        await SaveActiveDocumentTabStateForSwitchAsync();
        if (!await TryRestoreOrReopenDocumentTabAsync(tab))
        {
            return;
        }

        await EnsureActiveDocumentTabContentAsync();
        StatusText = L("status.tabs.activated", tab.Title);
    }

    private async Task CloseDocumentTabAsync(DocumentTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        if (IsDocumentTabDirty(tab))
        {
            EnqueueDirtyTabCloseConfirmation(tab);
            return;
        }

        await CloseDocumentTabCoreAsync(tab, discardDirty: true);
    }

    public async Task<bool> TryCloseAllDocumentTabsAsync()
    {
        if (!IsWindowsShellVisible)
        {
            return true;
        }

        foreach (var tab in DocumentTabs.ToArray())
        {
            if (IsDocumentTabDirty(tab))
            {
                await ActivateDocumentTabAsync(tab);
                EnqueueDirtyTabCloseConfirmation(tab);
                return false;
            }

            await CloseDocumentTabCoreAsync(tab, discardDirty: true);
        }

        return true;
    }

    private async Task<bool> TryActivateExistingDocumentTabAsync(string filePath)
    {
        var tab = DocumentTabs.FirstOrDefault(item => PathsEqual(item.FilePath, filePath));
        if (tab is null)
        {
            return false;
        }

        await ActivateDocumentTabAsync(tab);
        if (!ReferenceEquals(ActiveDocumentTab, tab))
        {
            await TryRestoreOrReopenDocumentTabAsync(tab);
        }

        ActiveDocumentTab = tab;
        UpdateDocumentTabSelection(tab);
        return true;
    }

    private bool CanOpenAdditionalDocumentTab()
    {
        if (DocumentTabs.Count < MaxOpenDocumentTabs)
        {
            return true;
        }

        EnqueueLocalizedWarning(
            "notification.tabs.limit.title",
            "notification.tabs.limit.message",
            replaceCurrent: true,
            MaxOpenDocumentTabs);
        StatusText = L("status.tabs.limit");
        return false;
    }

    private async Task SaveActiveDocumentTabStateForSwitchAsync()
    {
        if (!IsWindowsShellVisible || ActiveDocumentTab is null || !HasOpenDocument)
        {
            return;
        }

        CancelPendingViewportFitUpdate();
        CancelCurrentRender();
        CancelCurrentTextAnalysis();
        CancelThumbnailGeneration();

        _documentSessionStore.TryActivate(ActiveDocumentTab.SessionId);
        _documentTabStates[ActiveDocumentTab.SessionId] = CaptureCurrentDocumentTabState();
        UpdateActiveDocumentTabSummary();

        foreach (var thumbnail in Thumbnails)
        {
            thumbnail.IsLoading = false;
        }

        CurrentRenderedBitmap = null;
        CurrentAnnotationOverlayBitmap?.Dispose();
        CurrentAnnotationOverlayBitmap = null;
        SignaturePadPreviewBitmap?.Dispose();
        SignaturePadPreviewBitmap = null;
        Thumbnails.Clear();
        DocumentInfoItems.Clear();
        PrintDestinations.Clear();
        SearchResults.Clear();
        SearchHighlights.Clear();
        TextSelectionHighlights.Clear();
        CurrentPageAnnotations.Clear();
    }

    private DocumentTabState CaptureCurrentDocumentTabState()
    {
        return new DocumentTabState
        {
            CurrentDocumentName = CurrentDocumentName,
            CurrentDocumentType = CurrentDocumentType,
            CurrentDocumentPath = CurrentDocumentPath,
            EditableDocumentName = EditableDocumentName,
            IsEditingDocumentName = IsEditingDocumentName,
            IsCurrentImageDocument = IsCurrentImageDocument,
            IsImageAutoFitEnabled = _isImageAutoFitEnabled,
            IsApplyingPdfStructureOperation = _isApplyingPdfStructureOperation,
            HasPendingMergedDocumentSave = _hasPendingMergedDocumentSave,
            HasPendingPageReorder = HasPendingPageReorder,
            PendingMergedDocumentTemporaryDirectory = _pendingMergedDocumentTemporaryDirectory,
            PendingMergedDocumentSuggestedFileName = _pendingMergedDocumentSuggestedFileName,
            PendingMergedDocumentTargetPath = _pendingMergedDocumentTargetPath,
            PendingMergedDocumentSaveSuccessStatusKey = _pendingMergedDocumentSaveSuccessStatusKey,
            PendingMergedDocumentRequiresSavePicker = _pendingMergedDocumentRequiresSavePicker,
            CurrentPage = CurrentPage,
            TotalPages = TotalPages,
            CurrentZoom = CurrentZoom,
            CurrentRotation = CurrentRotation,
            GoToPageInput = GoToPageInput,
            CurrentRenderedBitmap = CurrentRenderedBitmap,
            ShowThumbnailsPanelPreference = ShowThumbnailsPanelPreference,
            IsInfoPanelVisible = IsInfoPanelVisible,
            IsSearchPanelVisible = IsSearchPanelVisible,
            IsPreferencesPanelVisible = IsPreferencesPanelVisible,
            IsPrintPanelVisible = IsPrintPanelVisible,
            IsAnnotationsPanelVisible = IsAnnotationsPanelVisible,
            DocumentInfoWarning = DocumentInfoWarning,
            SearchPanelNotice = SearchPanelNotice,
            PrintPanelNotice = PrintPanelNotice,
            SelectedPrintDestination = SelectedPrintDestination,
            SelectedPrintPageRangeOption = SelectedPrintPageRangeOption,
            PrintCustomPageRange = PrintCustomPageRange,
            PrintCopiesInput = PrintCopiesInput,
            SelectedPrintOrientationOption = SelectedPrintOrientationOption,
            PrintFitToPage = PrintFitToPage,
            SearchQueryInput = SearchQueryInput,
            SelectedDocumentText = SelectedDocumentText,
            CurrentDocumentTextIndex = _currentDocumentTextIndex,
            CurrentDocumentTextSelection = _currentDocumentTextSelection,
            DocumentTextSelectionAnchorPoint = _documentTextSelectionAnchorPoint,
            RequiresSearchOcr = _requiresSearchOcr,
            SelectedSearchResultIndex = _selectedSearchResultIndex,
            ActivePageIndex = _pageViewportStore.ActivePageIndex,
            PageRotations = BuildCurrentPageRotationMap(),
            Thumbnails = [.. Thumbnails],
            DocumentInfoItems = [.. DocumentInfoItems],
            PrintDestinations = [.. PrintDestinations],
            SearchResults = [.. SearchResults],
            SearchHighlights = [.. SearchHighlights],
            TextSelectionHighlights = [.. TextSelectionHighlights],
            Annotations = [.. _annotations.Select(annotation => annotation.DeepCopy())],
            AnnotationUndoSnapshots = [.. _annotationUndoSnapshots.Select(CloneAnnotations)],
            AnnotationRedoSnapshots = [.. _annotationRedoSnapshots.Select(CloneAnnotations)],
            ActiveAnnotationPoints = [.. _activeAnnotationPoints],
            SignatureCapturePoints = [.. _signatureCapturePoints],
            AnnotationPreview = _annotationPreview?.DeepCopy(),
            SelectedAnnotationId = _selectedAnnotationId,
            AnnotationAnchorPoint = _annotationAnchorPoint,
            IsCapturingAnnotation = _isCapturingAnnotation,
            SelectedAnnotationTool = SelectedAnnotationTool,
            AnnotationTextDraft = AnnotationTextDraft,
            SignatureAssetNameInput = SignatureAssetNameInput,
            SelectedAnnotationColorHex = SelectedAnnotationColorHex,
            SelectedAnnotationStrokeThickness = SelectedAnnotationStrokeThickness,
            SelectedSignatureAssetId = SelectedSignatureAssetId,
            AnnotationsPanelNotice = AnnotationsPanelNotice
        };
    }

    private bool RestoreDocumentTabState(DocumentTabViewModel tab)
    {
        if (!_documentSessionStore.TryActivate(tab.SessionId))
        {
            return false;
        }

        if (!_documentTabStates.TryGetValue(tab.SessionId, out var state))
        {
            ActiveDocumentTab = tab;
            UpdateDocumentTabSelection(tab);
            RefreshFromSession();
            _pageViewportStore.Initialize(TotalPages > 0 ? TotalPages : 1);
            _pageViewportStore.SetActivePage(new PageIndex(Math.Max(0, CurrentPage - 1)));
            RefreshPageViewState();
            NotifyRestoredDocumentTabState();
            return true;
        }

        _isRestoringDocumentTab = true;
        try
        {
            ActiveDocumentTab = tab;
            UpdateDocumentTabSelection(tab);

            _pageViewportStore.Initialize(Math.Max(1, state.TotalPages));
            foreach (var rotation in state.PageRotations)
            {
                _pageViewportStore.SetRotation(new PageIndex(rotation.Key - 1), rotation.Value);
            }

            _pageViewportStore.SetActivePage(state.ActivePageIndex);

            IsCurrentImageDocument = state.IsCurrentImageDocument;
            _isImageAutoFitEnabled = state.IsImageAutoFitEnabled;
            _isApplyingPdfStructureOperation = state.IsApplyingPdfStructureOperation;
            _hasPendingMergedDocumentSave = state.HasPendingMergedDocumentSave;
            HasPendingPageReorder = state.HasPendingPageReorder;
            _pendingMergedDocumentTemporaryDirectory = state.PendingMergedDocumentTemporaryDirectory;
            _pendingMergedDocumentSuggestedFileName = state.PendingMergedDocumentSuggestedFileName;
            _pendingMergedDocumentTargetPath = state.PendingMergedDocumentTargetPath;
            _pendingMergedDocumentSaveSuccessStatusKey = state.PendingMergedDocumentSaveSuccessStatusKey;
            _pendingMergedDocumentRequiresSavePicker = state.PendingMergedDocumentRequiresSavePicker;

            CurrentDocumentName = state.CurrentDocumentName;
            CurrentDocumentType = state.CurrentDocumentType;
            CurrentDocumentPath = state.CurrentDocumentPath;
            EditableDocumentName = state.EditableDocumentName;
            IsEditingDocumentName = state.IsEditingDocumentName;
            CurrentPage = state.CurrentPage;
            TotalPages = state.TotalPages;
            CurrentZoom = state.CurrentZoom;
            CurrentRotation = state.CurrentRotation;
            GoToPageInput = state.GoToPageInput;
            SearchQueryInput = state.SearchQueryInput;
            SelectedDocumentText = state.SelectedDocumentText;
            ShowThumbnailsPanelPreference = state.ShowThumbnailsPanelPreference;
            IsInfoPanelVisible = state.IsInfoPanelVisible;
            IsSearchPanelVisible = state.IsSearchPanelVisible;
            IsPreferencesPanelVisible = state.IsPreferencesPanelVisible;
            IsPrintPanelVisible = state.IsPrintPanelVisible;
            IsAnnotationsPanelVisible = state.IsAnnotationsPanelVisible;
            DocumentInfoWarning = state.DocumentInfoWarning;
            SearchPanelNotice = state.SearchPanelNotice;
            PrintPanelNotice = state.PrintPanelNotice;
            SelectedPrintDestination = state.SelectedPrintDestination;
            SelectedPrintPageRangeOption = state.SelectedPrintPageRangeOption;
            PrintCustomPageRange = state.PrintCustomPageRange;
            PrintCopiesInput = state.PrintCopiesInput;
            SelectedPrintOrientationOption = state.SelectedPrintOrientationOption;
            PrintFitToPage = state.PrintFitToPage;
            _currentDocumentTextIndex = state.CurrentDocumentTextIndex;
            _currentDocumentTextSelection = state.CurrentDocumentTextSelection;
            _documentTextSelectionAnchorPoint = state.DocumentTextSelectionAnchorPoint;
            _requiresSearchOcr = state.RequiresSearchOcr;
            _selectedSearchResultIndex = state.SelectedSearchResultIndex;

            RestoreCollection(Thumbnails, state.Thumbnails);
            RestoreCollection(DocumentInfoItems, state.DocumentInfoItems);
            RestoreCollection(PrintDestinations, state.PrintDestinations);
            RestoreCollection(SearchResults, state.SearchResults);
            RestoreCollection(SearchHighlights, state.SearchHighlights);
            RestoreCollection(TextSelectionHighlights, state.TextSelectionHighlights);

            _annotations.Clear();
            _annotations.AddRange(state.Annotations.Select(annotation => annotation.DeepCopy()));
            RestoreAnnotationSnapshotStack(_annotationUndoSnapshots, state.AnnotationUndoSnapshots);
            RestoreAnnotationSnapshotStack(_annotationRedoSnapshots, state.AnnotationRedoSnapshots);
            _activeAnnotationPoints.Clear();
            _activeAnnotationPoints.AddRange(state.ActiveAnnotationPoints);
            _signatureCapturePoints.Clear();
            _signatureCapturePoints.AddRange(state.SignatureCapturePoints);
            _annotationPreview = state.AnnotationPreview?.DeepCopy();
            _selectedAnnotationId = state.SelectedAnnotationId;
            _annotationAnchorPoint = state.AnnotationAnchorPoint;
            _isCapturingAnnotation = state.IsCapturingAnnotation;
            SelectedAnnotationTool = state.SelectedAnnotationTool;
            AnnotationTextDraft = state.AnnotationTextDraft;
            SignatureAssetNameInput = state.SignatureAssetNameInput;
            SelectedAnnotationColorHex = state.SelectedAnnotationColorHex;
            SelectedAnnotationStrokeThickness = state.SelectedAnnotationStrokeThickness;
            SelectedSignatureAssetId = state.SelectedSignatureAssetId;
            AnnotationsPanelNotice = state.AnnotationsPanelNotice;

            CurrentRenderedBitmap = state.CurrentRenderedBitmap;
            state.CurrentRenderedBitmap = null;
            RefreshAnnotationWorkspaceState();
            RefreshSignaturePadPreview();
            NotifyRestoredDocumentTabState();
            return true;
        }
        finally
        {
            _isRestoringDocumentTab = false;
        }
    }

    private async Task<bool> TryRestoreOrReopenDocumentTabAsync(DocumentTabViewModel tab)
    {
        if (RestoreDocumentTabState(tab))
        {
            return true;
        }

        var result = await _openDocumentUseCase.ExecuteAsync(
            new OpenDocumentRequest(tab.FilePath, DocumentOpenMode.AddToTabs));
        if (result.IsFailure || result.Value is null)
        {
            EnqueueLocalizedError(result.Error, "error.open.failed.title", "error.open.failed.message");
            StatusText = L("status.open.failed");
            return false;
        }

        tab.SessionId = result.Value.Id;
        tab.FilePath = result.Value.Metadata.FilePath;
        tab.Title = result.Value.Metadata.FileName;
        ActiveDocumentTab = tab;
        UpdateDocumentTabSelection(tab);
        RefreshFromSession();
        _pageViewportStore.Initialize(TotalPages > 0 ? TotalPages : 1);
        _pageViewportStore.SetActivePage(new PageIndex(0));
        RefreshPageViewState();
        ClearThumbnails();
        BuildThumbnailPlaceholders();
        NotifyRestoredDocumentTabState();
        return true;
    }

    private void RegisterOpenedDocumentTab(DocumentOpenMode openMode, string filePath)
    {
        if (!IsWindowsShellVisible || _documentSessionStore.Current is not { } session)
        {
            return;
        }

        if (openMode is DocumentOpenMode.ReplaceCurrent && ActiveDocumentTab is not null)
        {
            _documentTabStates.Remove(ActiveDocumentTab.SessionId);
            ActiveDocumentTab.SessionId = session.Id;
            ActiveDocumentTab.FilePath = session.Metadata.FilePath;
            ActiveDocumentTab.Title = CurrentDocumentName ?? session.Metadata.FileName;
            ActiveDocumentTab.IsDirty = false;
            UpdateDocumentTabSelection(ActiveDocumentTab);
            return;
        }

        var tab = new DocumentTabViewModel(
            session.Id,
            CurrentDocumentName ?? session.Metadata.FileName,
            session.Metadata.FilePath);

        DocumentTabs.Add(tab);
        ActiveDocumentTab = tab;
        UpdateDocumentTabSelection(tab);
        OnPropertyChanged(nameof(HasDocumentTabs));
    }

    private async Task CloseDocumentTabCoreAsync(DocumentTabViewModel tab, bool discardDirty)
    {
        var wasActive = ReferenceEquals(tab, ActiveDocumentTab);
        var tabIndex = DocumentTabs.IndexOf(tab);
        var nextTab = ResolveNextDocumentTab(tabIndex, tab);

        if (wasActive)
        {
            CancelPendingViewportFitUpdate();
            CancelCurrentRender();
            CancelCurrentTextAnalysis();
            CancelThumbnailGeneration();
            ClearPendingMergedDocumentSave(deleteTemporaryDirectory: discardDirty);
        }

        DisposeDocumentTabState(tab.SessionId, disposeActiveState: wasActive);
        await _closeDocumentUseCase.ExecuteAsync(new CloseDocumentRequest(tab.SessionId));
        DocumentTabs.Remove(tab);
        _documentTabStates.Remove(tab.SessionId);
        OnPropertyChanged(nameof(HasDocumentTabs));

        if (!wasActive)
        {
            StatusText = L("status.tabs.closed", tab.Title);
            return;
        }

        if (nextTab is not null)
        {
            ActiveDocumentTab = null;
            await TryRestoreOrReopenDocumentTabAsync(nextTab);
            await EnsureActiveDocumentTabContentAsync();
            StatusText = L("status.tabs.closed", tab.Title);
            return;
        }

        ActiveDocumentTab = null;
        ResetDocumentState();
        _pageViewportStore.Clear();
        StatusText = L("status.document.closed");
    }

    private DocumentTabViewModel? ResolveNextDocumentTab(int closingIndex, DocumentTabViewModel closingTab)
    {
        if (DocumentTabs.Count <= 1 || closingIndex < 0)
        {
            return null;
        }

        var rightIndex = closingIndex + 1;
        if (rightIndex < DocumentTabs.Count)
        {
            return DocumentTabs[rightIndex];
        }

        var leftIndex = closingIndex - 1;
        return leftIndex >= 0 && !ReferenceEquals(DocumentTabs[leftIndex], closingTab)
            ? DocumentTabs[leftIndex]
            : null;
    }

    private void EnqueueDirtyTabCloseConfirmation(DocumentTabViewModel tab)
    {
        EnqueueNotification(
            new NotificationEntry(
                L("notification.tabs.dirty.title"),
                L("notification.tabs.dirty.message", tab.Title),
                NotificationKind.Confirmation,
                L("notification.tabs.dirty.save"),
                () => _ = SaveAndCloseDocumentTabAsync(tab),
                L("notification.tabs.dirty.discard"),
                () => _ = CloseDocumentTabCoreAsync(tab, discardDirty: true),
                IsDismissible: true),
            replaceCurrent: true);
        StatusText = L("status.confirmation.required");
    }

    private async Task SaveAndCloseDocumentTabAsync(DocumentTabViewModel tab)
    {
        if (!ReferenceEquals(tab, ActiveDocumentTab))
        {
            await ActivateDocumentTabAsync(tab);
        }

        await SaveDocumentAsync();
        UpdateActiveDocumentTabSummary();

        if (!IsDocumentTabDirty(tab))
        {
            await CloseDocumentTabCoreAsync(tab, discardDirty: false);
        }
    }

    private bool IsDocumentTabDirty(DocumentTabViewModel tab)
    {
        return ReferenceEquals(tab, ActiveDocumentTab)
            ? HasDirtyDocumentState()
            : tab.IsDirty;
    }

    private bool HasDirtyDocumentState()
    {
        return HasOpenDocument &&
               !IsPdfStructureOperationInProgress &&
               !string.IsNullOrWhiteSpace(CurrentDocumentPath) &&
               ((IsPdfDocument && (_hasPendingMergedDocumentSave ||
                                   CanPersistCurrentPageRotation ||
                                   HasPendingPageReorder ||
                                   HasPendingRequestedSaveNameChange() ||
                                   HasPendingAnnotationChanges)) ||
                (IsCurrentImageDocument && (HasPendingRequestedSaveNameChange() || HasPendingAnnotationChanges)));
    }

    private void UpdateActiveDocumentTabSummary()
    {
        if (!IsWindowsShellVisible || ActiveDocumentTab is null || _isRestoringDocumentTab)
        {
            return;
        }

        ActiveDocumentTab.Title = CurrentDocumentName ?? HeaderTitle;
        if (!string.IsNullOrWhiteSpace(CurrentDocumentPath))
        {
            ActiveDocumentTab.FilePath = CurrentDocumentPath;
        }

        ActiveDocumentTab.IsDirty = HasDirtyDocumentState();
    }

    private void UpdateDocumentTabSelection(DocumentTabViewModel? activeTab)
    {
        foreach (var tab in DocumentTabs)
        {
            tab.IsActive = ReferenceEquals(tab, activeTab);
        }
    }

    private void DisposeDocumentTabState(DocumentId sessionId, bool disposeActiveState)
    {
        if (_documentTabStates.TryGetValue(sessionId, out var state))
        {
            state.Dispose();
        }

        if (!disposeActiveState)
        {
            return;
        }

        CurrentRenderedBitmap?.Dispose();
        CurrentRenderedBitmap = null;
        ClearThumbnails();
        CurrentAnnotationOverlayBitmap?.Dispose();
        CurrentAnnotationOverlayBitmap = null;
        SignaturePadPreviewBitmap?.Dispose();
        SignaturePadPreviewBitmap = null;
    }

    private void NotifyRestoredDocumentTabState()
    {
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(HeaderSubtitle));
        OnPropertyChanged(nameof(HasRenderedPage));
        OnPropertyChanged(nameof(HasThumbnails));
        OnPropertyChanged(nameof(HasSearchResults));
        OnPropertyChanged(nameof(HasSearchHighlights));
        OnPropertyChanged(nameof(HasTextSelectionHighlights));
        OnPropertyChanged(nameof(HasDocumentInfo));
        OnPropertyChanged(nameof(HasDocumentInfoWarning));
        OnPropertyChanged(nameof(HasPrintPanelNotice));
        OnPropertyChanged(nameof(HasSearchPanelNotice));
        OnPropertyChanged(nameof(ShowClassicHeader));
        OnPropertyChanged(nameof(CanSaveDocument));
        NotifySearchStateChanged();
        NotifySidebarVisibilityChanged();
        NotifyViewerModeChanged();
        SaveDocumentCommand.NotifyCanExecuteChanged();
    }

    private async Task EnsureActiveDocumentTabContentAsync()
    {
        if (!IsWindowsShellVisible || !HasOpenDocument)
        {
            return;
        }

        if (CurrentRenderedBitmap is null)
        {
            await ApplyPreferredDefaultZoomAsync(preserveStatusText: true);
        }

        if (CurrentRenderedBitmap is null)
        {
            await RenderCurrentPageAsync(preserveStatusText: true);
        }

        if (TotalPages > 0)
        {
            if (Thumbnails.Count == 0)
            {
                BuildThumbnailPlaceholders();
            }

            if (HasMissingThumbnails())
            {
                _ = GenerateThumbnailsAsync();
            }
        }

        RefreshAnnotationWorkspaceState();
    }

    private bool HasMissingThumbnails()
    {
        return Thumbnails.Any(thumbnail => thumbnail.Thumbnail is null);
    }

    private void NotifyWindowsRibbonSectionChanged()
    {
        OnPropertyChanged(nameof(IsWindowsRibbonFileSelected));
        OnPropertyChanged(nameof(IsWindowsRibbonHomeSelected));
        OnPropertyChanged(nameof(IsWindowsRibbonViewSelected));
        OnPropertyChanged(nameof(IsWindowsRibbonAnnotateSelected));
        OnPropertyChanged(nameof(IsWindowsRibbonHelpSelected));
        OnPropertyChanged(nameof(ShowWindowsRibbonFileCommands));
        OnPropertyChanged(nameof(ShowWindowsRibbonNavigationCommands));
        OnPropertyChanged(nameof(ShowWindowsRibbonZoomCommands));
        OnPropertyChanged(nameof(ShowWindowsRibbonSearchCommands));
        OnPropertyChanged(nameof(ShowWindowsRibbonViewCommands));
        OnPropertyChanged(nameof(ShowWindowsRibbonAnnotationCommands));
    }

    private static void RestoreAnnotationSnapshotStack(
        Stack<IReadOnlyList<DocumentAnnotation>> target,
        IReadOnlyList<IReadOnlyList<DocumentAnnotation>> snapshots)
    {
        target.Clear();
        foreach (var snapshot in snapshots.Reverse())
        {
            target.Push(CloneAnnotations(snapshot));
        }
    }

    private static void RestoreCollection<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private sealed class DocumentTabState : IDisposable
    {
        public string? CurrentDocumentName
        {
            get;
            init;
        }

        public string? CurrentDocumentType
        {
            get;
            init;
        }

        public string? CurrentDocumentPath
        {
            get;
            init;
        }

        public string EditableDocumentName
        {
            get;
            init;
        } = string.Empty;

        public bool IsEditingDocumentName
        {
            get;
            init;
        }

        public bool IsCurrentImageDocument
        {
            get;
            init;
        }

        public bool IsImageAutoFitEnabled
        {
            get;
            init;
        }

        public bool IsApplyingPdfStructureOperation
        {
            get;
            init;
        }

        public bool HasPendingMergedDocumentSave
        {
            get;
            init;
        }

        public bool HasPendingPageReorder
        {
            get;
            init;
        }

        public string? PendingMergedDocumentTemporaryDirectory
        {
            get;
            init;
        }

        public string? PendingMergedDocumentSuggestedFileName
        {
            get;
            init;
        }

        public string? PendingMergedDocumentTargetPath
        {
            get;
            init;
        }

        public string PendingMergedDocumentSaveSuccessStatusKey
        {
            get;
            init;
        } = "status.save.merged_pdf";

        public bool PendingMergedDocumentRequiresSavePicker
        {
            get;
            init;
        }

        public int CurrentPage
        {
            get;
            init;
        } = 1;

        public int TotalPages
        {
            get;
            init;
        }

        public string CurrentZoom
        {
            get;
            init;
        } = "100%";

        public string CurrentRotation
        {
            get;
            init;
        } = "0°";

        public string GoToPageInput
        {
            get;
            init;
        } = "1";

        public Bitmap? CurrentRenderedBitmap
        {
            get;
            set;
        }

        public bool ShowThumbnailsPanelPreference
        {
            get;
            init;
        }

        public bool IsInfoPanelVisible
        {
            get;
            init;
        }

        public bool IsSearchPanelVisible
        {
            get;
            init;
        }

        public bool IsPreferencesPanelVisible
        {
            get;
            init;
        }

        public bool IsPrintPanelVisible
        {
            get;
            init;
        }

        public bool IsAnnotationsPanelVisible
        {
            get;
            init;
        }

        public string? DocumentInfoWarning
        {
            get;
            init;
        }

        public string? SearchPanelNotice
        {
            get;
            init;
        }

        public string? PrintPanelNotice
        {
            get;
            init;
        }

        public PrintDestinationInfo? SelectedPrintDestination
        {
            get;
            init;
        }

        public LocalizedOption<PrintPageRangeChoice>? SelectedPrintPageRangeOption
        {
            get;
            init;
        }

        public string PrintCustomPageRange
        {
            get;
            init;
        } = string.Empty;

        public string PrintCopiesInput
        {
            get;
            init;
        } = "1";

        public LocalizedOption<PrintOrientationOption>? SelectedPrintOrientationOption
        {
            get;
            init;
        }

        public bool PrintFitToPage
        {
            get;
            init;
        }

        public string SearchQueryInput
        {
            get;
            init;
        } = string.Empty;

        public string? SelectedDocumentText
        {
            get;
            init;
        }

        public DocumentTextIndex? CurrentDocumentTextIndex
        {
            get;
            init;
        }

        public DocumentTextSelectionResult? CurrentDocumentTextSelection
        {
            get;
            init;
        }

        public DocumentTextSelectionPoint? DocumentTextSelectionAnchorPoint
        {
            get;
            init;
        }

        public bool RequiresSearchOcr
        {
            get;
            init;
        }

        public int SelectedSearchResultIndex
        {
            get;
            init;
        } = -1;

        public PageIndex ActivePageIndex
        {
            get;
            init;
        } = new(0);

        public Dictionary<int, Rotation> PageRotations
        {
            get;
            init;
        } = [];

        public IReadOnlyList<PageThumbnailItemViewModel> Thumbnails
        {
            get;
            init;
        } = [];

        public IReadOnlyList<DocumentInfoItem> DocumentInfoItems
        {
            get;
            init;
        } = [];

        public IReadOnlyList<PrintDestinationInfo> PrintDestinations
        {
            get;
            init;
        } = [];

        public IReadOnlyList<SearchResultItemViewModel> SearchResults
        {
            get;
            init;
        } = [];

        public IReadOnlyList<SearchHighlightItem> SearchHighlights
        {
            get;
            init;
        } = [];

        public IReadOnlyList<SearchHighlightItem> TextSelectionHighlights
        {
            get;
            init;
        } = [];

        public IReadOnlyList<DocumentAnnotation> Annotations
        {
            get;
            init;
        } = [];

        public IReadOnlyList<IReadOnlyList<DocumentAnnotation>> AnnotationUndoSnapshots
        {
            get;
            init;
        } = [];

        public IReadOnlyList<IReadOnlyList<DocumentAnnotation>> AnnotationRedoSnapshots
        {
            get;
            init;
        } = [];

        public IReadOnlyList<NormalizedPoint> ActiveAnnotationPoints
        {
            get;
            init;
        } = [];

        public IReadOnlyList<NormalizedPoint> SignatureCapturePoints
        {
            get;
            init;
        } = [];

        public DocumentAnnotation? AnnotationPreview
        {
            get;
            init;
        }

        public Guid? SelectedAnnotationId
        {
            get;
            init;
        }

        public NormalizedPoint? AnnotationAnchorPoint
        {
            get;
            init;
        }

        public bool IsCapturingAnnotation
        {
            get;
            init;
        }

        public AnnotationTool SelectedAnnotationTool
        {
            get;
            init;
        } = AnnotationTool.Select;

        public string AnnotationTextDraft
        {
            get;
            init;
        } = string.Empty;

        public string SignatureAssetNameInput
        {
            get;
            init;
        } = string.Empty;

        public string SelectedAnnotationColorHex
        {
            get;
            init;
        } = DefaultAnnotationColorHex;

        public double SelectedAnnotationStrokeThickness
        {
            get;
            init;
        } = DefaultAnnotationStrokeThickness;

        public string? SelectedSignatureAssetId
        {
            get;
            init;
        }

        public string? AnnotationsPanelNotice
        {
            get;
            init;
        }

        public void Dispose()
        {
            CurrentRenderedBitmap?.Dispose();
            foreach (var thumbnail in Thumbnails)
            {
                thumbnail.Dispose();
            }
        }
    }
}
