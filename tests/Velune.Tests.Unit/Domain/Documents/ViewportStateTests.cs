using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Domain.Documents;

public sealed class ViewportStateTests
{
    [Fact]
    public void DefaultViewportState_ShouldHaveExpectedDefaults()
    {
        var viewport = ViewportState.Default;

        Assert.Equal(new PageIndex(0), viewport.CurrentPage);
        Assert.Equal(1.0, viewport.ZoomFactor);
        Assert.Equal(ZoomMode.Custom, viewport.ZoomMode);
        Assert.Equal(Rotation.Deg0, viewport.Rotation);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenZoomFactorIsNotPositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportState(
            new PageIndex(0),
            0,
            ZoomMode.Custom,
            Rotation.Deg0));
    }
}
