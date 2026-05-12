using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;

namespace Velune.Tests.Unit.Support;

internal sealed class NoOpThumbnailDiskCache : IThumbnailDiskCache
{
    public bool TryGet(
        IDocumentSession session,
        RenderRequest request,
        out RenderedPage? renderedPage)
    {
        renderedPage = null;
        return false;
    }

    public void Store(
        IDocumentSession session,
        RenderRequest request,
        RenderedPage renderedPage)
    {
    }
}
