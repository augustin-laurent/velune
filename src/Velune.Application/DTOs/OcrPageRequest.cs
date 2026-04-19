using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

public sealed record OcrPageRequest(
    PageIndex PageIndex,
    string InputPath,
    double SourceWidth,
    double SourceHeight,
    TextSourceKind SourceKind = TextSourceKind.Ocr);
