using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Velune.Application.Abstractions;
using Velune.Application.Annotations;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Annotations;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Presentation.Imaging;

namespace Velune.Presentation.ViewModels;

public partial class MainWindowViewModel
{
    private const double AnnotationDefaultWidthRatio = 0.24;
    private const double AnnotationDefaultHeightRatio = 0.12;
    private const double SignatureDefaultWidthRatio = 0.24;
    private const double SignatureDefaultHeightRatio = 0.10;
    private const int SignaturePadPreviewWidth = 248;
    private const int SignaturePadPreviewHeight = 124;

    private readonly List<DocumentAnnotation> _annotations = [];
    private readonly Stack<IReadOnlyList<DocumentAnnotation>> _annotationUndoSnapshots = [];
    private readonly Stack<IReadOnlyList<DocumentAnnotation>> _annotationRedoSnapshots = [];
    private readonly List<NormalizedPoint> _activeAnnotationPoints = [];
    private readonly List<NormalizedPoint> _signatureCapturePoints = [];
    private readonly Dictionary<string, SignatureAsset> _signatureAssetLookup = new(StringComparer.Ordinal);
    private DocumentAnnotation? _annotationPreview;
    private Guid? _selectedAnnotationId;
    private NormalizedPoint? _annotationAnchorPoint;
    private bool _isCapturingAnnotation;

    [ObservableProperty]
    private bool _isAnnotationsPanelVisible;

    [ObservableProperty]
    private AnnotationTool _selectedAnnotationTool = AnnotationTool.Select;

    [ObservableProperty]
    private string _annotationTextDraft = string.Empty;

    [ObservableProperty]
    private string _signatureAssetNameInput = string.Empty;

    [ObservableProperty]
    private string? _selectedSignatureAssetId;

    [ObservableProperty]
    private string? _annotationsPanelNotice;

    [ObservableProperty]
    private Bitmap? _currentAnnotationOverlayBitmap;

    [ObservableProperty]
    private Bitmap? _signaturePadPreviewBitmap;

    public ObservableCollection<AnnotationListItemViewModel> CurrentPageAnnotations
    {
        get;
        private set;
    } = [];

    public ObservableCollection<SignatureAsset> SignatureAssets
    {
        get;
        private set;
    } = [];

    public bool CanAnnotateCurrentDocument => HasOpenDocument;
    public bool IsSearchAvailableForCurrentDocument => IsPdfDocument;
    public bool IsAnnotationModeActive => HasOpenDocument && IsAnnotationsPanelOpen;
    public bool SidebarHostVisible => HasOpenDocument;
    public double SidebarOpacity => IsSidebarVisible ? 1 : 0;
    public bool IsSidebarInteractive => IsSidebarVisible;
    public double InfoPanelOpacity => IsInfoPanelOpen ? 1 : 0;
    public bool IsInfoPanelInteractive => IsInfoPanelOpen;
    public double SearchPanelOpacity => IsSearchPanelOpen ? 1 : 0;
    public bool IsSearchPanelInteractive => IsSearchPanelOpen;
    public double PreferencesPanelOpacity => IsPreferencesPanelOpen ? 1 : 0;
    public bool IsPreferencesPanelInteractive => IsPreferencesPanelOpen;
    public double PrintPanelOpacity => IsPrintPanelOpen ? 1 : 0;
    public bool IsPrintPanelInteractive => IsPrintPanelOpen;
    public bool IsAnnotationsPanelOpen => HasOpenDocument && IsAnnotationsPanelVisible;
    public double AnnotationsPanelWidth => IsAnnotationsPanelOpen ? InfoPanelExpandedWidth : 0;
    public double AnnotationsPanelOpacity => IsAnnotationsPanelOpen ? 1 : 0;
    public bool IsAnnotationsPanelInteractive => IsAnnotationsPanelOpen;
    public bool HasCurrentPageAnnotations => CurrentPageAnnotations.Count > 0;
    public bool HasAnyAnnotations => _annotations.Count > 0;
    public bool HasPendingAnnotationChanges => _annotations.Count > 0;
    public bool HasAnnotationsPanelNotice => !string.IsNullOrWhiteSpace(AnnotationsPanelNotice);
    public bool CanUndoAnnotations => _annotationUndoSnapshots.Count > 0;
    public bool CanRedoAnnotations => _annotationRedoSnapshots.Count > 0;
    public bool CanDeleteSelectedAnnotation => SelectedAnnotation is not null;
    public bool CanCreateHighlightAnnotation =>
        IsPdfDocument &&
        _currentDocumentTextSelection is not null &&
        _currentDocumentTextSelection.Regions.Count > 0;
    public bool ShowAnnotationTextEditor =>
        SelectedAnnotationTool is AnnotationTool.Text or AnnotationTool.Note or AnnotationTool.Stamp;
    public bool HasSignatureAssets => SignatureAssets.Count > 0;
    public bool CanDeleteSelectedSignatureAsset => HasSignatureAssets;
    public bool CanSaveSignatureCapture =>
        _signatureCapturePoints.Count > 0 &&
        !string.IsNullOrWhiteSpace(SignatureAssetNameInput);
    public bool CanUseSignaturePlacement => HasSignatureAssets && !string.IsNullOrWhiteSpace(SelectedSignatureAssetId);

