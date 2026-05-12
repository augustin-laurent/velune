using Velune.Domain.Abstractions;
using Velune.Domain.Documents;
using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

/// <summary>Request to compute a text selection between two points on a page.</summary>
/// <param name="Session">The active document session.</param>
/// <param name="Index">The document text index to query.</param>
/// <param name="PageIndex">The page where selection occurs.</param>
/// <param name="AnchorPoint">The starting point of the selection.</param>
/// <param name="ActivePoint">The current end point of the selection.</param>
public sealed record DocumentTextSelectionRequest(
    IDocumentSession Session,
    DocumentTextIndex Index,
    PageIndex PageIndex,
    DocumentTextSelectionPoint AnchorPoint,
    DocumentTextSelectionPoint ActivePoint);
