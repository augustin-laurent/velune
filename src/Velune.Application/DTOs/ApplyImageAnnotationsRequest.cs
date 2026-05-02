using Velune.Domain.Abstractions;
using Velune.Domain.Annotations;

namespace Velune.Application.DTOs;

public sealed record ApplyImageAnnotationsRequest(
    IDocumentSession Session,
    string OutputPath,
    IReadOnlyList<DocumentAnnotation> Annotations);