    private DocumentAnnotation? SelectedAnnotation =>
        _selectedAnnotationId is { } selectedId
            ? _annotations.FirstOrDefault(annotation => annotation.Id == selectedId)
            : null;

    private void InitializeAnnotationWorkspace()
    {
        AnnotationTextDraft = L("annotation.default.note");
        SignatureAssetNameInput = L("annotation.default.signature_name");
        LoadSignatureAssets();
        RefreshAnnotationWorkspaceState();
        RefreshSignaturePadPreview();
    }

    private void ResetAnnotationWorkspace()
    {
        _annotations.Clear();
        _annotationUndoSnapshots.Clear();
        _annotationRedoSnapshots.Clear();
        _annotationPreview = null;
        _selectedAnnotationId = null;
        _annotationAnchorPoint = null;
        _activeAnnotationPoints.Clear();
        _signatureCapturePoints.Clear();
        _isCapturingAnnotation = false;
        IsAnnotationsPanelVisible = false;
        SelectedAnnotationTool = AnnotationTool.Select;
        AnnotationTextDraft = L("annotation.default.note");
        SignatureAssetNameInput = L("annotation.default.signature_name");
        AnnotationsPanelNotice = null;

        CurrentAnnotationOverlayBitmap?.Dispose();
        CurrentAnnotationOverlayBitmap = null;

        SignaturePadPreviewBitmap?.Dispose();
        SignaturePadPreviewBitmap = null;

        RefreshAnnotationWorkspaceState();
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
        OnPropertyChanged(nameof(HasSignatureAssets));
        OnPropertyChanged(nameof(CanUseSignaturePlacement));
    }

    private void RefreshAnnotationWorkspaceState()
    {
        RefreshCurrentPageAnnotations();
        RefreshAnnotationOverlay();
        OnPropertyChanged(nameof(IsSearchAvailableForCurrentDocument));
        OnPropertyChanged(nameof(IsAnnotationsPanelOpen));
        OnPropertyChanged(nameof(AnnotationsPanelWidth));
        OnPropertyChanged(nameof(AnnotationsPanelOpacity));
        OnPropertyChanged(nameof(IsAnnotationsPanelInteractive));
        OnPropertyChanged(nameof(IsAnnotationModeActive));
        OnPropertyChanged(nameof(HasCurrentPageAnnotations));
        OnPropertyChanged(nameof(HasAnyAnnotations));
        OnPropertyChanged(nameof(HasPendingAnnotationChanges));
        OnPropertyChanged(nameof(CanUndoAnnotations));
        OnPropertyChanged(nameof(CanRedoAnnotations));
        OnPropertyChanged(nameof(CanDeleteSelectedAnnotation));
        OnPropertyChanged(nameof(CanCreateHighlightAnnotation));
        OnPropertyChanged(nameof(ShowAnnotationTextEditor));
        OnPropertyChanged(nameof(CanDeleteSelectedSignatureAsset));
        OnPropertyChanged(nameof(CanSaveSignatureCapture));
        OnPropertyChanged(nameof(CanUseSignaturePlacement));
        SaveDocumentCommand.NotifyCanExecuteChanged();
        ToggleAnnotationsPanelCommand.NotifyCanExecuteChanged();
        DeleteSelectedAnnotationCommand.NotifyCanExecuteChanged();
        UndoAnnotationsCommand.NotifyCanExecuteChanged();
        RedoAnnotationsCommand.NotifyCanExecuteChanged();
        AddHighlightAnnotationFromSelectionCommand.NotifyCanExecuteChanged();
        DeleteSelectedSignatureAssetCommand.NotifyCanExecuteChanged();
        SaveDrawnSignatureAssetCommand.NotifyCanExecuteChanged();
        ToggleSearchPanelCommand.NotifyCanExecuteChanged();
        OpenSearchCommand.NotifyCanExecuteChanged();
        SearchTextCommand.NotifyCanExecuteChanged();
        RunSearchOcrCommand.NotifyCanExecuteChanged();
        CancelDocumentTextAnalysisCommand.NotifyCanExecuteChanged();
    }

