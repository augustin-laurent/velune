using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

public sealed record OcrPageContent(
    PageIndex PageIndex,
    string Text,
    IReadOnlyList<TextRun> Runs,
    double SourceWidth,
    double SourceHeight,
    TextSourceKind SourceKind);
