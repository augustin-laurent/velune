using Velune.Domain.Documents;

namespace Velune.Tests.Unit.Domain.Documents;

public sealed class ImageMetadataTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 10)]
    [InlineData(10, -1)]
    public void Constructor_ShouldRejectNonPositiveDimensions(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ImageMetadata(width, height));
    }
}
