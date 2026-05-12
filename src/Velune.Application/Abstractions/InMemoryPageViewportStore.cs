using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Abstractions;

/// <summary>In-memory implementation of <see cref="IPageViewportStore"/>.</summary>
public sealed class InMemoryPageViewportStore : IPageViewportStore
{
    private readonly object _gate = new();
    private readonly Dictionary<int, Rotation> _rotations = [];

    public PageIndex ActivePageIndex { get; private set; } = new(0);

    public void Initialize(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageCount);

        lock (_gate)
        {
            _rotations.Clear();

            for (int i = 0; i < pageCount; i++)
            {
                _rotations[i] = Rotation.Deg0;
            }

            ActivePageIndex = new PageIndex(0);
        }
    }

    public Rotation GetRotation(PageIndex pageIndex)
    {
        lock (_gate)
        {
            if (_rotations.TryGetValue(pageIndex.Value, out Rotation rotation))
            {
                return rotation;
            }

            _rotations[pageIndex.Value] = Rotation.Deg0;
            return Rotation.Deg0;
        }
    }

    public void SetActivePage(PageIndex pageIndex)
    {
        lock (_gate)
        {
            ActivePageIndex = pageIndex;
        }
    }

    public void SetRotation(PageIndex pageIndex, Rotation rotation)
    {
        lock (_gate)
        {
            _rotations[pageIndex.Value] = rotation;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _rotations.Clear();
            ActivePageIndex = new PageIndex(0);
        }
    }
}
