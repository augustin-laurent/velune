using Velune.Application.Abstractions;
using Velune.Application.DTOs;
using Velune.Domain.Abstractions;
using Velune.Domain.ValueObjects;

namespace Velune.Tests.Unit.Support;

public sealed class NoOpPerformanceMetrics : IPerformanceMetrics
{
    public static readonly NoOpPerformanceMetrics Instance = new();

    public void Clear(DocumentId documentId)
    {
    }

    public void RecordDocumentOpened(
        IDocumentSession session,
        TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(session);
    }

    public void RecordThumbnailCompleted(
        IDocumentSession session,
        RenderResult result)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(result);
    }

    public void RecordViewerRenderCompleted(
        IDocumentSession session,
        RenderResult result)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(result);
    }
}
