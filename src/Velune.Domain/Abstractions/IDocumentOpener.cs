namespace Velune.Domain.Abstractions;

public interface IDocumentOpener
{
    Task<IDocumentSession> OpenAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
