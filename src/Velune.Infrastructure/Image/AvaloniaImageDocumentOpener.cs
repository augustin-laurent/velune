using Avalonia.Media.Imaging;
using SkiaSharp;
using Velune.Application.Documents;
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

        var extension = Path.GetExtension(filePath);

        if (!SupportedDocumentFormats.IsImage(extension))
        {
            throw new NotSupportedException($"Unsupported image file type: {extension}");
        }

        var fileBytes = File.ReadAllBytes(filePath);

        using var sourceStream = new MemoryStream(fileBytes);
        var bitmap = new Bitmap(sourceStream);
        using var decodedBitmap = SKBitmap.Decode(fileBytes);

        var pixelWidth = decodedBitmap?.Width ?? bitmap.PixelSize.Width;
        var pixelHeight = decodedBitmap?.Height ?? bitmap.PixelSize.Height;

        var fileInfo = new FileInfo(filePath);
        var imageMetadata = new ImageMetadata(pixelWidth, pixelHeight);

        var metadata = new DocumentMetadata(
            fileName: fileInfo.Name,
            filePath: fileInfo.FullName,
            documentType: DocumentType.Image,
            fileSizeInBytes: fileInfo.Length,
            pageCount: 1,
            pixelWidth: pixelWidth,
            pixelHeight: pixelHeight,
            formatLabel: SupportedDocumentFormats.GetImageFormatLabel(extension),
            createdAt: fileInfo.CreationTimeUtc,
            modifiedAt: fileInfo.LastWriteTimeUtc);

        var resource = new ImageDocumentResource(fileBytes, bitmap);

        return new ImageDocumentSession(
            id: DocumentId.New(),
            metadata: metadata,
            viewport: ViewportState.Default,
            imageMetadata: imageMetadata,
            resource: resource);
    }
}
