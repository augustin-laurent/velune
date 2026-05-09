using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Velune.Application.Annotations;
using Velune.Domain.Annotations;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Windows.Foundation;

namespace Velune.Windows.ViewModels;

/// <summary>
/// View model for rendering a document annotation overlay on the page canvas.
/// </summary>
public sealed class WindowsAnnotationOverlayViewModel
{
    /// <summary>
    /// Initializes the overlay from a domain annotation, mapping normalized coordinates to pixel dimensions.
    /// </summary>
    /// <param name="annotation">The source annotation.</param>
    /// <param name="pageWidth">The rendered page width in pixels.</param>
    /// <param name="pageHeight">The rendered page height in pixels.</param>
    /// <param name="rotation">The current page rotation.</param>
    /// <param name="label">Display label for the annotation.</param>
    /// <param name="pageLabel">Localized page label text.</param>
    /// <param name="glyph">Icon glyph character for the annotation kind.</param>
    /// <param name="signatureAssets">Available signature image assets.</param>
    public WindowsAnnotationOverlayViewModel(
        DocumentAnnotation annotation,
        double pageWidth,
        double pageHeight,
        Rotation rotation,
        string label,
        string pageLabel,
        string glyph,
        IReadOnlyDictionary<string, SignatureAsset>? signatureAssets = null)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(pageLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(glyph);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageHeight);

