namespace Velune.Domain.Documents;

public static class RenderedPageViewportCalculator
{
    public static double CalculateFitToWidthZoom(
        int renderedWidth,
        double currentZoom,
        double availableWidth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(renderedWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(currentZoom);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(availableWidth);

        var baseWidth = renderedWidth / currentZoom;
        return availableWidth / Math.Max(1d, baseWidth);
    }

    public static double CalculateFitToPageZoom(
        int renderedWidth,
        int renderedHeight,
        double currentZoom,
        double availableWidth,
        double availableHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(renderedWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(renderedHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(currentZoom);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(availableWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(availableHeight);

        var baseWidth = renderedWidth / currentZoom;
        var baseHeight = renderedHeight / currentZoom;

        return Math.Min(
            availableWidth / Math.Max(1d, baseWidth),
            availableHeight / Math.Max(1d, baseHeight));
    }
}
