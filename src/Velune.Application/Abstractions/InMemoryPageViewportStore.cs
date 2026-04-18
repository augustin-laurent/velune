using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Abstractions;

public sealed class InMemoryPageViewportStore : IPageViewportStore
{
    private readonly Dictionary<int, Rotation> _rotations = [];
    private double _globalZoomFactor = 1.0;

    public PageIndex ActivePageIndex { get; private set; } = new(0);

    public void Initialize(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageCount);

        _rotations.Clear();
        _globalZoomFactor = 1.0;

        for (var i = 0; i < pageCount; i++)
        {
            _rotations[i] = Rotation.Deg0;
        }

        ActivePageIndex = new PageIndex(0);
    }

    public PageViewportState GetPageState(PageIndex pageIndex)
    {
        if (_rotations.TryGetValue(pageIndex.Value, out var rotation))
        {
            return new PageViewportState(pageIndex, _globalZoomFactor, rotation);
        }

        _rotations[pageIndex.Value] = Rotation.Deg0;
        return new PageViewportState(pageIndex, _globalZoomFactor, Rotation.Deg0);
    }

    public void SetActivePage(PageIndex pageIndex)
    {
        ActivePageIndex = pageIndex;
    }

    public void SetPageState(PageViewportState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _globalZoomFactor = state.ZoomFactor;
        _rotations[state.PageIndex.Value] = state.Rotation;
    }

    public void Clear()
    {
        _rotations.Clear();
        _globalZoomFactor = 1.0;
        ActivePageIndex = new PageIndex(0);
    }
}
