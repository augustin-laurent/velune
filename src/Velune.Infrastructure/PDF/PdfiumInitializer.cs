namespace Velune.Infrastructure.Pdf;

public sealed class PdfiumInitializer : IDisposable
{
    private int _initialized;
    private int _disposed;

    public void EnsureInitialized()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        PdfiumNative.FPDF_InitLibrary();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        if (Interlocked.Exchange(ref _initialized, 0) == 1)
        {
            PdfiumNative.FPDF_DestroyLibrary();
        }
    }
}
