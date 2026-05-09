namespace Velune.Infrastructure.Pdf;

/// <summary>
/// Manages the one-time initialization and teardown of the PDFium native library.
/// </summary>
public sealed class PdfiumInitializer : IDisposable
{
    private int _initialized;
    private int _disposed;

    /// <summary>
    /// Initializes the PDFium library if it has not been initialized yet.
    /// </summary>
    public void EnsureInitialized()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        PdfiumNative.FPDF_InitLibrary();
    }

    /// <inheritdoc />
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
