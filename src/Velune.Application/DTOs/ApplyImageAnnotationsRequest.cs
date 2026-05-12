using Velune.Domain.Abstractions;
using Velune.Domain.Annotations;

namespace Velune.Application.DTOs;

/// <summary>Request to burn annotations into an image and save the result.</summary>
public sealed record ApplyImageAnnotationsRequest(
    IDocumentSession Session,
    string OutputPath,
    IReadOnlyList<DocumentAnnotation> Annotations);
