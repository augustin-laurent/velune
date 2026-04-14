using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Domain.Documents;

public sealed class ImageViewportCalculatorTests
{
    [Fact]
    public void CalculateFitZoom_ShouldFitWideImageInsideViewport()
    {
        var zoom = ImageViewportCalculator.CalculateFitZoom(
            new ImageMetadata(4000, 1000),
            Rotation.Deg0,
            availableWidth: 1000,
            availableHeight: 700);

        Assert.Equal(0.25, zoom, 3);
    }

    [Fact]
    public void CalculateFitZoom_ShouldSwapDimensionsWhenImageIsRotated()
    {
        var zoom = ImageViewportCalculator.CalculateFitZoom(
            new ImageMetadata(4000, 1000),
            Rotation.Deg90,
            availableWidth: 1000,
            availableHeight: 700);

        Assert.Equal(0.175, zoom, 3);
    }

    [Fact]
    public void CalculateFitZoom_ShouldUseAvailableSpaceForSmallImages()
    {
        var zoom = ImageViewportCalculator.CalculateFitZoom(
            new ImageMetadata(400, 200),
            Rotation.Deg0,
            availableWidth: 1000,
            availableHeight: 700);

        Assert.Equal(2.5, zoom, 3);
    }

    [Fact]
    public void CalculateFitWidthZoom_ShouldUseImageWidth()
    {
        var zoom = ImageViewportCalculator.CalculateFitWidthZoom(
            new ImageMetadata(2000, 1000),
            Rotation.Deg0,
            availableWidth: 800);

        Assert.Equal(0.4, zoom, 3);
    }

    [Fact]
    public void CalculateFitWidthZoom_ShouldSwapDimensionsWhenImageIsRotated()
    {
        var zoom = ImageViewportCalculator.CalculateFitWidthZoom(
            new ImageMetadata(2000, 1000),
            Rotation.Deg90,
            availableWidth: 800);

        Assert.Equal(0.8, zoom, 3);
    }
}
