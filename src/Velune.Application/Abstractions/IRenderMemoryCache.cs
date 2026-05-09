using Velune.Application.DTOs;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Abstractions;

/// <summary>In-memory cache for rendered page bitmaps.</summary>
public interface IRenderMemoryCache
{
    /// <summary>Attempts to retrieve a cached rendered page.</summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="request">The render request used as cache key.</param>
    /// <param name="renderedPage">The cached page if found; otherwise null.</param>
    /// <returns>True if a cached entry was found; otherwise false.</returns>
    bool TryGet(
        DocumentId documentId,
        RenderRequest request,
        out RenderedPage? renderedPage);

    /// <summary>Stores a rendered page in the cache.</summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="request">The render request used as cache key.</param>
    /// <param name="renderedPage">The rendered page to cache.</param>
    void Store(
        DocumentId documentId,
        RenderRequest request,
        RenderedPage renderedPage);
}
