using Velune.Domain.Annotations;
using Velune.Domain.Abstractions;

namespace Velune.Application.DTOs;

public sealed record ApplyImageAnnotationsRequest(
    IDocumentSession Session,
    string OutputPath,
    IReadOnlyList<DocumentAnnotation> Annotations);
