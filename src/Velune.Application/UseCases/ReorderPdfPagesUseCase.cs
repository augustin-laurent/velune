using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.UseCases;

public sealed class ReorderPdfPagesUseCase
{
    private readonly IPdfDocumentStructureService _pdfDocumentStructureService;

    public ReorderPdfPagesUseCase(IPdfDocumentStructureService pdfDocumentStructureService)
    {
        ArgumentNullException.ThrowIfNull(pdfDocumentStructureService);
        _pdfDocumentStructureService = pdfDocumentStructureService;
    }

    public Task<Result<string>> ExecuteAsync(
        ReorderPdfPagesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _pdfDocumentStructureService.ReorderPagesAsync(
            request.SourcePath,
            request.OutputPath,
            request.OrderedPages,
            cancellationToken);
    }
}
