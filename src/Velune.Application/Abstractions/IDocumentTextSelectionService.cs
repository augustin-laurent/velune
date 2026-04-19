using Velune.Application.DTOs;
using Velune.Application.Results;

namespace Velune.Application.Abstractions;

public interface IDocumentTextSelectionService
{
    Result<DocumentTextSelectionResult> Resolve(DocumentTextSelectionRequest request);
}
