using System.Text.Json;
using Avalonia;
using Avalonia.Headless;
using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.UseCases;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Infrastructure.Documents;
using Velune.Infrastructure.Image;
using Velune.Infrastructure.Pdf;

namespace Velune.Tests.Render.Host;

internal static class Program
{
    [STAThread]
    public static async Task<int> Main(string[] args)
    {
        if (args.Length is < 1 or > 4)
        {
            WriteResult(new HostResult(false, null, null, 0, 0, 0, "Usage: <file-path> [rotation-degrees] [zoom-factor] [output-png-path]"));
            return 1;
        }

        var filePath = args[0];
        var rotation = args.Length >= 2
            ? ParseRotation(args[1])
            : Rotation.Deg0;
        var zoomFactor = args.Length >= 3
            ? double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture)
            : 1.0;
        var outputPngPath = args.Length >= 4
            ? args[3]
            : null;

        var session = HeadlessUnitTestSession.StartNew(typeof(TestAvaloniaApp));

        try
        {
            return await session.Dispatch(
                () => RunScenarioAsync(filePath, rotation, zoomFactor, outputPngPath),
                CancellationToken.None);
        }
        finally
        {
            try
            {
                session.Dispose();
            }
            catch (NullReferenceException)
            {
                // Avalonia.Headless can throw during shutdown even after a successful render.
                // The host is short-lived, so we ignore this framework cleanup issue.
            }
        }
    }

    private static async Task<int> RunScenarioAsync(
        string filePath,
        Rotation rotation,
        double zoomFactor,
        string? outputPngPath)
    {
        try
        {
            var initializer = new PdfiumInitializer();
            var sessionStore = new InMemoryDocumentSessionStore();
            var openDocumentUseCase = new OpenDocumentUseCase(
                new CompositeDocumentOpener(
                    new PdfiumDocumentOpener(initializer),
                    new AvaloniaImageDocumentOpener()),
                sessionStore,
                NoOpPerformanceMetrics.Instance);
            var renderUseCase = new RenderVisiblePageUseCase(
                sessionStore,
                new CompositeRenderService(
                    new PdfiumRenderService(initializer),
                    new ImageRenderService()));

            var openResult = await openDocumentUseCase.ExecuteAsync(new OpenDocumentRequest(filePath));
            if (openResult.IsFailure || sessionStore.Current is null)
            {
                WriteResult(new HostResult(
                    false,
                    null,
                    null,
                    0,
                    0,
                    0,
                    openResult.Error?.Message ?? "Unable to open document."));
                return 1;
            }

            var renderResult = await renderUseCase.ExecuteAsync(
                new RenderPageRequest(new PageIndex(0), zoomFactor, rotation));
            if (renderResult.IsFailure || renderResult.Value is null)
            {
                WriteResult(new HostResult(
                    false,
                    sessionStore.Current.Metadata.DocumentType.ToString(),
                    sessionStore.Current.Metadata.PageCount,
                    0,
                    0,
                    0,
                    renderResult.Error?.Message ?? "Unable to render document."));
                return 1;
            }

            if (!string.IsNullOrWhiteSpace(outputPngPath))
            {
                SnapshotPngWriter.Write(outputPngPath, renderResult.Value);
            }

            WriteResult(new HostResult(
                true,
                sessionStore.Current.Metadata.DocumentType.ToString(),
                sessionStore.Current.Metadata.PageCount,
                renderResult.Value.Width,
                renderResult.Value.Height,
                renderResult.Value.PixelData.Length,
                null));

            if (sessionStore.Current is IDisposable disposable)
            {
                disposable.Dispose();
            }

            return 0;
        }
        catch (Exception exception)
        {
            WriteResult(new HostResult(false, null, null, 0, 0, 0, exception.ToString()));
            return 1;
        }
    }

    private static Rotation ParseRotation(string value)
    {
        return value switch
        {
            "0" => Rotation.Deg0,
            "90" => Rotation.Deg90,
            "180" => Rotation.Deg180,
            "270" => Rotation.Deg270,
            _ => throw new ArgumentOutOfRangeException(nameof(value), "Rotation must be one of 0, 90, 180 or 270.")
        };
    }

    private static void WriteResult(HostResult result)
    {
        Console.Out.Write(JsonSerializer.Serialize(result));
    }

    private sealed class TestAvaloniaApp : Avalonia.Application
    {
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<TestAvaloniaApp>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = true
                });
        }
    }

    private sealed class NoOpPerformanceMetrics : IPerformanceMetrics
    {
        public static readonly NoOpPerformanceMetrics Instance = new();

        public void RecordDocumentOpened(IDocumentSession session, TimeSpan duration)
        {
        }

        public void RecordViewerRenderCompleted(IDocumentSession session, RenderResult result)
        {
        }

        public void RecordThumbnailCompleted(IDocumentSession session, RenderResult result)
        {
        }

        public void Clear(DocumentId documentId)
        {
        }
    }

    private sealed record HostResult(
        bool Success,
        string? DocumentType,
        int? PageCount,
        int RenderedWidth,
        int RenderedHeight,
        int PixelBufferLength,
        string? Error);

    private static class SnapshotPngWriter
    {
        public static void Write(string outputPath, RenderedPage renderedPage)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
            ArgumentNullException.ThrowIfNull(renderedPage);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var rgba = ConvertBgraToRgba(renderedPage.PixelData);
            using var stream = File.Create(outputPath);

            stream.Write(PngSignature);
            WriteChunk(stream, "IHDR", CreateHeaderData(renderedPage.Width, renderedPage.Height));
            WriteChunk(stream, "IDAT", CreateImageData(renderedPage.Width, renderedPage.Height, rgba));
            WriteChunk(stream, "IEND", []);
        }

        private static byte[] ConvertBgraToRgba(byte[] bgra)
        {
            var rgba = new byte[bgra.Length];

            for (var index = 0; index < bgra.Length; index += 4)
            {
                rgba[index] = bgra[index + 2];
                rgba[index + 1] = bgra[index + 1];
                rgba[index + 2] = bgra[index];
                rgba[index + 3] = bgra[index + 3];
            }

            return rgba;
        }

        private static byte[] CreateHeaderData(int width, int height)
        {
            using var stream = new MemoryStream();
            WriteBigEndian(stream, width);
            WriteBigEndian(stream, height);
            stream.WriteByte(8);
            stream.WriteByte(6);
            stream.WriteByte(0);
            stream.WriteByte(0);
            stream.WriteByte(0);
            return stream.ToArray();
        }

        private static byte[] CreateImageData(int width, int height, byte[] rgba)
        {
            using var raw = new MemoryStream();
            var stride = width * 4;

            for (var row = 0; row < height; row++)
            {
                raw.WriteByte(0);
                raw.Write(rgba, row * stride, stride);
            }

            using var compressed = new MemoryStream();
            using (var zlib = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionLevel.SmallestSize, leaveOpen: true))
            {
                raw.Position = 0;
                raw.CopyTo(zlib);
            }

            return compressed.ToArray();
        }

        private static void WriteChunk(Stream stream, string type, byte[] data)
        {
            var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            WriteBigEndian(stream, data.Length);
            stream.Write(typeBytes, 0, typeBytes.Length);
            stream.Write(data, 0, data.Length);

            WriteBigEndian(stream, unchecked((int)ComputeCrc32(typeBytes, data)));
        }

        private static void WriteBigEndian(Stream stream, int value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            stream.Write(bytes, 0, bytes.Length);
        }

        private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

        private static uint ComputeCrc32(byte[] typeBytes, byte[] data)
        {
            const uint polynomial = 0xEDB88320u;
            var crc = 0xFFFFFFFFu;

            foreach (var value in typeBytes)
            {
                crc = UpdateCrc(crc, value, polynomial);
            }

            foreach (var value in data)
            {
                crc = UpdateCrc(crc, value, polynomial);
            }

            return ~crc;
        }

        private static uint UpdateCrc(uint crc, byte value, uint polynomial)
        {
            crc ^= value;

            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0
                    ? (crc >> 1) ^ polynomial
                    : crc >> 1;
            }

            return crc;
        }
    }
}
