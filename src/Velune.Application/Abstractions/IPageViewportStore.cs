using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Abstractions;

/// <summary>Tracks per-page viewport state such as rotation and active page.</summary>
public interface IPageViewportStore
{
    /// <summary>Gets the index of the currently active page.</summary>
    PageIndex ActivePageIndex
    {
        get;
    }

    /// <summary>Initializes the store for the specified number of pages.</summary>
    /// <param name="pageCount">The total number of pages in the document.</param>
    void Initialize(int pageCount);

    /// <summary>Gets the rotation applied to the specified page.</summary>
    /// <param name="pageIndex">The page index to query.</param>
    /// <returns>The rotation for the page.</returns>
    Rotation GetRotation(PageIndex pageIndex);

    /// <summary>Sets the currently active page.</summary>
    /// <param name="pageIndex">The page index to activate.</param>
    void SetActivePage(PageIndex pageIndex);

    /// <summary>Sets the rotation for the specified page.</summary>
    /// <param name="pageIndex">The page index to rotate.</param>
    /// <param name="rotation">The rotation to apply.</param>
    void SetRotation(PageIndex pageIndex, Rotation rotation);

    /// <summary>Resets all page viewport state.</summary>
    void Clear();
}
