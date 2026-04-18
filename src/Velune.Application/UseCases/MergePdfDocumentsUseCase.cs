using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.UseCases;

public sealed class MergePdfDocumentsUseCase
{
    private readonly IPdfDocumentStructureService _pdfDocumentStructureService;

    public MergePdfDocumentsUseCase(IPdfDocumentStructureService pdfDocumentStructureService)
    {
        ArgumentNullException.ThrowIfNull(pdfDocumentStructureService);
        _pdfDocumentStructureService = pdfDocumentStructureService;
    }

    public Task<Result<string>> ExecuteAsync(
        MergePdfDocumentsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _pdfDocumentStructureService.MergeDocumentsAsync(
            request.SourcePaths,
            request.OutputPath,
            cancellationToken);
    }
}
