using Velune.Application.Abstractions;
using Velune.Application.DTOs;

namespace Velune.Application.UseCases;

public sealed class RunDocumentOcrUseCase
{
    private const string DefaultJobKey = "search:ocr";
    private readonly IDocumentTextAnalysisOrchestrator _orchestrator;

    public RunDocumentOcrUseCase(IDocumentTextAnalysisOrchestrator orchestrator)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        _orchestrator = orchestrator;
    }

    public DocumentTextJobHandle Execute(IReadOnlyList<string>? preferredLanguages = null)
    {
        return _orchestrator.Submit(
            new DocumentTextAnalysisRequest(
                DefaultJobKey,
                ForceOcr: true,
                preferredLanguages));
    }
}
