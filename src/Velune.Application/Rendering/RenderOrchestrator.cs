using System.Diagnostics;
using System.Runtime.CompilerServices;
using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Rendering;

public sealed class RenderOrchestrator : IRenderOrchestrator
{
    private readonly object _gate = new();
    private readonly Queue<Guid> _queue = [];
    private readonly Dictionary<Guid, QueuedRenderJob> _jobs = [];
    private readonly IPerformanceMetrics _performanceMetrics;
    private readonly IRenderMemoryCache _renderMemoryCache;
    private readonly IThumbnailDiskCache _thumbnailDiskCache;
    private readonly IDocumentSessionStore _sessionStore;
    private readonly IRenderService _renderService;
    private readonly CancellationTokenSource _shutdownCancellationTokenSource = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly Task _worker;
    private bool _disposed;

    public RenderOrchestrator(
        IPerformanceMetrics performanceMetrics,
        IRenderMemoryCache renderMemoryCache,
        IThumbnailDiskCache thumbnailDiskCache,
        IDocumentSessionStore sessionStore,
        IRenderService renderService)
    {
        ArgumentNullException.ThrowIfNull(performanceMetrics);
        ArgumentNullException.ThrowIfNull(renderMemoryCache);
        ArgumentNullException.ThrowIfNull(thumbnailDiskCache);
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(renderService);

        _performanceMetrics = performanceMetrics;
        _renderMemoryCache = renderMemoryCache;
        _thumbnailDiskCache = thumbnailDiskCache;
        _sessionStore = sessionStore;
        _renderService = renderService;
        _worker = Task.Run(ProcessQueueAsync);
    }

    public RenderJobHandle Submit(RenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(request.JobKey))
        {
            return CreateCompletedHandle(
                request,
                default,
                ResultFactory.Failure<RenderedPage>(
                    AppError.Validation(
                        "document.render.job_key.empty",
                        "The render job key cannot be empty.")));
        }

        if (request.ZoomFactor <= 0)
        {
            return CreateCompletedHandle(
                request,
                default,
                ResultFactory.Failure<RenderedPage>(
                    AppError.Validation(
                        "document.render.zoom.invalid",
                        "Zoom factor must be greater than zero.")));
        }

        var session = _sessionStore.Current;
        if (session is null)
        {
            return CreateCompletedHandle(
                request,
                default,
                ResultFactory.Failure<RenderedPage>(
                    AppError.NotFound(
                        "document.session.missing",
                        "No active document session.")));
        }

        var pageCount = session.Metadata.PageCount;
        if (pageCount.HasValue &&
            (request.PageIndex.Value < 0 || request.PageIndex.Value >= pageCount.Value))
        {
            return CreateCompletedHandle(
                request,
                session.Id,
                ResultFactory.Failure<RenderedPage>(
                    AppError.Validation(
                        "document.page.out_of_range",
                        "The requested page is out of range.")));
        }

        List<QueuedRenderJob> obsoleteJobs = [];

        lock (_gate)
        {
            foreach (var existingJob in _jobs.Values.Where(existingJob => existingJob.Request.JobKey == request.JobKey))
            {
                obsoleteJobs.Add(existingJob);
            }

            foreach (var obsoleteJob in obsoleteJobs.Where(obsoleteJob => !obsoleteJob.IsRunning))
            {
                _jobs.Remove(obsoleteJob.Id);
            }
        }

        foreach (var obsoleteJob in obsoleteJobs)
        {
            CancelJob(obsoleteJob, isObsolete: true);
        }

        if (_renderMemoryCache.TryGet(session.Id, request, out var cachedPage) &&
            cachedPage is not null)
        {
            var cachedJobId = Guid.NewGuid();
            var cachedResult = new RenderResult(
                cachedJobId,
                session.Id,
                request.JobKey,
                request.PageIndex,
                TimeSpan.Zero,
                cachedPage,
                Error: null,
                IsCanceled: false,
                IsObsolete: false);

            RecordPerformanceMetric(session, request, cachedResult);

            return new RenderJobHandle(
                cachedJobId,
                Task.FromResult(cachedResult));
        }

