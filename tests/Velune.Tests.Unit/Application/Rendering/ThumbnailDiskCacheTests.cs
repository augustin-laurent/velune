using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Velune.Application.Configuration;
using Velune.Application.DTOs;
using Velune.Application.Rendering;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Application.Rendering;

public sealed class ThumbnailDiskCacheTests
{
    [Fact]
    public void StoreAndTryGet_ShouldReuseThumbnailAcrossSessions()
    {
        using var workspace = new TemporaryDirectory();
        string documentPath = Path.Combine(workspace.Path, "document.pdf");
        File.WriteAllText(documentPath, "original-document");

        ThumbnailDiskCache cache = CreateCache(workspace.Path, maxAgeDays: 30);
        RenderRequest request = CreateThumbnailRequest(pageIndex: 0);
        RenderedPage renderedPage = CreatePage(pageIndex: 0, width: 2, height: 1);

        cache.Store(CreateSession(documentPath), request, renderedPage);

        bool reused = cache.TryGet(CreateSession(documentPath), request, out RenderedPage? cachedPage);

        Assert.True(reused);
        Assert.NotNull(cachedPage);
        Assert.Equal(renderedPage.Width, cachedPage.Width);
        Assert.Equal(renderedPage.Height, cachedPage.Height);
        Assert.True(renderedPage.PixelData.Span.SequenceEqual(cachedPage.PixelData.Span));
    }

    [Fact]
    public void TryGet_ShouldMiss_WhenDocumentChanges()
    {
        using var workspace = new TemporaryDirectory();
        string documentPath = Path.Combine(workspace.Path, "document.pdf");
        File.WriteAllText(documentPath, "original-document");

        ThumbnailDiskCache cache = CreateCache(workspace.Path, maxAgeDays: 30);
        RenderRequest request = CreateThumbnailRequest(pageIndex: 0);

        cache.Store(CreateSession(documentPath), request, CreatePage(pageIndex: 0, width: 2, height: 1));

        File.WriteAllText(documentPath, "updated-document-with-a-new-signature");
        File.SetLastWriteTimeUtc(documentPath, DateTime.UtcNow.AddMinutes(1));

        bool reused = cache.TryGet(CreateSession(documentPath), request, out RenderedPage? cachedPage);

        Assert.False(reused);
        Assert.Null(cachedPage);
    }

    [Fact]
    public void Constructor_ShouldDeleteExpiredCacheEntries()
    {
        using var workspace = new TemporaryDirectory();
        string documentPath = Path.Combine(workspace.Path, "document.pdf");
        File.WriteAllText(documentPath, "original-document");

        string cacheRootPath = Path.Combine(workspace.Path, "thumbnail-cache");
        RenderRequest request = CreateThumbnailRequest(pageIndex: 0);

        ThumbnailDiskCache initialCache = CreateCache(workspace.Path, maxAgeDays: 1);
        initialCache.Store(CreateSession(documentPath), request, CreatePage(pageIndex: 0, width: 2, height: 1));

        string cacheFilePath = Directory
            .EnumerateFiles(cacheRootPath, "*", SearchOption.AllDirectories)
            .Single();

        File.SetLastWriteTimeUtc(cacheFilePath, DateTime.UtcNow.AddDays(-3));

        _ = CreateCache(workspace.Path, maxAgeDays: 1);

        Assert.False(File.Exists(cacheFilePath));
    }

    private static ThumbnailDiskCache CreateCache(string workspacePath, int maxAgeDays)
    {
        return new ThumbnailDiskCache(
            NullLogger<ThumbnailDiskCache>.Instance,
            Options.Create(new AppOptions
            {
                Name = "Velune",
                ThumbnailDiskCachePath = Path.Combine(workspacePath, "thumbnail-cache"),
                ThumbnailDiskCacheMaxAgeDays = maxAgeDays
            }));
    }

    private static RenderRequest CreateThumbnailRequest(int pageIndex)
    {
        return new RenderRequest(
            $"thumbnail:{pageIndex}",
            new PageIndex(pageIndex),
            0.20,
            Rotation.Deg0,
            170,
            150,
            RenderPriority.Thumbnail);
    }

    private static DocumentSession CreateSession(string documentPath)
    {
        var fileInfo = new FileInfo(documentPath);

        return new DocumentSession(
            DocumentId.New(),
            new DocumentMetadata(
                fileInfo.Name,
                fileInfo.FullName,
                DocumentType.Pdf,
                fileInfo.Length,
                pageCount: 4),
            ViewportState.Default);
    }

    private static RenderedPage CreatePage(int pageIndex, int width, int height)
    {
        byte[] pixelData = new byte[width * height * 4];
        Array.Fill<byte>(pixelData, 255);

        return new RenderedPage(
            new PageIndex(pageIndex),
            pixelData,
            width,
            height);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"velune-thumbnail-cache-tests-{Guid.NewGuid():N}");

            Directory.CreateDirectory(Path);
        }

        public string Path
        {
            get;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
