using Velune.Application.Abstractions;

namespace Velune.Application.UseCases;

/// <summary>Cancels a running document text analysis job.</summary>
public sealed class CancelDocumentTextAnalysisUseCase
{
    private readonly IDocumentTextAnalysisOrchestrator _orchestrator;

    /// <summary>Initializes a new instance of the <see cref="CancelDocumentTextAnalysisUseCase"/> class.</summary>
    /// <param name="orchestrator">The text analysis orchestrator managing active jobs.</param>
    public CancelDocumentTextAnalysisUseCase(IDocumentTextAnalysisOrchestrator orchestrator)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        _orchestrator = orchestrator;
    }

    /// <summary>Attempts to cancel the text analysis job with the specified identifier.</summary>
    /// <param name="jobId">The unique identifier of the job to cancel.</param>
    /// <returns><c>true</c> if the job was successfully cancelled; otherwise <c>false</c>.</returns>
    public bool Execute(Guid jobId)
    {
        return _orchestrator.Cancel(jobId);
    }
}
