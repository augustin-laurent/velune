namespace Velune.Infrastructure.Pdf;

internal sealed class PdfiumDocumentResource : IDisposable
{
    private int _disposed;

    public PdfiumDocumentResource(nint handle, int pageCount)
    {
        if (handle == nint.Zero)
        {
            throw new ArgumentException("PDFium document handle cannot be zero.", nameof(handle));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(pageCount);

        Handle = handle;
        PageCount = pageCount;
    }

    public nint Handle
    {
        get; private set;
    }

    public int PageCount
    {
        get;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        if (Handle != nint.Zero)
        {
            PdfiumNative.FPDF_CloseDocument(Handle);
            Handle = nint.Zero;
        }

        GC.SuppressFinalize(this);
    }

    ~PdfiumDocumentResource()
    {
        Dispose();
    }
}
