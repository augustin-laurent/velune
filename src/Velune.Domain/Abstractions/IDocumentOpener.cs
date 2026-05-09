namespace Velune.Domain.Abstractions;

/// <summary>
/// Opens a document file and creates a session for viewing it.
/// </summary>
public interface IDocumentOpener
{
    /// <summary>
    /// Opens the document at the specified file path.
    /// </summary>
    /// <param name="filePath">Absolute path to the document file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A new document session for the opened file.</returns>
    Task<IDocumentSession> OpenAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
