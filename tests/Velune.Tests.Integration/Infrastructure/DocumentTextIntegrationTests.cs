using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Application.Text;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;
using Velune.Infrastructure.Documents;
using Velune.Infrastructure.Image;
using Velune.Infrastructure.Pdf;
using Velune.Infrastructure.Text;

namespace Velune.Tests.Integration.Infrastructure;

public sealed class DocumentTextIntegrationTests
{
    [Fact]
    public async Task SamplePdf_ShouldLoadEmbeddedTextWithoutOcr()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.pdf");
        var textService = CreateTextService(temporaryDirectory.Path);
        var session = await OpenSessionAsync(fixturePath);

        try
        {
            var result = await textService.LoadAsync(session, ["eng"]);

            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.NotNull(result.Value);
            Assert.False(result.Value.RequiresOcr);
            Assert.False(result.Value.UsedCache);
            Assert.NotNull(result.Value.Index);
            Assert.Equal(TextSourceKind.EmbeddedPdfText, result.Value.Index.Pages[0].SourceKind);
            Assert.Contains("Velune integration sample", NormalizeText(result.Value.Index.Pages[0].Text), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DisposeSession(session);
        }
    }

    [RequiresTesseractFact]
    public async Task ImageOcr_ShouldRecognizeTextAndReuseCacheBetweenSessions()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var imagePath = Path.Combine(temporaryDirectory.Path, "ocr-image.pgm");
        var raster = OcrTestAssetBuilder.CreateRaster("TEST");
        OcrTestAssetBuilder.WritePgm(imagePath, raster);

        var firstService = CreateTextService(temporaryDirectory.Path);
        var firstSession = CreateImageSession(imagePath, raster);

