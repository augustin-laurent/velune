namespace Velune.Application.Configuration;

public sealed class AppOptions
{
    public const string SectionName = "App";

    public string Name { get; set; } = "Velune";
    public string Environment { get; set; } = "Development";
    public int RecentFilesLimit { get; set; } = 10;
}