        Id = annotation.Id;
        Kind = annotation.Kind;
        Label = label;
        PageLabel = pageLabel;
        Glyph = glyph;
        Text = annotation.Text ?? label;
        PreviewText = string.Equals(Text, label, StringComparison.Ordinal)
            ? string.Empty
            : Text;
        TimeText = annotation.CreatedAt.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.CurrentCulture);
        Opacity = annotation.Appearance.Opacity;
        StrokeBrush = CreateBrush(annotation.Appearance.StrokeHex, 255);
        FillBrush = CreateBrush(ResolveFillHex(annotation), ResolveFillAlpha(annotation.Kind));
        TextBrush = CreateBrush(annotation.Kind is DocumentAnnotationKind.Text
            ? annotation.Appearance.StrokeHex : "#111827", 255);
        TextFontSize = annotation.Appearance.FontSize;
        TextFontFamily = annotation.Appearance.FontFamily ?? "Segoe UI";
        InkPoints = CreateInkPoints(annotation, pageWidth, pageHeight, rotation);
        SignatureImageSource = CreateSignatureImageSource(annotation, signatureAssets);
        BorderThickness = annotation.Kind is DocumentAnnotationKind.Highlight or DocumentAnnotationKind.Text ? new Thickness(0) : new Thickness(2);
        CornerRadius = annotation.Kind is DocumentAnnotationKind.Stamp ? new CornerRadius(2) : new CornerRadius(6);
        TextVisibility = annotation.Kind is DocumentAnnotationKind.Text or DocumentAnnotationKind.Stamp ||
                         annotation.Kind is DocumentAnnotationKind.Signature && SignatureImageSource is null
            ? Visibility.Visible
            : Visibility.Collapsed;
        GlyphVisibility = annotation.Kind is DocumentAnnotationKind.Signature && SignatureImageSource is null
            ? Visibility.Visible
            : Visibility.Collapsed;
        SignatureImageVisibility = SignatureImageSource is null
            ? Visibility.Collapsed
            : Visibility.Visible;
        InkVisibility = annotation.Kind is DocumentAnnotationKind.Ink ? Visibility.Visible : Visibility.Collapsed;
        BoxVisibility = annotation.Kind is DocumentAnnotationKind.Ink ? Visibility.Collapsed : Visibility.Visible;
        StrokeThickness = Math.Max(2, annotation.Appearance.StrokeThickness);

        var bounds = ResolveBounds(annotation, rotation);
        Left = bounds.X * pageWidth;
        Top = bounds.Y * pageHeight;
        Width = Math.Max(18, bounds.Width * pageWidth);
        Height = Math.Max(18, bounds.Height * pageHeight);

        if (annotation.Kind is DocumentAnnotationKind.Ink)
        {
            Left = 0;
            Top = 0;
            Width = pageWidth;
            Height = pageHeight;
            BorderThickness = new Thickness(Math.Max(2, annotation.Appearance.StrokeThickness));
            FillBrush = CreateBrush("#000000", 0);
            CornerRadius = new CornerRadius(0);
        }
    }

    public Guid Id
    {
        get;
    }

    public DocumentAnnotationKind Kind
    {
        get;
    }

    public string Label
    {
        get;
    }

    public string PageLabel
    {
        get;
    }

    public string Glyph
    {
        get;
    }

    public string Text
    {
        get;
    }

    public string PreviewText
    {
        get;
    }

    public bool HasPreviewText => !string.IsNullOrWhiteSpace(PreviewText);

    public string TimeText
    {
        get;
    }

    public double Left
    {
        get;
    }

    public double Top
    {
        get;
    }

    public double Width
    {
        get;
    }

    public double Height
    {
        get;
    }

    public Thickness Margin => new(Left, Top, 0, 0);

    public double Opacity
    {
        get;
    }

    public SolidColorBrush StrokeBrush
    {
        get;
    }

    public SolidColorBrush FillBrush
    {
        get;
    }

    public SolidColorBrush TextBrush
    {
        get;
    }

    public double TextFontSize
    {
        get;
    }

    public string TextFontFamily
    {
        get;
    }

    public Thickness BorderThickness
    {
        get;
    }

    public CornerRadius CornerRadius
    {
        get;
    }

    public Visibility TextVisibility
    {
        get;
    }

    public Visibility GlyphVisibility
    {
        get;
    }

    public Visibility InkVisibility
    {
        get;
    }

    public Visibility BoxVisibility
    {
        get;
    }

    public double StrokeThickness
    {
        get;
    }

    public PointCollection InkPoints
    {
        get;
    }

    public ImageSource? SignatureImageSource
    {
        get;
    }

    public Visibility SignatureImageVisibility
    {
        get;
    }

    public bool IsHidden
    {
        get; set;
    }

    public bool IsLocked
    {
        get; set;
    }

    public string HideMenuText
    {
        get; set;
    } = string.Empty;

    public string LockMenuText
    {
        get; set;
    } = string.Empty;

    public double ListItemOpacity => IsHidden ? 0.45 : 1.0;

    public string LockGlyph => IsLocked ? "" : string.Empty;

    public Visibility LockIconVisibility => IsLocked ? Visibility.Visible : Visibility.Collapsed;

    public Visibility HiddenIconVisibility => IsHidden ? Visibility.Visible : Visibility.Collapsed;

    private static BitmapImage? CreateSignatureImageSource(
        DocumentAnnotation annotation,
        IReadOnlyDictionary<string, SignatureAsset>? signatureAssets)
    {
        if (annotation.Kind is not DocumentAnnotationKind.Signature ||
            string.IsNullOrWhiteSpace(annotation.AssetId) ||
            signatureAssets is null ||
            !signatureAssets.TryGetValue(annotation.AssetId, out var asset) ||
            !File.Exists(asset.FilePath))
        {
            return null;
        }

        return new BitmapImage(new Uri(asset.FilePath, UriKind.Absolute));
    }

    private static PointCollection CreateInkPoints(
        DocumentAnnotation annotation,
        double pageWidth,
        double pageHeight,
        Rotation rotation)
    {
        var points = new PointCollection();
        foreach (var point in annotation.Points)
        {
            var mapped = DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(
                point,
                pageWidth,
                pageHeight,
                rotation);
            points.Add(new Point(mapped.X, mapped.Y));
        }

        return points;
    }

    private static NormalizedTextRegion ResolveBounds(DocumentAnnotation annotation, Rotation rotation)
    {
        if (annotation.Bounds is { } bounds)
        {
            return DocumentAnnotationCoordinateMapper.MapRegionToVisualBounds(bounds, rotation);
        }

        if (annotation.Points.Count == 0)
        {
            return new NormalizedTextRegion(0.1, 0.1, 0.2, 0.08);
        }

        var left = Math.Clamp(annotation.Points.Min(point => point.X), 0, 0.98);
        var top = Math.Clamp(annotation.Points.Min(point => point.Y), 0, 0.98);
        var right = Math.Clamp(annotation.Points.Max(point => point.X), left, 1);
        var bottom = Math.Clamp(annotation.Points.Max(point => point.Y), top, 1);
        return new NormalizedTextRegion(
            left,
            top,
            Math.Clamp(Math.Max(0.02, right - left), 0.02, 1 - left),
            Math.Clamp(Math.Max(0.02, bottom - top), 0.02, 1 - top));
    }

    private static string ResolveFillHex(DocumentAnnotation annotation)
    {
        if (!string.IsNullOrWhiteSpace(annotation.Appearance.FillHex))
        {
            return annotation.Appearance.FillHex;
        }

        return annotation.Kind switch
        {
            DocumentAnnotationKind.Note => "#FFF2D7",
            DocumentAnnotationKind.Stamp => "#FDE5F0",
            DocumentAnnotationKind.Signature => "#FFFFFF",
            DocumentAnnotationKind.Rectangle => "#EEF1FF",
            DocumentAnnotationKind.Text => "#000000",
            _ => annotation.Appearance.StrokeHex
        };
    }

    private static byte ResolveFillAlpha(DocumentAnnotationKind kind)
    {
        return kind switch
        {
            DocumentAnnotationKind.Highlight => 115,
            DocumentAnnotationKind.Rectangle => 70,
            DocumentAnnotationKind.Ink or DocumentAnnotationKind.Text => 0,
            _ => 232
        };
    }

    private static SolidColorBrush CreateBrush(string hex, byte alpha)
    {
        var normalized = hex.Trim().TrimStart('#');
        if (normalized.Length != 6)
        {
            normalized = "EEF1FF";
        }

        return new SolidColorBrush(global::Windows.UI.Color.FromArgb(
            alpha,
            Convert.ToByte(normalized[..2], 16),
            Convert.ToByte(normalized.Substring(2, 2), 16),
            Convert.ToByte(normalized.Substring(4, 2), 16)));
    }
}

