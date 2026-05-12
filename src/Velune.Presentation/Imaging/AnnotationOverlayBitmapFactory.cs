using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using Velune.Application.Annotations;
using Velune.Domain.Annotations;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Presentation.Imaging;

/// <summary>
/// Factory that renders document annotations into a transparent overlay bitmap using SkiaSharp.
/// </summary>
public static class AnnotationOverlayBitmapFactory
{
    /// <summary>
    /// Determines whether the exception indicates a missing native rendering dependency.
    /// </summary>
    /// <param name="exception">The exception to inspect.</param>
    /// <returns>True if the exception is caused by an unavailable native library.</returns>
    public static bool IsRenderingDependencyUnavailable(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception is InvalidOperationException or DllNotFoundException or EntryPointNotFoundException ||
               exception is TypeInitializationException typeInitializationException &&
               typeInitializationException.InnerException is DllNotFoundException or EntryPointNotFoundException;
    }

    /// <summary>
    /// Creates an overlay bitmap with all annotations rendered for a given page size and rotation.
    /// </summary>
    /// <param name="annotations">Annotations to render.</param>
    /// <param name="width">Target bitmap width in pixels.</param>
    /// <param name="height">Target bitmap height in pixels.</param>
    /// <param name="rotation">Current page rotation.</param>
    /// <param name="selectedAnnotationId">The currently selected annotation identifier, if any.</param>
    /// <param name="signatureAssets">Available signature assets keyed by ID.</param>
    /// <returns>A bitmap with the rendered annotations, or null if nothing to draw.</returns>
    public static WriteableBitmap? Create(
        IReadOnlyList<DocumentAnnotation> annotations,
        int width,
        int height,
        Rotation rotation,
        Guid? selectedAnnotationId,
        IReadOnlyDictionary<string, SignatureAsset> signatureAssets)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        ArgumentNullException.ThrowIfNull(signatureAssets);

        if (width <= 0 || height <= 0 || annotations.Count == 0)
        {
            return null;
        }

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        foreach (DocumentAnnotation annotation in annotations)
        {
            DrawAnnotation(canvas, annotation, width, height, rotation, signatureAssets, annotation.Id == selectedAnnotationId);
        }

