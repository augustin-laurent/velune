namespace Velune.Domain.Documents;

/// <summary>
/// Immutable metadata describing an opened document (name, path, size, page count, etc.).
/// </summary>
public sealed record DocumentMetadata
{
    public string FileName
    {
        get; init;
    }
    public string FilePath
    {
        get; init;
    }
    public DocumentType DocumentType
    {
        get; init;
    }
    public long FileSizeInBytes
    {
        get; init;
    }
    /// <summary>
    /// Total number of pages; null for single-page documents like images.
    /// </summary>
    public int? PageCount
    {
        get; init;
    }
    /// <summary>
    /// Native pixel width of the document; null if not applicable.
    /// </summary>
    public int? PixelWidth
    {
        get; init;
    }
    /// <summary>
    /// Native pixel height of the document; null if not applicable.
    /// </summary>
    public int? PixelHeight
    {
        get; init;
    }
    /// <summary>
    /// Human-readable format label (e.g. "PDF 1.7", "PNG").
    /// </summary>
    public string? FormatLabel
    {
        get; init;
    }
    public DateTimeOffset? CreatedAt
    {
        get; init;
    }
    public DateTimeOffset? ModifiedAt
    {
        get; init;
    }
    /// <summary>
    /// Title embedded in the document metadata; null if absent.
    /// </summary>
    public string? DocumentTitle
    {
        get; init;
    }
    public string? Author
    {
        get; init;
    }
    /// <summary>
    /// Application that created the document.
    /// </summary>
    public string? Creator
    {
        get; init;
    }
    /// <summary>
    /// Application that produced the file (e.g. PDF producer).
    /// </summary>
    public string? Producer
    {
        get; init;
    }
    /// <summary>
    /// Warning message shown in the details panel (e.g. unsupported features).
    /// </summary>
    public string? DetailsWarning
    {
        get; init;
    }

    public DocumentMetadata(
        string fileName,
        string filePath,
        DocumentType documentType,
        long fileSizeInBytes,
        int? pageCount = null,
        int? pixelWidth = null,
        int? pixelHeight = null,
        string? formatLabel = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? modifiedAt = null,
        string? documentTitle = null,
        string? author = null,
        string? creator = null,
        string? producer = null,
        string? detailsWarning = null)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(filePath);

        if (fileSizeInBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fileSizeInBytes), "File size cannot be negative.");
        }

        if (pageCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageCount), "Page count cannot be negative.");
        }

        if (pixelWidth is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelWidth), "Pixel width cannot be negative.");
        }

        if (pixelHeight is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelHeight), "Pixel height cannot be negative.");
        }

        FileName = fileName;
        FilePath = filePath;
        DocumentType = documentType;
        FileSizeInBytes = fileSizeInBytes;
        PageCount = pageCount;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        FormatLabel = formatLabel;
        CreatedAt = createdAt;
        ModifiedAt = modifiedAt;
        DocumentTitle = documentTitle;
        Author = author;
        Creator = creator;
        Producer = producer;
        DetailsWarning = detailsWarning;
    }
}
