namespace Velune.Application.DTOs;

public sealed record PrintDocumentRequest(
    string FilePath,
    string? PrinterName = null,
    int Copies = 1,
    string? PageRanges = null,
    PrintOrientationOption Orientation = PrintOrientationOption.Automatic,
    bool FitToPage = false);
