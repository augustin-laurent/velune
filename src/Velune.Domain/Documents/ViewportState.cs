using Velune.Domain.ValueObjects;

namespace Velune.Domain.Documents;

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

    public static ViewportState Default => new(
        new PageIndex(0),
        1.0,
        ZoomMode.Custom,
        Rotation.Deg0);

    public ViewportState WithPage(PageIndex pageIndex) =>
        new(pageIndex, ZoomFactor, ZoomMode, Rotation);

    public ViewportState WithZoom(double zoomFactor, ZoomMode zoomMode) =>
        new(CurrentPage, zoomFactor, zoomMode, Rotation);

    public ViewportState WithRotation(Rotation rotation) =>
        new(CurrentPage, ZoomFactor, ZoomMode, rotation);
}
