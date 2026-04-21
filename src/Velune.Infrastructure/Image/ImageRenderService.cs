using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Infrastructure.Image;

public sealed class ImageRenderService : IRenderService
{
    public Task<RenderedPage> RenderPageAsync(
        IDocumentSession session,
        PageIndex pageIndex,
        double zoomFactor,
        Rotation rotation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoomFactor);

        if (session is not ImageDocumentSession imageSession)
        {
            throw new NotSupportedException("The active session is not backed by the image renderer.");
        }

        if (pageIndex.Value != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), "Image documents only expose a single page.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var source = imageSession.Resource.Bitmap;

            var sourceWidth = source.PixelSize.Width;
            var sourceHeight = source.PixelSize.Height;

            var scaledWidth = Math.Max(1, (int)Math.Floor(sourceWidth * zoomFactor));
            var scaledHeight = Math.Max(1, (int)Math.Floor(sourceHeight * zoomFactor));

            var shouldScale = scaledWidth != sourceWidth || scaledHeight != sourceHeight;

            var workingBitmap = shouldScale
                ? source.CreateScaledBitmap(
                    new PixelSize(scaledWidth, scaledHeight),
                    BitmapInterpolationMode.HighQuality)
                : source;

            try
            {
                var resizedPixels = CopyBitmapPixels(workingBitmap);

                var rotatedPixels = RotateBgra(
                    resizedPixels,
                    workingBitmap.PixelSize.Width,
                    workingBitmap.PixelSize.Height,
                    rotation,
                    out var finalWidth,
                    out var finalHeight);

                return new RenderedPage(
                    pageIndex,
                    rotatedPixels,
                    finalWidth,
                    finalHeight);
            }
            finally
            {
                if (!ReferenceEquals(workingBitmap, source))
                {
                    workingBitmap.Dispose();
                }
            }
        }, cancellationToken);
    }

    private static byte[] CopyBitmapPixels(Bitmap bitmap)
    {
        var width = bitmap.PixelSize.Width;
        var height = bitmap.PixelSize.Height;
        var stride = width * 4;
        var bufferSize = stride * height;

        var result = new byte[bufferSize];
        var unmanagedBuffer = Marshal.AllocHGlobal(bufferSize);

        try
        {
            bitmap.CopyPixels(
                new PixelRect(0, 0, width, height),
                unmanagedBuffer,
                bufferSize,
                stride);

            Marshal.Copy(unmanagedBuffer, result, 0, bufferSize);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(unmanagedBuffer);
        }
    }

    private static byte[] RotateBgra(
        byte[] source,
        int width,
        int height,
        Rotation rotation,
        out int resultWidth,
        out int resultHeight)
    {
        if (rotation is Rotation.Deg0)
        {
            resultWidth = width;
            resultHeight = height;
            return source;
        }

        if (rotation is Rotation.Deg180)
        {
            resultWidth = width;
            resultHeight = height;

            var result = new byte[source.Length];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var srcIndex = (y * width + x) * 4;
                    var dstX = width - 1 - x;
                    var dstY = height - 1 - y;
                    var dstIndex = (dstY * width + dstX) * 4;

                    Buffer.BlockCopy(source, srcIndex, result, dstIndex, 4);
                }
            }

            return result;
        }

        resultWidth = height;
        resultHeight = width;

        var rotated = new byte[resultWidth * resultHeight * 4];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var srcIndex = (y * width + x) * 4;

                int dstX;
                int dstY;

                if (rotation is Rotation.Deg90)
                {
                    dstX = height - 1 - y;
                    dstY = x;
                }
                else
                {
                    dstX = y;
                    dstY = width - 1 - x;
                }

                var dstIndex = (dstY * resultWidth + dstX) * 4;
                Buffer.BlockCopy(source, srcIndex, rotated, dstIndex, 4);
            }
        }

        return rotated;
    }
}
