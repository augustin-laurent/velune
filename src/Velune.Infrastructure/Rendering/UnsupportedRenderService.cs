using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Infrastructure.Rendering;

public sealed class UnsupportedRenderService : IRenderService
{
    public Task<RenderedPage> RenderPageAsync(
        IDocumentSession session,
        PageIndex pageIndex,
        double zoomFactor,
        Rotation rotation,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Page rendering is not implemented yet.");
    }
}