    private void RefreshCurrentPageAnnotations()
    {
        CurrentPageAnnotations.Clear();

        foreach (var annotation in _annotations.Where(annotation => annotation.PageIndex.Value == CurrentPage - 1))
        {
            CurrentPageAnnotations.Add(new AnnotationListItemViewModel(annotation, _localizationService));
        }

        OnPropertyChanged(nameof(HasCurrentPageAnnotations));
    }

    private void RefreshAnnotationLocalization()
    {
        foreach (var annotation in CurrentPageAnnotations)
        {
            annotation.UpdateLocalization(_localizationService);
        }

        OnPropertyChanged(nameof(CurrentPageAnnotations));
    }

    private void RefreshAnnotationOverlay()
    {
        CurrentAnnotationOverlayBitmap?.Dispose();
        CurrentAnnotationOverlayBitmap = null;

        if (CurrentRenderedBitmap is null || !HasOpenDocument)
        {
            return;
        }

        var annotations = _annotations
            .Where(annotation => annotation.PageIndex.Value == CurrentPage - 1)
            .Select(annotation => annotation.DeepCopy())
            .ToList();

        if (_annotationPreview is not null && _annotationPreview.PageIndex.Value == CurrentPage - 1)
        {
            annotations.Add(_annotationPreview.DeepCopy());
        }

        if (annotations.Count == 0)
        {
            return;
        }

        try
        {
            CurrentAnnotationOverlayBitmap = AnnotationOverlayBitmapFactory.Create(
                annotations,
                CurrentRenderedBitmap.PixelSize.Width,
                CurrentRenderedBitmap.PixelSize.Height,
                GetCurrentRotation(),
                _selectedAnnotationId,
                _signatureAssetLookup);
        }
        catch (Exception exception) when (AnnotationOverlayBitmapFactory.IsRenderingDependencyUnavailable(exception))
        {
            CurrentAnnotationOverlayBitmap = null;
        }
    }

