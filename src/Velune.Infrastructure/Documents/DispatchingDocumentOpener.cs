using Velune.Application.Documents;
using Velune.Domain.Abstractions;
using Velune.Infrastructure.Image;
using Velune.Infrastructure.Pdf;

namespace Velune.Infrastructure.Documents;

/// <summary>
/// Routes document open requests to the appropriate format-specific opener.
/// </summary>
public sealed class DispatchingDocumentOpener : IDocumentOpener
{
    private readonly PdfiumDocumentOpener _pdfiumDocumentOpener;
    private readonly SkiaImageDocumentOpener _imageDocumentOpener;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchingDocumentOpener"/> class.
    /// </summary>
    /// <param name="pdfiumDocumentOpener">Opener for PDF documents.</param>
    /// <param name="imageDocumentOpener">Opener for image documents.</param>
    public DispatchingDocumentOpener(
        PdfiumDocumentOpener pdfiumDocumentOpener,
        SkiaImageDocumentOpener imageDocumentOpener)
    {
        ArgumentNullException.ThrowIfNull(pdfiumDocumentOpener);
        ArgumentNullException.ThrowIfNull(imageDocumentOpener);

        _pdfiumDocumentOpener = pdfiumDocumentOpener;
        _imageDocumentOpener = imageDocumentOpener;
    }

    /// <inheritdoc />
    public Task<IDocumentSession> OpenAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string extension = Path.GetExtension(filePath);

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateMagicBytes(filePath, extension);

            IDocumentSession session = extension switch
            {
                { } value when SupportedDocumentFormats.IsPdf(value) => _pdfiumDocumentOpener.Open(filePath),
                { } value when SupportedDocumentFormats.IsImage(value) => _imageDocumentOpener.Open(filePath),
                _ => throw new NotSupportedException($"Unsupported document file type: {extension}")
            };

            cancellationToken.ThrowIfCancellationRequested();
            return session;
        }, cancellationToken);
    }

    private static void ValidateMagicBytes(string filePath, string extension)
    {
        Span<byte> header = stackalloc byte[8];
        using FileStream stream = File.OpenRead(filePath);
        int bytesRead = stream.Read(header);
        if (bytesRead < 4)
        {
            throw new InvalidOperationException("The file is too small to be a valid document.");
        }

        if (SupportedDocumentFormats.IsPdf(extension))
        {
            if (header[0] != '%' || header[1] != 'P' || header[2] != 'D' || header[3] != 'F')
            {
                throw new InvalidOperationException("The file does not appear to be a valid PDF (missing %PDF header).");
            }
        }
        else if (SupportedDocumentFormats.IsImage(extension))
        {
            bool isPng = bytesRead >= 4 && header[0] == 0x89 && header[1] == 'P' && header[2] == 'N' && header[3] == 'G';
            bool isJpeg = bytesRead >= 2 && header[0] == 0xFF && header[1] == 0xD8;
            bool isWebp = bytesRead >= 4 && header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F';
            bool isBmp = bytesRead >= 2 && header[0] == 'B' && header[1] == 'M';

            if (!isPng && !isJpeg && !isWebp && !isBmp)
            {
                throw new InvalidOperationException("The file content does not match any supported image format.");
            }
        }
    }
}
