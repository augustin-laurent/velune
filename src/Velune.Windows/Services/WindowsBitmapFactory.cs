using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using Velune.Domain.Documents;

namespace Velune.Windows.Services;

public static class WindowsBitmapFactory
{
    public static WriteableBitmap Create(RenderedPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var bitmap = new WriteableBitmap(page.Width, page.Height);
        using (var stream = bitmap.PixelBuffer.AsStream())
        {
            stream.Seek(0, SeekOrigin.Begin);
            stream.Write(page.PixelData.Span);
            stream.Flush();
        }

        bitmap.Invalidate();
        return bitmap;
    }
}
