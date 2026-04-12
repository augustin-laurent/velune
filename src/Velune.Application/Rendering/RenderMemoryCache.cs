using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Application.DTOs;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Rendering;

public sealed partial class RenderMemoryCache : IRenderMemoryCache
{
    private readonly object _gate = new();
    private readonly Dictionary<RenderCacheKey, LinkedListNode<RenderCacheEntry>> _entries = [];
    private readonly LinkedList<RenderCacheEntry> _lru = [];
    private readonly ILogger<RenderMemoryCache> _logger;
    private readonly int _entryLimit;

    public RenderMemoryCache(
        ILogger<RenderMemoryCache> logger,
        IOptions<AppOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _entryLimit = Math.Max(0, options.Value.RenderCacheEntryLimit);
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

        var key = RenderCacheKey.Create(documentId, request);

        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var existingNode))
            {
                existingNode.Value = new RenderCacheEntry(key, renderedPage);
                _lru.Remove(existingNode);
                _lru.AddFirst(existingNode);
                return;
            }

            var node = new LinkedListNode<RenderCacheEntry>(
                new RenderCacheEntry(key, renderedPage));

            _entries[key] = node;
            _lru.AddFirst(node);

            while (_entries.Count > _entryLimit && _lru.Last is not null)
            {
                var leastRecentlyUsed = _lru.Last;
                _lru.RemoveLast();

                if (leastRecentlyUsed is not null)
                {
                    _entries.Remove(leastRecentlyUsed.Value.Key);
                }
            }
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
        double ZoomFactor,
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
                Math.Round(request.ZoomFactor, 4, MidpointRounding.AwayFromZero),
                request.Rotation,
                request.RequestedWidth ?? 0,
                request.RequestedHeight ?? 0);
        }
    }
}
