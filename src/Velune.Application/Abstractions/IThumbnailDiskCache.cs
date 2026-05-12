using Velune.Application.DTOs;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;

namespace Velune.Application.Abstractions;

/// <summary>Disk-based cache for rendered thumbnail images.</summary>
public interface IThumbnailDiskCache
{
    /// <summary>Attempts to retrieve a cached thumbnail from disk.</summary>
    /// <param name="session">The document session.</param>
    /// <param name="request">The render request used as cache key.</param>
    /// <param name="renderedPage">The cached rendered page if found; otherwise null.</param>
    /// <returns>True if a cached entry was found; otherwise false.</returns>
    bool TryGet(
        IDocumentSession session,
        RenderRequest request,
        out RenderedPage? renderedPage);

    /// <summary>Stores a rendered thumbnail to disk.</summary>
    /// <param name="session">The document session.</param>
    /// <param name="request">The render request used as cache key.</param>
    /// <param name="renderedPage">The rendered page to persist.</param>
    void Store(
        IDocumentSession session,
        RenderRequest request,
        RenderedPage renderedPage);
}
