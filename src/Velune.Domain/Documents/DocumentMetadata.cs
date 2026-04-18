namespace Velune.Domain.Documents;

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
    public int? PageCount
    {
        get; init;
    }
    public int? PixelWidth
    {
        get; init;
    }
    public int? PixelHeight
    {
        get; init;
    }
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
    public string? DocumentTitle
    {
        get; init;
    }
    public string? Author
    {
        get; init;
    }
    public string? Creator
    {
        get; init;
    }
    public string? Producer
    {
        get; init;
    }
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
