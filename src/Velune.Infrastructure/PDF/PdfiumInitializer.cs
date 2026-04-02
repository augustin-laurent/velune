namespace Velune.Infrastructure.Pdf;

public sealed class PdfiumInitializer
{
    private int _initialized;

    public void EnsureInitialized()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        PdfiumNative.FPDF_InitLibrary();
    }
}
