using Velune.Domain.Annotations;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Annotations;

/// <summary>
/// Maps coordinates between visual (screen) space and normalized (0-1) document space for annotations.
/// </summary>
public static class DocumentAnnotationCoordinateMapper
{
    /// <summary>
    /// Converts a visual point to a normalized coordinate, accounting for rotation.
    /// </summary>
    /// <param name="visualX">X position in visual layer pixels.</param>
    /// <param name="visualY">Y position in visual layer pixels.</param>
    /// <param name="layerWidth">Width of the visual layer.</param>
    /// <param name="layerHeight">Height of the visual layer.</param>
    /// <param name="rotation">Current page rotation.</param>
    /// <returns>A normalized point in the range [0,1].</returns>
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

        double xRatio = Math.Clamp(visualX / layerWidth, 0, 1);
        double yRatio = Math.Clamp(visualY / layerHeight, 0, 1);

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

        (double xRatio, double yRatio) = rotation switch
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

        double left = Math.Min(start.X, end.X);
        double top = Math.Min(start.Y, end.Y);
        double right = Math.Max(start.X, end.X);
        double bottom = Math.Max(start.Y, end.Y);

        double width = Math.Max(0.001, right - left);
        double height = Math.Max(0.001, bottom - top);

        return new NormalizedTextRegion(left, top, Math.Min(1 - left, width), Math.Min(1 - top, height));
    }

    public static NormalizedTextRegion InflatePoint(
        NormalizedPoint point,
        double widthRatio,
        double heightRatio)
    {
        ArgumentNullException.ThrowIfNull(point);

        double width = Math.Clamp(widthRatio, 0.02, 1);
        double height = Math.Clamp(heightRatio, 0.02, 1);
        double left = Math.Clamp(point.X, 0, Math.Max(0, 1 - width));
        double top = Math.Clamp(point.Y, 0, Math.Max(0, 1 - height));

        return new NormalizedTextRegion(left, top, width, height);
    }

    public static NormalizedTextRegion MapRegionToVisualBounds(
        NormalizedTextRegion region,
        Rotation rotation)
    {
        ArgumentNullException.ThrowIfNull(region);

        NormalizedPoint[] points = new[]
        {
            new NormalizedPoint(region.X, region.Y),
            new NormalizedPoint(region.X + region.Width, region.Y),
            new NormalizedPoint(region.X, region.Y + region.Height),
            new NormalizedPoint(region.X + region.Width, region.Y + region.Height)
        };

        (double X, double Y)[] visualPoints = points
            .Select(point => MapNormalizedPointToVisual(point, 1, 1, rotation))
            .ToArray();

        double left = visualPoints.Min(point => point.X);
        double top = visualPoints.Min(point => point.Y);
        double right = visualPoints.Max(point => point.X);
        double bottom = visualPoints.Max(point => point.Y);

        return new NormalizedTextRegion(left, top, Math.Max(0.001, right - left), Math.Max(0.001, bottom - top));
    }
}
