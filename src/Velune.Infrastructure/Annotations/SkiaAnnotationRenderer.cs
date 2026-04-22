using SkiaSharp;
using Velune.Application.Annotations;
using Velune.Domain.Annotations;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Infrastructure.Annotations;

internal static class SkiaAnnotationRenderer
{
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

        foreach (var annotation in annotations)
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

    public static byte[] RenderInkSignaturePng(
        IReadOnlyList<NormalizedPoint> points,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
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
            var first = points[0];
            path.MoveTo((float)(first.X * width), (float)(first.Y * height));

            for (var i = 1; i < points.Count; i++)
            {
                var point = points[i];
                path.LineTo((float)(point.X * width), (float)(point.Y * height));
            }

            canvas.DrawPath(path, paint);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
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

        var bounds = ResolveBounds(annotation.Bounds, width, height, rotation);
        using var paint = CreateFillPaint(annotation.Appearance, "#F6D98B");
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

        using var paint = CreateStrokePaint(annotation.Appearance);
        using var path = new SKPath();

        var first = DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(annotation.Points[0], width, height, rotation);
        path.MoveTo((float)first.X, (float)first.Y);

        for (var i = 1; i < annotation.Points.Count; i++)
        {
            var point = DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(annotation.Points[i], width, height, rotation);
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

        var bounds = ResolveBounds(annotation.Bounds, width, height, rotation);

        if (includeFill)
        {
            using var fillPaint = CreateFillPaint(annotation.Appearance, "#FFF5D8");
            canvas.DrawRoundRect(bounds, 10, 10, fillPaint);
        }

        using var strokePaint = CreateStrokePaint(annotation.Appearance);
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

        var bounds = ResolveBounds(annotation.Bounds, width, height, rotation);

        using var fillPaint = CreateFillPaint(annotation.Appearance, emphasizeFill ? "#FFF2D6" : "#EEF1FF");
        using var strokePaint = CreateStrokePaint(annotation.Appearance);

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

        var bounds = ResolveBounds(annotation.Bounds, width, height, rotation);
        using var strokePaint = CreateStrokePaint(annotation.Appearance);
        using var fillPaint = CreateFillPaint(annotation.Appearance, "#FEE6F2");

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

        var bounds = ResolveBounds(annotation.Bounds, width, height, rotation);

        if (!string.IsNullOrWhiteSpace(annotation.AssetId) &&
            signatureAssets.TryGetValue(annotation.AssetId, out var asset) &&
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
        var topLeft = DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(
            new NormalizedPoint(bounds.X, bounds.Y),
            width,
            height,
            rotation);
        var topRight = DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(
            new NormalizedPoint(bounds.X + bounds.Width, bounds.Y),
            width,
            height,
            rotation);
        var bottomLeft = DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(
            new NormalizedPoint(bounds.X, bounds.Y + bounds.Height),
            width,
            height,
            rotation);
        var bottomRight = DocumentAnnotationCoordinateMapper.MapNormalizedPointToVisual(
            new NormalizedPoint(bounds.X + bounds.Width, bounds.Y + bounds.Height),
            width,
            height,
            rotation);

        var left = (float)Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
        var top = (float)Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
        var right = (float)Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
        var bottom = (float)Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));

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
        var color = appearance.FillHex ?? fallbackFill;
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

        var maxTextWidth = Math.Max(24, bounds.Width - 20);
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var currentLine = string.Empty;

        foreach (var word in words)
        {
            var candidate = string.IsNullOrWhiteSpace(currentLine)
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

        var lineHeight = paint.TextSize * 1.22f;
        var totalHeight = lines.Count * lineHeight;
        var originY = center
            ? bounds.MidY - (totalHeight / 2) + paint.TextSize
            : bounds.Top + 18 + paint.TextSize;

        foreach (var line in lines)
        {
            var x = center
                ? bounds.MidX - (paint.MeasureText(line) / 2)
                : bounds.Left + 12;
            canvas.DrawText(line, x, originY, paint);
            originY += lineHeight;
        }
    }
}
