using Velune.Application.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Application.State;

public sealed class InMemoryPageViewportStoreTests
{
    [Fact]
    public void SetPageState_ShouldShareZoomAcrossPagesAndKeepRotationPerPage()
    {
        var store = new InMemoryPageViewportStore();
        store.Initialize(3);

        store.SetPageState(new PageViewportState(new PageIndex(0), 1.35, Rotation.Deg90));
        store.SetPageState(new PageViewportState(new PageIndex(1), 1.35, Rotation.Deg270));

        var firstPage = store.GetPageState(new PageIndex(0));
        var secondPage = store.GetPageState(new PageIndex(1));
        var thirdPage = store.GetPageState(new PageIndex(2));

        Assert.Equal(1.35, firstPage.ZoomFactor);
        Assert.Equal(1.35, secondPage.ZoomFactor);
        Assert.Equal(1.35, thirdPage.ZoomFactor);
        Assert.Equal(Rotation.Deg90, firstPage.Rotation);
        Assert.Equal(Rotation.Deg270, secondPage.Rotation);
        Assert.Equal(Rotation.Deg0, thirdPage.Rotation);
    }
}
