using Velune.Domain.Abstractions;

namespace Velune.Infrastructure.Documents;

/// <summary>
/// Placeholder opener that always throws <see cref="NotSupportedException"/>.
/// </summary>
public sealed class UnsupportedDocumentOpener : IDocumentOpener
{
    /// <inheritdoc />
    public Task<IDocumentSession> OpenAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Document opening is not implemented yet.");
    }
}
