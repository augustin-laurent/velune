using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

/// <summary>OCR-extracted text content for a single page.</summary>
/// <param name="PageIndex">The page this content belongs to.</param>
/// <param name="Text">The full extracted text.</param>
/// <param name="Runs">Individual text runs with position data.</param>
/// <param name="SourceWidth">Width of the source page in points.</param>
/// <param name="SourceHeight">Height of the source page in points.</param>
/// <param name="SourceKind">The kind of text source used.</param>
public sealed record OcrPageContent(
    PageIndex PageIndex,
    string Text,
    IReadOnlyList<TextRun> Runs,
    double SourceWidth,
    double SourceHeight,
    TextSourceKind SourceKind);
