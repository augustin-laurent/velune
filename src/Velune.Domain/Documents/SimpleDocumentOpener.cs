using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Infrastructure.Documents;

public sealed class SimpleDocumentOpener : IDocumentOpener
{
    public Task<IDocumentSession> OpenAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        var documentType = extension switch
        {
            ".pdf" => DocumentType.Pdf,
            ".png" or ".jpg" or ".jpeg" or ".webp" => DocumentType.Image,
            _ => DocumentType.Unknown
        };

        if (documentType is DocumentType.Unknown)
        {
            throw new NotSupportedException($"Unsupported file type: {extension}");
        }

        var fileInfo = new FileInfo(filePath);

        var metadata = new DocumentMetadata(
            fileName: fileInfo.Name,
            filePath: fileInfo.FullName,
            documentType: documentType,
            fileSizeInBytes: fileInfo.Exists ? fileInfo.Length : 0,
            pageCount: documentType == DocumentType.Pdf ? 1 : null);

        IDocumentSession session = new DocumentSession(
            id: DocumentId.New(),
            metadata: metadata,
            viewport: ViewportState.Default);

        return Task.FromResult(session);
    }
}
