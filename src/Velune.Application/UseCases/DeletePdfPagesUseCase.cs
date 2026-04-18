using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.UseCases;

public sealed class DeletePdfPagesUseCase
{
    private readonly IPdfDocumentStructureService _pdfDocumentStructureService;

    public DeletePdfPagesUseCase(IPdfDocumentStructureService pdfDocumentStructureService)
    {
        ArgumentNullException.ThrowIfNull(pdfDocumentStructureService);
        _pdfDocumentStructureService = pdfDocumentStructureService;
    }

    public Task<Result<string>> ExecuteAsync(
        DeletePdfPagesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _pdfDocumentStructureService.DeletePagesAsync(
            request.SourcePath,
            request.OutputPath,
            request.Pages,
            cancellationToken);
    }
}
