using Velune.Application.Abstractions;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Infrastructure.Pdf;

public sealed record PdfiumDocumentSession : IReleasableDocumentSession
{
    internal PdfiumDocumentSession(
        DocumentId id,
        DocumentMetadata metadata,
        ViewportState viewport,
        PdfiumDocumentResource resource)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(resource);

        Id = id;
        Metadata = metadata;
        Viewport = viewport;
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

    internal PdfiumDocumentResource Resource
    {
        get;
    }

    public IDocumentSession WithViewport(ViewportState viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        return new PdfiumDocumentSession(
            Id,
            Metadata,
            viewport,
            Resource);
    }

    public void ReleaseResources()
    {
        Resource.Dispose();
    }
}
