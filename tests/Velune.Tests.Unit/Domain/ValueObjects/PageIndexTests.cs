using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Domain.ValueObjects;

public sealed class PageIndexTests
{
    [Fact]
    public void Constructor_ShouldSetValue_WhenValueIsValid()
    {
        var pageIndex = new PageIndex(3);

        Assert.Equal(3, pageIndex.Value);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenValueIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PageIndex(-1));
    }
}
