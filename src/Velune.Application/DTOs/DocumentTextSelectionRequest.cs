using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

public sealed record DocumentTextSelectionRequest(
    IDocumentSession Session,
    DocumentTextIndex Index,
    PageIndex PageIndex,
    DocumentTextSelectionPoint AnchorPoint,
    DocumentTextSelectionPoint ActivePoint);
