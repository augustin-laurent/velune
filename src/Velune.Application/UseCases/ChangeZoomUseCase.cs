using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Documents;

namespace Velune.Application.UseCases;

public sealed class ChangeZoomUseCase
{
    private readonly IDocumentSessionStore _sessionStore;

    public ChangeZoomUseCase(IDocumentSessionStore sessionStore)
    {
        ArgumentNullException.ThrowIfNull(sessionStore);
        _sessionStore = sessionStore;
    }

    public Result<ViewportState> Execute(ChangeZoomRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = _sessionStore.Current;
        if (session is null)
        {
            return Result.Failure<ViewportState>("No active document session.");
        }

        var updatedViewport = session.Viewport.WithZoom(request.ZoomFactor, request.ZoomMode);
        var updatedSession = session.WithViewport(updatedViewport);

        _sessionStore.SetCurrent(updatedSession);

        return Result.Success(updatedViewport);
    }
}
