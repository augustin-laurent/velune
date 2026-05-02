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

public sealed partial class WindowsDocumentTabViewModel : ObservableObject
{
    private readonly IWindowsTextCatalog _textCatalog;
    private bool _isLightTheme;
    private bool _isPointerOver;
    private int _selectedSearchResultIndex = -1;

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

    public string PageText => $"{CurrentPage} / {TotalPages}";

    public string CurrentPageAnnotationCountText => CurrentPageAnnotationOverlays.Count.ToString(CultureInfo.CurrentCulture);

    public bool HasCurrentPageAnnotations => CurrentPageAnnotationOverlays.Count > 0;

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

    public void NotifyThumbnailStatusChanged()
    {
        OnPropertyChanged(nameof(HasMissingThumbnails));
    }

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

    public void AddAnnotation(DocumentAnnotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        Annotations.Add(annotation);
        RefreshAnnotationOverlays();
        IsDirty = true;
    }

    public void DeleteAnnotationById(Guid annotationId)
    {
        var annotation = Annotations.FirstOrDefault(item => item.Id == annotationId);
        if (annotation is null)
        {
            return;
        }

        Annotations.Remove(annotation);
        RefreshAnnotationOverlays();
        IsDirty = true;
    }

    public void RefreshAnnotationOverlays()
    {
        CurrentPageAnnotationOverlays.Clear();

        var pageIndex = new PageIndex(Math.Max(0, CurrentPage - 1));
        foreach (var annotation in Annotations.Where(item => item.PageIndex == pageIndex))
        {
            CurrentPageAnnotationOverlays.Add(new WindowsAnnotationOverlayViewModel(
                annotation,
                Math.Max(1, CurrentPagePixelWidth),
                Math.Max(1, CurrentPagePixelHeight),
                Rotation,
                ResolveAnnotationLabel(annotation),
                _textCatalog.Format("windows.thumbnail.page", annotation.PageIndex.Value + 1),
                ResolveAnnotationGlyph(annotation.Kind)));
        }

        OnPropertyChanged(nameof(CurrentPageAnnotationCountText));
        OnPropertyChanged(nameof(HasCurrentPageAnnotations));
    }

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

    public void ClearSearchResults()
    {
        SearchResults.Clear();
        _selectedSearchResultIndex = -1;
        ClearSearchHighlights();
        NotifySearchStateChanged();
    }

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

    public void SetTheme(bool isLightTheme)
    {
        _isLightTheme = isLightTheme;
        RefreshTabChrome(_isPointerOver);
    }

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
    }

    partial void OnIsActiveChanged(bool value)
    {
        RefreshTabChrome(_isPointerOver);
    }

    partial void OnRotationChanged(Rotation value)
    {
        RefreshAnnotationOverlays();
        RefreshSearchHighlights();
        RefreshDocumentTextSelectionHighlights();
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
        return annotation.Text ?? annotation.Kind switch
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
