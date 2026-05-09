using Velune.Application.Abstractions;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Infrastructure.Image;

/// <summary>
/// Document session backed by an in-memory decoded image.
/// </summary>
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

    /// <inheritdoc />
    public DocumentId Id
    {
        get;
    }

    /// <inheritdoc />
    public DocumentMetadata Metadata
    {
        get;
    }

    /// <inheritdoc />
    public ViewportState Viewport
    {
        get;
    }

    /// <inheritdoc />
    public ImageMetadata ImageMetadata
    {
        get;
    }

    internal ImageDocumentResource Resource
    {
        get;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void ReleaseResources()
    {
        Resource.Dispose();
    }
}
