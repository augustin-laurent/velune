using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Infrastructure.Rendering;

public sealed class UnsupportedThumbnailService : IThumbnailService
{
    public Task<RenderedPage> GenerateThumbnailAsync(
        IDocumentSession session,
        PageIndex pageIndex,
        int maxWidth,
        int maxHeight,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Thumbnail generation is not implemented yet.");
    }
}
