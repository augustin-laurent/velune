using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;

namespace Velune.Application.Rendering;

/// <summary>A no-op thumbnail disk cache that never caches or returns entries.</summary>
public sealed class NullThumbnailDiskCache : IThumbnailDiskCache
{
    /// <inheritdoc />
    public bool TryGet(
        IDocumentSession session,
        RenderRequest request,
        out RenderedPage? renderedPage)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        renderedPage = null;
        return false;
    }

    /// <inheritdoc />
    public void Store(
        IDocumentSession session,
        RenderRequest request,
        RenderedPage renderedPage)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(renderedPage);
    }
}
