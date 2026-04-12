using System.Diagnostics;
using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;
using Velune.Domain.Abstractions;

namespace Velune.Application.UseCases;

public sealed class OpenDocumentUseCase
{
    private readonly IDocumentOpener _documentOpener;
    private readonly IPerformanceMetrics _performanceMetrics;
    private readonly IDocumentSessionStore _sessionStore;

    public OpenDocumentUseCase(
        IDocumentOpener documentOpener,
        IDocumentSessionStore sessionStore,
        IPerformanceMetrics performanceMetrics)
    {
        ArgumentNullException.ThrowIfNull(documentOpener);
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(performanceMetrics);

        _documentOpener = documentOpener;
        _sessionStore = sessionStore;
        _performanceMetrics = performanceMetrics;
    }

    public async Task<Result<IDocumentSession>> ExecuteAsync(
        OpenDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return ResultFactory.Failure<IDocumentSession>(
                AppError.Validation(
                    "document.path.empty",
                    "File path cannot be empty."));
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var session = await _documentOpener.OpenAsync(request.FilePath, cancellationToken);
            _sessionStore.SetCurrent(session);
            _performanceMetrics.RecordDocumentOpened(session, stopwatch.Elapsed);

            return ResultFactory.Success(session);
        }
        catch (FileNotFoundException)
        {
            return ResultFactory.Failure<IDocumentSession>(
                AppError.NotFound(
                    "document.file.missing",
                    "The selected document could not be found."));
        }
        catch (DirectoryNotFoundException)
        {
            return ResultFactory.Failure<IDocumentSession>(
                AppError.NotFound(
                    "document.directory.missing",
                    "The selected document directory could not be found."));
        }
        catch (NotSupportedException ex)
        {
            return ResultFactory.Failure<IDocumentSession>(
                AppError.Unsupported(
                    "document.format.unsupported",
                    ex.Message));
        }
        catch (InvalidDataException ex)
        {
            return ResultFactory.Failure<IDocumentSession>(
                AppError.Infrastructure(
                    "document.data.invalid",
                    ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return ResultFactory.Failure<IDocumentSession>(
                AppError.Infrastructure(
                    "document.open.failed",
                    ex.Message));
        }
    }
}
