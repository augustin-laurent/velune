using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Velune.Tests.Render;

public sealed class RenderSnapshotTests
{
    private static readonly SnapshotComparisonProfile StrictImageProfile = new(
        MaxChannelDelta: 2,
        MaxDifferentPixelRatio: 0.001,
        NeighborhoodRadius: 0);

    private static readonly SnapshotComparisonProfile PdfPageProfile = new(
        MaxChannelDelta: 12,
        MaxDifferentPixelRatio: 0.015,
        NeighborhoodRadius: 1);

    private static readonly SnapshotComparisonProfile PdfThumbnailProfile = new(
        MaxChannelDelta: 14,
        MaxDifferentPixelRatio: 0.03,
        NeighborhoodRadius: 1);

    [Fact]
    public async Task PdfThumbnailSnapshot_ShouldMatchApprovedBaseline()
    {
        await AssertSnapshotAsync(
            fixtureName: "sample.pdf",
            snapshotName: "pdf-thumbnail",
            rotationDegrees: 0,
            zoomFactor: 0.20,
            comparisonProfile: PdfThumbnailProfile);
    }

    [Fact]
    public async Task PdfPageSnapshot_ShouldMatchApprovedBaseline()
    {
        await AssertSnapshotAsync(
            fixtureName: "sample.pdf",
            snapshotName: "pdf-page",
            rotationDegrees: 0,
            zoomFactor: 1.0,
            comparisonProfile: PdfPageProfile);
    }

    [Fact]
    public async Task RotatedImageSnapshot_ShouldMatchApprovedBaseline()
    {
        await AssertSnapshotAsync(
            fixtureName: "sample-portrait.png",
            snapshotName: "image-rotated",
            rotationDegrees: 90,
            zoomFactor: 1.0,
            comparisonProfile: StrictImageProfile);
    }

    private static async Task AssertSnapshotAsync(
        string fixtureName,
        string snapshotName,
        int rotationDegrees,
        double zoomFactor,
        SnapshotComparisonProfile comparisonProfile)
    {
        var repositoryRoot = GetRepositoryRoot();
        var hostDllPath = GetHostDllPath(repositoryRoot);
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);
        var approvedPath = Path.Combine(AppContext.BaseDirectory, "Snapshots", "Approved", $"{snapshotName}.png");
        var actualDirectory = Path.Combine(AppContext.BaseDirectory, "Snapshots", "Actual");
        var actualPath = Path.Combine(actualDirectory, $"{snapshotName}.png");

        Assert.True(File.Exists(hostDllPath), $"Host executable not found: {hostDllPath}");
        Assert.True(File.Exists(fixturePath), $"Fixture not found: {fixturePath}");
        Assert.True(File.Exists(approvedPath), $"Approved snapshot not found: {approvedPath}");

        Directory.CreateDirectory(actualDirectory);

        var processStartInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        processStartInfo.ArgumentList.Add(hostDllPath);
        processStartInfo.ArgumentList.Add(fixturePath);
        processStartInfo.ArgumentList.Add(rotationDegrees.ToString());
        processStartInfo.ArgumentList.Add(zoomFactor.ToString(System.Globalization.CultureInfo.InvariantCulture));
        processStartInfo.ArgumentList.Add(actualPath);