    private void RefreshSignaturePadPreview()
    {
        SignaturePadPreviewBitmap?.Dispose();

        try
        {
            SignaturePadPreviewBitmap = AnnotationOverlayBitmapFactory.CreateSignaturePadPreview(
                _signatureCapturePoints,
                SignaturePadPreviewWidth,
                SignaturePadPreviewHeight);
        }
        catch (Exception exception) when (AnnotationOverlayBitmapFactory.IsRenderingDependencyUnavailable(exception))
        {
            SignaturePadPreviewBitmap = null;
        }

        OnPropertyChanged(nameof(CanSaveSignatureCapture));
        SaveDrawnSignatureAssetCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanAnnotateCurrentDocument))]
    private void ToggleAnnotationsPanel()
    {
        if (IsAnnotationsPanelVisible)
        {
            IsAnnotationsPanelVisible = false;
            StatusText = L("status.annotations.hidden");
        }
        else
        {
            IsInfoPanelVisible = false;
            IsSearchPanelVisible = false;
            IsPreferencesPanelVisible = false;
            IsPrintPanelVisible = false;
            IsAnnotationsPanelVisible = true;
            StatusText = L("status.annotations.shown");
        }
    }

    [RelayCommand]
    private void SelectAnnotationTool(string? toolValue)
    {
        if (!Enum.TryParse<AnnotationTool>(toolValue, ignoreCase: true, out var tool))
        {
            return;
        }

        SelectedAnnotationTool = tool;
        StatusText = tool is AnnotationTool.Select
            ? L("status.annotation.tool.select")
            : L("status.annotation.tool.active", GetAnnotationToolLabel(tool));
    }

    [RelayCommand(CanExecute = nameof(CanCreateHighlightAnnotation))]
    private void AddHighlightAnnotationFromSelection()
    {
        if (_currentDocumentTextSelection is null || _currentDocumentTextSelection.Regions.Count == 0)
        {
            return;
        }

        CaptureAnnotationSnapshot();

        foreach (var region in _currentDocumentTextSelection.Regions)
        {
            _annotations.Add(new DocumentAnnotation(
                Guid.NewGuid(),
                DocumentAnnotationKind.Highlight,
                new PageIndex(CurrentPage - 1),
                BuildAppearanceForKind(DocumentAnnotationKind.Highlight),
                region));
        }

        _selectedAnnotationId = _annotations.LastOrDefault()?.Id;
        ClearDocumentTextSelection();
        AnnotationsPanelNotice = L("notice.annotation.highlight_added");
        StatusText = L("status.annotation.highlight_added");
        RefreshAnnotationWorkspaceState();
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelectedAnnotation))]
    private void DeleteSelectedAnnotation()
    {
        if (SelectedAnnotation is not { } annotation)
        {
            return;
        }

        CaptureAnnotationSnapshot();
        _annotations.RemoveAll(item => item.Id == annotation.Id);
        _selectedAnnotationId = null;
        AnnotationsPanelNotice = L("notice.annotation.removed");
        StatusText = L("status.annotation.deleted");
        RefreshAnnotationWorkspaceState();
    }

    [RelayCommand(CanExecute = nameof(CanUndoAnnotations))]
    private void UndoAnnotations()
    {
        if (_annotationUndoSnapshots.Count == 0)
        {
            return;
        }

        _annotationRedoSnapshots.Push(CloneAnnotations(_annotations));
        RestoreAnnotations(_annotationUndoSnapshots.Pop());
        _selectedAnnotationId = null;
        StatusText = L("status.annotation.undo");
        RefreshAnnotationWorkspaceState();
    }

    [RelayCommand(CanExecute = nameof(CanRedoAnnotations))]
    private void RedoAnnotations()
    {
        if (_annotationRedoSnapshots.Count == 0)
        {
            return;
        }

        _annotationUndoSnapshots.Push(CloneAnnotations(_annotations));
        RestoreAnnotations(_annotationRedoSnapshots.Pop());
        _selectedAnnotationId = null;
        StatusText = L("status.annotation.redo");
        RefreshAnnotationWorkspaceState();
    }

    [RelayCommand(CanExecute = nameof(CanAnnotateCurrentDocument))]
    private async Task ImportSignatureAssetAsync()
    {
        var imagePath = await _filePickerService.PickOpenFileAsync();
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return;
        }

        if (!Velune.Application.Documents.SupportedDocumentFormats.IsImage(Path.GetExtension(imagePath)))
        {
            EnqueueLocalizedError(null, "error.signature.unsupported_image.title", "error.signature.unsupported_image.message");
            return;
        }

        var result = _signatureAssetStore.Import(imagePath);
        if (result.IsFailure || result.Value is null)
        {
            EnqueueLocalizedError(result.Error, "error.signature.import_failed.title", "error.signature.import_failed.message");
            return;
        }

        LoadSignatureAssets();
        SelectedSignatureAssetId = result.Value.Id;
        StatusText = L("status.signature.imported", result.Value.DisplayName);
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

        if (_annotations.Any(annotation =>
                annotation.Kind == DocumentAnnotationKind.Signature &&
                string.Equals(annotation.AssetId, targetAssetId, StringComparison.Ordinal)))
        {
            EnqueueLocalizedWarning("prompt.signature.in_use.title", "prompt.signature.in_use.message");
            StatusText = L("status.signature.in_use");
            return;
        }

        var deletedAssetId = targetAssetId;
        var result = _signatureAssetStore.Delete(deletedAssetId);
        if (result.IsFailure)
        {
            EnqueueLocalizedError(result.Error, "error.signature.delete_failed.title", "error.signature.delete_failed.message");
            return;
        }

        LoadSignatureAssets();
        if (string.Equals(SelectedSignatureAssetId, deletedAssetId, StringComparison.Ordinal))
        {
            SelectedSignatureAssetId = SignatureAssets.FirstOrDefault()?.Id;
        }

        StatusText = L("status.signature.deleted");
        AnnotationsPanelNotice = L("notice.signature.deleted");
    }

    [RelayCommand(CanExecute = nameof(CanSaveSignatureCapture))]
    private void SaveDrawnSignatureAsset()
    {
        var result = _signatureAssetStore.SaveInkSignature(SignatureAssetNameInput.Trim(), _signatureCapturePoints);
        if (result.IsFailure || result.Value is null)
        {
            EnqueueLocalizedError(result.Error, "error.signature.save_failed.title", "error.signature.save_failed.message");
            return;
        }

        LoadSignatureAssets();
        SelectedSignatureAssetId = result.Value.Id;
        _signatureCapturePoints.Clear();
        RefreshSignaturePadPreview();
        StatusText = L("status.signature.saved", result.Value.DisplayName);
    }

    [RelayCommand]
    private void ClearSignatureCapture()
    {
        _signatureCapturePoints.Clear();
        RefreshSignaturePadPreview();
        StatusText = L("status.signature.cleared");
    }

    [RelayCommand]
    private void SelectSignatureAsset(string? assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return;
        }

        SelectedSignatureAssetId = assetId;
        StatusText = L("status.signature.selected");
    }

    [RelayCommand]
    private void SelectCurrentPageAnnotation(AnnotationListItemViewModel? annotation)
    {
        _selectedAnnotationId = annotation?.Id;
        RefreshAnnotationWorkspaceState();
    }

    public bool BeginAnnotationInteraction(double x, double y, double layerWidth, double layerHeight)
    {
        if (!IsAnnotationModeActive)
        {
            return false;
        }

        var normalizedPoint = DocumentAnnotationCoordinateMapper.MapVisualPointToNormalized(
            x,
            y,
            layerWidth,
            layerHeight,
            GetCurrentRotation());

        if (SelectedAnnotationTool is AnnotationTool.Select)
        {
            SelectAnnotationAt(normalizedPoint);
            return false;
        }

        if (SelectedAnnotationTool is AnnotationTool.Highlight)
        {
            if (CanCreateHighlightAnnotation)
            {
                AddHighlightAnnotationFromSelection();
            }

            return false;
        }

        if (SelectedAnnotationTool is AnnotationTool.Signature && !CanUseSignaturePlacement)
        {
            EnqueueLocalizedInfo("prompt.signature.choose_first.title", "prompt.signature.choose_first.message");
            return false;
        }

        _annotationAnchorPoint = normalizedPoint;
        _activeAnnotationPoints.Clear();
        _activeAnnotationPoints.Add(normalizedPoint);
        _annotationPreview = BuildPreviewAnnotation(normalizedPoint, normalizedPoint);
        _isCapturingAnnotation = true;
        RefreshAnnotationWorkspaceState();
        return true;
    }

    public void UpdateAnnotationInteraction(double x, double y, double layerWidth, double layerHeight)
    {
        if (!_isCapturingAnnotation || _annotationAnchorPoint is null)
        {
            return;
        }

        var normalizedPoint = DocumentAnnotationCoordinateMapper.MapVisualPointToNormalized(
            x,
            y,
            layerWidth,
            layerHeight,
            GetCurrentRotation());

        if (SelectedAnnotationTool is AnnotationTool.Ink)
        {
            _activeAnnotationPoints.Add(normalizedPoint);
        }

        _annotationPreview = BuildPreviewAnnotation(_annotationAnchorPoint, normalizedPoint);
        RefreshAnnotationOverlay();
    }

    public void CompleteAnnotationInteraction(double x, double y, double layerWidth, double layerHeight)
    {
        if (!_isCapturingAnnotation || _annotationAnchorPoint is null)
        {
            CancelAnnotationInteraction();
            return;
        }

        var normalizedPoint = DocumentAnnotationCoordinateMapper.MapVisualPointToNormalized(
            x,
            y,
            layerWidth,
            layerHeight,
            GetCurrentRotation());
        var finalAnnotation = BuildCommittedAnnotation(_annotationAnchorPoint, normalizedPoint);

        if (finalAnnotation is not null)
        {
            CaptureAnnotationSnapshot();
            _annotations.Add(finalAnnotation);
            _selectedAnnotationId = finalAnnotation.Id;
            StatusText = L("status.annotation.added", GetAnnotationKindLabel(finalAnnotation.Kind));
        }

        _annotationPreview = null;
        _annotationAnchorPoint = null;
        _activeAnnotationPoints.Clear();
        _isCapturingAnnotation = false;
        RefreshAnnotationWorkspaceState();
    }

    public void CancelAnnotationInteraction()
    {
        _annotationPreview = null;
        _annotationAnchorPoint = null;
        _activeAnnotationPoints.Clear();
        _isCapturingAnnotation = false;
        RefreshAnnotationOverlay();
    }

    public void BeginSignatureCapture(double x, double y, double layerWidth, double layerHeight)
    {
        _signatureCapturePoints.Clear();
        UpdateSignatureCapture(x, y, layerWidth, layerHeight);
    }

    public void UpdateSignatureCapture(double x, double y, double layerWidth, double layerHeight)
    {
        var normalizedPoint = DocumentAnnotationCoordinateMapper.MapVisualPointToNormalized(
            x,
            y,
            layerWidth,
            layerHeight,
            Rotation.Deg0);
        _signatureCapturePoints.Add(normalizedPoint);
        RefreshSignaturePadPreview();
    }

    public void CompleteSignatureCapture()
    {
        RefreshSignaturePadPreview();
    }

    private DocumentAnnotation? BuildPreviewAnnotation(NormalizedPoint start, NormalizedPoint end)
    {
        var pageIndex = new PageIndex(CurrentPage - 1);

        return SelectedAnnotationTool switch
        {
            AnnotationTool.Ink => new DocumentAnnotation(
                Guid.NewGuid(),
                DocumentAnnotationKind.Ink,
                pageIndex,
                BuildAppearanceForKind(DocumentAnnotationKind.Ink),
                points: [.. _activeAnnotationPoints.Select(point => new NormalizedPoint(point.X, point.Y))]),
            AnnotationTool.Rectangle => CreateBoundedAnnotation(DocumentAnnotationKind.Rectangle, pageIndex, start, end),
            AnnotationTool.Text => CreateBoundedAnnotation(DocumentAnnotationKind.Text, pageIndex, start, end, AnnotationTextDraft),
            AnnotationTool.Note => CreateBoundedAnnotation(DocumentAnnotationKind.Note, pageIndex, start, end, AnnotationTextDraft),
            AnnotationTool.Stamp => CreateBoundedAnnotation(DocumentAnnotationKind.Stamp, pageIndex, start, end, AnnotationTextDraft),
            AnnotationTool.Signature => CreateBoundedAnnotation(DocumentAnnotationKind.Signature, pageIndex, start, end, assetId: SelectedSignatureAssetId),
            _ => null
        };
    }

    private DocumentAnnotation? BuildCommittedAnnotation(NormalizedPoint start, NormalizedPoint end)
    {
        if (BuildPreviewAnnotation(start, end) is not { } annotation)
        {
            return null;
        }

        return new DocumentAnnotation(
            Guid.NewGuid(),
            annotation.Kind,
            annotation.PageIndex,
            annotation.Appearance,
            annotation.Bounds,
            annotation.Points,
            annotation.Text,
            annotation.AssetId);
    }

    private DocumentAnnotation CreateBoundedAnnotation(
        DocumentAnnotationKind kind,
        PageIndex pageIndex,
        NormalizedPoint start,
        NormalizedPoint end,
        string? text = null,
        string? assetId = null)
    {
        var bounds = BuildAnnotationBounds(kind, start, end);
        return new DocumentAnnotation(
            Guid.NewGuid(),
            kind,
            pageIndex,
            BuildAppearanceForKind(kind),
            bounds,
            text: ResolveAnnotationText(kind, text),
            assetId: assetId);
    }

    private NormalizedTextRegion BuildAnnotationBounds(
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

    private string? ResolveAnnotationText(DocumentAnnotationKind kind, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text.Trim();
        }

        return kind switch
        {
            DocumentAnnotationKind.Stamp => L("annotation.default.stamp"),
            DocumentAnnotationKind.Text => L("annotation.default.text"),
            DocumentAnnotationKind.Note => L("annotation.default.note_label"),
            _ => null
        };
    }

    private static AnnotationAppearance BuildAppearanceForKind(DocumentAnnotationKind kind)
    {
        return kind switch
        {
            DocumentAnnotationKind.Highlight => new AnnotationAppearance("#E2B54B", "#F9DE86", 1.5, 0.58),
            DocumentAnnotationKind.Ink => new AnnotationAppearance("#F39AC7", null, 4.0, 0.98),
            DocumentAnnotationKind.Rectangle => new AnnotationAppearance("#93B9FF", "#DDE9FF", 2.5, 0.95),
            DocumentAnnotationKind.Note => new AnnotationAppearance("#D9A357", "#FFF2D7", 2.0, 0.98),
            DocumentAnnotationKind.Stamp => new AnnotationAppearance("#E37AA7", "#FDE5F0", 2.5, 0.98),
            DocumentAnnotationKind.Signature => new AnnotationAppearance("#2F3150", "#FFFFFF", 1.5, 1.0),
            _ => new AnnotationAppearance("#93B9FF", "#EEF1FF", 2.0, 0.96)
        };
    }

    private void SelectAnnotationAt(NormalizedPoint point)
    {
        var candidate = CurrentPageAnnotations
            .Reverse()
            .FirstOrDefault(annotation => AnnotationContains(annotation.Annotation, point));
        _selectedAnnotationId = candidate?.Id;
        StatusText = candidate is null
            ? L("status.annotation.none_selected")
            : L("status.annotation.selected", candidate.KindLabel);
        RefreshAnnotationWorkspaceState();
    }

    private static bool AnnotationContains(DocumentAnnotation annotation, NormalizedPoint point)
    {
        if (annotation.Bounds is not null)
        {
            return point.X >= annotation.Bounds.X &&
                   point.X <= annotation.Bounds.X + annotation.Bounds.Width &&
                   point.Y >= annotation.Bounds.Y &&
                   point.Y <= annotation.Bounds.Y + annotation.Bounds.Height;
        }

        if (annotation.Points.Count == 0)
        {
            return false;
        }

        var minX = annotation.Points.Min(item => item.X) - 0.015;
        var maxX = annotation.Points.Max(item => item.X) + 0.015;
        var minY = annotation.Points.Min(item => item.Y) - 0.015;
        var maxY = annotation.Points.Max(item => item.Y) + 0.015;

        return point.X >= minX &&
               point.X <= maxX &&
               point.Y >= minY &&
               point.Y <= maxY;
    }

    private void CaptureAnnotationSnapshot()
    {
        _annotationUndoSnapshots.Push(CloneAnnotations(_annotations));
        _annotationRedoSnapshots.Clear();
    }

    private void RestoreAnnotations(IReadOnlyList<DocumentAnnotation> snapshot)
    {
        _annotations.Clear();

        foreach (var annotation in snapshot)
        {
            _annotations.Add(annotation.DeepCopy());
        }
    }

    private static IReadOnlyList<DocumentAnnotation> CloneAnnotations(IReadOnlyList<DocumentAnnotation> annotations)
    {
        return [.. annotations.Select(annotation => annotation.DeepCopy())];
    }

    private async Task SaveAnnotatedPdfInPlaceAsync(string currentDocumentPath)
    {
        if (_documentSessionStore.Current is not { } session)
        {
            return;
        }

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
            "velune-annotated-pdf-save",
            Guid.NewGuid().ToString("N"));
        var pendingRotations = GetPendingPdfRotationGroups();
        var orderedPages = Thumbnails
            .Select(thumbnail => thumbnail.SourcePageNumber)
            .ToArray();
        var hasPendingReorder = HasPendingPageReorder;
        var structuredInputPath = currentDocumentPath;
        var temporaryOutputPath = Path.Combine(temporaryDirectory, targetFileName);
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            SetPdfStructureOperationState(true);

            if (pendingRotations.Count > 0 || hasPendingReorder)
            {
                var structuredPath = Path.Combine(temporaryDirectory, $"structured-{targetFileName}");
                var structureResult = await SavePdfDocumentChangesAsync(
                    currentDocumentPath,
                    structuredPath,
                    pendingRotations,
                    orderedPages,
                    hasPendingReorder);

                if (structureResult.IsFailure)
                {
                    EnqueueLocalizedError(structureResult.Error, "error.save.pdf_structure.title", "error.save.pdf_structure.message");
                    StatusText = L("error.save.document.title");
                    return;
                }

                structuredInputPath = structureResult.Value ?? structuredPath;
            }

            var result = await _pdfMarkupService.ApplyAnnotationsAsync(
                new ApplyPdfAnnotationsRequest(
                    session,
                    structuredInputPath,
                    temporaryOutputPath,
                    CloneAnnotations(_annotations)));

            if (result.IsFailure)
            {
                EnqueueLocalizedError(result.Error, "error.save.document.title", "error.save.document.message");
                StatusText = L("error.save.document.title");
                return;
            }

            await CloseCurrentDocumentStateAsync(clearNotifications: false);

            File.Copy(
                result.Value ?? temporaryOutputPath,
                targetDocumentPath,
                overwrite: PathsEqual(currentDocumentPath, targetDocumentPath));

            await OpenDocumentFromPathAsync(targetDocumentPath);
            StatusText = L("status.document.saved");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            EnqueueLocalizedError(AppError.Infrastructure("document.save.copy_failed", exception.Message), "error.save.document.title", "error.save.document.message");
            StatusText = L("error.save.document.title");
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
                // Best-effort cleanup for temporary annotated PDF files.
            }

            SetPdfStructureOperationState(false);
        }
    }

    private async Task SaveImageDocumentInPlaceAsync(string currentDocumentPath)
    {
        if (_documentSessionStore.Current is not { } session)
        {
            return;
        }

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
            "velune-image-save",
            Guid.NewGuid().ToString("N"));
        var temporaryOutputPath = Path.Combine(temporaryDirectory, targetFileName);
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            SetPdfStructureOperationState(true);

            Result<string> result;
            if (HasPendingAnnotationChanges)
            {
                result = await _imageMarkupService.FlattenAnnotationsAsync(
                    new ApplyImageAnnotationsRequest(
                        session,
                        temporaryOutputPath,
                        CloneAnnotations(_annotations)));
            }
            else
            {
                File.Copy(currentDocumentPath, temporaryOutputPath, overwrite: true);
                result = Velune.Application.Results.ResultFactory.Success(temporaryOutputPath);
            }

            if (result.IsFailure)
            {
                EnqueueLocalizedError(result.Error, "error.save.document.title", "error.save.document.message");
                StatusText = L("error.save.document.title");
                return;
            }

            await CloseCurrentDocumentStateAsync(clearNotifications: false);

            File.Copy(
                result.Value ?? temporaryOutputPath,
                targetDocumentPath,
                overwrite: PathsEqual(currentDocumentPath, targetDocumentPath));

            await OpenDocumentFromPathAsync(targetDocumentPath);
            StatusText = L("status.document.saved");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            EnqueueLocalizedError(AppError.Infrastructure("image.save.copy_failed", exception.Message), "error.save.document.title", "error.save.document.message");
            StatusText = L("error.save.document.title");
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
                // Best-effort cleanup for temporary image save files.
            }

            SetPdfStructureOperationState(false);
        }
    }

    private Dictionary<int, Rotation> BuildCurrentPageRotationMap()
    {
        var rotations = new Dictionary<int, Rotation>();

        for (var pageNumber = 1; pageNumber <= TotalPages; pageNumber++)
        {
            var pageIndex = new PageIndex(pageNumber - 1);
            rotations[pageNumber] = _pageViewportStore.GetRotation(pageIndex);
        }

        return rotations;
    }

    partial void OnSelectedAnnotationToolChanged(AnnotationTool value)
    {
        OnPropertyChanged(nameof(ShowAnnotationTextEditor));
    }

    partial void OnSelectedSignatureAssetIdChanged(string? value)
    {
        OnPropertyChanged(nameof(CanDeleteSelectedSignatureAsset));
        OnPropertyChanged(nameof(CanUseSignaturePlacement));
        DeleteSelectedSignatureAssetCommand.NotifyCanExecuteChanged();
    }

    partial void OnSignatureAssetNameInputChanged(string value)
    {
        OnPropertyChanged(nameof(CanSaveSignatureCapture));
        SaveDrawnSignatureAssetCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsAnnotationsPanelVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsAnnotationsPanelOpen));
        OnPropertyChanged(nameof(AnnotationsPanelWidth));
        OnPropertyChanged(nameof(AnnotationsPanelOpacity));
        OnPropertyChanged(nameof(IsAnnotationsPanelInteractive));
        OnPropertyChanged(nameof(IsAnnotationModeActive));
    }

    partial void OnAnnotationsPanelNoticeChanged(string? value)
    {
        OnPropertyChanged(nameof(HasAnnotationsPanelNotice));
    }

    private string GetAnnotationToolLabel(AnnotationTool tool)
    {
        return tool switch
        {
            AnnotationTool.Select => L("annotation.tool.select"),
            AnnotationTool.Highlight => L("annotation.tool.highlight"),
            AnnotationTool.Ink => L("annotation.tool.ink"),
            AnnotationTool.Rectangle => L("annotation.tool.rectangle"),
            AnnotationTool.Text => L("annotation.tool.text"),
            AnnotationTool.Note => L("annotation.tool.note"),
            AnnotationTool.Stamp => L("annotation.tool.stamp"),
            AnnotationTool.Signature => L("annotation.tool.signature"),
            _ => tool.ToString()
        };
    }

    private string GetAnnotationKindLabel(DocumentAnnotationKind kind)
    {
        return kind switch
        {
            DocumentAnnotationKind.Highlight => L("annotation.kind.highlight"),
            DocumentAnnotationKind.Ink => L("annotation.kind.ink"),
            DocumentAnnotationKind.Rectangle => L("annotation.kind.rectangle"),
            DocumentAnnotationKind.Text => L("annotation.kind.text"),
            DocumentAnnotationKind.Note => L("annotation.kind.note"),
            DocumentAnnotationKind.Stamp => L("annotation.kind.stamp"),
            DocumentAnnotationKind.Signature => L("annotation.kind.signature"),
            _ => kind.ToString()
        };
    }
}
