using Velune.Windows.ViewModels;
using Velune.Domain.ValueObjects;

namespace Velune.Tests.Windows.Unit.ViewModels;

public sealed class WindowsPageThumbnailViewModelTests
{
    [Fact]
    public void Constructor_StartsIdleWithoutPlaceholder()
    {
        var thumbnail = new WindowsPageThumbnailViewModel(1, "Page 1", "Loading");

        Assert.Null(thumbnail.Image);
        Assert.False(thumbnail.IsLoading);
        Assert.False(thumbnail.HasPlaceholder);
        Assert.Equal(string.Empty, thumbnail.PlaceholderText);
    }

    [Fact]
    public void MarkRenderFailed_StopsLoadingAndShowsPlaceholder()
    {
        var thumbnail = new WindowsPageThumbnailViewModel(1, "Page 1", "Loading");

        thumbnail.MarkRenderFailed("Preview unavailable");

        Assert.Null(thumbnail.Image);
        Assert.False(thumbnail.IsLoading);
        Assert.True(thumbnail.HasPlaceholder);
        Assert.Equal("Preview unavailable", thumbnail.PlaceholderText);
    }

    [Fact]
    public void BeginRender_ClearsFailedPlaceholderAndRestartsLoading()
    {
        var thumbnail = new WindowsPageThumbnailViewModel(1, "Page 1", "Loading");
        thumbnail.MarkRenderFailed("Preview unavailable");

        thumbnail.BeginRender();

        Assert.Null(thumbnail.Image);
        Assert.True(thumbnail.IsLoading);
        Assert.False(thumbnail.HasPlaceholder);
        Assert.Equal(string.Empty, thumbnail.PlaceholderText);
    }

    [Fact]
    public void RotationAngle_FollowsRotation()
    {
        var thumbnail = new WindowsPageThumbnailViewModel(1, "Page 1", "Loading");

        thumbnail.Rotation = Rotation.Deg90;

        Assert.Equal(90, thumbnail.RotationAngle);
    }
}
