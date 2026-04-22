using System.Diagnostics;
using System.Text.Json;
using Velune.Domain.Documents;

namespace Velune.Tests.Integration.Infrastructure;

[Collection("IntegrationSerial")]
public sealed class AdaptersIntegrationTests
{
    [Fact]
    public async Task SamplePdf_ShouldOpenAndRenderFirstPage()
    {
        var result = await RunHostAsync("sample.pdf");

        Assert.True(result.Success, result.Error);
        Assert.Equal(nameof(DocumentType.Pdf), result.DocumentType);
        Assert.Equal(1, result.PageCount);
        Assert.True(result.RenderedWidth > 0);
        Assert.True(result.RenderedHeight > 0);
        Assert.Equal(result.RenderedWidth * result.RenderedHeight * 4, result.PixelBufferLength);
    }

    [Fact]
    public async Task SampleWideImage_ShouldOpenAndRenderMinimalPage()
    {
        var result = await RunHostAsync("sample-wide.png");

        Assert.True(result.Success, result.Error);
        Assert.Equal(nameof(DocumentType.Image), result.DocumentType);
        Assert.Equal(1, result.PageCount);
        Assert.Equal(8, result.RenderedWidth);
        Assert.Equal(4, result.RenderedHeight);
    }

    [Fact]
    public async Task SamplePortraitImage_ShouldOpenAndRenderMinimalPage()
    {
        var result = await RunHostAsync("sample-portrait.png", rotationDegrees: 90);

        Assert.True(result.Success, result.Error);
        Assert.Equal(nameof(DocumentType.Image), result.DocumentType);
        Assert.Equal(1, result.PageCount);
        Assert.Equal(8, result.RenderedWidth);
        Assert.Equal(4, result.RenderedHeight);
    }

    private static async Task<HostResult> RunHostAsync(string fixtureName, int rotationDegrees = 0)
    {
        var repositoryRoot = GetRepositoryRoot();
        var hostDllPath = GetHostDllPath(repositoryRoot);
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);

        Assert.True(File.Exists(hostDllPath), $"Host executable not found: {hostDllPath}");
        Assert.True(File.Exists(fixturePath), $"Fixture not found: {fixturePath}");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            ArgumentList = { hostDllPath, fixturePath, rotationDegrees.ToString() },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            Assert.Fail($"Host failed with exit code {process.ExitCode}:{Environment.NewLine}{standardError}{standardOutput}");
        }

        var result = JsonSerializer.Deserialize<HostResult>(
            standardOutput,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        Assert.NotNull(result);
        return result;
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

    private sealed record HostResult(
        bool Success,
        string? DocumentType,
        int? PageCount,
        int RenderedWidth,
        int RenderedHeight,
        int PixelBufferLength,
        string? Error);
}
