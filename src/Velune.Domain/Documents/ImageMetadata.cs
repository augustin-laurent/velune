namespace Velune.Domain.Documents;

/// <summary>
/// Pixel dimensions of an image document.
/// </summary>
public sealed record ImageMetadata
{
    /// <summary>
    /// Creates image metadata with the given pixel dimensions.
    /// </summary>
    /// <param name="width">Image width in pixels (must be positive).</param>
    /// <param name="height">Image height in pixels (must be positive).</param>
    public ImageMetadata(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
    }

    public int Width
    {
        get;
    }

    public int Height
    {
        get;
    }
}
