namespace Velune.Presentation.Platform;

internal static class PresentationPlatform
{
    internal static Func<bool> IsMacOSDetector { get; set; } = OperatingSystem.IsMacOS;

    internal static Func<bool> IsWindowsDetector { get; set; } = OperatingSystem.IsWindows;

    internal static bool IsMacOS => IsMacOSDetector();

    internal static bool IsWindows => IsWindowsDetector();
}
