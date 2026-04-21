using Microsoft.Extensions.Logging;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Application.DTOs;
using Velune.Application.Rendering;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Application.Rendering;

public sealed class RenderMemoryCacheTests
{
    [Fact]
    public void Store_ShouldRespectConfiguredLruLimit()
    {
        var logger = new ListLogger<RenderMemoryCache>();
        using var cache = CreateCache(logger, entryLimit: 2);
        var documentId = DocumentId.New();

        cache.Store(documentId, CreateRequest(pageIndex: 0), CreatePage(pageIndex: 0));
        cache.Store(documentId, CreateRequest(pageIndex: 1), CreatePage(pageIndex: 1));

        Assert.True(cache.TryGet(documentId, CreateRequest(pageIndex: 0), out _));

        cache.Store(documentId, CreateRequest(pageIndex: 2), CreatePage(pageIndex: 2));

        Assert.True(cache.TryGet(documentId, CreateRequest(pageIndex: 0), out _));
        Assert.False(cache.TryGet(documentId, CreateRequest(pageIndex: 1), out _));
        Assert.True(cache.TryGet(documentId, CreateRequest(pageIndex: 2), out _));
    }

    [Fact]
    public void TryGet_ShouldMiss_WhenZoomRotationOrResolutionDiffers()
    {
        var logger = new ListLogger<RenderMemoryCache>();
        using var cache = CreateCache(logger, entryLimit: 4);
        var documentId = DocumentId.New();

        cache.Store(
            documentId,
            CreateRequest(pageIndex: 0, zoomFactor: 1.0, rotation: Rotation.Deg0, requestedWidth: 170, requestedHeight: 150),
            CreatePage(pageIndex: 0));

        Assert.False(cache.TryGet(documentId, CreateRequest(pageIndex: 0, zoomFactor: 1.1, rotation: Rotation.Deg0, requestedWidth: 170, requestedHeight: 150), out _));
        Assert.False(cache.TryGet(documentId, CreateRequest(pageIndex: 0, zoomFactor: 1.0, rotation: Rotation.Deg90, requestedWidth: 170, requestedHeight: 150), out _));
        Assert.False(cache.TryGet(documentId, CreateRequest(pageIndex: 0, zoomFactor: 1.0, rotation: Rotation.Deg0, requestedWidth: 160, requestedHeight: 150), out _));
        Assert.True(cache.TryGet(documentId, CreateRequest(pageIndex: 0, zoomFactor: 1.0, rotation: Rotation.Deg0, requestedWidth: 170, requestedHeight: 150), out _));
    }

    [Fact]
    public void TryGet_ShouldLogHitAndMiss()
    {
        var logger = new ListLogger<RenderMemoryCache>();
        using var cache = CreateCache(logger, entryLimit: 2);
        var documentId = DocumentId.New();
        var request = CreateRequest(pageIndex: 0);

        Assert.False(cache.TryGet(documentId, request, out _));

        cache.Store(documentId, request, CreatePage(pageIndex: 0));

        Assert.True(cache.TryGet(documentId, request, out _));
        Assert.Contains(logger.Entries, entry => entry.LogLevel == LogLevel.Debug && entry.Message.Contains("miss", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logger.Entries, entry => entry.LogLevel == LogLevel.Debug && entry.Message.Contains("hit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PreferencesChange_ShouldTrimEntriesToUpdatedLimit()
    {
        var logger = new ListLogger<RenderMemoryCache>();
        var preferencesService = new StubUserPreferencesService(entryLimit: 3);
        using var cache = new RenderMemoryCache(logger, preferencesService);
        var documentId = DocumentId.New();

        cache.Store(documentId, CreateRequest(pageIndex: 0), CreatePage(pageIndex: 0));
        cache.Store(documentId, CreateRequest(pageIndex: 1), CreatePage(pageIndex: 1));
        cache.Store(documentId, CreateRequest(pageIndex: 2), CreatePage(pageIndex: 2));

        Assert.True(cache.TryGet(documentId, CreateRequest(pageIndex: 2), out _));

        preferencesService.UpdateEntryLimit(1);

        Assert.False(cache.TryGet(documentId, CreateRequest(pageIndex: 0), out _));
        Assert.False(cache.TryGet(documentId, CreateRequest(pageIndex: 1), out _));
        Assert.True(cache.TryGet(documentId, CreateRequest(pageIndex: 2), out _));
    }

    [Fact]
    public void TryGet_ShouldUseNormalizedZoomKey()
    {
        var logger = new ListLogger<RenderMemoryCache>();
        using var cache = CreateCache(logger, entryLimit: 4);
        var documentId = DocumentId.New();

        cache.Store(
            documentId,
            CreateRequest(pageIndex: 0, zoomFactor: 1.00004),
            CreatePage(pageIndex: 0));

        var reused = cache.TryGet(documentId, CreateRequest(pageIndex: 0, zoomFactor: 1.000049), out _);

        Assert.True(reused);
    }

    [Fact]
    public void Store_ShouldSkipPagesThatExceedPerEntryBudget()
    {
        var logger = new ListLogger<RenderMemoryCache>();
        using var cache = CreateCache(logger, entryLimit: 4);
        var documentId = DocumentId.New();
        var request = CreateRequest(pageIndex: 0);
        var oversizedPage = CreatePage(pageIndex: 0, width: 4097, height: 4096);

        cache.Store(documentId, request, oversizedPage);

        Assert.False(cache.TryGet(documentId, request, out _));
    }

    private static RenderMemoryCache CreateCache(ILogger<RenderMemoryCache> logger, int entryLimit)
    {
        return new RenderMemoryCache(
            logger,
            new StubUserPreferencesService(entryLimit));
    }

    private static RenderRequest CreateRequest(
        int pageIndex,
        double zoomFactor = 1.0,
        Rotation rotation = Rotation.Deg0,
        int? requestedWidth = null,
        int? requestedHeight = null)
    {
        return new RenderRequest(
            "viewer",
            new PageIndex(pageIndex),
            zoomFactor,
            rotation,
            requestedWidth,
            requestedHeight);
    }

    private static RenderedPage CreatePage(int pageIndex, int width = 1, int height = 1)
    {
        var pixelData = new byte[width * height * 4];

        return new RenderedPage(
            new PageIndex(pageIndex),
            pixelData,
            width,
            height);
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message);

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }

    private sealed class StubUserPreferencesService : IUserPreferencesService
    {
        public StubUserPreferencesService(int entryLimit)
        {
            Current = UserPreferences.CreateDefault(entryLimit);
        }

        public UserPreferences Current { get; private set; }

        public event EventHandler? PreferencesChanged;

        public Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default)
        {
            Current = preferences;
            PreferencesChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public void UpdateEntryLimit(int entryLimit)
        {
            Current = Current with { MemoryCacheEntryLimit = entryLimit };
            PreferencesChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