        var job = new QueuedRenderJob(
            Guid.NewGuid(),
            request,
            session,
            TaskCompletionSourceFactory.Create<RenderResult>(),
            CancellationTokenSource.CreateLinkedTokenSource(_shutdownCancellationTokenSource.Token));

        lock (_gate)
        {
            _jobs[job.Id] = job;
            _queue.Enqueue(job.Id);
        }

        _signal.Release();
        return new RenderJobHandle(job.Id, job.CompletionSource.Task);
    }

    public bool Cancel(Guid jobId)
    {
        ThrowIfDisposed();

        QueuedRenderJob? job;

        lock (_gate)
        {
            if (!_jobs.TryGetValue(jobId, out job))
            {
                return false;
            }

            if (!job.IsRunning)
            {
                _jobs.Remove(jobId);
            }
        }

        return CancelJob(job, isObsolete: false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdownCancellationTokenSource.Cancel();

        List<QueuedRenderJob> jobsToCancel;
        lock (_gate)
        {
            jobsToCancel = [.. _jobs.Values];
            _jobs.Clear();
        }

        foreach (var job in jobsToCancel)
        {
            CancelJob(job, isObsolete: false);
        }

        try
        {
            _worker.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _signal.Dispose();
            _shutdownCancellationTokenSource.Dispose();
        }
    }

    private bool CancelJob(QueuedRenderJob job, bool isObsolete)
    {
        ArgumentNullException.ThrowIfNull(job);

        job.IsObsolete |= isObsolete;
        job.CancellationTokenSource.Cancel();

        if (job.IsRunning)
        {
            return true;
        }

        if (job.TryComplete(CreateCanceledResult(job, TimeSpan.Zero)))
        {
            job.Dispose();
        }

        return true;
    }

    private RenderJobHandle CreateCompletedHandle(
        RenderRequest request,
        DocumentId documentId,
        Result<RenderedPage> result)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        var jobId = Guid.NewGuid();
        var renderResult = CreateResult(
            jobId,
            documentId,
            request,
            TimeSpan.Zero,
            result,
            isCanceled: false,
            isObsolete: false);

        return new RenderJobHandle(jobId, Task.FromResult(renderResult));
    }

    private async Task ProcessQueueAsync()
    {
        while (true)
        {
            try
            {
                await _signal.WaitAsync(_shutdownCancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var job = DequeueNextJob();
            if (job is null)
            {
                continue;
            }

            await ExecuteJobAsync(job);
        }
    }

    private QueuedRenderJob? DequeueNextJob()
    {
        lock (_gate)
        {
            while (_queue.Count > 0)
            {
                var jobId = _queue.Dequeue();
                if (!_jobs.TryGetValue(jobId, out var job))
                {
                    continue;
                }

                if (job.CompletionSource.Task.IsCompleted)
                {
                    _jobs.Remove(jobId);
                    job.Dispose();
                    continue;
                }

                job.IsRunning = true;
                return job;
            }
        }

        return null;
    }

    private async Task ExecuteJobAsync(QueuedRenderJob job)
    {
        var stopwatch = Stopwatch.StartNew();
        RenderResult result;

        try
        {
            if (IsThumbnailRequest(job.Request) &&
                _thumbnailDiskCache.TryGet(job.Session, job.Request, out var cachedThumbnail) &&
                cachedThumbnail is not null)
            {
                result = CreateResult(
                    job.Id,
                    job.Session.Id,
                    job.Request,
                    stopwatch.Elapsed,
                    ResultFactory.Success(cachedThumbnail),
                    isCanceled: false,
                    isObsolete: job.IsObsolete);

                _renderMemoryCache.Store(
                    job.Session.Id,
                    job.Request,
                    cachedThumbnail);
            }
            else
            {
                var renderedPage = await _renderService.RenderPageAsync(
                    job.Session,
                    job.Request.PageIndex,
                    job.Request.ZoomFactor,
                    job.Request.Rotation,
                    job.CancellationTokenSource.Token);

                result = CreateResult(
                    job.Id,
                    job.Session.Id,
                    job.Request,
                    stopwatch.Elapsed,
                    ResultFactory.Success(renderedPage),
                    isCanceled: false,
                    isObsolete: job.IsObsolete);

                _renderMemoryCache.Store(
                    job.Session.Id,
                    job.Request,
                    renderedPage);

                if (IsThumbnailRequest(job.Request))
                {
                    _thumbnailDiskCache.Store(
                        job.Session,
                        job.Request,
                        renderedPage);
                }
            }
        }
        catch (OperationCanceledException)
        {
            result = CreateCanceledResult(job, stopwatch.Elapsed);
        }
        catch (Exception exception)
        {
            result = CreateResult(
                job.Id,
                job.Session.Id,
                job.Request,
                stopwatch.Elapsed,
                ResultFactory.Failure<RenderedPage>(RenderErrorMapper.Map(exception)),
                isCanceled: false,
                isObsolete: job.IsObsolete);
        }
        finally
        {
            stopwatch.Stop();

            lock (_gate)
            {
                _jobs.Remove(job.Id);
            }
        }

        RecordPerformanceMetric(job.Session, job.Request, result);

        if (job.TryComplete(result))
        {
            job.Dispose();
        }
    }

    private static RenderResult CreateCanceledResult(
        QueuedRenderJob job,
        TimeSpan duration)
    {
        return new RenderResult(
            job.Id,
            job.Session.Id,
            job.Request.JobKey,
            job.Request.PageIndex,
            duration,
            Page: null,
            Error: null,
            IsCanceled: true,
            IsObsolete: job.IsObsolete);
    }

    private static RenderResult CreateResult(
        Guid jobId,
        DocumentId documentId,
        RenderRequest request,
        TimeSpan duration,
        Result<RenderedPage> result,
        bool isCanceled,
        bool isObsolete)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        return new RenderResult(
            jobId,
            documentId,
            request.JobKey,
            request.PageIndex,
            duration,
            Page: result.Value,
            Error: result.Error,
            IsCanceled: isCanceled,
            IsObsolete: isObsolete);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static bool IsThumbnailRequest(RenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.RequestedWidth.HasValue && request.RequestedHeight.HasValue;
    }

    private void RecordPerformanceMetric(
        IDocumentSession session,
        RenderRequest request,
        RenderResult result)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        if (!result.IsSuccess)
        {
            return;
        }

        if (IsThumbnailRequest(request))
        {
            _performanceMetrics.RecordThumbnailCompleted(session, result);
            return;
        }

        _performanceMetrics.RecordViewerRenderCompleted(session, result);
    }

    private sealed class QueuedRenderJob : IDisposable
    {
        private bool _disposed;

        public QueuedRenderJob(
            Guid id,
            RenderRequest request,
            IDocumentSession session,
            TaskCompletionSource<RenderResult> completionSource,
            CancellationTokenSource cancellationTokenSource)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(session);
            ArgumentNullException.ThrowIfNull(completionSource);
            ArgumentNullException.ThrowIfNull(cancellationTokenSource);

            Id = id;
            Request = request;
            Session = session;
            CompletionSource = completionSource;
            CancellationTokenSource = cancellationTokenSource;
        }

        public Guid Id
        {
            get;
        }

        public RenderRequest Request
        {
            get;
        }

        public IDocumentSession Session
        {
            get;
        }

        public TaskCompletionSource<RenderResult> CompletionSource
        {
            get;
        }

        public CancellationTokenSource CancellationTokenSource
        {
            get;
        }

        public bool IsRunning
        {
            get; set;
        }

        public bool IsObsolete
        {
            get; set;
        }

        public bool TryComplete(RenderResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            return CompletionSource.TrySetResult(result);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CancellationTokenSource.Dispose();
            _disposed = true;
        }
    }

    private static class TaskCompletionSourceFactory
    {
        public static TaskCompletionSource<T> Create<T>() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
