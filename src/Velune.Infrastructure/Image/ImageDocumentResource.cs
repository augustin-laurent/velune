using Avalonia.Media.Imaging;

namespace Velune.Infrastructure.Image;

internal sealed class ImageDocumentResource : IDisposable
{
    private bool _disposed;
    private Bitmap? _bitmap;

    public ImageDocumentResource(byte[] fileBytes, Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(fileBytes);
        ArgumentNullException.ThrowIfNull(bitmap);

        FileBytes = fileBytes;
        _bitmap = bitmap;
    }

    public byte[] FileBytes
    {
        get;
    }

    public Bitmap Bitmap
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _bitmap ?? throw new ObjectDisposedException(nameof(ImageDocumentResource));
        }
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
            _bitmap?.Dispose();
            _bitmap = null;
        }

        _disposed = true;
    }
}
