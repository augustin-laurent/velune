using System.Diagnostics;

namespace Velune.Infrastructure.FileSystem;

/// <summary>
/// Locates bundled external tool executables relative to the application directory.
/// </summary>
internal static class BundledToolResolver
{
    private const string ToolsDirectoryName = "tools";

    /// <summary>
    /// Resolves the path to a bundled tool executable.
    /// </summary>
    /// <param name="configuredExecutablePath">User-configured path override, or null.</param>
    /// <param name="defaultExecutableName">Default executable file name.</param>
    /// <param name="toolDirectoryName">Subdirectory name within the tools folder.</param>
    /// <returns>A resolved <see cref="BundledTool"/> with executable and library paths.</returns>
    internal static BundledTool Resolve(
        string? configuredExecutablePath,
        string defaultExecutableName,
        string toolDirectoryName)
    {
        if (!string.IsNullOrWhiteSpace(configuredExecutablePath) &&
            !IsDefaultCommand(configuredExecutablePath, defaultExecutableName))
        {
            return Create(configuredExecutablePath, toolDirectoryName);
        }

        foreach (var candidatePath in EnumerateBundledExecutableCandidates(defaultExecutableName, toolDirectoryName))
        {
            if (File.Exists(candidatePath))
            {
                return Create(candidatePath, toolDirectoryName);
            }
        }

        return Create(defaultExecutableName, toolDirectoryName);
    }

    /// <summary>
    /// Resolves the Tesseract tessdata directory path.
    /// </summary>
    /// <param name="configuredDataPath">User-configured data path override, or null.</param>
    /// <returns>The resolved tessdata path, or null if not found.</returns>
    internal static string? ResolveTesseractDataPath(string? configuredDataPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredDataPath))
        {
            return configuredDataPath;
        }

        foreach (var baseDirectory in EnumerateBaseDirectories())
        {
            var candidatePath = Path.Combine(
                baseDirectory,
                ToolsDirectoryName,
                "tesseract",
                "tessdata");

            if (Directory.Exists(candidatePath))
            {
                return candidatePath;
            }
        }

        return null;
    }

    /// <summary>
    /// Creates a <see cref="ProcessStartInfo"/> configured with native library paths for the given tool.
    /// </summary>
    /// <param name="tool">The bundled tool to launch.</param>
    /// <returns>A ready-to-use <see cref="ProcessStartInfo"/>.</returns>
    internal static ProcessStartInfo CreateStartInfo(BundledTool tool)
    {
        var startInfo = new ProcessStartInfo(tool.ExecutablePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        ApplyNativeLibrarySearchPath(startInfo, tool.NativeLibraryDirectories);
        return startInfo;
    }

    private static BundledTool Create(string executablePath, string toolDirectoryName)
    {
        var libraryDirectories = EnumerateNativeLibraryDirectories(executablePath, toolDirectoryName)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new BundledTool(executablePath, libraryDirectories);
    }

    private static bool IsDefaultCommand(string configuredExecutablePath, string defaultExecutableName)
    {
        return string.Equals(
                configuredExecutablePath,
                defaultExecutableName,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                configuredExecutablePath,
                WithPlatformExtension(defaultExecutableName),
                StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateBundledExecutableCandidates(
        string defaultExecutableName,
        string toolDirectoryName)
    {
        var executableName = WithPlatformExtension(defaultExecutableName);

        foreach (var baseDirectory in EnumerateBaseDirectories())
        {
            var toolRoot = Path.Combine(baseDirectory, ToolsDirectoryName, toolDirectoryName);

            yield return Path.Combine(toolRoot, executableName);
            yield return Path.Combine(toolRoot, "bin", executableName);
        }
    }

    private static IEnumerable<string> EnumerateBaseDirectories()
    {
        yield return AppContext.BaseDirectory;

        if (OperatingSystem.IsMacOS())
        {
            var macOsDirectory = new DirectoryInfo(AppContext.BaseDirectory);
            var contentsDirectory = macOsDirectory.Parent;
            if (contentsDirectory?.Name == "Contents")
            {
                yield return contentsDirectory.FullName;
            }
        }
    }

    private static IEnumerable<string> EnumerateNativeLibraryDirectories(
        string executablePath,
        string toolDirectoryName)
    {
        if (Path.GetDirectoryName(executablePath) is { } executableDirectory &&
            Directory.Exists(executableDirectory))
        {
            yield return executableDirectory;
        }

        foreach (var baseDirectory in EnumerateBaseDirectories())
        {
            var toolRoot = Path.Combine(baseDirectory, ToolsDirectoryName, toolDirectoryName);
            var toolLibDirectory = Path.Combine(toolRoot, "lib");
            if (Directory.Exists(toolLibDirectory))
            {
                yield return toolLibDirectory;
            }

            var sharedLibDirectory = Path.Combine(baseDirectory, ToolsDirectoryName, "lib");
            if (Directory.Exists(sharedLibDirectory))
            {
                yield return sharedLibDirectory;
            }
        }
    }

    private static void ApplyNativeLibrarySearchPath(
        ProcessStartInfo startInfo,
        IReadOnlyList<string> nativeLibraryDirectories)
    {
        if (nativeLibraryDirectories.Count == 0)
        {
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            PrependEnvironmentPath(startInfo, "LD_LIBRARY_PATH", nativeLibraryDirectories);
        }
        else if (OperatingSystem.IsMacOS())
        {
            PrependEnvironmentPath(startInfo, "DYLD_LIBRARY_PATH", nativeLibraryDirectories);
        }
        else if (OperatingSystem.IsWindows())
        {
            PrependEnvironmentPath(startInfo, "PATH", nativeLibraryDirectories);
        }
    }

    private static void PrependEnvironmentPath(
        ProcessStartInfo startInfo,
        string variableName,
        IReadOnlyList<string> values)
    {
        var separator = Path.PathSeparator.ToString();
        var existingValue = startInfo.Environment.TryGetValue(variableName, out var value)
            ? value
            : Environment.GetEnvironmentVariable(variableName);
        var prefix = string.Join(separator, values);

        startInfo.Environment[variableName] = string.IsNullOrWhiteSpace(existingValue)
            ? prefix
            : $"{prefix}{separator}{existingValue}";
    }

    private static string WithPlatformExtension(string executableName)
    {
        if (!OperatingSystem.IsWindows() ||
            executableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return executableName;
        }

        return $"{executableName}.exe";
    }
}

/// <summary>
/// Represents a resolved external tool with its executable path and native library directories.
/// </summary>
/// <param name="ExecutablePath">Full path to the executable.</param>
/// <param name="NativeLibraryDirectories">Directories containing required native libraries.</param>
internal sealed record BundledTool(
    string ExecutablePath,
    IReadOnlyList<string> NativeLibraryDirectories);
