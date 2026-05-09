using Velune.Domain.Annotations;

namespace Velune.Application.Abstractions;

/// <summary>Persists document annotations associated with PDF files.</summary>
public interface IPdfAnnotationStore
{
    /// <summary>Loads all persisted annotations for the specified PDF file.</summary>
    /// <param name="pdfFilePath">The path to the PDF file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The list of annotations for the document.</returns>
    Task<IReadOnlyList<DocumentAnnotation>> LoadAsync(string pdfFilePath, CancellationToken cancellationToken = default);

    /// <summary>Saves annotations for the specified PDF file, replacing any existing data.</summary>
    /// <param name="pdfFilePath">The path to the PDF file.</param>
    /// <param name="annotations">The annotations to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(string pdfFilePath, IReadOnlyList<DocumentAnnotation> annotations, CancellationToken cancellationToken = default);

    /// <summary>Removes all persisted annotations for the specified PDF file.</summary>
    /// <param name="pdfFilePath">The path to the PDF file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveAsync(string pdfFilePath, CancellationToken cancellationToken = default);
}
