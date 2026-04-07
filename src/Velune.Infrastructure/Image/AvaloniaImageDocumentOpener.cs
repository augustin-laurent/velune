using Avalonia.Media.Imaging;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Infrastructure.Image;

public sealed class AvaloniaImageDocumentOpener
{
    public IDocumentSession Open(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The image file does not exist.", filePath);
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
        {
            throw new NotSupportedException($"Unsupported image file type: {extension}");
        }

        var fileBytes = File.ReadAllBytes(filePath);

        using var sourceStream = new MemoryStream(fileBytes);
        var bitmap = new Bitmap(sourceStream);

        var fileInfo = new FileInfo(filePath);
        var imageMetadata = new ImageMetadata(bitmap.PixelSize.Width, bitmap.PixelSize.Height);

        var metadata = new DocumentMetadata(
            fileName: fileInfo.Name,
            filePath: fileInfo.FullName,
            documentType: DocumentType.Image,
            fileSizeInBytes: fileInfo.Length,
            pageCount: 1);

        var resource = new ImageDocumentResource(fileBytes, bitmap);

        return new ImageDocumentSession(
            id: DocumentId.New(),
            metadata: metadata,
            viewport: ViewportState.Default,
            imageMetadata: imageMetadata,
            resource: resource);
    }
}
