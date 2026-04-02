using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Infrastructure.Documents;

public sealed class SimpleImageDocumentOpener
{
    public IDocumentSession Open(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The image file does not exist.", filePath);
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        var documentType = extension switch
        {
            ".png" or ".jpg" or ".jpeg" or ".webp" => DocumentType.Image,
            _ => DocumentType.Unknown
        };

        if (documentType is DocumentType.Unknown)
        {
            throw new NotSupportedException($"Unsupported image file type: {extension}");
        }

        var fileInfo = new FileInfo(filePath);

        var metadata = new DocumentMetadata(
            fileName: fileInfo.Name,
            filePath: fileInfo.FullName,
            documentType: documentType,
            fileSizeInBytes: fileInfo.Length);

        return new DocumentSession(
            id: DocumentId.New(),
            metadata: metadata,
            viewport: ViewportState.Default);
    }
}
