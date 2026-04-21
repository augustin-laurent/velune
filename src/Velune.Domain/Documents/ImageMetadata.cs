namespace Velune.Domain.Documents;

public sealed record ImageMetadata
{
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