/// <summary>
/// View model for rendering a comment/note annotation in the side lane.
/// </summary>
public sealed partial class WindowsCommentOverlayViewModel : ObservableObject
{
    private const double LaneWidthValue = 270;
    private const double CardHeightEstimate = 88;

    /// <summary>
    /// Initializes a comment overlay from a note annotation.
    /// </summary>
    /// <param name="annotation">The source note annotation.</param>
    /// <param name="pageHeight">The rendered page height in pixels.</param>
    /// <param name="rotation">The current page rotation.</param>
    /// <param name="label">Display label for the comment.</param>
    /// <param name="pageLabel">Localized page label text.</param>
    public WindowsCommentOverlayViewModel(
        DocumentAnnotation annotation,
        double pageHeight,
        Rotation rotation,
        string label,
        string pageLabel)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(pageLabel);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageHeight);

        Id = annotation.Id;
        Text = string.IsNullOrWhiteSpace(annotation.Text) ? label : annotation.Text;
        EditText = Text;
        PageLabel = pageLabel;
        TimeText = annotation.CreatedAt.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.CurrentCulture);
        StrokeBrush = CreateBrush(annotation.Appearance.StrokeHex, 255);

        var bounds = annotation.Bounds is { } annotationBounds
            ? DocumentAnnotationCoordinateMapper.MapRegionToVisualBounds(annotationBounds, rotation)
            : new NormalizedTextRegion(0.08, 0.08, 0.2, 0.08);
        var top = Math.Clamp(
            bounds.Y * pageHeight - 18,
            0,
            Math.Max(0, pageHeight - CardHeightEstimate));

        Margin = new Thickness(0, top, 0, 0);
    }

    public Guid Id
    {
        get;
    }

    public string Text
    {
        get;
    }

    [ObservableProperty]
    public partial string EditText
    {
        get; set;
    } = string.Empty;

    [ObservableProperty]
    public partial bool IsEditing
    {
        get; set;
    }

    public Visibility ReadVisibility => IsEditing ? Visibility.Collapsed : Visibility.Visible;

    public Visibility EditVisibility => IsEditing ? Visibility.Visible : Visibility.Collapsed;

    public string PageLabel
    {
        get;
    }

    public string TimeText
    {
        get;
    }

    public Thickness Margin
    {
        get;
    }

    public double LaneWidth => LaneWidthValue;

    public SolidColorBrush StrokeBrush
    {
        get;
    }

    partial void OnIsEditingChanged(bool value)
    {
        OnPropertyChanged(nameof(ReadVisibility));
        OnPropertyChanged(nameof(EditVisibility));
    }

    private static SolidColorBrush CreateBrush(string hex, byte alpha)
    {
        var normalized = hex.Trim().TrimStart('#');
        if (normalized.Length != 6)
        {
            normalized = "EEF1FF";
        }

        return new SolidColorBrush(global::Windows.UI.Color.FromArgb(
            alpha,
            Convert.ToByte(normalized[..2], 16),
            Convert.ToByte(normalized.Substring(2, 2), 16),
            Convert.ToByte(normalized.Substring(4, 2), 16)));
    }
}

