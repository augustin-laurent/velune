using Velune.Application.DTOs;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Abstractions;

public interface IRenderMemoryCache
{
    bool TryGet(
        DocumentId documentId,
        RenderRequest request,
        out RenderedPage? renderedPage);

    void Store(
        DocumentId documentId,
        RenderRequest request,
        RenderedPage renderedPage);
}
