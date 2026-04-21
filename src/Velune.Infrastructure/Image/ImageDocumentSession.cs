using Velune.Application.Abstractions;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Infrastructure.Image;

public sealed record ImageDocumentSession : IImageDocumentSession, IReleasableDocumentSession
{
    internal ImageDocumentSession(
        DocumentId id,
        DocumentMetadata metadata,
        ViewportState viewport,
        ImageMetadata imageMetadata,
        ImageDocumentResource resource)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(imageMetadata);
        ArgumentNullException.ThrowIfNull(resource);

        Id = id;
        Metadata = metadata;
        Viewport = viewport;
        ImageMetadata = imageMetadata;
        Resource = resource;
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

    public ImageMetadata ImageMetadata
    {
        get;
    }

    internal ImageDocumentResource Resource
    {
        get;
    }

    public IDocumentSession WithViewport(ViewportState viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        return new ImageDocumentSession(
            Id,
            Metadata,
            viewport,
            ImageMetadata,
            Resource);
    }

    public void ReleaseResources()
    {
        Resource.Dispose();
    }
}
