using Velune.Application.Abstractions;
using Velune.Application.DTOs;

namespace Velune.Application.UseCases;

public sealed class LoadDocumentTextUseCase
{
    private const string DefaultJobKey = "search:load";
    private readonly IDocumentTextAnalysisOrchestrator _orchestrator;

    public LoadDocumentTextUseCase(IDocumentTextAnalysisOrchestrator orchestrator)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        _orchestrator = orchestrator;
    }

    public DocumentTextJobHandle Execute(IReadOnlyList<string>? preferredLanguages = null)
    {
        return _orchestrator.Submit(
            new DocumentTextAnalysisRequest(
                DefaultJobKey,
                ForceOcr: false,
                preferredLanguages));
    }
}
