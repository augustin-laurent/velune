using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Velune.Application.Annotations;
using Velune.Domain.Annotations;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

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
    /// <param name="label">Display a label for the annotation.</param>
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
        StrokeHex = FormatArgbHex(annotation.Appearance.StrokeHex, 255);
        FillHex = FormatArgbHex(ResolveFillHex(annotation), ResolveFillAlpha(annotation));
        TextHex = FormatArgbHex(annotation.Kind is DocumentAnnotationKind.Text
            ? annotation.Appearance.StrokeHex : "#111827", 255);
        TextFontSize = annotation.Appearance.FontSize;
        TextFontFamily = annotation.Appearance.FontFamily ?? "Segoe UI";
        RotationAngle = annotation.Appearance.RotationAngle;
        InkPoints = CreateInkPoints(annotation, pageWidth, pageHeight, rotation);
        SignatureImageSource = CreateSignatureImageSource(annotation, signatureAssets);
        BorderThicknessValue = annotation.Kind is DocumentAnnotationKind.Highlight or DocumentAnnotationKind.Text ? 0 : 2;
        CornerRadiusValue = annotation.Kind is DocumentAnnotationKind.Stamp ? 2 : 6;
        IsTextVisible = annotation.Kind is DocumentAnnotationKind.Text or DocumentAnnotationKind.Stamp ||
                         annotation.Kind is DocumentAnnotationKind.Signature && SignatureImageSource is null;
        IsGlyphVisible = annotation.Kind is DocumentAnnotationKind.Signature && SignatureImageSource is null;
        IsSignatureImageVisible = SignatureImageSource is not null;
        IsInkVisible = annotation.Kind is DocumentAnnotationKind.Ink;
        IsBoxVisible = annotation.Kind is not DocumentAnnotationKind.Ink;
        StrokeThickness = Math.Max(2, annotation.Appearance.StrokeThickness);

        NormalizedTextRegion bounds = ResolveBounds(annotation, rotation);
        Left = bounds.X * pageWidth;
        Top = bounds.Y * pageHeight;
        Width = Math.Max(18, bounds.Width * pageWidth);
        Height = Math.Max(18, bounds.Height * pageHeight);

        SelectionLeft = Left;
        SelectionTop = Top;
        SelectionWidth = Width;
        SelectionHeight = Height;
        RotationCenterX = Width / 2;
        RotationCenterY = Height / 2;

        if (annotation.Kind is not DocumentAnnotationKind.Ink)
        {
            return;
        }

        NormalizedTextRegion inkBounds = ComputeInkBounds(annotation, rotation);
        SelectionLeft = inkBounds.X * pageWidth;
        SelectionTop = inkBounds.Y * pageHeight;
        SelectionWidth = Math.Max(18, inkBounds.Width * pageWidth);
        SelectionHeight = Math.Max(18, inkBounds.Height * pageHeight);

        Left = 0;
        Top = 0;
        Width = pageWidth;
        Height = pageHeight;
        RotationCenterX = SelectionLeft + SelectionWidth / 2;
        RotationCenterY = SelectionTop + SelectionHeight / 2;
        BorderThicknessValue = Math.Max(2, annotation.Appearance.StrokeThickness);
        FillHex = FormatArgbHex("#000000", 0);
        CornerRadiusValue = 0;
    }

    private static NormalizedTextRegion ComputeInkBounds(DocumentAnnotation annotation, Rotation rotation)
    {
        if (annotation.Points.Count == 0)
        {
            return new NormalizedTextRegion(0, 0, 1, 1);
        }

        var visualPoints = annotation.Points
            .Select(p => DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(p, 1, 1, rotation))
            .ToArray();

        double minX = visualPoints.Min(p => p.X);
        double maxX = visualPoints.Max(p => p.X);
        double minY = visualPoints.Min(p => p.Y);
        double maxY = visualPoints.Max(p => p.Y);

        const double pad = 0.01;
        double x = Math.Max(0, minX - pad);
        double y = Math.Max(0, minY - pad);
        double w = Math.Min(1 - x, maxX - minX + 2 * pad);
        double h = Math.Min(1 - y, maxY - minY + 2 * pad);

        return new NormalizedTextRegion(x, y, Math.Max(0.01, w), Math.Max(0.01, h));
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

    public Thickness Margin => new(Left, Top, 0, 0);

    public double Width
    {
        get;
    }

    public double Height
    {
        get;
    }

    public double SelectionLeft
    {
        get;
    }

    public double SelectionTop
    {
        get;
    }

    public double SelectionWidth
    {
        get;
    }

    public double SelectionHeight
    {
        get;
    }

    public double SelectionMarginLeft => SelectionLeft - Left;

    public double SelectionMarginTop => SelectionTop - Top;

    public double Opacity
    {
        get;
    }

    public string StrokeHex
    {
        get;
    }

    public string FillHex
    {
        get;
    }

    public string TextHex
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

    public double BorderThicknessValue
    {
        get;
    }

    public double CornerRadiusValue
    {
        get;
    }

    public bool IsTextVisible
    {
        get;
    }

    public bool IsGlyphVisible
    {
        get;
    }

    public bool IsInkVisible
    {
        get;
    }

    public bool IsBoxVisible
    {
        get;
    }

    public double StrokeThickness
    {
        get;
    }

    public double RotationAngle
    {
        get;
    }

    public double RotationCenterX
    {
        get;
        private set;
    }

    public double RotationCenterY
    {
        get;
        private set;
    }

    public double RotateButtonLeft => SelectionWidth / 2 - 10;

    public double RotateButtonTop => -30;

    public double RotateStemLeft => SelectionWidth / 2;

    public IReadOnlyList<(double X, double Y)> InkPoints
    {
        get;
    }

    public ImageSource? SignatureImageSource
    {
        get;
    }

    public bool IsSignatureImageVisible
    {
        get;
    }

    public bool IsSelected
    {
        get; set;
    }

    public bool IsSelectionVisible => IsSelected;

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

    public bool IsLockIconVisible => IsLocked;

    public bool IsHiddenIconVisible => IsHidden;

    private static BitmapImage? CreateSignatureImageSource(
        DocumentAnnotation annotation,
        IReadOnlyDictionary<string, SignatureAsset>? signatureAssets)
    {
        if (annotation.Kind is not DocumentAnnotationKind.Signature ||
            string.IsNullOrWhiteSpace(annotation.AssetId) ||
            signatureAssets is null ||
            !signatureAssets.TryGetValue(annotation.AssetId, out SignatureAsset? asset) ||
            !File.Exists(asset.FilePath))
        {
            return null;
        }

        return new BitmapImage(new Uri(asset.FilePath, UriKind.Absolute));
    }

    private static List<(double X, double Y)> CreateInkPoints(
        DocumentAnnotation annotation,
        double pageWidth,
        double pageHeight,
        Rotation rotation)
    {
        var points = new List<(double X, double Y)>();
        foreach (NormalizedPoint point in annotation.Points)
        {
            (double X, double Y) mapped = DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(
                point,
                pageWidth,
                pageHeight,
                rotation);
            points.Add((mapped.X, mapped.Y));
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

        double left = Math.Clamp(annotation.Points.Min(point => point.X), 0, 0.98);
        double top = Math.Clamp(annotation.Points.Min(point => point.Y), 0, 0.98);
        double right = Math.Clamp(annotation.Points.Max(point => point.X), left, 1);
        double bottom = Math.Clamp(annotation.Points.Max(point => point.Y), top, 1);
        return new NormalizedTextRegion(
            left,
            top,
            Math.Clamp(Math.Max(0.02, right - left), 0.02, 1 - left),
            Math.Clamp(Math.Max(0.02, bottom - top), 0.02, 1 - top));
    }

    private static string ResolveFillHex(DocumentAnnotation annotation)
    {
        return !string.IsNullOrWhiteSpace(annotation.Appearance.FillHex)
            ? annotation.Appearance.FillHex
            : annotation.Kind switch
            {
                DocumentAnnotationKind.Note => "#FFF2D7",
                DocumentAnnotationKind.Stamp => "#FDE5F0",
                DocumentAnnotationKind.Signature => "#FFFFFF",
                DocumentAnnotationKind.Text => "#FFFFFF",
                _ => "#000000"
            };
    }

    private static byte ResolveFillAlpha(DocumentAnnotation annotation)
    {
        return annotation.Kind switch
        {
            DocumentAnnotationKind.Highlight => 115,
            DocumentAnnotationKind.Ink or DocumentAnnotationKind.Text => 0,
            DocumentAnnotationKind.Rectangle => annotation.Appearance.FillHex is not null ? (byte)200 : (byte)0,
            _ when annotation.Appearance.FillHex is null => 0,
            _ => 232
        };
    }

    private static string FormatArgbHex(string hex, byte alpha)
    {
        string normalized = hex.Trim().TrimStart('#');
        if (normalized.Length != 6)
        {
            normalized = "EEF1FF";
        }

        return $"#{alpha:X2}{normalized}";
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
        StrokeHex = FormatArgbHex(annotation.Appearance.StrokeHex, 255);

        NormalizedTextRegion bounds = annotation.Bounds is { } annotationBounds
            ? DocumentAnnotationCoordinateMapper.MapRegionToVisualBounds(annotationBounds, rotation)
            : new NormalizedTextRegion(0.08, 0.08, 0.2, 0.08);
        MarginTop = Math.Clamp(
            bounds.Y * pageHeight - 18,
            0,
            Math.Max(0, pageHeight - CardHeightEstimate));
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
    }

    [ObservableProperty]
    public partial bool IsEditing
    {
        get; set;
    }

    public bool IsReadVisible => !IsEditing;

    public bool IsEditVisible => IsEditing;

    public string PageLabel
    {
        get;
    }

    public string TimeText
    {
        get;
    }

    public double MarginTop
    {
        get;
    }

    public Thickness Margin => new(0, MarginTop, 0, 0);

    public double LaneWidth => LaneWidthValue;

    public string StrokeHex
    {
        get;
    }

    partial void OnIsEditingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsReadVisible));
        OnPropertyChanged(nameof(IsEditVisible));
    }

    private static string FormatArgbHex(string hex, byte alpha)
    {
        string normalized = hex.Trim().TrimStart('#');
        if (normalized.Length != 6)
        {
            normalized = "EEF1FF";
        }

        return $"#{alpha:X2}{normalized}";
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
        StrokeHex = FormatArgbHex(annotation.Appearance.StrokeHex, 255);
        FillHex = FormatArgbHex(annotation.Appearance.FillHex ?? "#EEF1FF", 238);

        NormalizedTextRegion bounds = annotation.Bounds is { } annotationBounds
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
    public partial string Text
    {
        get; set;
    }

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

    public Thickness Margin => new(Left, Top, 0, 0);

    public double Width
    {
        get;
    }

    public double Height
    {
        get;
    }

    public string StrokeHex
    {
        get;
    }

    public string FillHex
    {
        get;
    }

    private static string FormatArgbHex(string hex, byte alpha)
    {
        string normalized = hex.Trim().TrimStart('#');
        if (normalized.Length != 6)
        {
            normalized = "EEF1FF";
        }

        return $"#{alpha:X2}{normalized}";
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
    }

    public string Hex
    {
        get;
    }

    [ObservableProperty]
    public partial bool IsSelected
    {
        get; set;
    }
}
