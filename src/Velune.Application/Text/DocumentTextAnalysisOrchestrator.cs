using System.Diagnostics;
using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Orchestration;
using Velune.Application.Results;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Text;

/// <summary>Queues and executes document text extraction and OCR analysis jobs.</summary>
public sealed class DocumentTextAnalysisOrchestrator
    : BackgroundJobOrchestrator<DocumentTextAnalysisRequest, DocumentTextAnalysisResult>,
      IDocumentTextAnalysisOrchestrator
{
    private readonly Queue<Guid> _queue = [];
    private readonly IDocumentTextService _documentTextService;

    /// <summary>Initializes a new instance of the <see cref="DocumentTextAnalysisOrchestrator"/> class.</summary>
    /// <param name="documentTextService">The text extraction service.</param>
    /// <param name="sessionStore">The document session store.</param>
    public DocumentTextAnalysisOrchestrator(
        IDocumentTextService documentTextService,
        IDocumentSessionStore sessionStore)
    {
        ArgumentNullException.ThrowIfNull(documentTextService);
        ArgumentNullException.ThrowIfNull(sessionStore);

        _documentTextService = documentTextService;
        SessionStore = sessionStore;
    }

    /// <inheritdoc />
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

        IDocumentSession? session = SessionStore.Current;
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

        FindAndCancelObsoleteJobs(request.JobKey);

        QueuedJob job = CreateJob(request, session);
        RegisterAndEnqueue(job);

        return new DocumentTextJobHandle(job.Id, job.CompletionSource.Task);
    }

    protected override string GetJobKey(DocumentTextAnalysisRequest request) => request.JobKey;

    protected override void Enqueue(QueuedJob job)
    {
        _queue.Enqueue(job.Id);
    }

    protected override QueuedJob? DequeueNextJob()
    {
        lock (Gate)
        {
            return TryDequeueFromQueue(_queue, out QueuedJob? job) ? job : null;
        }
    }

    protected override async Task<DocumentTextAnalysisResult> ExecuteJobAsync(
        QueuedJob job,
        Stopwatch stopwatch)
    {
        try
        {
            Result<DocumentTextLoadResult> result;
            if (job.Request.ForceOcr)
            {
                Result<DocumentTextIndex> ocrResult = await _documentTextService.RunOcrAsync(
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

    protected override DocumentTextAnalysisResult CreateCanceledResult(QueuedJob job, TimeSpan duration)
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

    private DocumentTextJobHandle CreateCompletedHandle(
        DocumentTextAnalysisRequest request,
        DocumentId documentId,
        Result<DocumentTextLoadResult> result)
    {
        var jobId = Guid.NewGuid();
        DocumentTextAnalysisResult analysisResult = CreateResult(
            jobId,
            documentId,
            request,
            TimeSpan.Zero,
            result,
            isCanceled: false,
            isObsolete: false);

        return new DocumentTextJobHandle(jobId, Task.FromResult(analysisResult));
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
}
