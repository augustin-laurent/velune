namespace Velune.Application.DTOs;

/// <summary>Information about an available print destination.</summary>
/// <param name="Name">The printer name.</param>
/// <param name="IsDefault">Whether this is the system default printer.</param>
public sealed record PrintDestinationInfo(
    string Name,
    bool IsDefault)
{
    /// <summary>Gets a display-friendly name, annotated if default.</summary>
    public string DisplayName => IsDefault ? $"{Name} (Default)" : Name;
}
