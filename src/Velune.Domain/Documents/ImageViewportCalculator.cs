using Velune.Domain.ValueObjects;

namespace Velune.Domain.Documents;

/// <summary>
/// Calculates zoom factors to fit an image within available viewport dimensions.
/// </summary>
public static class ImageViewportCalculator
{
    /// <summary>
    /// Calculates the zoom factor that fits the entire image within the available area.
    /// </summary>
    /// <param name="imageMetadata">Image dimensions.</param>
    /// <param name="rotation">Current rotation applied to the image.</param>
    /// <param name="availableWidth">Available viewport width in pixels.</param>
    /// <param name="availableHeight">Available viewport height in pixels.</param>
    /// <returns>Zoom factor where 1.0 means the image is shown at native size.</returns>
    public static double CalculateFitZoom(
        ImageMetadata imageMetadata,
        Rotation rotation,
        double availableWidth,
        double availableHeight)
    {
        ArgumentNullException.ThrowIfNull(imageMetadata);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(availableWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(availableHeight);

        if (imageMetadata.Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageMetadata), "Image width must be greater than zero.");
        }

        if (imageMetadata.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageMetadata), "Image height must be greater than zero.");
        }

        var imageWidth = rotation is Rotation.Deg90 or Rotation.Deg270
            ? imageMetadata.Height
            : imageMetadata.Width;

        var imageHeight = rotation is Rotation.Deg90 or Rotation.Deg270
            ? imageMetadata.Width
            : imageMetadata.Height;

        return Math.Min(availableWidth / imageWidth, availableHeight / imageHeight);
    }

    /// <summary>
    /// Calculates the zoom factor that fits the image width to the available width.
    /// </summary>
    /// <param name="imageMetadata">Image dimensions.</param>
    /// <param name="rotation">Current rotation applied to the image.</param>
    /// <param name="availableWidth">Available viewport width in pixels.</param>
    /// <returns>Zoom factor that makes the image fill the horizontal space.</returns>
    public static double CalculateFitWidthZoom(
        ImageMetadata imageMetadata,
        Rotation rotation,
        double availableWidth)
    {
        ArgumentNullException.ThrowIfNull(imageMetadata);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(availableWidth);

        if (imageMetadata.Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageMetadata), "Image width must be greater than zero.");
        }

        if (imageMetadata.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageMetadata), "Image height must be greater than zero.");
        }

        var imageWidth = rotation is Rotation.Deg90 or Rotation.Deg270
            ? imageMetadata.Height
            : imageMetadata.Width;

        return availableWidth / imageWidth;
    }
}
