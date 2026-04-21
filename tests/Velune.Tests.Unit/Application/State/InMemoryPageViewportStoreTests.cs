using Velune.Application.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Application.State;

public sealed class InMemoryPageViewportStoreTests
{
    [Fact]
    public void SetRotation_ShouldKeepRotationPerPage()
    {
        var store = new InMemoryPageViewportStore();
        store.Initialize(3);

        store.SetRotation(new PageIndex(0), Rotation.Deg90);
        store.SetRotation(new PageIndex(1), Rotation.Deg270);

        var firstPageRotation = store.GetRotation(new PageIndex(0));
        var secondPageRotation = store.GetRotation(new PageIndex(1));
        var thirdPageRotation = store.GetRotation(new PageIndex(2));

        Assert.Equal(Rotation.Deg90, firstPageRotation);
        Assert.Equal(Rotation.Deg270, secondPageRotation);
        Assert.Equal(Rotation.Deg0, thirdPageRotation);
    }
}
