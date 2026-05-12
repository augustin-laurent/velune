using Velune.Domain.ValueObjects;

namespace Velune.Domain.Documents;

/// <summary>
/// Immutable snapshot of the document viewport (current page, zoom, and rotation).
/// </summary>
public sealed record ViewportState
{
    public PageIndex CurrentPage
    {
        get;
    }
    public double ZoomFactor
    {
        get;
    }
    public ZoomMode ZoomMode
    {
        get;
    }
    public Rotation Rotation
    {
        get;
    }

    public ViewportState(
        PageIndex currentPage,
        double zoomFactor,
        ZoomMode zoomMode,
        Rotation rotation)
    {
        if (zoomFactor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zoomFactor), "Zoom factor must be greater than zero.");
        }

        CurrentPage = currentPage;
        ZoomFactor = zoomFactor;
        ZoomMode = zoomMode;
        Rotation = rotation;
    }

    /// <summary>
    /// Default viewport: page 0, 100% zoom, no rotation.
    /// </summary>
    public static ViewportState Default => new(
        new PageIndex(0),
        1.0,
        ZoomMode.Custom,
        Rotation.Deg0);

    /// <summary>
    /// Returns a new viewport navigated to the specified page.
    /// </summary>
    /// <param name="pageIndex">Target page index.</param>
    /// <returns>New viewport state at the given page.</returns>
    public ViewportState WithPage(PageIndex pageIndex) =>
        new(pageIndex, ZoomFactor, ZoomMode, Rotation);

    /// <summary>
    /// Returns a new viewport with the specified zoom settings.
    /// </summary>
    /// <param name="zoomFactor">New zoom multiplier.</param>
    /// <param name="zoomMode">New zoom mode.</param>
    /// <returns>New viewport state with updated zoom.</returns>
    public ViewportState WithZoom(double zoomFactor, ZoomMode zoomMode) =>
        new(CurrentPage, zoomFactor, zoomMode, Rotation);

    /// <summary>
    /// Returns a new viewport with the specified rotation.
    /// </summary>
    /// <param name="rotation">New rotation value.</param>
    /// <returns>New viewport state with updated rotation.</returns>
    public ViewportState WithRotation(Rotation rotation) =>
        new(CurrentPage, ZoomFactor, ZoomMode, rotation);
}
