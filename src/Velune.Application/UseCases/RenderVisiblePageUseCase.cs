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
        RenderPageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.ZoomFactor);

        var session = _sessionStore.Current;
        if (session is null)
        {
            return ResultFactory.Failure<RenderedPage>(
                AppError.NotFound(
                    "document.session.missing",
                    "No active document session."));
        }

        var pageCount = session.Metadata.PageCount;
        if (pageCount.HasValue &&
            (request.PageIndex.Value < 0 || request.PageIndex.Value >= pageCount.Value))
        {
            return ResultFactory.Failure<RenderedPage>(
                AppError.Validation(
                    "document.page.out_of_range",
                    "The requested page is out of range."));
        }

        try
        {
            var renderedPage = await _renderService.RenderPageAsync(
                session,
                request.PageIndex,
                request.ZoomFactor,
                request.Rotation,
                cancellationToken);

            return ResultFactory.Success(renderedPage);
        }
        catch (DllNotFoundException ex)
        {
            return ResultFactory.Failure<RenderedPage>(
                AppError.Infrastructure(
                    "document.pdfium.missing",
                    $"PDF rendering engine not found: {ex.Message}"));
        }
        catch (BadImageFormatException ex)
        {
            return ResultFactory.Failure<RenderedPage>(
                AppError.Infrastructure(
                    "document.pdfium.invalid_binary",
                    $"PDF rendering engine is invalid or incompatible: {ex.Message}"));
        }
        catch (InvalidOperationException ex)
        {
            return ResultFactory.Failure<RenderedPage>(
                AppError.Infrastructure(
                    "document.render.failed",
                    ex.Message));
        }
    }
}
