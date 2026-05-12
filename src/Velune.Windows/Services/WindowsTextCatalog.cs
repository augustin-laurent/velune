using System.Globalization;

namespace Velune.Windows.Services;

/// <summary>
/// Provides localized strings loaded from .lang files for the Windows UI layer.
/// </summary>
public interface IWindowsTextCatalog
{
    /// <summary>
    /// Gets a localized string by key, falling back to the English catalog.
    /// </summary>
    /// <param name="key">The localization key.</param>
    /// <returns>The localized string, or the key itself if not found.</returns>
    string GetString(string key);

    /// <summary>
    /// Gets a localized format string and applies the given arguments.
    /// </summary>
    /// <param name="key">The localization key.</param>
    /// <param name="args">Format arguments.</param>
    /// <returns>The formatted localized string.</returns>
    string Format(string key, params object[] args);
}

/// <summary>
/// Loads and resolves localized strings from .lang files at application startup.
/// </summary>
public sealed class WindowsTextCatalog : IWindowsTextCatalog
{
    private readonly Dictionary<string, string> _fallbackCatalog;
    private readonly Dictionary<string, string> _activeCatalog;

    /// <summary>
    /// Initializes the catalog by loading the appropriate language file based on the current UI culture.
    /// </summary>
    public WindowsTextCatalog()
    {
        string catalogRoot = Path.Combine(AppContext.BaseDirectory, "Localization");
        _fallbackCatalog = LoadCatalog(Path.Combine(catalogRoot, "en.lang"));

        string language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "fr" => "fr",
            "es" => "es",
            _ => "en"
        };

        _activeCatalog = language == "en"
            ? _fallbackCatalog
            : LoadCatalog(Path.Combine(catalogRoot, $"{language}.lang"));
    }

    /// <inheritdoc />
    public string GetString(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _activeCatalog.TryGetValue(key, out string? activeValue)
            ? activeValue
            : _fallbackCatalog.TryGetValue(key, out string? fallbackValue)
                ? fallbackValue
                : key;
    }

    /// <inheritdoc />
    public string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, GetString(key), args);
    }

    private static Dictionary<string, string> LoadCatalog(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return values;
        }

        foreach (string rawLine in File.ReadLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            int separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            string key = line[..separatorIndex].Trim();
            string value = line[(separatorIndex + 1)..].Trim();
            values[key] = value.Replace(@"\n", Environment.NewLine, StringComparison.Ordinal);
        }

        return values;
    }
}
