using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Documents;

namespace Velune.Application.UseCases;

public sealed class ChangePageUseCase
{
    private readonly IDocumentSessionStore _sessionStore;

    public ChangePageUseCase(IDocumentSessionStore sessionStore)
    {
        ArgumentNullException.ThrowIfNull(sessionStore);
        _sessionStore = sessionStore;
    }

    public Result<ViewportState> Execute(ChangePageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = _sessionStore.Current;
        if (session is null)
        {
            return ResultFactory.Failure<ViewportState>(
                AppError.NotFound(
                    "document.session.missing",
                    "No active document session."));
        }

        var pageCount = session.Metadata.PageCount;
        if (pageCount.HasValue)
        {
            if (request.PageIndex.Value < 0 || request.PageIndex.Value >= pageCount.Value)
            {
                return ResultFactory.Failure<ViewportState>(
                    AppError.Validation(
                        "document.page.out_of_range",
                        "The requested page is out of range."));
            }
        }

        var updatedViewport = session.Viewport.WithPage(request.PageIndex);
        var updatedSession = session.WithViewport(updatedViewport);

        _sessionStore.SetCurrent(updatedSession);

        return ResultFactory.Success(updatedViewport);
    }
}
