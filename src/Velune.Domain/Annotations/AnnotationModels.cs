using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Domain.Annotations;

public enum AnnotationTool
{
    Select,
    Highlight,
    Ink,
    Text,
    Rectangle,
    Note,
    Stamp,
    Signature
}

public enum DocumentAnnotationKind
{
    Highlight,
    Ink,
    Text,
    Rectangle,
    Note,
    Stamp,
    Signature
}

public sealed record NormalizedPoint
{
    public NormalizedPoint(double x, double y)
    {
        if (x < 0 || x > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "X must be between 0 and 1.");
        }

        if (y < 0 || y > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(y), "Y must be between 0 and 1.");
        }

        X = x;
        Y = y;
    }

    public double X
    {
        get;
    }

    public double Y
    {
        get;
    }
}

public sealed record AnnotationAppearance
{
    public AnnotationAppearance(
        string strokeHex,
        string? fillHex,
        double strokeThickness,
        double opacity = 1.0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strokeHex);

        if (strokeThickness <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(strokeThickness), "Stroke thickness must be greater than zero.");
        }

        if (opacity is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(opacity), "Opacity must be between 0 and 1.");
        }

        StrokeHex = strokeHex;
        FillHex = fillHex;
        StrokeThickness = strokeThickness;
        Opacity = opacity;
    }

    public string StrokeHex
    {
        get;
    }

    public string? FillHex
    {
        get;
    }

    public double StrokeThickness
    {
        get;
    }

    public double Opacity
    {
        get;
    }
}

public sealed record DocumentAnnotation
{
    private readonly IReadOnlyList<NormalizedPoint> _points;

    public DocumentAnnotation(
        Guid id,
        DocumentAnnotationKind kind,
        PageIndex pageIndex,
        AnnotationAppearance appearance,
        NormalizedTextRegion? bounds = null,
        IReadOnlyList<NormalizedPoint>? points = null,
        string? text = null,
        string? assetId = null)
    {
        ArgumentNullException.ThrowIfNull(appearance);

        if (id == Guid.Empty)
        {
            throw new ArgumentException("Annotation id cannot be empty.", nameof(id));
        }

        var normalizedPoints = points?.ToArray() ?? [];

        if (kind is DocumentAnnotationKind.Ink && normalizedPoints.Length == 0)
        {
            throw new ArgumentException("Ink annotations require at least one point.", nameof(points));
        }

        if (kind is not DocumentAnnotationKind.Ink && bounds is null)
        {
            throw new ArgumentException("This annotation kind requires bounds.", nameof(bounds));
        }

        Id = id;
        Kind = kind;
        PageIndex = pageIndex;
        Appearance = appearance;
        Bounds = bounds;
        _points = normalizedPoints;
        Text = text;
        AssetId = assetId;
    }

    public Guid Id
    {
        get;
    }

    public DocumentAnnotationKind Kind
    {
        get;
    }

    public PageIndex PageIndex
    {
        get;
    }

    public AnnotationAppearance Appearance
    {
        get;
    }

    public NormalizedTextRegion? Bounds
    {
        get;
    }

    public IReadOnlyList<NormalizedPoint> Points => _points;

    public string? Text
    {
        get;
    }

    public string? AssetId
    {
        get;
    }

    public DocumentAnnotation DeepCopy()
    {
        return new DocumentAnnotation(
            Id,
            Kind,
            new PageIndex(PageIndex.Value),
            new AnnotationAppearance(
                Appearance.StrokeHex,
                Appearance.FillHex,
                Appearance.StrokeThickness,
                Appearance.Opacity),
            Bounds is null
                ? null
                : new NormalizedTextRegion(
                    Bounds.X,
                    Bounds.Y,
                    Bounds.Width,
                    Bounds.Height),
            [.. Points.Select(point => new NormalizedPoint(point.X, point.Y))],
            Text,
            AssetId);
    }
}

public sealed record SignatureAsset
{
    public SignatureAsset(
        string id,
        string displayName,
        string filePath,
        int pixelWidth,
        int pixelHeight,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelHeight);

        Id = id;
        DisplayName = displayName;
        FilePath = filePath;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        CreatedAt = createdAt;
    }

    public string Id
    {
        get;
    }

    public string DisplayName
    {
        get;
    }

    public string FilePath
    {
        get;
    }

    public int PixelWidth
    {
        get;
    }

    public int PixelHeight
    {
        get;
    }

    public DateTimeOffset CreatedAt
    {
        get;
    }
}
