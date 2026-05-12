using Velune.Application.DTOs;
using Velune.Domain.Abstractions;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Abstractions;

/// <summary>Collects performance measurements for document operations.</summary>
public interface IPerformanceMetrics
{
    /// <summary>Records the time taken to open a document.</summary>
    /// <param name="session">The session that was opened.</param>
    /// <param name="duration">The elapsed time to open the document.</param>
    void RecordDocumentOpened(
        IDocumentSession session,
        TimeSpan duration);

    /// <summary>Records a completed viewer page render.</summary>
    /// <param name="session">The session the render belongs to.</param>
    /// <param name="result">The render result with timing data.</param>
    void RecordViewerRenderCompleted(
        IDocumentSession session,
        RenderResult result);

    /// <summary>Records a completed thumbnail render.</summary>
    /// <param name="session">The session the render belongs to.</param>
    /// <param name="result">The render result with timing data.</param>
    void RecordThumbnailCompleted(
        IDocumentSession session,
        RenderResult result);

    /// <summary>Clears all recorded metrics for the specified document.</summary>
    /// <param name="documentId">The document identifier to clear metrics for.</param>
    void Clear(DocumentId documentId);
}
