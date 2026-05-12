using System.Runtime.InteropServices;
using Velune.Domain.ValueObjects;

namespace Velune.Domain.Documents;

/// <summary>
/// A rendered page stored as raw BGRA pixel data.
/// </summary>
public sealed class RenderedPage
{
    private readonly byte[] _pixelData;

    public PageIndex PageIndex
    {
        get;
    }

    /// <summary>
    /// Raw pixel data in BGRA format.
    /// </summary>
    public ReadOnlyMemory<byte> PixelData
    {
        get;
    }

    /// <summary>
    /// Rendered width in pixels.
    /// </summary>
    public int Width
    {
        get;
    }

    /// <summary>
    /// Rendered height in pixels.
    /// </summary>
    public int Height
    {
        get;
    }

    /// <summary>
    /// Total byte count of the pixel data buffer.
    /// </summary>
    public int ByteCount => _pixelData.Length;

    /// <summary>
    /// Creates a rendered page from raw pixel data.
    /// </summary>
    /// <param name="pageIndex">Page index that was rendered.</param>
    /// <param name="pixelData">Raw BGRA pixel data.</param>
    /// <param name="width">Rendered width in pixels.</param>
    /// <param name="height">Rendered height in pixels.</param>
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

    /// <summary>
    /// Copies the pixel data to an unmanaged memory address.
    /// </summary>
    /// <param name="destination">Target memory address (must not be zero).</param>
    public void CopyPixelDataTo(nint destination)
    {
        if (destination == nint.Zero)
        {
            throw new ArgumentException("Destination address cannot be zero.", nameof(destination));
        }

        Marshal.Copy(_pixelData, 0, destination, _pixelData.Length);
    }
}