        using SKImage image = surface.Snapshot();
        var bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer framebuffer = bitmap.Lock();
        image.ReadPixels(
            new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul),
            framebuffer.Address,
            framebuffer.RowBytes,
            0,
            0);

        return bitmap;
    }

    /// <summary>
    /// Creates a preview bitmap of the signature pad showing drawn strokes.
    /// </summary>
    /// <param name="points">Normalized ink points captured from the signature pad.</param>
    /// <param name="width">Preview width in pixels.</param>
    /// <param name="height">Preview height in pixels.</param>
    /// <returns>A bitmap showing the signature preview.</returns>
    public static WriteableBitmap CreateSignaturePadPreview(
        IReadOnlyList<NormalizedPoint> points,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(points);

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColor.Parse("#00000000"));

        using var outlinePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            Color = SKColor.Parse("#6470A0"),
            IsAntialias = true
        };

        canvas.DrawRoundRect(new SKRect(2, 2, width - 2, height - 2), 20, 20, outlinePaint);

        if (points.Count > 0)
        {
            using var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 4,
                Color = SKColor.Parse("#2F3150"),
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                IsAntialias = true
            };

            using var path = new SKPath();
            NormalizedPoint first = points[0];
            path.MoveTo((float)(first.X * width), (float)(first.Y * height));

            for (int i = 1; i < points.Count; i++)
            {
                NormalizedPoint point = points[i];
                path.LineTo((float)(point.X * width), (float)(point.Y * height));
            }

            canvas.DrawPath(path, paint);
        }

        using SKImage image = surface.Snapshot();
        var bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer framebuffer = bitmap.Lock();
        image.ReadPixels(
            new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul),
            framebuffer.Address,
            framebuffer.RowBytes,
            0,
            0);

        return bitmap;
    }

    private static void DrawAnnotation(
        SKCanvas canvas,
        DocumentAnnotation annotation,
        int width,
        int height,
        Rotation rotation,
        IReadOnlyDictionary<string, SignatureAsset> signatureAssets,
        bool isSelected)
    {
        switch (annotation.Kind)
        {
            case DocumentAnnotationKind.Highlight:
                DrawRectAnnotation(canvas, annotation, width, height, rotation, isSelected, 0.56f);
                break;
            case DocumentAnnotationKind.Rectangle:
                DrawRectAnnotation(canvas, annotation, width, height, rotation, isSelected, 0.22f);
                break;
            case DocumentAnnotationKind.Text:
            case DocumentAnnotationKind.Note:
            case DocumentAnnotationKind.Stamp:
                DrawTextAnnotation(canvas, annotation, width, height, rotation, isSelected);
                break;
            case DocumentAnnotationKind.Signature:
                DrawSignatureAnnotation(canvas, annotation, width, height, rotation, signatureAssets, isSelected);
                break;
            case DocumentAnnotationKind.Ink:
                DrawInkAnnotation(canvas, annotation, width, height, rotation, isSelected);
                break;
        }
    }

    private static void DrawRectAnnotation(
        SKCanvas canvas,
        DocumentAnnotation annotation,
        int width,
        int height,
        Rotation rotation,
        bool isSelected,
        float fillAlpha)
    {
        if (annotation.Bounds is null)
        {
            return;
        }

        SKRect rect = ResolveRect(annotation.Bounds, width, height, rotation);
        using var fillPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = SKColor.Parse(annotation.Appearance.FillHex ?? "#EEF1FF").WithAlpha((byte)(fillAlpha * 255)),
            IsAntialias = true
        };
        using var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)annotation.Appearance.StrokeThickness,
            Color = SKColor.Parse(annotation.Appearance.StrokeHex),
            IsAntialias = true
        };

        canvas.DrawRoundRect(rect, 12, 12, fillPaint);
        canvas.DrawRoundRect(rect, 12, 12, strokePaint);

        if (isSelected)
        {
            DrawSelectionOutline(canvas, rect);
        }
    }

    private static void DrawTextAnnotation(
        SKCanvas canvas,
        DocumentAnnotation annotation,
        int width,
        int height,
        Rotation rotation,
        bool isSelected)
    {
        if (annotation.Bounds is null)
        {
            return;
        }

        SKRect rect = ResolveRect(annotation.Bounds, width, height, rotation);
        string fallbackFill = annotation.Kind switch
        {
            DocumentAnnotationKind.Note => "#FFF2D7",
            DocumentAnnotationKind.Stamp => "#FDE5F0",
            _ => "#EEF1FF"
        };

        using var fillPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = SKColor.Parse(annotation.Appearance.FillHex ?? fallbackFill).WithAlpha(220),
            IsAntialias = true
        };
        using var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)annotation.Appearance.StrokeThickness,
            Color = SKColor.Parse(annotation.Appearance.StrokeHex),
            IsAntialias = true
        };
        using var textPaint = new SKPaint
        {
            Color = SKColor.Parse(annotation.Appearance.StrokeHex),
            IsAntialias = true,
            TextSize = Math.Max(12, rect.Height * 0.18f),
            Typeface = annotation.Kind is DocumentAnnotationKind.Stamp
                ? SKTypeface.FromFamilyName(null, SKFontStyle.Bold)
                : SKTypeface.Default
        };

        canvas.DrawRoundRect(rect, 14, 14, fillPaint);
        canvas.DrawRoundRect(rect, 14, 14, strokePaint);

        string content = string.IsNullOrWhiteSpace(annotation.Text)
            ? annotation.Kind.ToString()
            : annotation.Text!;
        float textX = rect.Left + 12;
        float textY = rect.Top + 18 + textPaint.TextSize;
        canvas.DrawText(content, textX, textY, textPaint);

        if (isSelected)
        {
            DrawSelectionOutline(canvas, rect);
        }
    }

    private static void DrawSignatureAnnotation(
        SKCanvas canvas,
        DocumentAnnotation annotation,
        int width,
        int height,
        Rotation rotation,
        IReadOnlyDictionary<string, SignatureAsset> signatureAssets,
        bool isSelected)
    {
        if (annotation.Bounds is null)
        {
            return;
        }

        SKRect rect = ResolveRect(annotation.Bounds, width, height, rotation);

        if (!string.IsNullOrWhiteSpace(annotation.AssetId) &&
            signatureAssets.TryGetValue(annotation.AssetId, out SignatureAsset? asset) &&
            File.Exists(asset.FilePath))
        {
            using var signatureBitmap = SKBitmap.Decode(asset.FilePath);
            if (signatureBitmap is not null)
            {
                canvas.DrawBitmap(signatureBitmap, rect);
            }
        }
        else
        {
            using var strokePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                Color = SKColor.Parse("#2F3150"),
                IsAntialias = true
            };
            canvas.DrawRoundRect(rect, 14, 14, strokePaint);
        }

        if (isSelected)
        {
            DrawSelectionOutline(canvas, rect);
        }
    }

    private static void DrawInkAnnotation(
        SKCanvas canvas,
        DocumentAnnotation annotation,
        int width,
        int height,
        Rotation rotation,
        bool isSelected)
    {
        if (annotation.Points.Count == 0)
        {
            return;
        }

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)annotation.Appearance.StrokeThickness,
            Color = SKColor.Parse(annotation.Appearance.StrokeHex),
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true
        };
        using var path = new SKPath();

        (double X, double Y) first = DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(annotation.Points[0], width, height, rotation);
        path.MoveTo((float)first.X, (float)first.Y);

        for (int i = 1; i < annotation.Points.Count; i++)
        {
            (double X, double Y) point = DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(annotation.Points[i], width, height, rotation);
            path.LineTo((float)point.X, (float)point.Y);
        }

        canvas.DrawPath(path, paint);

        if (isSelected)
        {
            double minX = annotation.Points.Min(point => point.X);
            double maxX = annotation.Points.Max(point => point.X);
            double minY = annotation.Points.Min(point => point.Y);
            double maxY = annotation.Points.Max(point => point.Y);
            DrawSelectionOutline(
                canvas,
                ResolveRect(new NormalizedTextRegion(minX, minY, Math.Max(0.01, maxX - minX), Math.Max(0.01, maxY - minY)), width, height, rotation));
        }
    }

    private static SKRect ResolveRect(
        NormalizedTextRegion region,
        int width,
        int height,
        Rotation rotation)
    {
        (double X, double Y) topLeft = DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(
            new NormalizedPoint(region.X, region.Y),
            width,
            height,
            rotation);
        (double X, double Y) topRight = DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(
            new NormalizedPoint(region.X + region.Width, region.Y),
            width,
            height,
            rotation);
        (double X, double Y) bottomLeft = DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(
            new NormalizedPoint(region.X, region.Y + region.Height),
            width,
            height,
            rotation);
        (double X, double Y) bottomRight = DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(
            new NormalizedPoint(region.X + region.Width, region.Y + region.Height),
            width,
            height,
            rotation);

        float left = (float)Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
        float top = (float)Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
        float right = (float)Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
        float bottom = (float)Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));

        return new SKRect(left, top, right, bottom);
    }

    private static void DrawSelectionOutline(SKCanvas canvas, SKRect rect)
    {
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            Color = SKColor.Parse("#A9D0FF"),
            PathEffect = SKPathEffect.CreateDash([8, 6], 0),
            IsAntialias = true
        };

        canvas.DrawRoundRect(rect, 14, 14, paint);
    }
}
