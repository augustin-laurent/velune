using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;

namespace Velune.Application.Rendering;

public sealed class NullThumbnailDiskCache : IThumbnailDiskCache
{
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
