using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Domain.Abstractions;

/// <summary>
/// Renders document pages to pixel data at a given zoom and rotation.
/// </summary>
public interface IRenderService
{
    /// <summary>
    /// Renders a single page of the document.
    /// </summary>
    /// <param name="session">The document session to render from.</param>
    /// <param name="pageIndex">Zero-based page index to render.</param>
    /// <param name="zoomFactor">Zoom multiplier (1.0 = 100%).</param>
    /// <param name="rotation">Rotation to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered page containing pixel data and dimensions.</returns>
    Task<RenderedPage> RenderPageAsync(
        IDocumentSession session,
        PageIndex pageIndex,
        double zoomFactor,
        Rotation rotation,
        CancellationToken cancellationToken = default);
}
