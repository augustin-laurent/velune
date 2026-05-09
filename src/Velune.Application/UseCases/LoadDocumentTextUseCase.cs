using Velune.Application.Abstractions;
using Velune.Application.DTOs;

namespace Velune.Application.UseCases;

/// <summary>Submits a text extraction job for the active document.</summary>
public sealed class LoadDocumentTextUseCase
{
    private const string DefaultJobKey = "search:load";
    private readonly IDocumentTextAnalysisOrchestrator _orchestrator;

    /// <summary>Initializes a new instance of the <see cref="LoadDocumentTextUseCase"/> class.</summary>
    /// <param name="orchestrator">The text analysis orchestrator that manages extraction jobs.</param>
    public LoadDocumentTextUseCase(IDocumentTextAnalysisOrchestrator orchestrator)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        _orchestrator = orchestrator;
    }

    /// <summary>Submits a text loading job using embedded text (no OCR).</summary>
    /// <param name="preferredLanguages">Optional preferred languages for text extraction.</param>
    /// <returns>A handle to track the submitted text analysis job.</returns>
    public DocumentTextJobHandle Execute(IReadOnlyList<string>? preferredLanguages = null)
    {
        return _orchestrator.Submit(
            new DocumentTextAnalysisRequest(
                DefaultJobKey,
                ForceOcr: false,
                preferredLanguages));
    }
}
