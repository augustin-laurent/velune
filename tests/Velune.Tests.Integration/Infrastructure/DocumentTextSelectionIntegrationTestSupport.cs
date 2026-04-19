using System.IO.Compression;

namespace Velune.Tests.Integration.Infrastructure;

internal sealed class TemporaryDirectory : IDisposable
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

internal sealed record TextRaster(int Width, int Height, byte[] GrayscalePixels);

internal sealed record ImageInfo(int Width, int Height);

internal static class ImageInfoReader
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

internal static class OcrTestAssetBuilder
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

                    var pixelX = currentX + (column * Scale);
                    var pixelY = Margin + (row * Scale);

                    PaintGlyphBlock(pixels, totalWidth, totalHeight, pixelX, pixelY);
                }
            }

            currentX += (glyph[0].Length * Scale) + Spacing;
        }

        return new TextRaster(totalWidth, totalHeight, pixels);
    }

    public static void WritePgm(string filePath, TextRaster raster)
    {
        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream);
        var header = $"P5\n{raster.Width} {raster.Height}\n255\n";
        writer.Write(System.Text.Encoding.ASCII.GetBytes(header));
        writer.Write(raster.GrayscalePixels);
    }

    public static void WriteScannedPdf(string pdfPath, TextRaster raster)
    {
        var temporaryDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "velune-ocr-pdf", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var imagePath = System.IO.Path.Combine(temporaryDirectory, "page.pgm");
            WritePgm(imagePath, raster);

            var content = CreatePdfContent();
            var pdfDirectory = System.IO.Path.GetDirectoryName(pdfPath);
            if (!string.IsNullOrEmpty(pdfDirectory))
            {
                Directory.CreateDirectory(pdfDirectory);
            }

            File.WriteAllText(pdfPath, content);
            using var archive = ZipFile.Open(pdfPath, ZipArchiveMode.Update);
            archive.CreateEntryFromFile(imagePath, "page.pgm");
        }
        finally
        {
            try
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static void PaintGlyphBlock(byte[] pixels, int width, int height, int startX, int startY)
    {
        for (var y = startY; y < startY + Scale; y++)
        {
            if (y < 0 || y >= height)
            {
                continue;
            }

            for (var x = startX; x < startX + Scale; x++)
            {
                if (x < 0 || x >= width)
                {
                    continue;
                }

                pixels[(y * width) + x] = 0;
            }
        }
    }

    private static string[] GetGlyph(char character)
    {
        if (Glyphs.TryGetValue(character, out var glyph))
        {
            return glyph;
        }

        throw new InvalidOperationException($"Unsupported OCR test glyph '{character}'.");
    }

    private static string CreatePdfContent()
    {
        return """
               %PDF-1.4
               1 0 obj
               << /Type /Catalog /Pages 2 0 R >>
               endobj
               2 0 obj
               << /Type /Pages /Kids [3 0 R] /Count 1 >>
               endobj
               3 0 obj
               << /Type /Page /Parent 2 0 R /MediaBox [0 0 400 200] /Resources << >> /Contents 4 0 R >>
               endobj
               4 0 obj
               << /Length 35 >>
               stream
               q
               400 0 0 200 0 0 cm
               /Im0 Do
               Q
               endstream
               endobj
               xref
               0 5
               0000000000 65535 f 
               0000000010 00000 n 
               0000000062 00000 n 
               0000000122 00000 n 
               0000000217 00000 n 
               trailer
               << /Size 5 /Root 1 0 R >>
               startxref
               303
               %%EOF
               """;
    }
}
