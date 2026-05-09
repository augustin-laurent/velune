using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

/// <summary>Request to perform OCR on a single page.</summary>
/// <param name="PageIndex">The page to OCR.</param>
/// <param name="InputPath">Path to the rendered page image.</param>
/// <param name="SourceWidth">Width of the source page in points.</param>
/// <param name="SourceHeight">Height of the source page in points.</param>
/// <param name="SourceKind">The text source kind to tag results with.</param>
public sealed record OcrPageRequest(
    PageIndex PageIndex,
    string InputPath,
    double SourceWidth,
    double SourceHeight,
    TextSourceKind SourceKind = TextSourceKind.Ocr);
