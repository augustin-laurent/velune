namespace Velune.Domain.Documents;

public sealed record DocumentTextIndex
{
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

    public bool HasSearchableText => Pages.Any(page => !string.IsNullOrWhiteSpace(page.Text));
}