/// <summary>
/// View model for the inline text annotation editor positioned on the page canvas.
/// </summary>
public sealed partial class WindowsInlineTextEditorViewModel : ObservableObject
{
    /// <summary>
    /// Initializes the inline text editor from an existing text annotation.
    /// </summary>
    /// <param name="annotation">The text annotation being edited.</param>
    /// <param name="pageWidth">The rendered page width in pixels.</param>
    /// <param name="pageHeight">The rendered page height in pixels.</param>
    /// <param name="rotation">The current page rotation.</param>
    public WindowsInlineTextEditorViewModel(
        DocumentAnnotation annotation,
        double pageWidth,
        double pageHeight,
        Rotation rotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageHeight);

        AnnotationId = annotation.Id;
        Text = annotation.Text ?? string.Empty;
        FontSize = annotation.Appearance.FontSize;
        FontFamily = annotation.Appearance.FontFamily ?? "Segoe UI";
        StrokeBrush = CreateBrush(annotation.Appearance.StrokeHex, 255);
        FillBrush = CreateBrush(annotation.Appearance.FillHex ?? "#EEF1FF", 238);

        var bounds = annotation.Bounds is { } annotationBounds
            ? DocumentAnnotationCoordinateMapper.MapRegionToVisualBounds(annotationBounds, rotation)
            : new NormalizedTextRegion(0.1, 0.1, 0.24, 0.12);

        Left = bounds.X * pageWidth;
        Top = bounds.Y * pageHeight;
        Width = Math.Max(160, bounds.Width * pageWidth);
        Height = Math.Max(72, bounds.Height * pageHeight);
    }

    public Guid AnnotationId
    {
        get;
    }

    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    public double FontSize
    {
        get;
    }

    public string FontFamily
    {
        get;
    }

    public double Left
    {
        get;
    }

    public double Top
    {
        get;
    }

    public double Width
    {
        get;
    }

    public double Height
    {
        get;
    }

    public Thickness Margin => new(Left, Top, 0, 0);

    public SolidColorBrush StrokeBrush
    {
        get;
    }

    public SolidColorBrush FillBrush
    {
        get;
    }

    private static SolidColorBrush CreateBrush(string hex, byte alpha)
    {
        var normalized = hex.Trim().TrimStart('#');
        if (normalized.Length != 6)
        {
            normalized = "EEF1FF";
        }

        return new SolidColorBrush(global::Windows.UI.Color.FromArgb(
            alpha,
            Convert.ToByte(normalized[..2], 16),
            Convert.ToByte(normalized.Substring(2, 2), 16),
            Convert.ToByte(normalized.Substring(4, 2), 16)));
    }
}

/// <summary>
/// Represents a selectable color option in the annotation color picker.
/// </summary>
public sealed partial class WindowsAnnotationColorItem : ObservableObject
{
    /// <summary>
    /// Initializes a color item from a hex color string.
    /// </summary>
    /// <param name="hex">The hex color value (e.g. "#FFE600").</param>
    public WindowsAnnotationColorItem(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);

        Hex = hex;
        Brush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(
            255,
            Convert.ToByte(hex.Substring(1, 2), 16),
            Convert.ToByte(hex.Substring(3, 2), 16),
            Convert.ToByte(hex.Substring(5, 2), 16)));
    }

    public string Hex
    {
        get;
    }

    public SolidColorBrush Brush
    {
        get;
    }

    [ObservableProperty]
    public partial bool IsSelected
    {
        get; set;
    }
}
