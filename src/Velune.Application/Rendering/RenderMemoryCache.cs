using Microsoft.Extensions.Logging;
using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Rendering;

public sealed partial class RenderMemoryCache : IRenderMemoryCache, IDisposable
{
    private const int MaxCachedPageBytes = 64 * 1024 * 1024;
    private const long MaxTotalCacheBytes = 256L * 1024 * 1024;
    private readonly object _gate = new();
    private readonly Dictionary<RenderCacheKey, LinkedListNode<RenderCacheEntry>> _entries = [];
    private readonly LinkedList<RenderCacheEntry> _lru = [];
    private readonly ILogger<RenderMemoryCache> _logger;
    private readonly IUserPreferencesService _userPreferencesService;
    private volatile int _entryLimit;
    private long _totalCachedBytes;
    private bool _disposed;

    public RenderMemoryCache(
        ILogger<RenderMemoryCache> logger,
        IUserPreferencesService userPreferencesService)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(userPreferencesService);

        _logger = logger;
        _userPreferencesService = userPreferencesService;
        _entryLimit = Math.Max(0, _userPreferencesService.Current.MemoryCacheEntryLimit);
        _userPreferencesService.PreferencesChanged += OnPreferencesChanged;
    }

    public bool TryGet(
        DocumentId documentId,
        RenderRequest request,
        out RenderedPage? renderedPage)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_entryLimit == 0)
        {
            renderedPage = null;
            LogCacheMiss(_logger, documentId, request.PageIndex, request.ZoomFactor, request.Rotation, request.RequestedWidth, request.RequestedHeight);
            return false;
        }

        var key = RenderCacheKey.Create(documentId, request);

        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var node))
            {
                renderedPage = null;
                LogCacheMiss(_logger, documentId, request.PageIndex, request.ZoomFactor, request.Rotation, request.RequestedWidth, request.RequestedHeight);
                return false;
            }

            _lru.Remove(node);
            _lru.AddFirst(node);

            renderedPage = node.Value.RenderedPage;
            LogCacheHit(_logger, documentId, request.PageIndex, request.ZoomFactor, request.Rotation, request.RequestedWidth, request.RequestedHeight);
            return true;
        }
    }

    public void Store(
        DocumentId documentId,
        RenderRequest request,
        RenderedPage renderedPage)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(renderedPage);

        if (_entryLimit == 0)
        {
            return;
        }

        if (renderedPage.ByteCount > MaxCachedPageBytes)
        {
            return;
        }

        var key = RenderCacheKey.Create(documentId, request);

        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var existingNode))
            {
                _totalCachedBytes -= existingNode.Value.RenderedPage.ByteCount;
                existingNode.Value = new RenderCacheEntry(key, renderedPage);
                _lru.Remove(existingNode);
                _lru.AddFirst(existingNode);
                _totalCachedBytes += renderedPage.ByteCount;
                TrimToBudget();
                return;
            }

            var node = new LinkedListNode<RenderCacheEntry>(
                new RenderCacheEntry(key, renderedPage));

            _entries[key] = node;
            _lru.AddFirst(node);
            _totalCachedBytes += renderedPage.ByteCount;
            TrimToBudget();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _userPreferencesService.PreferencesChanged -= OnPreferencesChanged;
        _disposed = true;
    }

    private void OnPreferencesChanged(object? sender, EventArgs e)
    {
        var updatedLimit = Math.Max(0, _userPreferencesService.Current.MemoryCacheEntryLimit);
        _entryLimit = updatedLimit;
        TrimToLimit(updatedLimit);
    }

    private void TrimToLimit(int entryLimit)
    {
        lock (_gate)
        {
            if (entryLimit == 0)
            {
                _entries.Clear();
                _lru.Clear();
                _totalCachedBytes = 0;
                return;
            }

            TrimToBudget(entryLimit);
        }
    }

    private void TrimToBudget(int? entryLimitOverride = null)
    {
        var entryLimit = entryLimitOverride ?? _entryLimit;

        while ((_entries.Count > entryLimit || _totalCachedBytes > MaxTotalCacheBytes) &&
               _lru.Last is not null)
        {
            var leastRecentlyUsed = _lru.Last;
            _lru.RemoveLast();

            if (leastRecentlyUsed is null)
            {
                continue;
            }

            _entries.Remove(leastRecentlyUsed.Value.Key);
            _totalCachedBytes -= leastRecentlyUsed.Value.RenderedPage.ByteCount;
        }
    }

    [LoggerMessage(
        EventId = 20,
        Level = LogLevel.Debug,
        Message = "Render cache hit for DocumentId={DocumentId}, PageIndex={PageIndex}, ZoomFactor={ZoomFactor}, Rotation={Rotation}, RequestedWidth={RequestedWidth}, RequestedHeight={RequestedHeight}")]
    private static partial void LogCacheHit(
        ILogger logger,
        DocumentId documentId,
        PageIndex pageIndex,
        double zoomFactor,
        Rotation rotation,
        int? requestedWidth,
        int? requestedHeight);

    [LoggerMessage(
        EventId = 21,
        Level = LogLevel.Debug,
        Message = "Render cache miss for DocumentId={DocumentId}, PageIndex={PageIndex}, ZoomFactor={ZoomFactor}, Rotation={Rotation}, RequestedWidth={RequestedWidth}, RequestedHeight={RequestedHeight}")]
    private static partial void LogCacheMiss(
        ILogger logger,
        DocumentId documentId,
        PageIndex pageIndex,
        double zoomFactor,
        Rotation rotation,
        int? requestedWidth,
        int? requestedHeight);

    private sealed record RenderCacheEntry(
        RenderCacheKey Key,
        RenderedPage RenderedPage);

    private sealed record RenderCacheKey(
        DocumentId DocumentId,
        PageIndex PageIndex,
        int ZoomFactorTimes10000,
        Rotation Rotation,
        int RequestedWidth,
        int RequestedHeight)
    {
        public static RenderCacheKey Create(
            DocumentId documentId,
            RenderRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return new RenderCacheKey(
                documentId,
                request.PageIndex,
                (int)Math.Round(request.ZoomFactor * 10000, 0, MidpointRounding.AwayFromZero),
                request.Rotation,
                request.RequestedWidth ?? 0,
                request.RequestedHeight ?? 0);
        }
    }
}
