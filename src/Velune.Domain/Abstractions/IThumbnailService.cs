using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Domain.Abstractions;

public interface IThumbnailService
{
    Task<RenderedPage> GenerateThumbnailAsync(
        IDocumentSession session,
        PageIndex pageIndex,
        int maxWidth,
        int maxHeight,
        CancellationToken cancellationToken = default);
}
