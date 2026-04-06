using Velune.Domain.ValueObjects;

namespace Velune.Domain.Documents;

public sealed record PageViewportState
{
    public PageViewportState(
        PageIndex pageIndex,
        double zoomFactor,
        Rotation rotation)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoomFactor);

        PageIndex = pageIndex;
        ZoomFactor = zoomFactor;
        Rotation = rotation;
    }

    public PageIndex PageIndex
    {
        get;
    }
    public double ZoomFactor
    {
        get;
    }
    public Rotation Rotation
    {
        get;
    }

    public static PageViewportState Default(PageIndex pageIndex) =>
        new(pageIndex, 1.0, Rotation.Deg0);

    public PageViewportState WithZoom(double zoomFactor)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoomFactor);
        return new PageViewportState(PageIndex, zoomFactor, Rotation);
    }

    public PageViewportState WithRotation(Rotation rotation) =>
        new(PageIndex, ZoomFactor, rotation);
}
