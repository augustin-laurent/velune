using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

/// <summary>Request to rotate all pages in the active document.</summary>
/// <param name="Rotation">The rotation to apply.</param>
public sealed record RotateDocumentRequest(Rotation Rotation);
