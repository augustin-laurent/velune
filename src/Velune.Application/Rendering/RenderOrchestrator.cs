using System.Diagnostics;
using System.Runtime.CompilerServices;
using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Orchestration;
using Velune.Application.Results;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Rendering;

/// <summary>Queues, prioritizes, and executes render jobs with caching support.</summary>
public sealed class RenderOrchestrator
    : BackgroundJobOrchestrator<RenderRequest, RenderResult>,
      IRenderOrchestrator
{
    private readonly Queue<Guid> _viewerQueue = [];
    private readonly Queue<Guid> _thumbnailQueue = [];
    private readonly IPerformanceMetrics _performanceMetrics;
    private readonly IRenderMemoryCache _renderMemoryCache;
    private readonly IThumbnailDiskCache _thumbnailDiskCache;
    private readonly IRenderService _renderService;

    /// <summary>Initializes a new instance of the <see cref="RenderOrchestrator"/> class.</summary>
    /// <param name="performanceMetrics">The performance metrics recorder.</param>
    /// <param name="renderMemoryCache">The in-memory render cache.</param>
    /// <param name="thumbnailDiskCache">The on-disk thumbnail cache.</param>
    /// <param name="sessionStore">The document session store.</param>
    /// <param name="renderService">The render service implementation.</param>
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
        SessionStore = sessionStore;
        _renderService = renderService;
    }

    /// <inheritdoc />
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

        IDocumentSession? session = SessionStore.Current;
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

        int? pageCount = session.Metadata.PageCount;
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

        FindAndCancelObsoleteJobs(request.JobKey);

        if (_renderMemoryCache.TryGet(session.Id, request, out RenderedPage? cachedPage) &&
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

        QueuedJob job = CreateJob(request, session);
        RegisterAndEnqueue(job);

        return new RenderJobHandle(job.Id, job.CompletionSource.Task);
    }

    /// <inheritdoc />
    public async Task CancelDocumentJobsAsync(
        DocumentId documentId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        List<QueuedJob> jobsToCancel = GetJobsForDocument(documentId);
        RemoveNonRunningJobs(jobsToCancel);

        if (jobsToCancel.Count == 0)
        {
            return;
        }

        foreach (QueuedJob job in jobsToCancel)
        {
            Cancel(job.Id);
        }

        await Task.WhenAll(jobsToCancel.Select(job => job.CompletionSource.Task)).WaitAsync(cancellationToken);
    }

    protected override string GetJobKey(RenderRequest request) => request.JobKey;

    protected override void Enqueue(QueuedJob job)
    {
        if (job.Request.Priority == RenderPriority.Thumbnail)
        {
            _thumbnailQueue.Enqueue(job.Id);
        }
        else
        {
            _viewerQueue.Enqueue(job.Id);
        }
    }

    protected override QueuedJob? DequeueNextJob()
    {
        lock (Gate)
        {
            if (TryDequeueFromQueue(_viewerQueue, out QueuedJob? job))
            {
                return job;
            }

            return TryDequeueFromQueue(_thumbnailQueue, out job) ? job : null;
        }
    }

    protected override async Task<RenderResult> ExecuteJobAsync(QueuedJob job, Stopwatch stopwatch)
    {
        RenderResult result;

        try
        {
            if (ShouldUseThumbnailDiskCache(job.Request) &&
                _thumbnailDiskCache.TryGet(job.Session, job.Request, out RenderedPage? cachedThumbnail) &&
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
                RenderedPage renderedPage = await _renderService.RenderPageAsync(
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

                if (ShouldUseThumbnailDiskCache(job.Request))
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

        RecordPerformanceMetric(job.Session, job.Request, result);
        return result;
    }

    protected override RenderResult CreateCanceledResult(QueuedJob job, TimeSpan duration)
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

    private RenderJobHandle CreateCompletedHandle(
        RenderRequest request,
        DocumentId documentId,
        Result<RenderedPage> result)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        var jobId = Guid.NewGuid();
        RenderResult renderResult = CreateResult(
            jobId,
            documentId,
            request,
            TimeSpan.Zero,
            result,
            isCanceled: false,
            isObsolete: false);

        return new RenderJobHandle(jobId, Task.FromResult(renderResult));
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
    private static bool IsThumbnailRequest(RenderRequest request)
    {
        return request.Priority == RenderPriority.Thumbnail ||
               (request.RequestedWidth.HasValue && request.RequestedHeight.HasValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldUseThumbnailDiskCache(RenderRequest request)
    {
        return request.UseThumbnailDiskCache && IsThumbnailRequest(request);
    }

    private void RecordPerformanceMetric(
        IDocumentSession session,
        RenderRequest request,
        RenderResult result)
    {
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
}
