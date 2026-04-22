using System.Runtime.InteropServices;
using SkiaSharp;
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

            using var sourceBitmap = SKBitmap.Decode(imageSession.Resource.FileBytes);
            if (sourceBitmap is null)
            {
                throw new InvalidOperationException("Unable to decode the image file.");
            }

            var sourceWidth = sourceBitmap.Width;
            var sourceHeight = sourceBitmap.Height;

            var scaledWidth = Math.Max(1, (int)Math.Floor(sourceWidth * zoomFactor));
            var scaledHeight = Math.Max(1, (int)Math.Floor(sourceHeight * zoomFactor));

            var resizedPixels = RenderBitmapPixels(sourceBitmap, scaledWidth, scaledHeight);

            var rotatedPixels = RotateBgra(
                resizedPixels,
                scaledWidth,
                scaledHeight,
                rotation,
                out var finalWidth,
                out var finalHeight);

            return new RenderedPage(
                pageIndex,
                rotatedPixels,
                finalWidth,
                finalHeight);
        }, cancellationToken);
    }

    private static byte[] RenderBitmapPixels(SKBitmap sourceBitmap, int width, int height)
    {
        using var scaledBitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        if (!sourceBitmap.ScalePixels(scaledBitmap, SKFilterQuality.High))
        {
            throw new InvalidOperationException("Unable to scale the image.");
        }

        return CopySkiaPixels(scaledBitmap);
    }

    private static byte[] CopySkiaPixels(SKBitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var stride = width * 4;
        var bufferSize = stride * height;
        var result = new byte[bufferSize];
        var sourceStride = bitmap.RowBytes;

        if (sourceStride == stride)
        {
            Marshal.Copy(bitmap.GetPixels(), result, 0, bufferSize);
            return result;
        }

        for (var row = 0; row < height; row++)
        {
            Marshal.Copy(
                nint.Add(bitmap.GetPixels(), row * sourceStride),
                result,
                row * stride,
                stride);
        }

        return result;
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
