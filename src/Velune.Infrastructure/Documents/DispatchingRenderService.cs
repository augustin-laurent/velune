using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Infrastructure.Image;
using Velune.Infrastructure.Pdf;

namespace Velune.Infrastructure.Documents;

/// <summary>
/// Routes page render requests to the appropriate format-specific render service.
/// </summary>
public sealed class DispatchingRenderService : IRenderService
{
    private readonly PdfiumRenderService _pdfiumRenderService;
    private readonly ImageRenderService _imageRenderService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchingRenderService"/> class.
    /// </summary>
    /// <param name="pdfiumRenderService">Render service for PDF documents.</param>
    /// <param name="imageRenderService">Render service for image documents.</param>
    public DispatchingRenderService(
        PdfiumRenderService pdfiumRenderService,
        ImageRenderService imageRenderService)
    {
        ArgumentNullException.ThrowIfNull(pdfiumRenderService);
        ArgumentNullException.ThrowIfNull(imageRenderService);

        _pdfiumRenderService = pdfiumRenderService;
        _imageRenderService = imageRenderService;
    }

    /// <inheritdoc />
    public Task<RenderedPage> RenderPageAsync(
        IDocumentSession session,
        PageIndex pageIndex,
        double zoomFactor,
        Rotation rotation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        return session.Metadata.DocumentType switch
        {
            DocumentType.Pdf => _pdfiumRenderService.RenderPageAsync(
                session,
                pageIndex,
                zoomFactor,
                rotation,
                cancellationToken),

            DocumentType.Image => _imageRenderService.RenderPageAsync(
                session,
                pageIndex,
                zoomFactor,
                rotation,
                cancellationToken),

            _ => throw new NotSupportedException(
                $"Unsupported document type for rendering: {session.Metadata.DocumentType}")
        };
    }
}
