using Velune.Application.DTOs;

namespace Velune.Application.Abstractions;

/// <summary>Orchestrates background text analysis jobs for documents.</summary>
public interface IDocumentTextAnalysisOrchestrator : IDisposable
{
    /// <summary>Submits a text analysis request for background processing.</summary>
    /// <param name="request">The analysis request to process.</param>
    /// <returns>A handle to track the submitted job.</returns>
    DocumentTextJobHandle Submit(DocumentTextAnalysisRequest request);

    /// <summary>Cancels a previously submitted analysis job.</summary>
    /// <param name="jobId">The identifier of the job to cancel.</param>
    /// <returns>True if the job was found and cancelled; otherwise false.</returns>
    bool Cancel(Guid jobId);
}
