using Velune.Domain.Abstractions;

namespace Velune.Infrastructure.Documents;

public sealed class UnsupportedDocumentOpener : IDocumentOpener
{
    public Task<IDocumentSession> OpenAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Document opening is not implemented yet.");
    }
}
