namespace Velune.Application.Documents;

/// <summary>
/// Provides a private temp directory under LocalApplicationData for Velune operations,
/// avoiding shared system temp and enabling reliable startup cleanup.
/// </summary>
public static class VeluneTempDirectory
{
    private static readonly string BasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Velune",
        "Temp");

    /// <summary>
    /// Creates and returns a unique subdirectory under the private Velune temp root.
    /// </summary>
    /// <param name="prefix">A short prefix to identify the operation type.</param>
    /// <returns>The full path to the created temporary directory.</returns>
    public static string Create(string prefix)
    {
        string path = Path.Combine(BasePath, $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Creates a unique temporary file under the private Velune temp root.
    /// </summary>
    /// <param name="extension">File extension including the dot (e.g., ".tmp").</param>
    /// <returns>The full path to the created temporary file.</returns>
    public static string CreateFile(string extension = ".tmp")
    {
        Directory.CreateDirectory(BasePath);
        string path = Path.Combine(BasePath, $"{Guid.NewGuid():N}{extension}");
        File.Create(path).Dispose();
        return path;
    }

    /// <summary>
    /// Removes all stale temporary directories and files from previous sessions.
    /// Safe to call at startup; swallows all exceptions.
    /// </summary>
    public static void CleanupStale()
    {
        try
        {
            if (!Directory.Exists(BasePath))
            {
                return;
            }

            foreach (string directory in Directory.EnumerateDirectories(BasePath))
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                }
                catch
                {
                    // Do nothing
                }
            }

            foreach (string file in Directory.EnumerateFiles(BasePath))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Do nothing
                }
            }
        }
        catch
        {
            // Do nothing
        }
    }
}
