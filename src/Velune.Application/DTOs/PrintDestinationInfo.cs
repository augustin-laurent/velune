namespace Velune.Application.DTOs;

public sealed record PrintDestinationInfo(
    string Name,
    bool IsDefault)
{
    public string DisplayName => IsDefault ? $"{Name} (Default)" : Name;
}
