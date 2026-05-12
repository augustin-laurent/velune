using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Abstractions;

namespace Velune.Application.UseCases;

/// <summary>Closes a document session and releases its associated resources.</summary>
public sealed class CloseDocumentUseCase
{
    private readonly IPerformanceMetrics _performanceMetrics;
    private readonly IRenderOrchestrator _renderOrchestrator;
    private readonly IDocumentSessionStore _sessionStore;

    /// <summary>Initializes a new instance of the <see cref="CloseDocumentUseCase"/> class.</summary>
    /// <param name="sessionStore">The store holding active document sessions.</param>
    /// <param name="performanceMetrics">The performance metrics tracker.</param>
    /// <param name="renderOrchestrator">The orchestrator managing render jobs.</param>
    public CloseDocumentUseCase(
        IDocumentSessionStore sessionStore,
        IPerformanceMetrics performanceMetrics,
        IRenderOrchestrator renderOrchestrator)
    {
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(performanceMetrics);
        ArgumentNullException.ThrowIfNull(renderOrchestrator);
        _sessionStore = sessionStore;
        _performanceMetrics = performanceMetrics;
        _renderOrchestrator = renderOrchestrator;
    }

    /// <summary>Closes the current active document session.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result indicating whether the close operation succeeded.</returns>
    public Task<Result<bool>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(new CloseDocumentRequest(), cancellationToken);
    }

    /// <summary>Closes the document session identified by the request.</summary>
    /// <param name="request">The close request, optionally specifying a document ID.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A result indicating whether the close operation succeeded.</returns>
    public async Task<Result<bool>> ExecuteAsync(
        CloseDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        IDocumentSession? session = request.DocumentId is { } documentId
            ? _sessionStore.Sessions.FirstOrDefault(item => item.Id == documentId)
            : _sessionStore.Current;

        if (session is null)
        {
            return ResultFactory.Success(true);
        }

        await _renderOrchestrator.CancelDocumentJobsAsync(session.Id, cancellationToken);
        _performanceMetrics.Clear(session.Id);

        if (request.DocumentId is null)
        {
            _sessionStore.Clear();
        }
        else
        {
            _sessionStore.Remove(session.Id);
        }

        if (session is IReleasableDocumentSession releasableSession)
        {
            releasableSession.ReleaseResources();
        }

        return ResultFactory.Success(true);
    }
}
