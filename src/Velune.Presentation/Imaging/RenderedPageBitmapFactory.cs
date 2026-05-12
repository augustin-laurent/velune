using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Velune.Domain.Documents;

namespace Velune.Presentation.Imaging;

/// <summary>
/// Factory that converts a <see cref="RenderedPage"/> into an Avalonia <see cref="WriteableBitmap"/>.
/// </summary>
public static class RenderedPageBitmapFactory
{
    /// <summary>
    /// Creates a writeable bitmap from a rendered page's pixel data.
    /// </summary>
    /// <param name="renderedPage">The rendered page to convert.</param>
    /// <returns>A writeable bitmap containing the page pixels.</returns>
    public static WriteableBitmap Create(RenderedPage renderedPage)
    {
        ArgumentNullException.ThrowIfNull(renderedPage);

        var bitmap = new WriteableBitmap(
            new PixelSize(renderedPage.Width, renderedPage.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);

        using ILockedFramebuffer framebuffer = bitmap.Lock();

        renderedPage.CopyPixelDataTo(framebuffer.Address);

        return bitmap;
    }
}
