using System.Runtime.InteropServices;
using Velune.Domain.ValueObjects;

namespace Velune.Domain.Documents;

public sealed class RenderedPage
{
    private readonly byte[] _pixelData;

    public PageIndex PageIndex
    {
        get;
    }
    public ReadOnlyMemory<byte> PixelData
    {
        get;
    }
    public int Width
    {
        get;
    }
    public int Height
    {
        get;
    }
    public int ByteCount => _pixelData.Length;

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
        _pixelData = [.. pixelData];
        PixelData = _pixelData;
        Width = width;
        Height = height;
    }

    public void CopyPixelDataTo(nint destination)
    {
        if (destination == nint.Zero)
        {
            throw new ArgumentException("Destination address cannot be zero.", nameof(destination));
        }

        Marshal.Copy(_pixelData, 0, destination, _pixelData.Length);
    }
}
