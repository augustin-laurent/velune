using System.Text.Json.Serialization;

namespace Velune.Application.DTOs;

/// <summary>Represents a recently opened file entry.</summary>
public sealed record RecentFileItem
{
    /// <summary>Creates a recent file item with the current timestamp.</summary>
    /// <param name="fileName">Display name of the file.</param>
    /// <param name="filePath">Full path to the file.</param>
    /// <param name="documentType">The document type identifier.</param>
    public RecentFileItem(
        string fileName,
        string filePath,
        string documentType)
        : this(fileName, filePath, documentType, DateTimeOffset.UtcNow)
    {
    }

    /// <summary>Creates a recent file item with an explicit timestamp.</summary>
    /// <param name="fileName">Display name of the file.</param>
    /// <param name="filePath">Full path to the file.</param>
    /// <param name="documentType">The document type identifier.</param>
    /// <param name="openedAt">When the file was last opened.</param>
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
