using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Velune.Domain.Documents;

namespace Velune.Presentation.Imaging;

public static class RenderedPageBitmapFactory
{
    public static WriteableBitmap Create(RenderedPage renderedPage)
    {
        ArgumentNullException.ThrowIfNull(renderedPage);

        var bitmap = new WriteableBitmap(
            new PixelSize(renderedPage.Width, renderedPage.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);

        using var framebuffer = bitmap.Lock();

        renderedPage.CopyPixelDataTo(framebuffer.Address);

        return bitmap;
    }
}
