using System.Runtime.InteropServices;
using SkiaSharp;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Infrastructure.Image;

/// <summary>
/// Renders image documents by decoding, scaling, and rotating via SkiaSharp.
/// </summary>
public sealed class ImageRenderService : IRenderService
{
    /// <inheritdoc />
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

            int sourceWidth = sourceBitmap.Width;
            int sourceHeight = sourceBitmap.Height;

            int scaledWidth = Math.Max(1, (int)Math.Floor(sourceWidth * zoomFactor));
            int scaledHeight = Math.Max(1, (int)Math.Floor(sourceHeight * zoomFactor));

            byte[] resizedPixels = RenderBitmapPixels(sourceBitmap, scaledWidth, scaledHeight);

            byte[] rotatedPixels = RotateBgra(
                resizedPixels,
                scaledWidth,
                scaledHeight,
                rotation,
                out int finalWidth,
                out int finalHeight);

            return new RenderedPage(
                pageIndex,
                rotatedPixels,
                finalWidth,
                finalHeight);
        }, cancellationToken);
    }

    private static byte[] RenderBitmapPixels(SKBitmap sourceBitmap, int width, int height)
    {
        using var scaledBitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        if (!sourceBitmap.ScalePixels(scaledBitmap, SKFilterQuality.High))
        {
            throw new InvalidOperationException("Unable to scale the image.");
        }

        return CopySkiaPixels(scaledBitmap);
    }

    private static byte[] CopySkiaPixels(SKBitmap bitmap)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;
        int stride = width * 4;
        int bufferSize = stride * height;
        byte[] result = new byte[bufferSize];
        int sourceStride = bitmap.RowBytes;

        if (sourceStride == stride)
        {
            Marshal.Copy(bitmap.GetPixels(), result, 0, bufferSize);
            return result;
        }

        for (int row = 0; row < height; row++)
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

            byte[] result = new byte[source.Length];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int srcIndex = (y * width + x) * 4;
                    int dstX = width - 1 - x;
                    int dstY = height - 1 - y;
                    int dstIndex = (dstY * width + dstX) * 4;

                    Buffer.BlockCopy(source, srcIndex, result, dstIndex, 4);
                }
            }

            return result;
        }

        resultWidth = height;
        resultHeight = width;

        byte[] rotated = new byte[resultWidth * resultHeight * 4];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int srcIndex = (y * width + x) * 4;

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

                int dstIndex = (dstY * resultWidth + dstX) * 4;
                Buffer.BlockCopy(source, srcIndex, rotated, dstIndex, 4);
            }
        }

        return rotated;
    }
}
