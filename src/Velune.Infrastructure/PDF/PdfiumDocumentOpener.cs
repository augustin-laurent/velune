using System.Text;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Infrastructure.Pdf;

/// <summary>
/// Opens PDF documents using the PDFium native library.
/// </summary>
public sealed class PdfiumDocumentOpener
{
    private readonly PdfiumInitializer _initializer;
    private readonly PdfiumExecutionGate _executionGate;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfiumDocumentOpener"/> class.
    /// </summary>
    /// <param name="initializer">Ensures PDFium is initialized before use.</param>
    /// <param name="executionGate">Serializes access to the single-threaded PDFium library.</param>
    public PdfiumDocumentOpener(PdfiumInitializer initializer, PdfiumExecutionGate executionGate)
    {
        ArgumentNullException.ThrowIfNull(initializer);
        ArgumentNullException.ThrowIfNull(executionGate);

        _initializer = initializer;
        _executionGate = executionGate;
    }

    /// <summary>
    /// Opens a PDF file and returns a document session with page metadata.
    /// </summary>
    /// <param name="filePath">Absolute path to the PDF file.</param>
    /// <returns>A document session backed by a PDFium document handle.</returns>
    public IDocumentSession Open(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The PDF file does not exist.", filePath);
        }

        using IDisposable pdfiumAccess = _executionGate.Enter();
        _initializer.EnsureInitialized();

        nint handle = PdfiumNative.FPDF_LoadDocument(filePath, null);
        if (handle == nint.Zero)
        {
            uint error = PdfiumNative.FPDF_GetLastError();
            if (error is 4 or 5)
            {
                throw new InvalidOperationException(
                    "This PDF is password-protected. Velune does not support encrypted documents.");
            }

            throw new InvalidOperationException(
                $"PDFium failed to open the PDF document. Error code: {error}.");
        }

        int pageCount = PdfiumNative.FPDF_GetPageCount(handle);
        var fileInfo = new FileInfo(filePath);
        PdfDetails pdfMetadata = TryReadPdfMetadata(handle);

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
        uint length = PdfiumNative.FPDF_GetMetaText(handle, tag, buffer, (uint)buffer.Length);

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

        string value = Encoding.Unicode.GetString(buffer, 0, (int)length - 2).Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed record PdfDetails(
        string? Title,
        string? Author,
        string? Creator,
        string? Producer,
        string? WarningMessage);
}
