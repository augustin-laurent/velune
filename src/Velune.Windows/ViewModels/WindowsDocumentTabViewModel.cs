using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Velune.Application.DTOs;
using Velune.Domain.Annotations;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Windows.Services;

namespace Velune.Windows.ViewModels;

/// <summary>
/// View model representing a single open document tab with its page state, annotations, and search context.
/// </summary>
public sealed partial class WindowsDocumentTabViewModel : ObservableObject
{
    private readonly IWindowsTextCatalog _textCatalog;
    private readonly Dictionary<int, Rotation> _pendingPageRotations = [];
    private readonly HashSet<Guid> _hiddenAnnotations = [];
    private readonly HashSet<Guid> _lockedAnnotations = [];
    private IReadOnlyDictionary<string, SignatureAsset> _signatureAssets = new Dictionary<string, SignatureAsset>(StringComparer.Ordinal);
    private bool _isLightTheme;
    private bool _isPointerOver;
    private int _selectedSearchResultIndex = -1;

    /// <summary>
    /// Initializes a document tab from session metadata.
    /// </summary>
    /// <param name="sessionId">The document session identifier.</param>
    /// <param name="metadata">The document metadata from the opened file.</param>
    /// <param name="textCatalog">Provides localized labels.</param>
    public WindowsDocumentTabViewModel(DocumentId sessionId, DocumentMetadata metadata, IWindowsTextCatalog textCatalog)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(textCatalog);

        _textCatalog = textCatalog;
        SessionId = sessionId;
        FilePath = metadata.FilePath;
        Title = metadata.FileName;
        TotalPages = metadata.PageCount ?? 1;
        DocumentType = metadata.DocumentType;
        FileSize = FormatFileSize(metadata.FileSizeInBytes);
        Dimensions = FormatDimensions(metadata.PixelWidth, metadata.PixelHeight);
        Format = metadata.FormatLabel ?? metadata.DocumentType.ToString();
        DocumentTitle = metadata.DocumentTitle;
        Author = metadata.Author;
        Creator = metadata.Creator;
        Producer = metadata.Producer;
        CreatedDate = FormatDate(metadata.CreatedAt);
        ModifiedDate = FormatDate(metadata.ModifiedAt);
        DetailsWarning = metadata.DetailsWarning;
        CurrentPagePixelWidth = metadata.PixelWidth ?? 900;
        CurrentPagePixelHeight = metadata.PixelHeight ?? 1200;

