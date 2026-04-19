using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.UseCases;

public sealed class ExtractPdfPagesUseCase
{
    private readonly IPdfDocumentStructureService _pdfDocumentStructureService;

    public ExtractPdfPagesUseCase(IPdfDocumentStructureService pdfDocumentStructureService)
    {
        ArgumentNullException.ThrowIfNull(pdfDocumentStructureService);
        _pdfDocumentStructureService = pdfDocumentStructureService;
    }

    public Task<Result<string>> ExecuteAsync(
        ExtractPdfPagesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _pdfDocumentStructureService.ExtractPagesAsync(
            request.SourcePath,
            request.OutputPath,
            request.Pages,
            cancellationToken);
    }
}
