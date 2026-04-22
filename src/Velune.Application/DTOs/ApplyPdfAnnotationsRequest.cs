using Velune.Domain.Annotations;
using Velune.Domain.Abstractions;
using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

public sealed record ApplyPdfAnnotationsRequest(
    IDocumentSession Session,
    string InputPath,
    string OutputPath,
    IReadOnlyList<DocumentAnnotation> Annotations);
