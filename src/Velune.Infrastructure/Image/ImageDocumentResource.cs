using Avalonia.Media.Imaging;

namespace Velune.Infrastructure.Image;

internal sealed class ImageDocumentResource : IDisposable
{
    private bool _disposed;

    public ImageDocumentResource(byte[] fileBytes, Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(fileBytes);
        ArgumentNullException.ThrowIfNull(bitmap);

        FileBytes = fileBytes;
        Bitmap = bitmap;
    }

    public byte[] FileBytes
    {
        get;
    }

    public Bitmap Bitmap
    {
        get; private set;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            Bitmap.Dispose();
        }

        _disposed = true;
    }
}
