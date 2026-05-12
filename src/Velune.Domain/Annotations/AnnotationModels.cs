using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Domain.Annotations;

/// <summary>
/// Available annotation tools in the toolbar.
/// </summary>
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

/// <summary>
/// Classifies the kind of annotation placed on a document.
/// </summary>
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

/// <summary>
/// A point normalized to [0,1] coordinates relative to the page dimensions.
/// </summary>
public sealed record NormalizedPoint
{
    /// <summary>
    /// Creates a normalized point with coordinates in the range [0,1].
    /// </summary>
    /// <param name="x">Horizontal position (0 = left, 1 = right).</param>
    /// <param name="y">Vertical position (0 = top, 1 = bottom).</param>
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

/// <summary>
/// Visual appearance settings for an annotation (color, stroke, font, opacity).
/// </summary>
public sealed record AnnotationAppearance
{
    /// <summary>
    /// Creates an annotation appearance with the specified visual properties.
    /// </summary>
    /// <param name="strokeHex">Stroke color as a hex string (e.g. "#FF0000").</param>
    /// <param name="fillHex">Optional fill color as a hex string.</param>
    /// <param name="strokeThickness">Stroke thickness in pixels.</param>
    /// <param name="opacity">Opacity from 0 (transparent) to 1 (opaque).</param>
    /// <param name="fontSize">Font size for text annotations (6-200).</param>
    /// <param name="fontFamily">Optional font family name.</param>
    public AnnotationAppearance(
        string strokeHex,
        string? fillHex,
        double strokeThickness,
        double opacity = 1.0,
        double fontSize = 14,
        string? fontFamily = null,
        double rotationAngle = 0)
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

        if (fontSize is < 6 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSize), "Font size must be between 6 and 200.");
        }

        StrokeHex = strokeHex;
        FillHex = fillHex;
        StrokeThickness = strokeThickness;
        Opacity = opacity;
        FontSize = fontSize;
        FontFamily = fontFamily;
        RotationAngle = rotationAngle;
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

    public double FontSize
    {
        get;
    }

    public string? FontFamily
    {
        get;
    }

    public double RotationAngle
    {
        get;
    }
}

/// <summary>
/// A single annotation placed on a document page.
/// </summary>
public sealed record DocumentAnnotation
{
    private readonly IReadOnlyList<NormalizedPoint> _points;

    /// <summary>
    /// Creates a document annotation.
    /// </summary>
    /// <param name="id">Unique annotation identifier.</param>
    /// <param name="kind">The type of annotation.</param>
    /// <param name="pageIndex">Page on which the annotation is placed.</param>
    /// <param name="appearance">Visual appearance settings.</param>
    /// <param name="bounds">Bounding rectangle (required for non-ink annotations).</param>
    /// <param name="points">Collection of points (required for ink annotations).</param>
    /// <param name="text">Optional text content.</param>
    /// <param name="assetId">Optional reference to an external asset (e.g. signature image).</param>
    /// <param name="createdAt">Creation timestamp; defaults to UTC now.</param>
    public DocumentAnnotation(
        Guid id,
        DocumentAnnotationKind kind,
        PageIndex pageIndex,
        AnnotationAppearance appearance,
        NormalizedTextRegion? bounds = null,
        IReadOnlyList<NormalizedPoint>? points = null,
        string? text = null,
        string? assetId = null,
        DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(appearance);

        if (id == Guid.Empty)
        {
            throw new ArgumentException("Annotation id cannot be empty.", nameof(id));
        }

        NormalizedPoint[] normalizedPoints = points?.ToArray() ?? [];

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
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
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

    /// <summary>
    /// Bounding rectangle in normalized coordinates; null for ink annotations.
    /// </summary>
    public NormalizedTextRegion? Bounds
    {
        get;
    }

    /// <summary>
    /// Stroke points in normalized coordinates (used by ink annotations).
    /// </summary>
    public IReadOnlyList<NormalizedPoint> Points => _points;

    /// <summary>
    /// Text content for text/note annotations; null otherwise.
    /// </summary>
    public string? Text
    {
        get;
    }

    /// <summary>
    /// External asset identifier (e.g. signature image ID); null if not applicable.
    /// </summary>
    public string? AssetId
    {
        get;
    }

    public DateTimeOffset CreatedAt
    {
        get;
    }

    /// <summary>
    /// Creates an independent copy of this annotation.
    /// </summary>
    /// <returns>A deep-copied annotation instance.</returns>
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
                Appearance.Opacity,
                Appearance.FontSize,
                Appearance.FontFamily,
                Appearance.RotationAngle),
            Bounds is null
                ? null
                : new NormalizedTextRegion(
                    Bounds.X,
                    Bounds.Y,
                    Bounds.Width,
                    Bounds.Height),
            [.. Points.Select(point => new NormalizedPoint(point.X, point.Y))],
            Text,
            AssetId,
            CreatedAt);
    }
}

/// <summary>
/// A saved signature asset that can be stamped onto documents.
/// </summary>
public sealed record SignatureAsset
{
    /// <summary>
    /// Creates a signature asset record.
    /// </summary>
    /// <param name="id">Unique asset identifier.</param>
    /// <param name="displayName">User-facing name.</param>
    /// <param name="filePath">Path to the signature image file.</param>
    /// <param name="pixelWidth">Image width in pixels.</param>
    /// <param name="pixelHeight">Image height in pixels.</param>
    /// <param name="createdAt">When the asset was created.</param>
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
