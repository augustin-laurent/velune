using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Abstractions;

public sealed class InMemoryPageViewportStore : IPageViewportStore
{
    private readonly Dictionary<int, Rotation> _rotations = [];

    public PageIndex ActivePageIndex { get; private set; } = new(0);

    public void Initialize(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageCount);

        _rotations.Clear();

        for (var i = 0; i < pageCount; i++)
        {
            _rotations[i] = Rotation.Deg0;
        }

        ActivePageIndex = new PageIndex(0);
    }

    public Rotation GetRotation(PageIndex pageIndex)
    {
        if (_rotations.TryGetValue(pageIndex.Value, out var rotation))
        {
            return rotation;
        }

        _rotations[pageIndex.Value] = Rotation.Deg0;
        return Rotation.Deg0;
    }

    public void SetActivePage(PageIndex pageIndex)
    {
        ActivePageIndex = pageIndex;
    }

    public void SetRotation(PageIndex pageIndex, Rotation rotation)
    {
        _rotations[pageIndex.Value] = rotation;
    }

    public void Clear()
    {
        _rotations.Clear();
        ActivePageIndex = new PageIndex(0);
    }
}
