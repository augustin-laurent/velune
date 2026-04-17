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

            using var stream = new MemoryStream(imageSession.Resource.FileBytes);
            using var source = new Bitmap(stream);

            var sourceWidth = source.PixelSize.Width;
            var sourceHeight = source.PixelSize.Height;

            var scaledWidth = Math.Max(1, (int)Math.Floor(sourceWidth * zoomFactor));
            var scaledHeight = Math.Max(1, (int)Math.Floor(sourceHeight * zoomFactor));

            var shouldScale = scaledWidth != sourceWidth || scaledHeight != sourceHeight;

            if (!shouldScale &&
                TryDecodeOriginalPng(imageSession.Resource.FileBytes, out var originalPixels, out var originalWidth, out var originalHeight))
            {
                var rotatedOriginalPixels = RotateBgra(
                    originalPixels,
                    originalWidth,
                    originalHeight,
                    rotation,
                    out var originalFinalWidth,
                    out var originalFinalHeight);

                return new RenderedPage(
                    pageIndex,
                    rotatedOriginalPixels,
                    originalFinalWidth,
                    originalFinalHeight);
            }

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
        return CopyBitmapPixels(bitmap, allowFallback: true);
    }

    private static byte[] CopyBitmapPixels(Bitmap bitmap, bool allowFallback)
    {
        var width = bitmap.PixelSize.Width;
        var height = bitmap.PixelSize.Height;
        var stride = width * 4;
        var bufferSize = stride * height;

        var result = new byte[bufferSize];
        var unmanagedBuffer = Marshal.AllocHGlobal(bufferSize);

        try
        {
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
            catch (NotSupportedException) when (allowFallback)
            {
                using var stream = new MemoryStream();
                bitmap.Save(stream);
                return DecodeSavedBitmapToBgra(stream.ToArray(), width, height);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(unmanagedBuffer);
        }
    }

    private static byte[] DecodeSavedBitmapToBgra(byte[] pngBytes, int width, int height)
    {
        var offset = 8;
        using var idat = new MemoryStream();

        while (offset < pngBytes.Length)
        {
            var chunkLength = ReadInt32BigEndian(pngBytes, offset);
            offset += 4;

            var chunkType = System.Text.Encoding.ASCII.GetString(pngBytes, offset, 4);
            offset += 4;

            if (chunkType == "IDAT")
            {
                idat.Write(pngBytes, offset, chunkLength);
            }

            offset += chunkLength + 4;

            if (chunkType == "IEND")
            {
                break;
            }
        }

        using var compressedStream = new MemoryStream(idat.ToArray());
        using var zlib = new System.IO.Compression.ZLibStream(
            compressedStream,
            System.IO.Compression.CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);

        var rgba = Unfilter(raw.ToArray(), width, height);
        return ConvertRgbaToBgra(rgba);
    }

    private static bool TryDecodeOriginalPng(
        byte[] fileBytes,
        out byte[] bgra,
        out int width,
        out int height)
    {
        bgra = [];
        width = 0;
        height = 0;

        if (fileBytes.Length < 8 ||
            fileBytes[0] != 137 ||
            fileBytes[1] != 80 ||
            fileBytes[2] != 78 ||
            fileBytes[3] != 71)
        {
            return false;
        }

        var offset = 8;
        using var idat = new MemoryStream();

        while (offset < fileBytes.Length)
        {
            var chunkLength = ReadInt32BigEndian(fileBytes, offset);
            offset += 4;

            var chunkType = System.Text.Encoding.ASCII.GetString(fileBytes, offset, 4);
            offset += 4;

            if (chunkType == "IHDR")
            {
                width = ReadInt32BigEndian(fileBytes, offset);
                height = ReadInt32BigEndian(fileBytes, offset + 4);
            }
            else if (chunkType == "IDAT")
            {
                idat.Write(fileBytes, offset, chunkLength);
            }

            offset += chunkLength + 4;

            if (chunkType == "IEND")
            {
                break;
            }
        }

        if (width <= 0 || height <= 0)
        {
            return false;
        }

        using var compressedStream = new MemoryStream(idat.ToArray());
        using var zlib = new System.IO.Compression.ZLibStream(
            compressedStream,
            System.IO.Compression.CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);

        var rgba = Unfilter(raw.ToArray(), width, height);
        bgra = ConvertRgbaToBgra(rgba);
        return true;
    }

    private static byte[] Unfilter(byte[] data, int width, int height)
    {
        var bytesPerPixel = 4;
        var stride = width * bytesPerPixel;
        var result = new byte[height * stride];
        var sourceOffset = 0;

        for (var row = 0; row < height; row++)
        {
            var filter = data[sourceOffset++];
            var rowStart = row * stride;

            for (var column = 0; column < stride; column++)
            {
                var raw = data[sourceOffset++];
                var left = column >= bytesPerPixel
                    ? result[rowStart + column - bytesPerPixel]
                    : 0;
                var up = row > 0
                    ? result[rowStart + column - stride]
                    : 0;
                var upLeft = row > 0 && column >= bytesPerPixel
                    ? result[rowStart + column - stride - bytesPerPixel]
                    : 0;

                result[rowStart + column] = filter switch
                {
                    0 => raw,
                    1 => unchecked((byte)(raw + left)),
                    2 => unchecked((byte)(raw + up)),
                    3 => unchecked((byte)(raw + ((left + up) / 2))),
                    4 => unchecked((byte)(raw + PaethPredictor(left, up, upLeft))),
                    _ => throw new InvalidOperationException($"Unsupported PNG filter type: {filter}.")
                };
            }
        }

        return result;
    }

    private static byte[] ConvertRgbaToBgra(byte[] rgba)
    {
        var bgra = new byte[rgba.Length];

        for (var index = 0; index < rgba.Length; index += 4)
        {
            bgra[index] = rgba[index + 2];
            bgra[index + 1] = rgba[index + 1];
            bgra[index + 2] = rgba[index];
            bgra[index + 3] = rgba[index + 3];
        }

        return bgra;
    }

    private static int ReadInt32BigEndian(byte[] data, int offset)
    {
        return (data[offset] << 24) |
               (data[offset + 1] << 16) |
               (data[offset + 2] << 8) |
               data[offset + 3];
    }

    private static int PaethPredictor(int left, int up, int upLeft)
    {
        var initial = left + up - upLeft;
        var distanceLeft = Math.Abs(initial - left);
        var distanceUp = Math.Abs(initial - up);
        var distanceUpLeft = Math.Abs(initial - upLeft);

        if (distanceLeft <= distanceUp && distanceLeft <= distanceUpLeft)
        {
            return left;
        }

        if (distanceUp <= distanceUpLeft)
        {
            return up;
        }

        return upLeft;
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
