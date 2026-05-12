namespace Velune.Application.DTOs;

/// <summary>Request to print a document with specified settings.</summary>
/// <param name="FilePath">Path to the document to print.</param>
/// <param name="PrinterName">Target printer name, or null for default.</param>
/// <param name="Copies">Number of copies to print.</param>
/// <param name="PageRanges">Page range expression (e.g. "1-3,5"), or null for all pages.</param>
/// <param name="Orientation">Page orientation setting.</param>
/// <param name="FitToPage">Whether to scale content to fit the page.</param>
public sealed record PrintDocumentRequest(
    string FilePath,
    string? PrinterName = null,
    int Copies = 1,
    string? PageRanges = null,
    PrintOrientationOption Orientation = PrintOrientationOption.Automatic,
    bool FitToPage = false);
