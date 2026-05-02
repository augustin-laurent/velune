using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Velune.Application.Annotations;
using Velune.Domain.Annotations;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Windows.Foundation;

namespace Velune.Windows.ViewModels;

public sealed class WindowsAnnotationOverlayViewModel
{
    public WindowsAnnotationOverlayViewModel(
        DocumentAnnotation annotation,
        double pageWidth,
        double pageHeight,
        Rotation rotation,
        string label,
        string pageLabel,
        string glyph)
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
        TextBrush = CreateBrush("#111827", 255);
        InkPoints = CreateInkPoints(annotation, pageWidth, pageHeight, rotation);
        BorderThickness = annotation.Kind is DocumentAnnotationKind.Highlight ? new Thickness(0) : new Thickness(2);
        CornerRadius = annotation.Kind is DocumentAnnotationKind.Stamp ? new CornerRadius(2) : new CornerRadius(6);
        TextVisibility = annotation.Kind is DocumentAnnotationKind.Text or DocumentAnnotationKind.Note or DocumentAnnotationKind.Stamp
            ? Visibility.Visible
            : Visibility.Collapsed;
        GlyphVisibility = annotation.Kind is DocumentAnnotationKind.Note or DocumentAnnotationKind.Signature
            ? Visibility.Visible
            : Visibility.Collapsed;
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
            DocumentAnnotationKind.Rectangle or DocumentAnnotationKind.Text => "#EEF1FF",
            _ => annotation.Appearance.StrokeHex
        };
    }

    private static byte ResolveFillAlpha(DocumentAnnotationKind kind)
    {
        return kind switch
        {
            DocumentAnnotationKind.Highlight => 115,
            DocumentAnnotationKind.Rectangle => 70,
            DocumentAnnotationKind.Ink => 0,
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

public sealed partial class WindowsAnnotationColorItem : ObservableObject
{
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
