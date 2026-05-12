namespace Velune.Domain.Documents;

/// <summary>
/// Full-text index for a document, containing extracted text from all pages.
/// </summary>
public sealed record DocumentTextIndex
{
    /// <summary>
    /// Creates a text index for the specified document.
    /// </summary>
    /// <param name="filePath">Path to the source document.</param>
    /// <param name="documentType">Type of document indexed.</param>
    /// <param name="pages">Text content extracted per page.</param>
    /// <param name="languages">Languages detected in the document.</param>
    public DocumentTextIndex(
        string filePath,
        DocumentType documentType,
        IReadOnlyList<PageTextContent> pages,
        IReadOnlyList<string> languages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(languages);

        FilePath = filePath;
        DocumentType = documentType;
        Pages = pages;
        Languages = languages;
    }

    public string FilePath
    {
        get;
    }

    public DocumentType DocumentType
    {
        get;
    }

    public IReadOnlyList<PageTextContent> Pages
    {
        get;
    }

    public IReadOnlyList<string> Languages
    {
        get;
    }

    /// <summary>
    /// Indicates whether any page contains non-empty searchable text.
    /// </summary>
    public bool HasSearchableText => Pages.Any(page => !string.IsNullOrWhiteSpace(page.Text));
}
