using Velune.Application.DTOs;

namespace Velune.Application.Abstractions;

public interface IDocumentTextAnalysisOrchestrator : IDisposable
{
    DocumentTextJobHandle Submit(DocumentTextAnalysisRequest request);

    bool Cancel(Guid jobId);
}
