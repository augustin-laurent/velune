using System.Diagnostics;
using Velune.Application.Abstractions;
using Velune.Domain.Abstractions;

namespace Velune.Application.Orchestration;

/// <summary>Base class for background job orchestrators that queue, cancel, and execute work items.</summary>
/// <typeparam name="TRequest">The request type submitted by callers.</typeparam>
/// <typeparam name="TResult">The result type produced by job execution.</typeparam>
public abstract class BackgroundJobOrchestrator<TRequest, TResult> : IDisposable
    where TRequest : class
    where TResult : class
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, QueuedJob> _jobs = [];
    private readonly CancellationTokenSource _shutdownCancellationTokenSource = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly Task _worker;
    private bool _disposed;

    protected BackgroundJobOrchestrator()
    {
        _worker = Task.Run(ProcessQueueAsync);
    }

    protected object Gate => _gate;

    protected IDocumentSessionStore SessionStore { get; init; } = null!;

    protected bool IsDisposed => _disposed;

    public bool Cancel(Guid jobId)
    {
        ThrowIfDisposed();

        QueuedJob? job;
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
        GC.SuppressFinalize(this);
        _shutdownCancellationTokenSource.Cancel();

        List<QueuedJob> jobsToCancel;
        lock (_gate)
        {
            jobsToCancel = [.. _jobs.Values];
            _jobs.Clear();
        }

        foreach (QueuedJob job in jobsToCancel)
        {
            CancelJob(job, isObsolete: false);
        }

        try
        {
            if (!_worker.Wait(TimeSpan.FromSeconds(5)))
            {
                Debug.WriteLine($"{GetType().Name}: worker did not complete within the shutdown timeout.");
            }
        }
        catch (OperationCanceledException)
        {
            // Do Nothing
        }
        finally
        {
            _signal.Dispose();
            _shutdownCancellationTokenSource.Dispose();
        }
    }

    protected QueuedJob CreateJob(
        TRequest request,
        IDocumentSession session)
    {
        return new QueuedJob(
            Guid.NewGuid(),
            request,
            session,
            new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously),
            CancellationTokenSource.CreateLinkedTokenSource(_shutdownCancellationTokenSource.Token));
    }

    protected void RegisterAndEnqueue(QueuedJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        lock (_gate)
        {
            _jobs[job.Id] = job;
            Enqueue(job);
        }

        _signal.Release();
    }

    protected List<QueuedJob> FindAndCancelObsoleteJobs(string jobKey)
    {
        List<QueuedJob> obsoleteJobs = [];
        lock (_gate)
        {
            foreach (QueuedJob? existingJob in _jobs.Values.Where(j => GetJobKey(j.Request) == jobKey))
            {
                obsoleteJobs.Add(existingJob);
            }

            foreach (QueuedJob? obsoleteJob in obsoleteJobs.Where(j => !j.IsRunning))
            {
                _jobs.Remove(obsoleteJob.Id);
            }
        }

        foreach (QueuedJob obsoleteJob in obsoleteJobs)
        {
            CancelJob(obsoleteJob, isObsolete: true);
        }

        return obsoleteJobs;
    }

    protected List<QueuedJob> GetJobsForDocument(Domain.ValueObjects.DocumentId documentId)
    {
        lock (_gate)
        {
            return [.. _jobs.Values.Where(job => job.Session.Id == documentId)];
        }
    }

    protected void RemoveNonRunningJobs(List<QueuedJob> jobs)
    {
        lock (_gate)
        {
            foreach (QueuedJob? job in jobs.Where(j => !j.IsRunning))
            {
                _jobs.Remove(job.Id);
            }
        }
    }

    protected void RemoveJob(Guid jobId)
    {
        lock (_gate)
        {
            _jobs.Remove(jobId);
        }
    }

    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    protected abstract string GetJobKey(TRequest request);

    protected abstract void Enqueue(QueuedJob job);

    protected abstract QueuedJob? DequeueNextJob();

    protected abstract Task<TResult> ExecuteJobAsync(QueuedJob job, Stopwatch stopwatch);

    protected abstract TResult CreateCanceledResult(QueuedJob job, TimeSpan duration);

    protected bool TryDequeueFromQueue(Queue<Guid> queue, out QueuedJob? job)
    {
        ArgumentNullException.ThrowIfNull(queue);

        while (queue.Count > 0)
        {
            Guid jobId = queue.Dequeue();
            if (!_jobs.TryGetValue(jobId, out QueuedJob? candidate))
            {
                continue;
            }

            if (candidate.CompletionSource.Task.IsCompleted)
            {
                _jobs.Remove(jobId);
                candidate.Dispose();
                continue;
            }

            candidate.IsRunning = true;
            job = candidate;
            return true;
        }

        job = null;
        return false;
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

            QueuedJob? job = DequeueNextJob();
            if (job is null)
            {
                continue;
            }

            var stopwatch = Stopwatch.StartNew();
            TResult result = await ExecuteJobAsync(job, stopwatch);

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

    private bool CancelJob(QueuedJob job, bool isObsolete)
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

    protected sealed class QueuedJob : IDisposable
    {
        private bool _disposed;

        public QueuedJob(
            Guid id,
            TRequest request,
            IDocumentSession session,
            TaskCompletionSource<TResult> completionSource,
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

        public TRequest Request
        {
            get;
        }

        public IDocumentSession Session
        {
            get;
        }

        public TaskCompletionSource<TResult> CompletionSource
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

        public bool TryComplete(TResult result)
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
}
