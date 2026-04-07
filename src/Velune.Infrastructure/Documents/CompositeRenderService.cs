using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Infrastructure.Image;
using Velune.Infrastructure.Pdf;

namespace Velune.Infrastructure.Documents;

public sealed class CompositeRenderService : IRenderService
{
    private readonly PdfiumRenderService _pdfiumRenderService;
    private readonly ImageRenderService _imageRenderService;

    public CompositeRenderService(
        PdfiumRenderService pdfiumRenderService,
        ImageRenderService imageRenderService)
    {
        ArgumentNullException.ThrowIfNull(pdfiumRenderService);
        ArgumentNullException.ThrowIfNull(imageRenderService);

        _pdfiumRenderService = pdfiumRenderService;
        _imageRenderService = imageRenderService;
    }

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
