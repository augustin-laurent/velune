namespace Velune.Application.DTOs;

public sealed record RecentFileItem(
    string FileName,
    string FilePath,
    string DocumentType);
