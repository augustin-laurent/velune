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

        var metadata = new DocumentMetadata(
            fileName: fileInfo.Name,
            filePath: fileInfo.FullName,
            documentType: DocumentType.Pdf,
            fileSizeInBytes: fileInfo.Length,
            pageCount: pageCount);

        var resource = new PdfiumDocumentResource(handle, pageCount);

        return new PdfiumDocumentSession(
            id: DocumentId.New(),
            metadata: metadata,
            viewport: ViewportState.Default,
            resource: resource);
    }
}