        try
        {
            var ocrResult = await firstService.RunOcrAsync(firstSession, ["eng"]);

            Assert.True(ocrResult.IsSuccess, ocrResult.Error?.Message);
            Assert.NotNull(ocrResult.Value);
            Assert.Contains("TEST", NormalizeText(ocrResult.Value!.Pages[0].Text), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DisposeSession(firstSession);
        }

        var secondService = CreateTextService(temporaryDirectory.Path);
        var secondSession = CreateImageSession(imagePath, raster);

        try
        {
            var loadResult = await secondService.LoadAsync(secondSession, ["eng"]);

            Assert.True(loadResult.IsSuccess, loadResult.Error?.Message);
            Assert.NotNull(loadResult.Value);
            Assert.True(loadResult.Value.UsedCache);
            Assert.NotNull(loadResult.Value.Index);
            Assert.Equal(TextSourceKind.Ocr, loadResult.Value.Index.Pages[0].SourceKind);
            Assert.Contains("TEST", NormalizeText(loadResult.Value.Index.Pages[0].Text), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DisposeSession(secondSession);
        }
    }

    [RequiresTesseractFact]
    public async Task ImageOcrCache_ShouldInvalidate_WhenSourceFileChanges()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var imagePath = Path.Combine(temporaryDirectory.Path, "ocr-image.pgm");
        var initialRaster = OcrTestAssetBuilder.CreateRaster("TEST");
        OcrTestAssetBuilder.WritePgm(imagePath, initialRaster);

        var initialService = CreateTextService(temporaryDirectory.Path);
        var initialSession = CreateImageSession(imagePath, initialRaster);

        try
        {
            var initialResult = await initialService.RunOcrAsync(initialSession, ["eng"]);
            Assert.True(initialResult.IsSuccess, initialResult.Error?.Message);
            Assert.NotNull(initialResult.Value);
            Assert.Contains("TEST", NormalizeText(initialResult.Value!.Pages[0].Text), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DisposeSession(initialSession);
        }

        var updatedRaster = OcrTestAssetBuilder.CreateRaster("TESTS");
        OcrTestAssetBuilder.WritePgm(imagePath, updatedRaster);
        File.SetLastWriteTimeUtc(imagePath, DateTime.UtcNow.AddMinutes(1));

        var updatedService = CreateTextService(temporaryDirectory.Path);
        var updatedSession = CreateImageSession(imagePath, updatedRaster);

        try
        {
            var loadResult = await updatedService.LoadAsync(updatedSession, ["eng"]);

            Assert.True(loadResult.IsSuccess, loadResult.Error?.Message);
            Assert.NotNull(loadResult.Value);
            Assert.True(loadResult.Value.RequiresOcr);
            Assert.False(loadResult.Value.UsedCache);

            var rerunResult = await updatedService.RunOcrAsync(updatedSession, ["eng"]);

            Assert.True(rerunResult.IsSuccess, rerunResult.Error?.Message);
            Assert.NotNull(rerunResult.Value);
            Assert.Equal(TextSourceKind.Ocr, rerunResult.Value!.Pages[0].SourceKind);
            Assert.False(string.IsNullOrWhiteSpace(NormalizeText(rerunResult.Value.Pages[0].Text)));
        }
        finally
        {
            DisposeSession(updatedSession);
        }
    }

    [RequiresTesseractFact]
    public async Task ScannedPdf_ShouldRunOcr_WhenPdfHasNoEmbeddedText()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var pdfPath = Path.Combine(temporaryDirectory.Path, "scanned.pdf");
        OcrTestAssetBuilder.WriteScannedPdf(pdfPath, OcrTestAssetBuilder.CreateRaster("TEST"));

        var textService = CreateTextService(temporaryDirectory.Path);
        var session = await OpenSessionAsync(pdfPath);

        try
        {
            var loadResult = await textService.LoadAsync(session, ["eng"]);

            Assert.True(loadResult.IsSuccess, loadResult.Error?.Message);
            Assert.NotNull(loadResult.Value);
            Assert.True(loadResult.Value.RequiresOcr);

            var ocrResult = await textService.RunOcrAsync(session, ["eng"]);

            Assert.True(ocrResult.IsSuccess, ocrResult.Error?.Message);
            Assert.NotNull(ocrResult.Value);
            Assert.Contains("TEST", NormalizeText(ocrResult.Value!.Pages[0].Text), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(TextSourceKind.Ocr, ocrResult.Value.Pages[0].SourceKind);
        }
        finally
        {
            DisposeSession(session);
        }
    }

    private static IDocumentTextService CreateTextService(string cachePath)
    {
        var options = Options.Create(new AppOptions
        {
            OcrCachePath = cachePath,
            TesseractExecutablePath = TesseractTestSupport.GetExecutablePath(),
            DefaultOcrLanguages = ["eng"]
        });
        var initializer = new PdfiumInitializer();

        return new DocumentTextService(
            new DocumentTextDiskCache(NullLogger<DocumentTextDiskCache>.Instance, options),
            new TesseractOcrEngine(options),
            new CompositeRenderService(
                new PdfiumRenderService(initializer),
                new ImageRenderService()),
            options);
    }

    private static async Task<IDocumentSession> OpenSessionAsync(string filePath)
    {
        if (string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return new PdfiumDocumentOpener(new PdfiumInitializer()).Open(filePath);
        }

        var fileInfo = new FileInfo(filePath);
        var imageInfo = ImageInfoReader.ReadPng(filePath);

        return new TestImageDocumentSession(
            DocumentId.New(),
            new DocumentMetadata(
                Path.GetFileName(filePath),
                filePath,
                DocumentType.Image,
                fileInfo.Length,
                1,
                pixelWidth: imageInfo.Width,
                pixelHeight: imageInfo.Height,
                formatLabel: "PNG image"),
            ViewportState.Default,
            new ImageMetadata(imageInfo.Width, imageInfo.Height));
    }

    private static IDocumentSession CreateImageSession(string filePath, TextRaster raster)
    {
        var fileInfo = new FileInfo(filePath);

        return new TestImageDocumentSession(
            DocumentId.New(),
            new DocumentMetadata(
                Path.GetFileName(filePath),
                filePath,
                DocumentType.Image,
                fileInfo.Length,
                1,
                pixelWidth: raster.Width,
                pixelHeight: raster.Height,
                formatLabel: $"{Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant()} image"),
            ViewportState.Default,
            new ImageMetadata(raster.Width, raster.Height));
    }

    private static void DisposeSession(IDocumentSession session)
    {
        if (session is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static string NormalizeText(string text)
    {
        return string.Join(
            ' ',
            text.ReplaceLineEndings(" ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "velune-ocr-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    private sealed record TextRaster(int Width, int Height, byte[] GrayscalePixels);

    private sealed record TestImageDocumentSession(
        DocumentId Id,
        DocumentMetadata Metadata,
        ViewportState Viewport,
        ImageMetadata ImageMetadata) : IImageDocumentSession
    {
        public IDocumentSession WithViewport(ViewportState viewport)
        {
            return this with
            {
                Viewport = viewport
            };
        }
    }

    private sealed record ImageInfo(int Width, int Height);

    private static class ImageInfoReader
    {
        public static ImageInfo ReadPng(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            Span<byte> header = stackalloc byte[24];
            var read = stream.Read(header);

            if (read < 24)
            {
                throw new InvalidOperationException("The PNG header is incomplete.");
            }

            var width =
                (header[16] << 24) |
                (header[17] << 16) |
                (header[18] << 8) |
                header[19];
            var height =
                (header[20] << 24) |
                (header[21] << 16) |
                (header[22] << 8) |
                header[23];

            return new ImageInfo(width, height);
        }
    }

    private static class OcrTestAssetBuilder
    {
        private const int GlyphWidth = 5;
        private const int GlyphHeight = 7;
        private const int Scale = 18;
        private const int Margin = 24;
        private const int Spacing = 12;

        private static readonly IReadOnlyDictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            [' '] = ["000", "000", "000", "000", "000", "000", "000"],
            ['C'] = ["01111", "10000", "10000", "10000", "10000", "10000", "01111"],
            ['D'] = ["11110", "10001", "10001", "10001", "10001", "10001", "11110"],
            ['E'] = ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
            ['F'] = ["11111", "10000", "10000", "11110", "10000", "10000", "10000"],
            ['I'] = ["11111", "00100", "00100", "00100", "00100", "00100", "11111"],
            ['L'] = ["10000", "10000", "10000", "10000", "10000", "10000", "11111"],
            ['N'] = ["10001", "11001", "10101", "10011", "10001", "10001", "10001"],
            ['O'] = ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
            ['R'] = ["11110", "10001", "10001", "11110", "10100", "10010", "10001"],
            ['S'] = ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
            ['T'] = ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
            ['U'] = ["10001", "10001", "10001", "10001", "10001", "10001", "01110"],
            ['V'] = ["10001", "10001", "10001", "10001", "10001", "01010", "00100"]
        };

        public static TextRaster CreateRaster(string text)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);

            var uppercase = text.ToUpperInvariant();
            var totalWidth = Margin * 2;

            foreach (var character in uppercase)
            {
                var glyph = GetGlyph(character);
                totalWidth += (glyph[0].Length * Scale) + Spacing;
            }

            totalWidth -= Spacing;

            var totalHeight = Margin * 2 + (GlyphHeight * Scale);
            var pixels = Enumerable.Repeat((byte)255, totalWidth * totalHeight).ToArray();
            var currentX = Margin;

            foreach (var character in uppercase)
            {
                var glyph = GetGlyph(character);

                for (var row = 0; row < glyph.Length; row++)
                {
                    for (var column = 0; column < glyph[row].Length; column++)
                    {
                        if (glyph[row][column] != '1')
                        {
                            continue;
                        }

                        FillBlock(
                            pixels,
                            totalWidth,
                            currentX + (column * Scale),
                            Margin + (row * Scale),
                            Scale,
                            Scale,
                            0);
                    }
                }

                currentX += (glyph[0].Length * Scale) + Spacing;
            }

            return new TextRaster(totalWidth, totalHeight, pixels);
        }

        public static void WritePng(string outputPath, TextRaster raster)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            using var stream = File.Create(outputPath);
            stream.Write(PngSignature);
            WriteChunk(stream, "IHDR", CreatePngHeader(raster.Width, raster.Height));
            WriteChunk(stream, "IDAT", CreatePngImageData(raster));
            WriteChunk(stream, "IEND", []);
        }

        public static void WritePgm(string outputPath, TextRaster raster)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            using var stream = File.Create(outputPath);
            var header = Encoding.ASCII.GetBytes($"P5\n{raster.Width} {raster.Height}\n255\n");
            stream.Write(header, 0, header.Length);
            stream.Write(raster.GrayscalePixels, 0, raster.GrayscalePixels.Length);
        }

        public static void WriteScannedPdf(string outputPath, TextRaster raster)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            var contentBytes = Encoding.ASCII.GetBytes($"q {raster.Width} 0 0 {raster.Height} 0 0 cm /Im0 Do Q");
            var compressedImageData = Compress(raster.GrayscalePixels);

            using var stream = File.Create(outputPath);
            var offsets = new List<long> { 0 };

            WriteAscii(stream, "%PDF-1.4\n%VELUNE\n");
            WriteObject(stream, offsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
            WriteObject(stream, offsets, 2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
            WriteObject(
                stream,
                offsets,
                3,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {raster.Width} {raster.Height}] /Resources << /XObject << /Im0 5 0 R >> >> /Contents 4 0 R >>");
            WriteStreamObject(stream, offsets, 4, $"<< /Length {contentBytes.Length} >>", contentBytes);
            WriteStreamObject(
                stream,
                offsets,
                5,
                $"<< /Type /XObject /Subtype /Image /Width {raster.Width} /Height {raster.Height} /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /FlateDecode /Length {compressedImageData.Length} >>",
                compressedImageData);

            var startXref = stream.Position;
            WriteAscii(stream, $"xref\n0 {offsets.Count}\n");
            WriteAscii(stream, "0000000000 65535 f \n");

            for (var objectNumber = 1; objectNumber < offsets.Count; objectNumber++)
            {
                WriteAscii(stream, $"{offsets[objectNumber]:D10} 00000 n \n");
            }

            WriteAscii(stream, $"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{startXref}\n%%EOF");
        }

        private static string[] GetGlyph(char character)
        {
            if (Glyphs.TryGetValue(character, out var glyph))
            {
                return glyph;
            }

            throw new InvalidOperationException($"The OCR test glyph '{character}' is not defined.");
        }

        private static void FillBlock(
            byte[] pixels,
            int stride,
            int startX,
            int startY,
            int width,
            int height,
            byte value)
        {
            for (var y = startY; y < startY + height; y++)
            {
                var rowStart = y * stride;
                for (var x = startX; x < startX + width; x++)
                {
                    pixels[rowStart + x] = value;
                }
            }
        }

        private static byte[] CreatePngHeader(int width, int height)
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

        private static byte[] CreatePngImageData(TextRaster raster)
        {
            using var raw = new MemoryStream();
            var rgba = ConvertGrayscaleToRgba(raster.GrayscalePixels);
            var stride = raster.Width * 4;

            for (var row = 0; row < raster.Height; row++)
            {
                raw.WriteByte(0);
                raw.Write(rgba, row * stride, stride);
            }

            return Compress(raw.ToArray());
        }

        private static byte[] ConvertGrayscaleToRgba(byte[] grayscale)
        {
            var rgba = new byte[grayscale.Length * 4];

            for (var index = 0; index < grayscale.Length; index++)
            {
                var value = grayscale[index];
                var rgbaIndex = index * 4;
                rgba[rgbaIndex] = value;
                rgba[rgbaIndex + 1] = value;
                rgba[rgbaIndex + 2] = value;
                rgba[rgbaIndex + 3] = 255;
            }

            return rgba;
        }

        private static byte[] Compress(byte[] raw)
        {
            using var output = new MemoryStream();

            using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                zlib.Write(raw, 0, raw.Length);
            }

            return output.ToArray();
        }

        private static void WriteObject(Stream stream, List<long> offsets, int objectNumber, string body)
        {
            offsets.Add(stream.Position);
            WriteAscii(stream, $"{objectNumber} 0 obj\n{body}\nendobj\n");
        }

        private static void WriteStreamObject(Stream stream, List<long> offsets, int objectNumber, string dictionary, byte[] data)
        {
            offsets.Add(stream.Position);
            WriteAscii(stream, $"{objectNumber} 0 obj\n{dictionary}\nstream\n");
            stream.Write(data, 0, data.Length);
            WriteAscii(stream, "\nendstream\nendobj\n");
        }

        private static void WriteChunk(Stream stream, string type, byte[] data)
        {
            WriteBigEndian(stream, data.Length);
            var typeBytes = Encoding.ASCII.GetBytes(type);
            stream.Write(typeBytes, 0, typeBytes.Length);
            stream.Write(data, 0, data.Length);

            var crc = new Crc32();
            crc.Append(typeBytes);
            crc.Append(data);
            WriteBigEndian(stream, (int)crc.GetCurrentHashAsUInt32());
        }

        private static void WriteAscii(Stream stream, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteBigEndian(Stream stream, int value)
        {
            Span<byte> buffer = stackalloc byte[4];
            buffer[0] = (byte)((value >> 24) & 0xff);
            buffer[1] = (byte)((value >> 16) & 0xff);
            buffer[2] = (byte)((value >> 8) & 0xff);
            buffer[3] = (byte)(value & 0xff);
            stream.Write(buffer);
        }

        private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    }

    private sealed class Crc32
    {
        private const uint Polynomial = 0xEDB88320u;
        private static readonly uint[] Table = BuildTable();
        private uint _current = 0xFFFFFFFFu;

        public void Append(ReadOnlySpan<byte> bytes)
        {
            foreach (var current in bytes)
            {
                var tableIndex = (_current ^ current) & 0xFF;
                _current = Table[tableIndex] ^ (_current >> 8);
            }
        }

        public uint GetCurrentHashAsUInt32()
        {
            return _current ^ 0xFFFFFFFFu;
        }

        private static uint[] BuildTable()
        {
            var table = new uint[256];

            for (uint index = 0; index < table.Length; index++)
            {
                var value = index;
                for (var bit = 0; bit < 8; bit++)
                {
                    value = (value & 1) != 0
                        ? Polynomial ^ (value >> 1)
                        : value >> 1;
                }

                table[index] = value;
            }

            return table;
        }
    }
}
