namespace Velune.Infrastructure.Pdf;

internal sealed class PdfiumDocumentResource : IDisposable
{
    private int _disposed;
    private nint _handle;

    public PdfiumDocumentResource(nint handle, int pageCount)
    {
        if (handle == nint.Zero)
        {
            throw new ArgumentException("PDFium document handle cannot be zero.", nameof(handle));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(pageCount);

        _handle = handle;
        PageCount = pageCount;
    }

    public nint Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
            return _handle;
        }
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

        var handle = Interlocked.Exchange(ref _handle, nint.Zero);
        if (handle != nint.Zero)
        {
            PdfiumNative.FPDF_CloseDocument(handle);
        }

        GC.SuppressFinalize(this);
    }

    ~PdfiumDocumentResource()
    {
        Dispose();
    }
}
