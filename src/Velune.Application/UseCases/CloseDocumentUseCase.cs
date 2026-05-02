using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.UseCases;

public sealed class CloseDocumentUseCase
{
    private readonly IPerformanceMetrics _performanceMetrics;
    private readonly IRenderOrchestrator _renderOrchestrator;
    private readonly IDocumentSessionStore _sessionStore;

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

    public Task<Result<bool>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(new CloseDocumentRequest(), cancellationToken);
    }

    public async Task<Result<bool>> ExecuteAsync(
        CloseDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = request.DocumentId is { } documentId
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
