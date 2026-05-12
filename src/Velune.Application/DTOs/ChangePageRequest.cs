using Velune.Domain.ValueObjects;

namespace Velune.Application.DTOs;

/// <summary>Request to navigate to a specific page.</summary>
public sealed record ChangePageRequest(PageIndex PageIndex);
