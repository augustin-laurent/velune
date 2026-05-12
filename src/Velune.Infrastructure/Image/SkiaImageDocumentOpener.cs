using SkiaSharp;
using Velune.Application.Documents;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Infrastructure.Image;

/// <summary>
/// Opens image files by decoding them with SkiaSharp and creating a session with pixel metadata.
/// </summary>
public sealed class SkiaImageDocumentOpener
{
    /// <summary>
    /// Opens an image file and returns a fully initialized document session.
    /// </summary>
    /// <param name="filePath">Absolute path to the image file.</param>
    /// <returns>A document session containing the decoded image data.</returns>
    public IDocumentSession Open(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The image file does not exist.", filePath);
        }

        string extension = Path.GetExtension(filePath);

        if (!SupportedDocumentFormats.IsImage(extension))
        {
            throw new NotSupportedException($"Unsupported image file type: {extension}");
        }

        const long maxFileSizeBytes = 500 * 1024 * 1024;
        long fileLength = new FileInfo(filePath).Length;
        if (fileLength > maxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"The image file exceeds the maximum supported size of 500 MB ({fileLength / (1024 * 1024)} MB).");
        }

        byte[] fileBytes = File.ReadAllBytes(filePath);

        using SKBitmap? decodedBitmap = SKBitmap.Decode(fileBytes);
        if (decodedBitmap is null)
        {
            throw new InvalidOperationException("Unable to decode the image file.");
        }

        int pixelWidth = decodedBitmap.Width;
        int pixelHeight = decodedBitmap.Height;

        const long maxDecodedPixels = 100_000_000;
        if ((long)pixelWidth * pixelHeight > maxDecodedPixels)
        {
            throw new InvalidOperationException(
                $"The decoded image dimensions ({pixelWidth}x{pixelHeight}) exceed the maximum of 100 megapixels.");
        }

        FileInfo fileInfo = new FileInfo(filePath);
        ImageMetadata imageMetadata = new ImageMetadata(pixelWidth, pixelHeight);

        DocumentMetadata metadata = new DocumentMetadata(
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

        ImageDocumentResource resource = new ImageDocumentResource(fileBytes);

        return new ImageDocumentSession(
            id: DocumentId.New(),
            metadata: metadata,
            viewport: ViewportState.Default,
            imageMetadata: imageMetadata,
            resource: resource);
    }
}
