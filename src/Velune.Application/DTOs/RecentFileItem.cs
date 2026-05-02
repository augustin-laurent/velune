using System.Text.Json.Serialization;

namespace Velune.Application.DTOs;

public sealed record RecentFileItem
{
    public RecentFileItem(
        string fileName,
        string filePath,
        string documentType)
        : this(fileName, filePath, documentType, DateTimeOffset.UtcNow)
    {
    }

    [JsonConstructor]
    public RecentFileItem(
        string fileName,
        string filePath,
        string documentType,
        DateTimeOffset openedAt)
    {
        FileName = fileName;
        FilePath = filePath;
        DocumentType = documentType;
        OpenedAt = openedAt;
    }

    public string FileName
    {
        get;
        init;
    }

    public string FilePath
    {
        get;
        init;
    }

    public string DocumentType
    {
        get;
        init;
    }

    public DateTimeOffset OpenedAt
    {
        get;
        init;
    }
}
