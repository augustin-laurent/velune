using Velune.Application.Abstractions;
using Velune.Application.Results;

namespace Velune.Application.UseCases;

public sealed class ShowSystemPrintDialogUseCase
{
    private readonly IPrintService _printService;

    public ShowSystemPrintDialogUseCase(IPrintService printService)
    {
        ArgumentNullException.ThrowIfNull(printService);
        _printService = printService;
    }

    public Task<Result> ExecuteAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Task.FromResult(ResultFactory.Failure(
                AppError.Validation(
                    "print.path.empty",
                    "A document must be open before it can be printed.")));
        }

        if (!File.Exists(filePath))
        {
            return Task.FromResult(ResultFactory.Failure(
                AppError.NotFound(
                    "print.file.missing",
                    "The current document could not be found anymore.")));
        }

        return _printService.ShowSystemPrintDialogAsync(filePath, cancellationToken);
    }
}
