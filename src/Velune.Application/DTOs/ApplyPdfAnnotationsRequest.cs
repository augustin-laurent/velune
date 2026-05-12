using Velune.Domain.Abstractions;
using Velune.Domain.Annotations;
using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

/// <summary>Request to burn annotations into a PDF and save the result.</summary>
public sealed record ApplyPdfAnnotationsRequest(
    IDocumentSession Session,
    string InputPath,
    string OutputPath,
    IReadOnlyList<DocumentAnnotation> Annotations);
