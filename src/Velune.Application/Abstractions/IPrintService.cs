using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.Abstractions;

public interface IPrintService
{
    bool SupportsSystemPrintDialog
    {
        get;
    }

    Task<Result> ShowSystemPrintDialogAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PrintDestinationInfo>>> GetAvailablePrintersAsync(
        CancellationToken cancellationToken = default);

    Task<Result> PrintAsync(
        PrintDocumentRequest request,
        CancellationToken cancellationToken = default);
}
