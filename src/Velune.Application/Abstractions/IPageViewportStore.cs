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

    Rotation GetRotation(PageIndex pageIndex);

    void SetActivePage(PageIndex pageIndex);

    void SetRotation(PageIndex pageIndex, Rotation rotation);

    void Clear();
}
