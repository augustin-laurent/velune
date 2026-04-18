using System.Runtime.InteropServices;

namespace Velune.Tests.Integration.Infrastructure;

public sealed class RequiresQpdfFactAttribute : FactAttribute
{
    public RequiresQpdfFactAttribute()
    {
        if (!QpdfTestSupport.IsAvailable())
        {
            Skip = "qpdf is not available on this machine.";
        }
    }
}

internal static class QpdfTestSupport
{
    private static readonly Lazy<bool> Availability = new(ResolveAvailability);

    public static bool IsAvailable() => Availability.Value;

    public static string GetExecutablePath() =>
        Environment.GetEnvironmentVariable("VELUNE_QPDF_PATH") ?? "qpdf";

    private static bool ResolveAvailability()
    {
        var configuredPath = Environment.GetEnvironmentVariable("VELUNE_QPDF_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return File.Exists(configuredPath);
        }

        var executableNames = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { "qpdf.exe", "qpdf.cmd", "qpdf.bat" }
            : new[] { "qpdf" };

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