        for (var page = 1; page <= TotalPages; page++)
        {
            Thumbnails.Add(new WindowsPageThumbnailViewModel(
                page,
                textCatalog.Format("windows.thumbnail.page", page),
                textCatalog.GetString("windows.thumbnail.loading")));
        }
    }

    public DocumentId SessionId
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string FilePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsActive
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool IsDirty
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool HasPendingPageReorder
    {
        get; set;
    }

    [ObservableProperty]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    public partial int TotalPages
    {
        get; set;
    }

    [ObservableProperty]
    public partial double ZoomFactor { get; set; } = 1.0;

    [ObservableProperty]
    public partial string ZoomText { get; set; } = "100%";

    [ObservableProperty]
    public partial Rotation Rotation { get; set; } = Rotation.Deg0;

    [ObservableProperty]
    public partial ImageSource? CurrentPageImage
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool IsRendering
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool IsGeneratingThumbnails
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool IsPagesPanelOpen { get; set; } = true;

    [ObservableProperty]
    public partial bool IsAnnotationsPanelOpen
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool IsSearchPanelOpen
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool IsInfoPanelOpen
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool IsSettingsPanelOpen
    {
        get; set;
    }

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? SearchPanelNotice
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool IsAnalyzingDocumentText
    {
        get; set;
    }

    [ObservableProperty]
    public partial bool RequiresSearchOcr
    {
        get; set;
    }

    [ObservableProperty]
    public partial AnnotationTool SelectedAnnotationTool { get; set; } = AnnotationTool.Select;

    [ObservableProperty]
    public partial WindowsInlineTextEditorViewModel? InlineTextEditor
    {
        get; set;
    }

    [ObservableProperty]
    public partial Guid? EditingCommentId
    {
        get; set;
    }

    [ObservableProperty]
    public partial double CurrentPagePixelWidth { get; set; } = 900;

    [ObservableProperty]
    public partial double CurrentPagePixelHeight { get; set; } = 1200;

    public DocumentType DocumentType
    {
        get;
    }

    public ObservableCollection<WindowsPageThumbnailViewModel> Thumbnails
    {
        get;
    } = [];

    public ObservableCollection<DocumentAnnotation> Annotations
    {
        get;
    } = [];

    public ObservableCollection<WindowsAnnotationOverlayViewModel> CurrentPageAnnotationOverlays
    {
        get;
    } = [];

    public ObservableCollection<WindowsCommentOverlayViewModel> CurrentPageCommentOverlays
    {
        get;
    } = [];

    public ObservableCollection<WindowsSearchResultItemViewModel> SearchResults
    {
        get;
    } = [];

    public ObservableCollection<TextSelectionHighlightItem> SearchHighlights
    {
        get;
    } = [];

    public ObservableCollection<TextSelectionHighlightItem> TextSelectionHighlights
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

    public bool HasMissingThumbnails => Thumbnails.Any(thumbnail => thumbnail.Image is null);

    public bool HasPendingPageRotations => _pendingPageRotations.Count > 0;

    public bool HasPendingPageEdits => HasPendingPageReorder || HasPendingPageRotations;

    public string PageText => $"{CurrentPage} / {TotalPages}";

    public string CurrentPageAnnotationCountText => CurrentPageAnnotationOverlays.Count.ToString(CultureInfo.CurrentCulture);

    public bool HasCurrentPageAnnotations => CurrentPageAnnotationOverlays.Count > 0;

    public string CurrentPageCommentCountText => CurrentPageCommentOverlays.Count.ToString(CultureInfo.CurrentCulture);

    public bool HasCurrentPageComments => CurrentPageCommentOverlays.Count > 0;

    public double CommentLaneWidth => HasCurrentPageComments ? 280 : 0;

    public bool HasInlineTextEditor => InlineTextEditor is not null;

    public bool HasSearchResults => SearchResults.Count > 0;

    public bool HasSearchHighlights => SearchHighlights.Count > 0;

    public bool HasSearchPanelNotice => !string.IsNullOrWhiteSpace(SearchPanelNotice);

    public bool HasTextSelectionHighlights => TextSelectionHighlights.Count > 0;

    public string SearchResultSummary => IsAnalyzingDocumentText
        ? _textCatalog.GetString("search.summary.loading")
        : RequiresSearchOcr
            ? _textCatalog.GetString("search.summary.ocr_required")
            : SearchResults.Count switch
            {
                0 => _textCatalog.GetString("search.summary.none"),
                1 => _textCatalog.GetString("search.summary.one"),
                _ => _textCatalog.Format("search.summary.many", SearchResults.Count)
            };

    public string SearchSelectionIndicator => _selectedSearchResultIndex < 0 || SearchResults.Count == 0
        ? _textCatalog.GetString("search.selection.none")
        : _textCatalog.Format("search.selection.current", _selectedSearchResultIndex + 1, SearchResults.Count);

    public string FileSize
    {
        get;
    }

    public string Dimensions
    {
        get;
    }

    public string PageCountText => TotalPages.ToString(CultureInfo.CurrentCulture);

    public string Format
    {
        get;
    }

    public string? DocumentTitle
    {
        get;
    }

    public string? Author
    {
        get;
    }

    public string? Creator
    {
        get;
    }

    public string? Producer
    {
        get;
    }

    public string? CreatedDate
    {
        get;
    }

    public string? ModifiedDate
    {
        get;
    }

    public string? DetailsWarning
    {
        get;
    }

    public DocumentTextIndex? DocumentTextIndex
    {
        get;
        set;
    }

    public DocumentTextSelectionPoint? DocumentTextSelectionAnchorPoint
    {
        get;
        set;
    }

    public DocumentTextSelectionResult? CurrentDocumentTextSelection
    {
        get;
        set;
    }

    public SolidColorBrush TabBackground
    {
        get;
        private set;
    } = CreateBrush("#00000000");

    public SolidColorBrush TabBorderBrush
    {
        get;
        private set;
    } = CreateBrush("#00000000");

    /// <summary>
    /// Raises property-changed for thumbnail loading status.
    /// </summary>
    public void NotifyThumbnailStatusChanged()
    {
        OnPropertyChanged(nameof(HasMissingThumbnails));
    }

    /// <summary>
    /// Updates the rendered page pixel dimensions and refreshes overlays.
    /// </summary>
    /// <param name="width">Page width in pixels.</param>
    /// <param name="height">Page height in pixels.</param>
    public void SetCurrentPagePixels(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        CurrentPagePixelWidth = width;
        CurrentPagePixelHeight = height;
        RefreshAnnotationOverlays();
        RefreshSearchHighlights();
        RefreshDocumentTextSelectionHighlights();
    }

    /// <summary>
    /// Gets the pending rotation for the specified page.
    /// </summary>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <returns>The rotation, or Deg0 if none pending.</returns>
    public Rotation GetPageRotation(int pageNumber)
    {
        return _pendingPageRotations.TryGetValue(pageNumber, out var rotation)
            ? rotation
            : Rotation.Deg0;
    }

    /// <summary>
    /// Gets all pending page rotations as a list.
    /// </summary>
    /// <returns>Page numbers with their pending rotations.</returns>
    public IReadOnlyList<(int PageNumber, Rotation Rotation)> GetPendingPageRotations()
    {
        return _pendingPageRotations
            .OrderBy(item => item.Key)
            .Select(item => (item.Key, item.Value))
            .ToArray();
    }

    /// <summary>
    /// Sets a pending rotation for the specified page.
    /// </summary>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <param name="rotation">The rotation to apply.</param>
    public void SetPageRotation(int pageNumber, Rotation rotation)
    {
        if (pageNumber < 1 || pageNumber > TotalPages)
        {
            return;
        }

        if (rotation is Rotation.Deg0)
        {
            _pendingPageRotations.Remove(pageNumber);
        }
        else
        {
            _pendingPageRotations[pageNumber] = rotation;
        }

        if (Thumbnails.FirstOrDefault(thumbnail => thumbnail.PageNumber == pageNumber) is { } thumbnail)
        {
            thumbnail.Rotation = rotation;
        }

        if (CurrentPage == pageNumber)
        {
            Rotation = rotation;
        }

        NotifyPendingPageEditStateChanged();
    }

    /// <summary>
    /// Synchronizes the active rotation property with the current page's pending rotation.
    /// </summary>
    public void SyncRotationToCurrentPage()
    {
        Rotation = GetPageRotation(CurrentPage);
    }

    /// <summary>
    /// Clears all pending page rotations and resets thumbnails.
    /// </summary>
    public void ClearPendingPageRotations()
    {
        if (_pendingPageRotations.Count == 0)
        {
            return;
        }

        _pendingPageRotations.Clear();
        foreach (var thumbnail in Thumbnails)
        {
            thumbnail.Rotation = Rotation.Deg0;
        }

        Rotation = Rotation.Deg0;
        NotifyPendingPageEditStateChanged();
    }

    /// <summary>
    /// Raises property-changed notifications for page edit state properties.
    /// </summary>
    public void NotifyPendingPageEditStateChanged()
    {
        OnPropertyChanged(nameof(HasPendingPageRotations));
        OnPropertyChanged(nameof(HasPendingPageEdits));
    }

    /// <summary>
    /// Adds an annotation to the document and refreshes overlays.
    /// </summary>
    /// <param name="annotation">The annotation to add.</param>
    public void AddAnnotation(DocumentAnnotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        Annotations.Add(annotation);
        RefreshAnnotationOverlays();
        IsDirty = true;
    }

    /// <summary>
    /// Deletes the annotation with the specified identifier.
    /// </summary>
    /// <param name="annotationId">The annotation identifier to remove.</param>
    public void DeleteAnnotationById(Guid annotationId)
    {
        var annotation = Annotations.FirstOrDefault(item => item.Id == annotationId);
        if (annotation is null)
        {
            return;
        }

        Annotations.Remove(annotation);
        if (InlineTextEditor?.AnnotationId == annotationId)
        {
            InlineTextEditor = null;
        }

        RefreshAnnotationOverlays();
        IsDirty = true;
    }

    /// <summary>
    /// Toggles the visibility of an annotation overlay.
    /// </summary>
    /// <param name="annotationId">The annotation identifier.</param>
    public void ToggleAnnotationVisibility(Guid annotationId)
    {
        if (!_hiddenAnnotations.Remove(annotationId))
        {
            _hiddenAnnotations.Add(annotationId);
        }

        RefreshAnnotationOverlays();
    }

    /// <summary>
    /// Toggles the lock state of an annotation.
    /// </summary>
    /// <param name="annotationId">The annotation identifier.</param>
    public void ToggleAnnotationLock(Guid annotationId)
    {
        if (!_lockedAnnotations.Remove(annotationId))
        {
            _lockedAnnotations.Add(annotationId);
        }
    }

    /// <summary>
    /// Gets whether the specified annotation is locked.
    /// </summary>
    /// <param name="annotationId">The annotation identifier.</param>
    /// <returns>True if locked.</returns>
    public bool IsAnnotationLocked(Guid annotationId)
    {
        return _lockedAnnotations.Contains(annotationId);
    }

    /// <summary>
    /// Opens the inline text editor for the specified annotation.
    /// </summary>
    /// <param name="annotation">The text or stamp annotation to edit.</param>
    public void BeginInlineTextEdit(DocumentAnnotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        if (annotation.Kind is not (DocumentAnnotationKind.Text or DocumentAnnotationKind.Stamp))
        {
            return;
        }

        InlineTextEditor = new WindowsInlineTextEditorViewModel(
            annotation,
            Math.Max(1, CurrentPagePixelWidth),
            Math.Max(1, CurrentPagePixelHeight),
            Rotation);
        RefreshAnnotationOverlays();
    }

    /// <summary>
    /// Updates the text content of the specified annotation.
    /// </summary>
    /// <param name="annotationId">The annotation identifier.</param>
    /// <param name="text">The new text content.</param>
    public void UpdateAnnotationText(Guid annotationId, string? text)
    {
        var annotationIndex = FindAnnotationIndex(annotationId);
        if (annotationIndex < 0)
        {
            return;
        }

        var annotation = Annotations[annotationIndex];
        Annotations[annotationIndex] = new DocumentAnnotation(
            annotation.Id,
            annotation.Kind,
            annotation.PageIndex,
            annotation.Appearance,
            annotation.Bounds,
            annotation.Points,
            text,
            annotation.AssetId,
            annotation.CreatedAt);
        IsDirty = true;
    }

    /// <summary>
    /// Closes the inline text editor.
    /// </summary>
    public void EndInlineTextEdit()
    {
        InlineTextEditor = null;
        RefreshAnnotationOverlays();
    }

    /// <summary>
    /// Rebuilds the annotation and comment overlay collections for the current page.
    /// </summary>
    public void RefreshAnnotationOverlays()
    {
        CurrentPageAnnotationOverlays.Clear();
        CurrentPageCommentOverlays.Clear();

        var pageIndex = new PageIndex(Math.Max(0, CurrentPage - 1));
        foreach (var annotation in Annotations.Where(item => item.PageIndex == pageIndex))
        {
            if (InlineTextEditor?.AnnotationId == annotation.Id)
            {
                continue;
            }

            var isHidden = _hiddenAnnotations.Contains(annotation.Id);
            var isLocked = _lockedAnnotations.Contains(annotation.Id);

            if (annotation.Kind is DocumentAnnotationKind.Note)
            {
                if (!isHidden)
                {
                    CurrentPageCommentOverlays.Add(new WindowsCommentOverlayViewModel(
                        annotation,
                        Math.Max(1, CurrentPagePixelHeight),
                        Rotation,
                        ResolveAnnotationLabel(annotation),
                        _textCatalog.Format("windows.thumbnail.page", annotation.PageIndex.Value + 1)));
                }

                continue;
            }

            var overlay = new WindowsAnnotationOverlayViewModel(
                annotation,
                Math.Max(1, CurrentPagePixelWidth),
                Math.Max(1, CurrentPagePixelHeight),
                Rotation,
                ResolveAnnotationLabel(annotation),
                _textCatalog.Format("windows.thumbnail.page", annotation.PageIndex.Value + 1),
                ResolveAnnotationGlyph(annotation.Kind),
                _signatureAssets)
            {
                IsHidden = isHidden,
                IsLocked = isLocked,
                HideMenuText = isHidden
                    ? _textCatalog.GetString("panel.annotations.menu.show")
                    : _textCatalog.GetString("panel.annotations.menu.hide"),
                LockMenuText = isLocked
                    ? _textCatalog.GetString("panel.annotations.menu.unlock")
                    : _textCatalog.GetString("panel.annotations.menu.lock")
            };

            CurrentPageAnnotationOverlays.Add(overlay);
        }

        OnPropertyChanged(nameof(CurrentPageAnnotationCountText));
        OnPropertyChanged(nameof(HasCurrentPageAnnotations));
        OnPropertyChanged(nameof(CurrentPageCommentCountText));
        OnPropertyChanged(nameof(HasCurrentPageComments));
        OnPropertyChanged(nameof(CommentLaneWidth));
    }

    /// <summary>
    /// Updates the available signature assets and refreshes overlays.
    /// </summary>
    /// <param name="signatureAssets">The signature asset lookup by ID.</param>
    public void UpdateSignatureAssets(IReadOnlyDictionary<string, SignatureAsset> signatureAssets)
    {
        ArgumentNullException.ThrowIfNull(signatureAssets);

        _signatureAssets = new Dictionary<string, SignatureAsset>(signatureAssets, StringComparer.Ordinal);
        RefreshAnnotationOverlays();
    }

    /// <summary>
    /// Applies text search results to the search panel.
    /// </summary>
    /// <param name="hits">The search hits to display.</param>
    public void ApplySearchResults(IReadOnlyList<SearchHit> hits)
    {
        ArgumentNullException.ThrowIfNull(hits);

        ClearSearchResults();
        foreach (var hit in hits)
        {
            SearchResults.Add(new WindowsSearchResultItemViewModel(hit, hit.PageIndex.Value + 1, _textCatalog));
        }

        _selectedSearchResultIndex = SearchResults.Count > 0 ? 0 : -1;
        NotifySearchStateChanged();
    }

    /// <summary>
    /// Gets the previous search result, wrapping to the end.
    /// </summary>
    /// <returns>The previous search result item, or null if empty.</returns>
    public WindowsSearchResultItemViewModel? PreviousSearchResult()
    {
        if (SearchResults.Count == 0)
        {
            return null;
        }

        var index = _selectedSearchResultIndex <= 0
            ? SearchResults.Count - 1
            : _selectedSearchResultIndex - 1;

        return SearchResults[index];
    }

    /// <summary>
    /// Gets the next search result, wrapping to the beginning.
    /// </summary>
    /// <returns>The next search result item, or null if empty.</returns>
    public WindowsSearchResultItemViewModel? NextSearchResult()
    {
        if (SearchResults.Count == 0)
        {
            return null;
        }

        var index = _selectedSearchResultIndex < 0 || _selectedSearchResultIndex >= SearchResults.Count - 1
            ? 0
            : _selectedSearchResultIndex + 1;

        return SearchResults[index];
    }

    /// <summary>
    /// Selects a search result and updates highlights to the matching page.
    /// </summary>
    /// <param name="result">The search result to select.</param>
    public void SelectSearchResult(WindowsSearchResultItemViewModel result)
    {
        ArgumentNullException.ThrowIfNull(result);

        for (var i = 0; i < SearchResults.Count; i++)
        {
            SearchResults[i].IsSelected = ReferenceEquals(SearchResults[i], result);
            if (ReferenceEquals(SearchResults[i], result))
            {
                _selectedSearchResultIndex = i;
            }
        }

        RefreshSearchHighlights();
        OnPropertyChanged(nameof(SearchSelectionIndicator));
    }

    /// <summary>
    /// Clears all search results and highlights.
    /// </summary>
    public void ClearSearchResults()
    {
        SearchResults.Clear();
        _selectedSearchResultIndex = -1;
        ClearSearchHighlights();
        NotifySearchStateChanged();
    }

    /// <summary>
    /// Rebuilds the search highlight overlays for the current page.
    /// </summary>
    public void RefreshSearchHighlights()
    {
        SearchHighlights.Clear();

        if (_selectedSearchResultIndex < 0 ||
            _selectedSearchResultIndex >= SearchResults.Count ||
            SearchResults[_selectedSearchResultIndex] is not { } result ||
            result.PageNumber != CurrentPage)
        {
            OnPropertyChanged(nameof(HasSearchHighlights));
            return;
        }

        foreach (var region in result.Hit.Regions)
        {
            var bounds = Velune.Application.Annotations.DocumentAnnotationCoordinateMapper.MapRegionToVisualBounds(region, Rotation);
            SearchHighlights.Add(new TextSelectionHighlightItem
            {
                Left = bounds.X * CurrentPagePixelWidth,
                Top = bounds.Y * CurrentPagePixelHeight,
                Width = bounds.Width * CurrentPagePixelWidth,
                Height = bounds.Height * CurrentPagePixelHeight
            });
        }

        OnPropertyChanged(nameof(HasSearchHighlights));
    }

    /// <summary>
    /// Raises property-changed notifications for all search-related properties.
    /// </summary>
    public void NotifySearchStateChanged()
    {
        OnPropertyChanged(nameof(HasSearchResults));
        OnPropertyChanged(nameof(SearchResultSummary));
        OnPropertyChanged(nameof(SearchSelectionIndicator));
        OnPropertyChanged(nameof(HasSearchPanelNotice));
    }

    private void ClearSearchHighlights()
    {
        SearchHighlights.Clear();
        OnPropertyChanged(nameof(HasSearchHighlights));
    }

    /// <summary>
    /// Rebuilds the text selection highlight overlays for the current page.
    /// </summary>
    public void RefreshDocumentTextSelectionHighlights()
    {
        TextSelectionHighlights.Clear();

        if (CurrentDocumentTextSelection is not { } selection ||
            selection.PageIndex.Value != CurrentPage - 1)
        {
            OnPropertyChanged(nameof(HasTextSelectionHighlights));
            return;
        }

        foreach (var region in selection.Regions)
        {
            var bounds = Velune.Application.Annotations.DocumentAnnotationCoordinateMapper.MapRegionToVisualBounds(region, Rotation);
            TextSelectionHighlights.Add(new TextSelectionHighlightItem
            {
                Left = bounds.X * CurrentPagePixelWidth,
                Top = bounds.Y * CurrentPagePixelHeight,
                Width = bounds.Width * CurrentPagePixelWidth,
                Height = bounds.Height * CurrentPagePixelHeight
            });
        }

        OnPropertyChanged(nameof(HasTextSelectionHighlights));
    }

    /// <summary>
    /// Applies the current theme and refreshes the tab chrome appearance.
    /// </summary>
    /// <param name="isLightTheme">True for light theme, false for dark.</param>
    public void SetTheme(bool isLightTheme)
    {
        _isLightTheme = isLightTheme;
        RefreshTabChrome(_isPointerOver);
    }

    /// <summary>
    /// Refreshes the tab background and border brushes based on active/hover state.
    /// </summary>
    /// <param name="isPointerOver">Whether the pointer is hovering over the tab.</param>
    public void RefreshTabChrome(bool isPointerOver)
    {
        _isPointerOver = isPointerOver;

        if (IsActive)
        {
            TabBackground = CreateBrush(_isLightTheme ? "#FFFFFF" : "#2C2C2C");
            TabBorderBrush = CreateBrush(_isLightTheme ? "#E5E5E5" : "#3D3D3D");
        }
        else if (isPointerOver)
        {
            TabBackground = CreateBrush(_isLightTheme ? "#F5F5F5" : "#1F1F1F");
            TabBorderBrush = CreateBrush(_isLightTheme ? "#E5E5E5" : "#333333");
        }
        else
        {
            TabBackground = CreateBrush("#00000000");
            TabBorderBrush = CreateBrush("#00000000");
        }

        OnPropertyChanged(nameof(TabBackground));
        OnPropertyChanged(nameof(TabBorderBrush));
    }

    partial void OnCurrentPageChanged(int value)
    {
        SyncRotationToCurrentPage();
        OnPropertyChanged(nameof(PageText));
        RefreshAnnotationOverlays();
        RefreshSearchHighlights();
        RefreshDocumentTextSelectionHighlights();
    }

    partial void OnTotalPagesChanged(int value)
    {
        OnPropertyChanged(nameof(PageText));
        OnPropertyChanged(nameof(PageCountText));
        OnPropertyChanged(nameof(HasMissingThumbnails));
        NotifyPendingPageEditStateChanged();
    }

    partial void OnHasPendingPageReorderChanged(bool value)
    {
        NotifyPendingPageEditStateChanged();
    }

    partial void OnIsActiveChanged(bool value)
    {
        RefreshTabChrome(_isPointerOver);
    }

    partial void OnRotationChanged(Rotation value)
    {
        InlineTextEditor = null;
        RefreshAnnotationOverlays();
        RefreshSearchHighlights();
        RefreshDocumentTextSelectionHighlights();
    }

    partial void OnInlineTextEditorChanged(WindowsInlineTextEditorViewModel? value)
    {
        OnPropertyChanged(nameof(HasInlineTextEditor));
    }

    partial void OnSearchQueryChanged(string value)
    {
        if (SearchResults.Count > 0)
        {
            ClearSearchResults();
        }
    }

    partial void OnSearchPanelNoticeChanged(string? value)
    {
        OnPropertyChanged(nameof(HasSearchPanelNotice));
    }

    partial void OnIsAnalyzingDocumentTextChanged(bool value)
    {
        NotifySearchStateChanged();
    }

    partial void OnRequiresSearchOcrChanged(bool value)
    {
        NotifySearchStateChanged();
    }

    private string ResolveAnnotationLabel(DocumentAnnotation annotation)
    {
        if (!string.IsNullOrWhiteSpace(annotation.Text))
        {
            return annotation.Text;
        }

        return annotation.Kind switch
        {
            DocumentAnnotationKind.Highlight => _textCatalog.GetString("annotation.kind.highlight"),
            DocumentAnnotationKind.Ink => _textCatalog.GetString("annotation.kind.ink"),
            DocumentAnnotationKind.Text => _textCatalog.GetString("annotation.kind.text"),
            DocumentAnnotationKind.Rectangle => _textCatalog.GetString("annotation.kind.rectangle"),
            DocumentAnnotationKind.Note => _textCatalog.GetString("annotation.kind.note"),
            DocumentAnnotationKind.Stamp => _textCatalog.GetString("annotation.kind.stamp"),
            DocumentAnnotationKind.Signature => _textCatalog.GetString("annotation.kind.signature"),
            _ => annotation.Kind.ToString()
        };
    }

    private static string ResolveAnnotationGlyph(DocumentAnnotationKind kind)
    {
        return kind switch
        {
            DocumentAnnotationKind.Highlight => "\uE7FB",
            DocumentAnnotationKind.Ink => "\uED5F",
            DocumentAnnotationKind.Text => "\uE8D2",
            DocumentAnnotationKind.Rectangle => "\uE9F5",
            DocumentAnnotationKind.Note => "\uE90A",
            DocumentAnnotationKind.Signature => "\uED5F",
            DocumentAnnotationKind.Stamp => "\uE8B7",
            _ => "\uE8A5"
        };
    }

    /// <summary>
    /// Finds a text annotation at the specified normalized point on the current page.
    /// </summary>
    /// <param name="point">The normalized point to hit-test.</param>
    /// <returns>The matching annotation, or null.</returns>
    public DocumentAnnotation? FindTextAnnotationAtPoint(NormalizedPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);

        var pageIndex = new PageIndex(Math.Max(0, CurrentPage - 1));
        foreach (var annotation in Annotations.Where(item =>
                     item.PageIndex == pageIndex && item.Kind is DocumentAnnotationKind.Text))
        {
            if (annotation.Bounds is not { } bounds)
            {
                continue;
            }

            if (point.X >= bounds.X && point.X <= bounds.X + bounds.Width &&
                point.Y >= bounds.Y && point.Y <= bounds.Y + bounds.Height)
            {
                return annotation;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds any non-note annotation at the specified normalized point on the current page.
    /// </summary>
    /// <param name="point">The normalized point to hit-test.</param>
    /// <returns>The matching annotation, or null.</returns>
    public DocumentAnnotation? FindAnnotationAtPoint(NormalizedPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);

        var pageIndex = new PageIndex(Math.Max(0, CurrentPage - 1));
        foreach (var annotation in Annotations.Where(item => item.PageIndex == pageIndex))
        {
            if (annotation.Kind is DocumentAnnotationKind.Note)
            {
                continue;
            }

            if (annotation.Bounds is not { } bounds)
            {
                continue;
            }

            if (point.X >= bounds.X && point.X <= bounds.X + bounds.Width &&
                point.Y >= bounds.Y && point.Y <= bounds.Y + bounds.Height)
            {
                return annotation;
            }
        }

        return null;
    }

    /// <summary>
    /// Moves the annotation to the specified normalized bounds.
    /// </summary>
    /// <param name="annotationId">The annotation identifier.</param>
    /// <param name="newBounds">The new normalized position and size.</param>
    public void MoveAnnotation(Guid annotationId, NormalizedTextRegion newBounds)
    {
        var annotationIndex = FindAnnotationIndex(annotationId);
        if (annotationIndex < 0)
        {
            return;
        }

        var annotation = Annotations[annotationIndex];
        Annotations[annotationIndex] = new DocumentAnnotation(
            annotation.Id,
            annotation.Kind,
            annotation.PageIndex,
            annotation.Appearance,
            newBounds,
            annotation.Points,
            annotation.Text,
            annotation.AssetId,
            annotation.CreatedAt);
        IsDirty = true;
        RefreshAnnotationOverlays();
    }

    private int FindAnnotationIndex(Guid annotationId)
    {
        for (var index = 0; index < Annotations.Count; index++)
        {
            if (Annotations[index].Id == annotationId)
            {
                return index;
            }
        }

        return -1;
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024d:0.#} KB";
        }

        return $"{bytes / 1024d / 1024d:0.#} MB";
    }

    private static string FormatDimensions(int? width, int? height)
    {
        return width is { } pixelWidth && height is { } pixelHeight
            ? $"{pixelWidth} x {pixelHeight} px"
            : "-";
    }

    private static string? FormatDate(DateTimeOffset? value)
    {
        return value?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    }

    private static SolidColorBrush CreateBrush(string hex)
    {
        var normalized = hex.Trim().TrimStart('#');
        return new SolidColorBrush(global::Windows.UI.Color.FromArgb(
            255,
            Convert.ToByte(normalized[..2], 16),
            Convert.ToByte(normalized.Substring(2, 2), 16),
            Convert.ToByte(normalized.Substring(4, 2), 16)));
    }
}
