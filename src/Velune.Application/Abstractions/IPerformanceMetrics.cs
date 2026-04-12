using Velune.Application.DTOs;
using Velune.Domain.Abstractions;
using Velune.Domain.ValueObjects;

namespace Velune.Application.Abstractions;

public interface IPerformanceMetrics
{
    void RecordDocumentOpened(
        IDocumentSession session,
        TimeSpan duration);

    void RecordViewerRenderCompleted(
        IDocumentSession session,
        RenderResult result);

    void RecordThumbnailCompleted(
        IDocumentSession session,
        RenderResult result);

    void Clear(DocumentId documentId);
}
