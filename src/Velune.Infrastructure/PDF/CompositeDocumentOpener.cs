using Velune.Domain.Abstractions;
using Velune.Infrastructure.Documents;

namespace Velune.Infrastructure.Pdf;

public sealed class CompositeDocumentOpener : IDocumentOpener
{
    private readonly PdfiumDocumentOpener _pdfiumDocumentOpener;
    private readonly SimpleImageDocumentOpener _simpleImageDocumentOpener;

    public CompositeDocumentOpener(
        PdfiumDocumentOpener pdfiumDocumentOpener,
        SimpleImageDocumentOpener simpleImageDocumentOpener)
    {
        ArgumentNullException.ThrowIfNull(pdfiumDocumentOpener);
        ArgumentNullException.ThrowIfNull(simpleImageDocumentOpener);

        _pdfiumDocumentOpener = pdfiumDocumentOpener;
        _simpleImageDocumentOpener = simpleImageDocumentOpener;
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
            ".png" or ".jpg" or ".jpeg" or ".webp" => _simpleImageDocumentOpener.Open(filePath),
            _ => throw new NotSupportedException($"Unsupported document file type: {extension}")
        };

        return Task.FromResult(session);
    }
}
