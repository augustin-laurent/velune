using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Velune.Application.Configuration;
using Velune.Application.Text;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Application.Text;

public sealed class DocumentTextDiskCacheTests
{
    [Fact]
    public void StoreAndTryGet_ShouldRoundTripIndex()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        using var temporaryFile = new TemporaryFile(".png");
        var cache = CreateCache(temporaryDirectory.Path);
        var session = CreateSession(temporaryFile.Path);
        var index = CreateIndex(temporaryFile.Path, "Velune OCR");

        cache.Store(session, "tesseract-5", ["eng"], forceOcr: true, index);

        var found = cache.TryGet(session, "tesseract-5", ["eng"], forceOcr: true, out var cachedIndex);

        Assert.True(found);
        Assert.NotNull(cachedIndex);
        Assert.Equal("Velune OCR", cachedIndex.Pages[0].Text);
    }

    [Fact]
    public void TryGet_ShouldMiss_WhenSourceFileChanges()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        using var temporaryFile = new TemporaryFile(".png");
        var cache = CreateCache(temporaryDirectory.Path);
        var index = CreateIndex(temporaryFile.Path, "Velune OCR");

        cache.Store(CreateSession(temporaryFile.Path), "tesseract-5", ["eng"], forceOcr: true, index);

        File.WriteAllText(temporaryFile.Path, "changed-content");
        File.SetLastWriteTimeUtc(temporaryFile.Path, DateTime.UtcNow.AddMinutes(1));

        var found = cache.TryGet(CreateSession(temporaryFile.Path), "tesseract-5", ["eng"], forceOcr: true, out _);

        Assert.False(found);
    }

    [Fact]
    public void TryGet_ShouldMiss_WhenEngineFingerprintOrLanguagesDiffer()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        using var temporaryFile = new TemporaryFile(".png");
        var cache = CreateCache(temporaryDirectory.Path);
        var session = CreateSession(temporaryFile.Path);

        cache.Store(session, "tesseract-5", ["eng"], forceOcr: true, CreateIndex(temporaryFile.Path, "Velune OCR"));

        Assert.False(cache.TryGet(session, "tesseract-6", ["eng"], forceOcr: true, out _));
        Assert.False(cache.TryGet(session, "tesseract-5", ["fra"], forceOcr: true, out _));
        Assert.False(cache.TryGet(session, "tesseract-5", ["eng"], forceOcr: false, out _));
    }

    private static DocumentTextDiskCache CreateCache(string rootPath)
    {
        return new DocumentTextDiskCache(
            NullLogger<DocumentTextDiskCache>.Instance,
            Options.Create(new AppOptions
            {
                OcrCachePath = rootPath
            }));
    }

    private static DocumentSession CreateSession(string filePath)
    {
        var fileInfo = new FileInfo(filePath);

        return new DocumentSession(
            DocumentId.New(),
            new DocumentMetadata(
                Path.GetFileName(filePath),
                filePath,
                DocumentType.Image,
                fileInfo.Length,
                1,
                pixelWidth: 640,
                pixelHeight: 240,
                formatLabel: "PNG image"),
            ViewportState.Default);
    }

    private static DocumentTextIndex CreateIndex(string filePath, string text)
    {
        return new DocumentTextIndex(
            filePath,
            DocumentType.Image,
            [
                new PageTextContent(
                    new PageIndex(0),
                    TextSourceKind.Ocr,
                    text,
                    [new TextRun(
                        text,
                        0,
                        text.Length,
                        [new NormalizedTextRegion(0.1, 0.1, 0.6, 0.08)])],
                    640,
                    240)
            ],
            ["eng"]);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "velune-tests", Guid.NewGuid().ToString("N"));
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

    private sealed class TemporaryFile : IDisposable
    {
        public TemporaryFile(string extension)
        {
            Path = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), extension);
            File.WriteAllText(Path, "seed");
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (File.Exists(Path))
                {
                    File.Delete(Path);
                }
            }
            catch
            {
            }
        }
    }
}
