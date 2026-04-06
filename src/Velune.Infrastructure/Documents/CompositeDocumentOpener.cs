using Velune.Domain.Abstractions;
using Velune.Infrastructure.Image;
using Velune.Infrastructure.Pdf;

namespace Velune.Infrastructure.Documents;

public sealed class CompositeDocumentOpener : IDocumentOpener
{
    private readonly PdfiumDocumentOpener _pdfiumDocumentOpener;
    private readonly AvaloniaImageDocumentOpener _imageDocumentOpener;

    public CompositeDocumentOpener(
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

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        IDocumentSession session = extension switch
        {
            ".pdf" => _pdfiumDocumentOpener.Open(filePath),
            ".jpg" or ".jpeg" or ".png" or ".webp" => _imageDocumentOpener.Open(filePath),
            _ => throw new NotSupportedException($"Unsupported document file type: {extension}")
        };

        return Task.FromResult(session);
    }
}
