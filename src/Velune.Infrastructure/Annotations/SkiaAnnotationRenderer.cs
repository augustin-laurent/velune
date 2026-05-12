using SkiaSharp;
using Velune.Application.Annotations;
using Velune.Domain.Annotations;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Infrastructure.Annotations;

/// <summary>
/// Renders document annotations onto a SkiaSharp canvas.
/// </summary>
internal static class SkiaAnnotationRenderer
{
    /// <summary>
    /// Draws all provided annotations onto the given canvas.
    /// </summary>
    /// <param name="canvas">The target SkiaSharp canvas.</param>
    /// <param name="width">Page width in pixels.</param>
    /// <param name="height">Page height in pixels.</param>
    /// <param name="rotation">Current page rotation.</param>
    /// <param name="annotations">Annotations to render.</param>
    /// <param name="signatureAssets">Available signature assets keyed by ID.</param>
    public static void DrawAnnotations(
        SKCanvas canvas,
        float width,
        float height,
        Rotation rotation,
        IReadOnlyList<DocumentAnnotation> annotations,
        IReadOnlyDictionary<string, SignatureAsset> signatureAssets)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(annotations);
        ArgumentNullException.ThrowIfNull(signatureAssets);

        foreach (DocumentAnnotation annotation in annotations)
        {
            switch (annotation.Kind)
            {
                case DocumentAnnotationKind.Highlight:
                    DrawHighlight(canvas, annotation, width, height, rotation);
                    break;
                case DocumentAnnotationKind.Ink:
                    DrawInk(canvas, annotation, width, height, rotation);
                    break;
                case DocumentAnnotationKind.Rectangle:
                    DrawRectangle(canvas, annotation, width, height, rotation, includeFill: false);
                    break;
                case DocumentAnnotationKind.Note:
                    DrawTextBox(canvas, annotation, width, height, rotation, emphasizeFill: true);
                    break;
                case DocumentAnnotationKind.Text:
                    DrawTextBox(canvas, annotation, width, height, rotation, emphasizeFill: false);
                    break;
                case DocumentAnnotationKind.Stamp:
                    DrawStamp(canvas, annotation, width, height, rotation);
                    break;
                case DocumentAnnotationKind.Signature:
                    DrawSignature(canvas, annotation, width, height, rotation, signatureAssets);
                    break;
            }
        }
    }

    /// <summary>
    /// Renders ink signature points to a transparent PNG byte array.
    /// </summary>
    /// <param name="points">Normalized ink points to draw.</param>
    /// <param name="width">Output image width in pixels.</param>
    /// <param name="height">Output image height in pixels.</param>
    /// <returns>PNG-encoded byte array of the rendered signature.</returns>
    public static byte[] RenderInkSignaturePng(
        IReadOnlyList<NormalizedPoint> points,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        SKCanvas? canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (points.Count > 0)
        {
            using var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = Math.Max(3, Math.Min(width, height) * 0.028f),
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

        using SKImage? image = surface.Snapshot();
        using SKData? data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data?.ToArray() ?? [];
    }

    private static void DrawHighlight(
        SKCanvas canvas,
        DocumentAnnotation annotation,
        float width,
        float height,
        Rotation rotation)
    {
        if (annotation.Bounds is null)
        {
            return;
        }

        SKRect bounds = ResolveBounds(annotation.Bounds, width, height, rotation);
        using SKPaint paint = CreateFillPaint(annotation.Appearance, "#F6D98B");
        canvas.DrawRoundRect(bounds, 4, 4, paint);
    }

    private static void DrawInk(
        SKCanvas canvas,
        DocumentAnnotation annotation,
        float width,
        float height,
        Rotation rotation)
    {
        if (annotation.Points.Count == 0)
        {
            return;
        }

        using SKPaint paint = CreateStrokePaint(annotation.Appearance);
        using var path = new SKPath();

        (double X, double Y) first = DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(annotation.Points[0], width, height, rotation);
        path.MoveTo((float)first.X, (float)first.Y);

        for (int i = 1; i < annotation.Points.Count; i++)
        {
            (double X, double Y) point = DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(annotation.Points[i], width, height, rotation);
            path.LineTo((float)point.X, (float)point.Y);
        }

        canvas.DrawPath(path, paint);
    }

    private static void DrawRectangle(
        SKCanvas canvas,
        DocumentAnnotation annotation,
        float width,
        float height,
        Rotation rotation,
        bool includeFill)
    {
        if (annotation.Bounds is null)
        {
            return;
        }

        SKRect bounds = ResolveBounds(annotation.Bounds, width, height, rotation);

        if (includeFill)
        {
            using SKPaint fillPaint = CreateFillPaint(annotation.Appearance, "#FFF5D8");
            canvas.DrawRoundRect(bounds, 10, 10, fillPaint);
        }

        using SKPaint strokePaint = CreateStrokePaint(annotation.Appearance);
        canvas.DrawRoundRect(bounds, 10, 10, strokePaint);
    }

    private static void DrawTextBox(
        SKCanvas canvas,
        DocumentAnnotation annotation,
        float width,
        float height,
        Rotation rotation,
        bool emphasizeFill)
    {
        if (annotation.Bounds is null)
        {
            return;
        }

        SKRect bounds = ResolveBounds(annotation.Bounds, width, height, rotation);

        using SKPaint fillPaint = CreateFillPaint(annotation.Appearance, emphasizeFill ? "#FFF2D6" : "#EEF1FF");
        using SKPaint strokePaint = CreateStrokePaint(annotation.Appearance);

        canvas.DrawRoundRect(bounds, 12, 12, fillPaint);
        canvas.DrawRoundRect(bounds, 12, 12, strokePaint);

        DrawWrappedText(
            canvas,
            string.IsNullOrWhiteSpace(annotation.Text) ? "New annotation" : annotation.Text!,
            bounds,
            emphasizeFill ? "#5D4A2B" : "#2F3150");
    }

    private static void DrawStamp(
        SKCanvas canvas,
        DocumentAnnotation annotation,
        float width,
        float height,
        Rotation rotation)
    {
        if (annotation.Bounds is null)
        {
            return;
        }

        SKRect bounds = ResolveBounds(annotation.Bounds, width, height, rotation);
        using SKPaint strokePaint = CreateStrokePaint(annotation.Appearance);
        using SKPaint fillPaint = CreateFillPaint(annotation.Appearance, "#FEE6F2");

        canvas.DrawRoundRect(bounds, 16, 16, fillPaint);
        canvas.DrawRoundRect(bounds, 16, 16, strokePaint);
        DrawWrappedText(
            canvas,
            string.IsNullOrWhiteSpace(annotation.Text) ? "STAMP" : annotation.Text!.ToUpperInvariant(),
            bounds,
            annotation.Appearance.StrokeHex,
            isBold: true,
            center: true);
    }

    private static void DrawSignature(
        SKCanvas canvas,
        DocumentAnnotation annotation,
        float width,
        float height,
        Rotation rotation,
        IReadOnlyDictionary<string, SignatureAsset> signatureAssets)
    {
        if (annotation.Bounds is null)
        {
            return;
        }

        SKRect bounds = ResolveBounds(annotation.Bounds, width, height, rotation);

        if (!string.IsNullOrWhiteSpace(annotation.AssetId) &&
            signatureAssets.TryGetValue(annotation.AssetId, out SignatureAsset? asset) &&
            File.Exists(asset.FilePath))
        {
            using var signatureBitmap = SKBitmap.Decode(asset.FilePath);
            if (signatureBitmap is not null)
            {
                canvas.DrawBitmap(signatureBitmap, bounds);
                return;
            }
        }

        DrawRectangle(canvas, annotation, width, height, rotation, includeFill: true);
        DrawWrappedText(canvas, "Signature", bounds, annotation.Appearance.StrokeHex, center: true);
    }

    private static SKRect ResolveBounds(
        NormalizedTextRegion bounds,
        float width,
        float height,
        Rotation rotation)
    {
        (double X, double Y) topLeft = DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(
            new NormalizedPoint(bounds.X, bounds.Y),
            width,
            height,
            rotation);
        (double X, double Y) topRight = DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(
            new NormalizedPoint(bounds.X + bounds.Width, bounds.Y),
            width,
            height,
            rotation);
        (double X, double Y) bottomLeft = DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(
            new NormalizedPoint(bounds.X, bounds.Y + bounds.Height),
            width,
            height,
            rotation);
        (double X, double Y) bottomRight = DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(
            new NormalizedPoint(bounds.X + bounds.Width, bounds.Y + bounds.Height),
            width,
            height,
            rotation);

        float left = (float)Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
        float top = (float)Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
        float right = (float)Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
        float bottom = (float)Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));

        return new SKRect(left, top, right, bottom);
    }

    private static SKPaint CreateStrokePaint(AnnotationAppearance appearance)
    {
        return new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (float)Math.Max(1, appearance.StrokeThickness),
            Color = SKColor.Parse(appearance.StrokeHex).WithAlpha((byte)(appearance.Opacity * 255)),
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };
    }

    private static SKPaint CreateFillPaint(AnnotationAppearance appearance, string fallbackFill)
    {
        string color = appearance.FillHex ?? fallbackFill;
        return new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = SKColor.Parse(color).WithAlpha((byte)(appearance.Opacity * 255)),
            IsAntialias = true
        };
    }

    private static void DrawWrappedText(
        SKCanvas canvas,
        string text,
        SKRect bounds,
        string textHex,
        bool isBold = false,
        bool center = false)
    {
        using var paint = new SKPaint
        {
            Color = SKColor.Parse(textHex),
            IsAntialias = true,
            TextSize = Math.Max(12, bounds.Height * 0.18f),
            Typeface = isBold
                ? SKTypeface.FromFamilyName(null, SKFontStyle.Bold)
                : SKTypeface.Default
        };

        float maxTextWidth = Math.Max(24, bounds.Width - 20);
        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        string currentLine = string.Empty;

        foreach (string word in words)
        {
            string candidate = string.IsNullOrWhiteSpace(currentLine)
                ? word
                : $"{currentLine} {word}";

            if (!string.IsNullOrWhiteSpace(currentLine) &&
                paint.MeasureText(candidate) > maxTextWidth)
            {
                lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                currentLine = candidate;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentLine))
        {
            lines.Add(currentLine);
        }

        if (lines.Count == 0)
        {
            lines.Add(text);
        }

        float lineHeight = paint.TextSize * 1.22f;
        float totalHeight = lines.Count * lineHeight;
        float originY = center
            ? bounds.MidY - (totalHeight / 2) + paint.TextSize
            : bounds.Top + 18 + paint.TextSize;

        foreach (string line in lines)
        {
            float x = center
                ? bounds.MidX - (paint.MeasureText(line) / 2)
                : bounds.Left + 12;
            canvas.DrawText(line, x, originY, paint);
            originY += lineHeight;
        }
    }
}
