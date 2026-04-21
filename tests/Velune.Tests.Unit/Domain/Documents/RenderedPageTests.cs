using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Domain.Documents;

public sealed class RenderedPageTests
{
    [Fact]
    public void Constructor_ShouldDefensivelyCopyPixelData()
    {
        var originalBuffer = new byte[] { 1, 2, 3, 4 };
        var renderedPage = new RenderedPage(new PageIndex(0), originalBuffer, 1, 1);

        originalBuffer[0] = 99;

        Assert.Equal(4, renderedPage.ByteCount);
        Assert.Equal((byte)1, renderedPage.PixelData.Span[0]);
    }
}
