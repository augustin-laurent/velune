using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Domain.Abstractions;

public interface IRenderService
{
    Task<RenderedPage> RenderPageAsync(
        IDocumentSession session,
        PageIndex pageIndex,
        double zoomFactor,
        Rotation rotation,
        CancellationToken cancellationToken = default);
}
