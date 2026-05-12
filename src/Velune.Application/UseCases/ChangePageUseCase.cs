using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Abstractions;
using Velune.Domain.Documents;

namespace Velune.Application.UseCases;

/// <summary>Navigates to a specific page in the active document session.</summary>
public sealed class ChangePageUseCase
{
    private readonly IDocumentSessionStore _sessionStore;

    /// <summary>Initializes a new instance of the <see cref="ChangePageUseCase"/> class.</summary>
    /// <param name="sessionStore">The store holding active document sessions.</param>
    public ChangePageUseCase(IDocumentSessionStore sessionStore)
    {
        ArgumentNullException.ThrowIfNull(sessionStore);
        _sessionStore = sessionStore;
    }

    /// <summary>Changes the current page of the active document viewport.</summary>
    /// <param name="request">The page navigation request containing the target page index.</param>
    /// <returns>The updated viewport state, or a failure result if the page is out of range.</returns>
    public Result<ViewportState> Execute(ChangePageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        IDocumentSession? session = _sessionStore.Current;
        if (session is null)
        {
            return ResultFactory.Failure<ViewportState>(
                AppError.NotFound(
                    "document.session.missing",
                    "No active document session."));
        }

        int? pageCount = session.Metadata.PageCount;
        if (pageCount.HasValue && (request.PageIndex.Value < 0 || request.PageIndex.Value >= pageCount.Value))
        {
            return ResultFactory.Failure<ViewportState>(
                AppError.Validation(
                    "document.page.out_of_range",
                    "The requested page is out of range."));
        }

        ViewportState updatedViewport = session.Viewport.WithPage(request.PageIndex);
        IDocumentSession updatedSession = session.WithViewport(updatedViewport);

        _sessionStore.SetCurrent(updatedSession);

        return ResultFactory.Success(updatedViewport);
    }
}
