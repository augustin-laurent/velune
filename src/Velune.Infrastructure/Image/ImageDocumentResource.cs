namespace Velune.Infrastructure.Image;

/// <summary>
/// Holds the raw byte content of an opened image document.
/// </summary>
internal sealed class ImageDocumentResource : IDisposable
{
    private bool _disposed;

    public ImageDocumentResource(byte[] fileBytes)
    {
        ArgumentNullException.ThrowIfNull(fileBytes);

        FileBytes = fileBytes;
    }

    public byte[] FileBytes
    {
        get;
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
        }

        _disposed = true;
    }
}
