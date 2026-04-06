using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Abstractions;

public interface IPageViewportStore
{
    PageIndex ActivePageIndex
    {
        get;
    }

    void Initialize(int pageCount);

    PageViewportState GetPageState(PageIndex pageIndex);

    void SetActivePage(PageIndex pageIndex);

    void SetPageState(PageViewportState state);

    void Clear();
}
