using Velune.Domain.Abstractions;
using Velune.Domain.ValueObjects;

namespace Velune.Domain.Documents;

public sealed record DocumentSession : IDocumentSession
{
    public DocumentSession(
        DocumentId id,
        DocumentMetadata metadata,
        ViewportState viewport)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(viewport);

        Id = id;
        Metadata = metadata;
        Viewport = viewport;
    }

    public DocumentId Id
    {
        get;
    }
    public DocumentMetadata Metadata
    {
        get;
    }
    public ViewportState Viewport
    {
        get;
    }

    public IDocumentSession WithViewport(ViewportState viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        return new DocumentSession(
            Id,
            Metadata,
            viewport);
    }
}
