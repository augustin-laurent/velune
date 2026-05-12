using Velune.Application.Abstractions;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Infrastructure.Pdf;

/// <summary>
/// Document session backed by a PDFium-loaded PDF document.
/// </summary>
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

    internal PdfiumDocumentResource Resource
    {
        get;
    }

    /// <inheritdoc />
    public IDocumentSession WithViewport(ViewportState viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        return new PdfiumDocumentSession(
            Id,
            Metadata,
            viewport,
            Resource);
    }

    /// <inheritdoc />
    public void ReleaseResources()
    {
        Resource.Dispose();
    }
}
