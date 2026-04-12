using Velune.Domain.Documents;

namespace Velune.Tests.Unit.Domain.Documents;

public sealed class RenderedPageViewportCalculatorTests
{
    [Fact]
    public void CalculateFitToWidthZoom_ShouldPreserveUnderlyingPageScale()
    {
        var zoom = RenderedPageViewportCalculator.CalculateFitToWidthZoom(
            renderedWidth: 1200,
            currentZoom: 1.5,
            availableWidth: 600);

        Assert.Equal(0.75, zoom, 3);
    }

    [Fact]
    public void CalculateFitToPageZoom_ShouldUseMostRestrictiveAxis()
    {
        var zoom = RenderedPageViewportCalculator.CalculateFitToPageZoom(
            renderedWidth: 1000,
            renderedHeight: 2000,
            currentZoom: 2.0,
            availableWidth: 600,
            availableHeight: 700);

        Assert.Equal(0.7, zoom, 3);
    }
}
