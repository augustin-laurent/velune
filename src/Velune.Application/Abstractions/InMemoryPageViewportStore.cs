using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Abstractions;

public sealed class InMemoryPageViewportStore : IPageViewportStore
{
    private readonly Dictionary<int, PageViewportState> _states = [];

    public PageIndex ActivePageIndex { get; private set; } = new(0);

    public void Initialize(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageCount);

        _states.Clear();

        for (var i = 0; i < pageCount; i++)
        {
            var pageIndex = new PageIndex(i);
            _states[i] = PageViewportState.Default(pageIndex);
        }

        ActivePageIndex = new PageIndex(0);
    }

    public PageViewportState GetPageState(PageIndex pageIndex)
    {

        if (_states.TryGetValue(pageIndex.Value, out var state))
        {
            return state;
        }

        var created = PageViewportState.Default(pageIndex);
        _states[pageIndex.Value] = created;
        return created;
    }

    public void SetActivePage(PageIndex pageIndex)
    {
        ActivePageIndex = pageIndex;
    }

    public void SetPageState(PageViewportState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _states[state.PageIndex.Value] = state;
    }

    public void Clear()
    {
        _states.Clear();
        ActivePageIndex = new PageIndex(0);
    }
}
