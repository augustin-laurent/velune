using Velune.Application.Abstractions;

namespace Velune.Application.UseCases;

public sealed class CancelDocumentTextAnalysisUseCase
{
    private readonly IDocumentTextAnalysisOrchestrator _orchestrator;

    public CancelDocumentTextAnalysisUseCase(IDocumentTextAnalysisOrchestrator orchestrator)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        _orchestrator = orchestrator;
    }

    public bool Execute(Guid jobId)
    {
        return _orchestrator.Cancel(jobId);
    }
}
