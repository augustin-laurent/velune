using Velune.Domain.Annotations;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Annotations;

public static class DocumentAnnotationCoordinateMapper
{
    public static NormalizedPoint MapVisualPointToNormalized(
        double visualX,
        double visualY,
        double layerWidth,
        double layerHeight,
        Rotation rotation)
    {
        if (layerWidth <= 0 || layerHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(layerWidth), "Layer dimensions must be greater than zero.");
        }

        var xRatio = Math.Clamp(visualX / layerWidth, 0, 1);
        var yRatio = Math.Clamp(visualY / layerHeight, 0, 1);

        return rotation switch
        {
            Rotation.Deg90 => new NormalizedPoint(yRatio, 1 - xRatio),
            Rotation.Deg180 => new NormalizedPoint(1 - xRatio, 1 - yRatio),
            Rotation.Deg270 => new NormalizedPoint(1 - yRatio, xRatio),
            _ => new NormalizedPoint(xRatio, yRatio)
        };
    }

    public static (double X, double Y) MapNormalizedPointToVisual(
        NormalizedPoint point,
        double targetWidth,
        double targetHeight,
        Rotation rotation)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetHeight);

        var (xRatio, yRatio) = rotation switch
        {
            Rotation.Deg90 => (1 - point.Y, point.X),
            Rotation.Deg180 => (1 - point.X, 1 - point.Y),
            Rotation.Deg270 => (point.Y, 1 - point.X),
            _ => (point.X, point.Y)
        };

        return (
            Math.Clamp(xRatio, 0, 1) * targetWidth,
            Math.Clamp(yRatio, 0, 1) * targetHeight);
    }

    public static NormalizedTextRegion CreateBounds(
        NormalizedPoint start,
        NormalizedPoint end)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);

        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var right = Math.Max(start.X, end.X);
        var bottom = Math.Max(start.Y, end.Y);

        var width = Math.Max(0.001, right - left);
        var height = Math.Max(0.001, bottom - top);

        return new NormalizedTextRegion(left, top, Math.Min(1 - left, width), Math.Min(1 - top, height));
    }

    public static NormalizedTextRegion InflatePoint(
        NormalizedPoint point,
        double widthRatio,
        double heightRatio)
    {
        ArgumentNullException.ThrowIfNull(point);

        var width = Math.Clamp(widthRatio, 0.02, 1);
        var height = Math.Clamp(heightRatio, 0.02, 1);
        var left = Math.Clamp(point.X, 0, Math.Max(0, 1 - width));
        var top = Math.Clamp(point.Y, 0, Math.Max(0, 1 - height));

        return new NormalizedTextRegion(left, top, width, height);
    }

    public static NormalizedTextRegion MapRegionToVisualBounds(
        NormalizedTextRegion region,
        Rotation rotation)
    {
        ArgumentNullException.ThrowIfNull(region);

        var points = new[]
        {
            new NormalizedPoint(region.X, region.Y),
            new NormalizedPoint(region.X + region.Width, region.Y),
            new NormalizedPoint(region.X, region.Y + region.Height),
            new NormalizedPoint(region.X + region.Width, region.Y + region.Height)
        };

        var visualPoints = points
            .Select(point => MapNormalizedPointToVisual(point, 1, 1, rotation))
            .ToArray();

        var left = visualPoints.Min(point => point.X);
        var top = visualPoints.Min(point => point.Y);
        var right = visualPoints.Max(point => point.X);
        var bottom = visualPoints.Max(point => point.Y);

        return new NormalizedTextRegion(left, top, Math.Max(0.001, right - left), Math.Max(0.001, bottom - top));
    }
}
