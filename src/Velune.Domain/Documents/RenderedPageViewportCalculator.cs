namespace Velune.Domain.Documents;

/// <summary>
/// Calculates zoom factors to fit a rendered page within available viewport dimensions.
/// </summary>
public static class RenderedPageViewportCalculator
{
    /// <summary>
    /// Calculates the zoom factor that fits the page width to the available width.
    /// </summary>
    /// <param name="renderedWidth">Currently rendered width in pixels.</param>
    /// <param name="currentZoom">Zoom factor used when the page was rendered.</param>
    /// <param name="availableWidth">Available viewport width.</param>
    /// <returns>New zoom factor for fit-to-width.</returns>
    public static double CalculateFitToWidthZoom(
        int renderedWidth,
        double currentZoom,
        double availableWidth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(renderedWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(currentZoom);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(availableWidth);

        double baseWidth = renderedWidth / currentZoom;
        return availableWidth / Math.Max(1d, baseWidth);
    }

    /// <summary>
    /// Calculates the zoom factor that fits the entire page within the available area.
    /// </summary>
    /// <param name="renderedWidth">Currently rendered width in pixels.</param>
    /// <param name="renderedHeight">Currently rendered height in pixels.</param>
    /// <param name="currentZoom">Zoom factor used when the page was rendered.</param>
    /// <param name="availableWidth">Available viewport width.</param>
    /// <param name="availableHeight">Available viewport height.</param>
    /// <returns>New zoom factor for fit-to-page.</returns>
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

        double baseWidth = renderedWidth / currentZoom;
        double baseHeight = renderedHeight / currentZoom;

        return Math.Min(
            availableWidth / Math.Max(1d, baseWidth),
            availableHeight / Math.Max(1d, baseHeight));
    }
}
