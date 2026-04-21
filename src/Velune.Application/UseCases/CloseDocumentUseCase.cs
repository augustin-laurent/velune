using Velune.Application.Abstractions;
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

    public async Task<Result<bool>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var currentSession = _sessionStore.Current;
        if (currentSession is null)
        {
            return ResultFactory.Success(true);
        }

        await _renderOrchestrator.CancelDocumentJobsAsync(currentSession.Id, cancellationToken);
        _performanceMetrics.Clear(currentSession.Id);
        _sessionStore.Clear();

        if (currentSession is IReleasableDocumentSession releasableSession)
        {
            releasableSession.ReleaseResources();
        }

        return ResultFactory.Success(true);
    }
}