        using var process = Process.Start(processStartInfo);
        Assert.NotNull(process);

        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            Assert.Fail($"Render host failed with exit code {process.ExitCode}:{Environment.NewLine}{standardError}{standardOutput}");
        }

        var hostResult = JsonSerializer.Deserialize<HostResult>(
            standardOutput,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        Assert.NotNull(hostResult);
        Assert.True(hostResult.Success, hostResult.Error);
        Assert.True(File.Exists(actualPath), $"Actual snapshot was not written: {actualPath}");

        var approved = SnapshotPngReader.Read(approvedPath);
        var actual = SnapshotPngReader.Read(actualPath);

        Assert.Equal(approved.Width, actual.Width);
        Assert.Equal(approved.Height, actual.Height);

        var pixelCount = approved.Width * approved.Height;
        var differentPixelsForward = CountDifferentPixels(approved, actual, comparisonProfile);
        var differentPixelsBackward = CountDifferentPixels(actual, approved, comparisonProfile);
        var differentPixels = Math.Max(differentPixelsForward, differentPixelsBackward);
        var differentPixelRatio = pixelCount == 0 ? 0 : (double)differentPixels / pixelCount;

        Assert.True(
            differentPixelRatio <= comparisonProfile.MaxDifferentPixelRatio,
            $"Snapshot regression detected for '{snapshotName}'. Different pixels: {differentPixels}/{pixelCount} ({differentPixelRatio:P3}). Actual: {actualPath}");
    }

    private static int CountDifferentPixels(
        SnapshotImage source,
        SnapshotImage target,
        SnapshotComparisonProfile comparisonProfile)
    {
        var differentPixels = 0;

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                if (!PixelMatchesWithinNeighborhood(source, target, x, y, comparisonProfile))
                {
                    differentPixels++;
                }
            }
        }

        return differentPixels;
    }

    private static bool PixelMatchesWithinNeighborhood(
        SnapshotImage source,
        SnapshotImage target,
        int x,
        int y,
        SnapshotComparisonProfile comparisonProfile)
    {
        var radius = comparisonProfile.NeighborhoodRadius;

        for (var targetY = Math.Max(0, y - radius); targetY <= Math.Min(target.Height - 1, y + radius); targetY++)
        {
            for (var targetX = Math.Max(0, x - radius); targetX <= Math.Min(target.Width - 1, x + radius); targetX++)
            {
                if (PixelsAreClose(source, target, x, y, targetX, targetY, comparisonProfile.MaxChannelDelta))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool PixelsAreClose(
        SnapshotImage source,
        SnapshotImage target,
        int sourceX,
        int sourceY,
        int targetX,
        int targetY,
        int maxChannelDelta)
    {
        var sourceIndex = ((sourceY * source.Width) + sourceX) * 4;
        var targetIndex = ((targetY * target.Width) + targetX) * 4;

        for (var channel = 0; channel < 4; channel++)
        {
            var delta = Math.Abs(source.Rgba[sourceIndex + channel] - target.Rgba[targetIndex + channel]);
            if (delta > maxChannelDelta)
            {
                return false;
            }
        }

        return true;
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    }

    private static string GetHostDllPath(string repositoryRoot)
    {
        var currentConfiguration = new DirectoryInfo(AppContext.BaseDirectory)
            .Parent?
            .Parent?
            .Name;
        var hostBinDirectory = Path.Combine(repositoryRoot, "tests", "Velune.Tests.Render.Host", "bin");

        if (!string.IsNullOrWhiteSpace(currentConfiguration))
        {
            var configurationPath = Path.Combine(
                hostBinDirectory,
                currentConfiguration,
                "net10.0",
                "Velune.Tests.Render.Host.dll");

            if (File.Exists(configurationPath))
            {
                return configurationPath;
            }
        }

        var fallbackPath = Directory
            .EnumerateFiles(hostBinDirectory, "Velune.Tests.Render.Host.dll", SearchOption.AllDirectories)
            .OrderByDescending(path => path.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(path => path.Contains($"{Path.DirectorySeparatorChar}Debug{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        return fallbackPath ?? Path.Combine(hostBinDirectory, "Velune.Tests.Render.Host.dll");
    }

    private sealed record HostResult(bool Success, string? Error);

    private sealed record SnapshotComparisonProfile(
        int MaxChannelDelta,
        double MaxDifferentPixelRatio,
        int NeighborhoodRadius);

    private sealed record SnapshotImage(int Width, int Height, byte[] Rgba);

    private static class SnapshotPngReader
    {
        public static SnapshotImage Read(string path)
        {
            var data = File.ReadAllBytes(path);
            ValidateSignature(data);

            var offset = 8;
            var width = 0;
            var height = 0;
            using var idat = new MemoryStream();

            while (offset < data.Length)
            {
                var chunkLength = ReadInt32BigEndian(data, offset);
                offset += 4;

                var chunkType = Encoding.ASCII.GetString(data, offset, 4);
                offset += 4;

                var chunkData = new byte[chunkLength];
                Buffer.BlockCopy(data, offset, chunkData, 0, chunkLength);
                offset += chunkLength;

                offset += 4;

                switch (chunkType)
                {
                    case "IHDR":
                        width = ReadInt32BigEndian(chunkData, 0);
                        height = ReadInt32BigEndian(chunkData, 4);

                        if (chunkData[8] != 8 || chunkData[9] != 6)
                        {
                            throw new InvalidOperationException("Only RGBA8 PNG snapshots are supported.");
                        }
                        break;

                    case "IDAT":
                        idat.Write(chunkData, 0, chunkData.Length);
                        break;

                    case "IEND":
                        return new SnapshotImage(width, height, Inflate(width, height, idat.ToArray()));
                }
            }

            throw new InvalidOperationException("PNG snapshot is missing IEND.");
        }

        private static byte[] Inflate(int width, int height, byte[] compressed)
        {
            using var compressedStream = new MemoryStream(compressed);
            using var zlib = new ZLibStream(compressedStream, CompressionMode.Decompress);
            using var raw = new MemoryStream();
            zlib.CopyTo(raw);

            var decompressed = raw.ToArray();
            var stride = width * 4;
            var rgba = new byte[width * height * 4];
            var sourceOffset = 0;
            var targetOffset = 0;

            for (var row = 0; row < height; row++)
            {
                var filter = decompressed[sourceOffset++];
                if (filter != 0)
                {
                    throw new InvalidOperationException("Only PNG snapshots with filter type 0 are supported.");
                }

                Buffer.BlockCopy(decompressed, sourceOffset, rgba, targetOffset, stride);
                sourceOffset += stride;
                targetOffset += stride;
            }

            return rgba;
        }

        private static void ValidateSignature(byte[] data)
        {
            byte[] expected = [137, 80, 78, 71, 13, 10, 26, 10];

            for (var index = 0; index < expected.Length; index++)
            {
                if (data[index] != expected[index])
                {
                    throw new InvalidOperationException("Invalid PNG signature.");
                }
            }
        }

        private static int ReadInt32BigEndian(byte[] data, int offset)
        {
            return (data[offset] << 24) |
                   (data[offset + 1] << 16) |
                   (data[offset + 2] << 8) |
                   data[offset + 3];
        }
    }
}
