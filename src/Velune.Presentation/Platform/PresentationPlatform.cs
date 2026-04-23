namespace Velune.Presentation.Platform;

internal static class PresentationPlatform
{
    internal static Func<bool> IsMacOSDetector { get; set; } = OperatingSystem.IsMacOS;

    internal static bool IsMacOS => IsMacOSDetector();
}
