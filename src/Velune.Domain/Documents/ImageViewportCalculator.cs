using Velune.Domain.ValueObjects;

namespace Velune.Domain.Documents;

public static class ImageViewportCalculator
{
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
