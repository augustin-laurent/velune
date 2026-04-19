using System.Runtime.InteropServices;

namespace Velune.Tests.Integration.Infrastructure;

public sealed class RequiresTesseractFactAttribute : FactAttribute
{
    public RequiresTesseractFactAttribute()
    {
        if (!TesseractTestSupport.IsAvailable())
        {
            Skip = "tesseract is not available on this machine.";
        }
    }
}

internal static class TesseractTestSupport
{
    private static readonly Lazy<bool> Availability = new(ResolveAvailability);

    public static bool IsAvailable() => Availability.Value;

    public static string GetExecutablePath() =>
        Environment.GetEnvironmentVariable("VELUNE_TESSERACT_PATH") ?? "tesseract";

    private static bool ResolveAvailability()
    {
        var configuredPath = Environment.GetEnvironmentVariable("VELUNE_TESSERACT_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return File.Exists(configuredPath);
        }

        var executableNames = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { "tesseract.exe", "tesseract.cmd", "tesseract.bat" }
            : new[] { "tesseract" };

        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return false;
        }

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var executableName in executableNames)
            {
                var candidate = Path.Combine(directory, executableName);
                if (File.Exists(candidate))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
