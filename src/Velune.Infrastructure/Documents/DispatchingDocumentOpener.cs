using Velune.Application.Documents;
using Velune.Domain.Abstractions;
using Velune.Infrastructure.Image;
using Velune.Infrastructure.Pdf;

namespace Velune.Infrastructure.Documents;

public sealed class DispatchingDocumentOpener : IDocumentOpener
{
    private readonly PdfiumDocumentOpener _pdfiumDocumentOpener;
    private readonly AvaloniaImageDocumentOpener _imageDocumentOpener;

    public DispatchingDocumentOpener(
        PdfiumDocumentOpener pdfiumDocumentOpener,
        AvaloniaImageDocumentOpener imageDocumentOpener)
    {
        ArgumentNullException.ThrowIfNull(pdfiumDocumentOpener);
        ArgumentNullException.ThrowIfNull(imageDocumentOpener);

        _pdfiumDocumentOpener = pdfiumDocumentOpener;
        _imageDocumentOpener = imageDocumentOpener;
    }

    public Task<IDocumentSession> OpenAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var extension = Path.GetExtension(filePath);

        IDocumentSession session = extension switch
        {
            var value when SupportedDocumentFormats.IsPdf(value) => _pdfiumDocumentOpener.Open(filePath),
            var value when SupportedDocumentFormats.IsImage(value) => _imageDocumentOpener.Open(filePath),
            _ => throw new NotSupportedException($"Unsupported document file type: {extension}")
        };

        return Task.FromResult(session);
    }
}
