using System.Text;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Infrastructure.Pdf;

public sealed class PdfiumDocumentOpener
{
    private readonly PdfiumInitializer _initializer;

    public PdfiumDocumentOpener(PdfiumInitializer initializer)
    {
        ArgumentNullException.ThrowIfNull(initializer);
        _initializer = initializer;
    }

    public IDocumentSession Open(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The PDF file does not exist.", filePath);
        }

        _initializer.EnsureInitialized();

        var handle = PdfiumNative.FPDF_LoadDocument(filePath, null);
        if (handle == nint.Zero)
        {
            var error = PdfiumNative.FPDF_GetLastError();
            throw new InvalidOperationException(
                $"PDFium failed to open the PDF document. Error code: {error}.");
        }

        var pageCount = PdfiumNative.FPDF_GetPageCount(handle);
        var fileInfo = new FileInfo(filePath);
        var pdfMetadata = TryReadPdfMetadata(handle);

        var metadata = new DocumentMetadata(
            fileName: fileInfo.Name,
            filePath: fileInfo.FullName,
            documentType: DocumentType.Pdf,
            fileSizeInBytes: fileInfo.Length,
            pageCount: pageCount,
            formatLabel: "PDF document",
            createdAt: fileInfo.CreationTimeUtc,
            modifiedAt: fileInfo.LastWriteTimeUtc,
            documentTitle: pdfMetadata.Title,
            author: pdfMetadata.Author,
            creator: pdfMetadata.Creator,
            producer: pdfMetadata.Producer,
            detailsWarning: pdfMetadata.WarningMessage);

        var resource = new PdfiumDocumentResource(handle, pageCount);

        return new PdfiumDocumentSession(
            id: DocumentId.New(),
            metadata: metadata,
            viewport: ViewportState.Default,
            resource: resource);
    }

    private static PdfDetails TryReadPdfMetadata(nint handle)
    {
        try
        {
            return new PdfDetails(
                ReadMetaText(handle, "Title"),
                ReadMetaText(handle, "Author"),
                ReadMetaText(handle, "Creator"),
                ReadMetaText(handle, "Producer"),
                null);
        }
        catch
        {
            return new PdfDetails(
                null,
                null,
                null,
                null,
                "Some document details are unavailable.");
        }
    }

    private static string? ReadMetaText(nint handle, string tag)
    {
        byte[] buffer = new byte[512];
        var length = PdfiumNative.FPDF_GetMetaText(handle, tag, buffer, (uint)buffer.Length);

        if (length <= 2)
        {
            return null;
        }

        if (length > buffer.Length)
        {
            buffer = new byte[length];
            length = PdfiumNative.FPDF_GetMetaText(handle, tag, buffer, (uint)buffer.Length);

            if (length <= 2)
            {
                return null;
            }
        }

        var value = Encoding.Unicode.GetString(buffer, 0, (int)length - 2).Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed record PdfDetails(
        string? Title,
        string? Author,
        string? Creator,
        string? Producer,
        string? WarningMessage);
}
