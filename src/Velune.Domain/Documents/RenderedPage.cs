using Velune.Domain.ValueObjects;

namespace Velune.Domain.Documents;

public sealed record RenderedPage
{
    public PageIndex PageIndex
    {
        get; init;
    }
    public byte[] PixelData
    {
        get; init;
    }
    public int Width
    {
        get; init;
    }
    public int Height
    {
        get; init;
    }

    public RenderedPage(
        PageIndex pageIndex,
        byte[] pixelData,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(pixelData);

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
        }

        PageIndex = pageIndex;
        PixelData = pixelData;
        Width = width;
        Height = height;
    }
}
