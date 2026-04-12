using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Application.DTOs;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Instrumentation;

public sealed partial class DevelopmentPerformanceMetrics : IPerformanceMetrics
{
    private readonly ConcurrentDictionary<DocumentId, OpenMetricState> _openedDocuments = [];
    private readonly bool _isEnabled;
    private readonly ILogger<DevelopmentPerformanceMetrics> _logger;

    public DevelopmentPerformanceMetrics(
        ILogger<DevelopmentPerformanceMetrics> logger,
        IOptions<AppOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _isEnabled = string.Equals(
            options.Value.Environment,
            "Development",
            StringComparison.OrdinalIgnoreCase);
    }

    public void RecordDocumentOpened(
        IDocumentSession session,
        TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!_isEnabled)
        {
            return;
        }

        var memorySnapshot = MemorySnapshot.Capture();

        _openedDocuments[session.Id] = new OpenMetricState(
            duration,
            Stopwatch.GetTimestamp());

        LogDocumentOpenMetric(
            _logger,
            session.Metadata.FileName,
            session.Metadata.DocumentType,
            session.Metadata.PageCount ?? 1,
            session.Metadata.FileSizeInBytes,
            duration.TotalMilliseconds,
            memorySnapshot.ManagedMemoryMb,
            memorySnapshot.WorkingSetMb);
    }

    public void RecordViewerRenderCompleted(
        IDocumentSession session,
        RenderResult result)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(result);

        if (!_isEnabled ||
            !result.IsSuccess ||
            !_openedDocuments.TryRemove(session.Id, out var openMetricState))
        {
            return;
        }

        var memorySnapshot = MemorySnapshot.Capture();
        var timeToFirstPage = openMetricState.OpenDuration
                              + Stopwatch.GetElapsedTime(openMetricState.OpenedAtTimestamp);

        LogFirstPageRenderMetric(
            _logger,
            session.Metadata.FileName,
            result.PageIndex.Value + 1,
            result.Duration.TotalMilliseconds,
            timeToFirstPage.TotalMilliseconds,
            memorySnapshot.ManagedMemoryMb,
            memorySnapshot.WorkingSetMb);
    }

    public void RecordThumbnailCompleted(
        IDocumentSession session,
        RenderResult result)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(result);

        if (!_isEnabled ||
            !result.IsSuccess ||
            result.Page is null)
        {
            return;
        }

        var memorySnapshot = MemorySnapshot.Capture();

        LogThumbnailRenderMetric(
            _logger,
            session.Metadata.FileName,
            result.PageIndex.Value + 1,
            result.Duration.TotalMilliseconds,
            result.Page.Width,
            result.Page.Height,
            memorySnapshot.ManagedMemoryMb,
            memorySnapshot.WorkingSetMb);
    }

    public void Clear(DocumentId documentId)
    {
        if (!_isEnabled)
        {
            return;
        }

        _openedDocuments.TryRemove(documentId, out _);
    }

    [LoggerMessage(
        EventId = 40,
        Level = LogLevel.Information,
        Message = "MVP metric | DocumentOpen | FileName={FileName} | DocumentType={DocumentType} | PageCount={PageCount} | FileSizeBytes={FileSizeBytes} | OpenDurationMs={OpenDurationMs} | ManagedMemoryMb={ManagedMemoryMb} | WorkingSetMb={WorkingSetMb}")]
    private static partial void LogDocumentOpenMetric(
        ILogger logger,
        string fileName,
        DocumentType documentType,
        int pageCount,
        long fileSizeBytes,
        double openDurationMs,
        double managedMemoryMb,
        double workingSetMb);

    [LoggerMessage(
        EventId = 41,
        Level = LogLevel.Information,
        Message = "MVP metric | FirstPageRender | FileName={FileName} | PageNumber={PageNumber} | RenderDurationMs={RenderDurationMs} | TimeToFirstPageMs={TimeToFirstPageMs} | ManagedMemoryMb={ManagedMemoryMb} | WorkingSetMb={WorkingSetMb}")]
    private static partial void LogFirstPageRenderMetric(
        ILogger logger,
        string fileName,
        int pageNumber,
        double renderDurationMs,
        double timeToFirstPageMs,
        double managedMemoryMb,
        double workingSetMb);

    [LoggerMessage(
        EventId = 42,
        Level = LogLevel.Information,
        Message = "MVP metric | ThumbnailRender | FileName={FileName} | PageNumber={PageNumber} | ThumbnailDurationMs={ThumbnailDurationMs} | Width={Width} | Height={Height} | ManagedMemoryMb={ManagedMemoryMb} | WorkingSetMb={WorkingSetMb}")]
    private static partial void LogThumbnailRenderMetric(
        ILogger logger,
        string fileName,
        int pageNumber,
        double thumbnailDurationMs,
        int width,
        int height,
        double managedMemoryMb,
        double workingSetMb);

    private readonly record struct OpenMetricState(
        TimeSpan OpenDuration,
        long OpenedAtTimestamp);

    private readonly record struct MemorySnapshot(
        double ManagedMemoryMb,
        double WorkingSetMb)
    {
        public static MemorySnapshot Capture()
        {
            var managedMemoryMb = Math.Round(
                GC.GetTotalMemory(forceFullCollection: false) / (1024d * 1024d),
                1);

            using var process = Process.GetCurrentProcess();
            var workingSetMb = Math.Round(
                process.WorkingSet64 / (1024d * 1024d),
                1);

            return new MemorySnapshot(
                managedMemoryMb,
                workingSetMb);
        }
    }
}
