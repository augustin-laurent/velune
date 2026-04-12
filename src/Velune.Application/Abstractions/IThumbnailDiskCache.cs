using Velune.Application.DTOs;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;

namespace Velune.Application.Abstractions;

public interface IThumbnailDiskCache
{
    bool TryGet(
        IDocumentSession session,
        RenderRequest request,
        out RenderedPage? renderedPage);

    void Store(
        IDocumentSession session,
        RenderRequest request,
        RenderedPage renderedPage);
}
