using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.UseCases;

public sealed class RotatePdfPagesUseCase
{
    private readonly IPdfDocumentStructureService _pdfDocumentStructureService;

    public RotatePdfPagesUseCase(IPdfDocumentStructureService pdfDocumentStructureService)
    {
        ArgumentNullException.ThrowIfNull(pdfDocumentStructureService);
        _pdfDocumentStructureService = pdfDocumentStructureService;
    }

    public Task<Result<string>> ExecuteAsync(
        RotatePdfPagesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _pdfDocumentStructureService.RotatePagesAsync(
            request.SourcePath,
            request.OutputPath,
            request.Pages,
            request.Rotation,
            cancellationToken);
    }
}
