namespace Velune.Infrastructure.Image;

/// <summary>
/// Holds the raw byte content of an opened image document.
/// </summary>
internal sealed class ImageDocumentResource
{
    public ImageDocumentResource(byte[] fileBytes)
    {
        ArgumentNullException.ThrowIfNull(fileBytes);

        FileBytes = fileBytes;
    }

    public byte[] FileBytes
    {
        get;
    }
}
