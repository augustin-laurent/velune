using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.UseCases;

/// <summary>Resolves a text selection from visual coordinates within the document.</summary>
public sealed class ResolveDocumentTextSelectionUseCase
{
    private readonly IDocumentTextSelectionService _selectionService;

    /// <summary>Initializes a new instance of the <see cref="ResolveDocumentTextSelectionUseCase"/> class.</summary>
    /// <param name="selectionService">The text selection service.</param>
    public ResolveDocumentTextSelectionUseCase(IDocumentTextSelectionService selectionService)
    {
        ArgumentNullException.ThrowIfNull(selectionService);
        _selectionService = selectionService;
    }

    /// <summary>Resolves the text selection for the given request.</summary>
    /// <param name="request">The text selection request.</param>
    /// <returns>A result containing the resolved text selection or an error.</returns>
    public Result<DocumentTextSelectionResult> Execute(DocumentTextSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _selectionService.Resolve(request);
    }
}
