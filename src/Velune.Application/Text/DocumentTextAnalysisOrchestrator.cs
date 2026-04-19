using System.Diagnostics;
using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Abstractions;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Text;

public sealed class DocumentTextAnalysisOrchestrator : IDocumentTextAnalysisOrchestrator
{
    private readonly object _gate = new();
    private readonly Queue<Guid> _queue = [];
    private readonly Dictionary<Guid, QueuedTextJob> _jobs = [];
    private readonly IDocumentTextService _documentTextService;
    private readonly IDocumentSessionStore _sessionStore;
    private readonly CancellationTokenSource _shutdownCancellationTokenSource = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly Task _worker;
    private bool _disposed;

    public DocumentTextAnalysisOrchestrator(
        IDocumentTextService documentTextService,
        IDocumentSessionStore sessionStore)
    {
        ArgumentNullException.ThrowIfNull(documentTextService);
        ArgumentNullException.ThrowIfNull(sessionStore);

        _documentTextService = documentTextService;
        _sessionStore = sessionStore;
        _worker = Task.Run(ProcessQueueAsync);
    }

    public DocumentTextJobHandle Submit(DocumentTextAnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(request.JobKey))
        {
            return CreateCompletedHandle(
                request,
                default,
                ResultFactory.Failure<DocumentTextLoadResult>(
                    AppError.Validation(
                        "document.text.job_key.empty",
                        "The text analysis job key cannot be empty.")));
        }

        var session = _sessionStore.Current;
        if (session is null)
        {
            return CreateCompletedHandle(
                request,
                default,
                ResultFactory.Failure<DocumentTextLoadResult>(
                    AppError.NotFound(
                        "document.session.missing",
                        "No active document session.")));
        }

        List<QueuedTextJob> obsoleteJobs = [];
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

        var job = new QueuedTextJob(
            Guid.NewGuid(),
            request,
            session,
            TaskCompletionSourceFactory.Create<DocumentTextAnalysisResult>(),
            CancellationTokenSource.CreateLinkedTokenSource(_shutdownCancellationTokenSource.Token));

        lock (_gate)
        {
            _jobs[job.Id] = job;
            _queue.Enqueue(job.Id);
        }

        _signal.Release();
        return new DocumentTextJobHandle(job.Id, job.CompletionSource.Task);
    }

    public bool Cancel(Guid jobId)
    {
        ThrowIfDisposed();

        QueuedTextJob? job;
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

        List<QueuedTextJob> jobsToCancel;
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

    private async Task ProcessQueueAsync()
    {
        while (true)
        {
            await _signal.WaitAsync(_shutdownCancellationTokenSource.Token);

            QueuedTextJob? job = null;
            lock (_gate)
            {
                while (_queue.Count > 0)
                {
                    var jobId = _queue.Dequeue();
                    if (_jobs.TryGetValue(jobId, out job))
                    {
                        job.IsRunning = true;
                        break;
                    }
                }
            }

            if (job is null)
            {
                continue;
            }

            var stopwatch = Stopwatch.StartNew();
            var result = await ExecuteJobAsync(job, stopwatch);

            lock (_gate)
            {
                _jobs.Remove(job.Id);
            }

            if (job.TryComplete(result))
            {
                job.Dispose();
            }
        }
    }

    private async Task<DocumentTextAnalysisResult> ExecuteJobAsync(
        QueuedTextJob job,
        Stopwatch stopwatch)
    {
        try
        {
            Result<DocumentTextLoadResult> result;
            if (job.Request.ForceOcr)
            {
                var ocrResult = await _documentTextService.RunOcrAsync(
                    job.Session,
                    job.Request.PreferredLanguages,
                    job.CancellationTokenSource.Token);

                result = ocrResult.IsFailure
                    ? ResultFactory.Failure<DocumentTextLoadResult>(ocrResult.Error!)
                    : ResultFactory.Success(
                        new DocumentTextLoadResult(
                            ocrResult.Value,
                            RequiresOcr: false,
                            UsedCache: false));
            }
            else
            {
                result = await _documentTextService.LoadAsync(
                    job.Session,
                    job.Request.PreferredLanguages,
                    job.CancellationTokenSource.Token);
            }

            return CreateResult(
                job.Id,
                job.Session.Id,
                job.Request,
                stopwatch.Elapsed,
                result,
                isCanceled: false,
                isObsolete: job.IsObsolete);
        }
        catch (OperationCanceledException)
        {
            return CreateCanceledResult(job, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            return CreateResult(
                job.Id,
                job.Session.Id,
                job.Request,
                stopwatch.Elapsed,
                ResultFactory.Failure<DocumentTextLoadResult>(
                    AppError.Infrastructure(
                        "document.text.analysis.failed",
                        ex.Message)),
                isCanceled: false,
                isObsolete: job.IsObsolete);
        }
    }

    private DocumentTextJobHandle CreateCompletedHandle(
        DocumentTextAnalysisRequest request,
        DocumentId documentId,
        Result<DocumentTextLoadResult> result)
    {
        var jobId = Guid.NewGuid();
        var analysisResult = CreateResult(
            jobId,
            documentId,
            request,
            TimeSpan.Zero,
            result,
            isCanceled: false,
            isObsolete: false);

        return new DocumentTextJobHandle(jobId, Task.FromResult(analysisResult));
    }

    private static DocumentTextAnalysisResult CreateCanceledResult(QueuedTextJob job, TimeSpan duration)
    {
        return new DocumentTextAnalysisResult(
            job.Id,
            job.Session.Id,
            job.Request.JobKey,
            duration,
            Index: null,
            Error: null,
            IsCanceled: true,
            IsObsolete: job.IsObsolete,
            RequiresOcr: false);
    }

    private static DocumentTextAnalysisResult CreateResult(
        Guid jobId,
        DocumentId documentId,
        DocumentTextAnalysisRequest request,
        TimeSpan duration,
        Result<DocumentTextLoadResult> result,
        bool isCanceled,
        bool isObsolete)
    {
        return new DocumentTextAnalysisResult(
            jobId,
            documentId,
            request.JobKey,
            duration,
            result.Value?.Index,
            result.Error,
            isCanceled,
            isObsolete,
            result.Value?.RequiresOcr ?? false);
    }

    private bool CancelJob(QueuedTextJob job, bool isObsolete)
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

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class QueuedTextJob : IDisposable
    {
        private bool _disposed;

        public QueuedTextJob(
            Guid id,
            DocumentTextAnalysisRequest request,
            IDocumentSession session,
            TaskCompletionSource<DocumentTextAnalysisResult> completionSource,
            CancellationTokenSource cancellationTokenSource)
        {
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

        public DocumentTextAnalysisRequest Request
        {
            get;
        }

        public IDocumentSession Session
        {
            get;
        }

        public TaskCompletionSource<DocumentTextAnalysisResult> CompletionSource
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

        public bool TryComplete(DocumentTextAnalysisResult result)
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
