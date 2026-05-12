using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using Velune.Domain.Documents;

namespace Velune.Windows.Services;

/// <summary>
/// Creates WinUI <see cref="WriteableBitmap"/> instances from rendered page pixel data.
/// </summary>
public static class WindowsBitmapFactory
{
    /// <summary>
    /// Creates a <see cref="WriteableBitmap"/> from the given rendered page.
    /// </summary>
    /// <param name="page">The rendered page containing pixel data and dimensions.</param>
    /// <returns>A bitmap ready for display in the UI.</returns>
    public static WriteableBitmap Create(RenderedPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        int expectedSize = page.Width * page.Height * 4;
        if (page.PixelData.Length != expectedSize)
        {
            throw new ArgumentException(
                $"PixelData length {page.PixelData.Length} does not match expected {expectedSize} (Width={page.Width}, Height={page.Height}).");
        }

        var bitmap = new WriteableBitmap(page.Width, page.Height);
        using (Stream stream = bitmap.PixelBuffer.AsStream())
        {
            stream.Seek(0, SeekOrigin.Begin);
            stream.Write(page.PixelData.Span);
            stream.Flush();
        }

        bitmap.Invalidate();
        return bitmap;
    }
}
