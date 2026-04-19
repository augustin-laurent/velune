using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.UseCases;

public sealed class ResolveDocumentTextSelectionUseCase
{
    private readonly IDocumentTextSelectionService _selectionService;

    public ResolveDocumentTextSelectionUseCase(IDocumentTextSelectionService selectionService)
    {
        ArgumentNullException.ThrowIfNull(selectionService);
        _selectionService = selectionService;
    }

    public Result<DocumentTextSelectionResult> Execute(DocumentTextSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _selectionService.Resolve(request);
    }
}
