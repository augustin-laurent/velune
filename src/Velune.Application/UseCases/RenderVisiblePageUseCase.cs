using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;

namespace Velune.Application.UseCases;

public sealed class RenderVisiblePageUseCase
{
    private readonly IDocumentSessionStore _sessionStore;
    private readonly IRenderService _renderService;

    public RenderVisiblePageUseCase(
        IDocumentSessionStore sessionStore,
        IRenderService renderService)
    {
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(renderService);

        _sessionStore = sessionStore;
        _renderService = renderService;
    }

    public async Task<Result<RenderedPage>> ExecuteAsync(
        RenderVisiblePageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = _sessionStore.Current;
        if (session is null)
        {
            return ResultFactory.Failure<RenderedPage>(
                AppError.NotFound(
                    "document.session.missing",
                    "No active document session."));
        }

        var viewport = session.Viewport;

        var renderedPage = await _renderService.RenderPageAsync(
            session,
            viewport.CurrentPage,
            viewport.ZoomFactor,
            viewport.Rotation,
            cancellationToken);

        return ResultFactory.Success(renderedPage);
    }
}
