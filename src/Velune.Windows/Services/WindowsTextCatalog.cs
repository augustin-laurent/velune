using System.Globalization;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;

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

    /// <summary>
    /// Raised after the active catalog has been reloaded with a new language.
    /// </summary>
    event EventHandler? LanguageChanged;

    /// <summary>
    /// Reloads the catalog for the given language preference.
    /// </summary>
    /// <param name="preference">The language preference to apply.</param>
    void Reload(AppLanguagePreference preference);
}

/// <summary>
/// Loads and resolves localized strings from .lang files for the Windows UI layer.
/// </summary>
public sealed class WindowsTextCatalog : IWindowsTextCatalog, IDisposable
{
    private static readonly Dictionary<AppLanguagePreference, string> LanguageCodes = new()
    {
        [AppLanguagePreference.English] = "en",
        [AppLanguagePreference.French] = "fr",
        [AppLanguagePreference.Spanish] = "es"
    };

    private readonly string _catalogRoot;
    private readonly IUserPreferencesService _userPreferencesService;
    private Dictionary<string, string> _fallbackCatalog;
    private Dictionary<string, string> _activeCatalog;
    private bool _disposed;

    /// <summary>
    /// Initializes the catalog by loading the language from saved user preferences.
    /// </summary>
    public WindowsTextCatalog(IUserPreferencesService userPreferencesService)
    {
        ArgumentNullException.ThrowIfNull(userPreferencesService);
        _userPreferencesService = userPreferencesService;
        _catalogRoot = Path.Combine(AppContext.BaseDirectory, "Localization");
        _fallbackCatalog = LoadCatalog(Path.Combine(_catalogRoot, "en.lang"));

        string language = ResolveLanguageCode(userPreferencesService.Current.Language);
        _activeCatalog = language == "en"
            ? _fallbackCatalog
            : LoadCatalog(Path.Combine(_catalogRoot, $"{language}.lang"));

        _userPreferencesService.PreferencesChanged += OnPreferencesChanged;
    }

    /// <inheritdoc />
    public event EventHandler? LanguageChanged;

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

    /// <inheritdoc />
    public void Reload(AppLanguagePreference preference)
    {
        string language = ResolveLanguageCode(preference);
        _fallbackCatalog = LoadCatalog(Path.Combine(_catalogRoot, "en.lang"));
        _activeCatalog = language == "en"
            ? _fallbackCatalog
            : LoadCatalog(Path.Combine(_catalogRoot, $"{language}.lang"));

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _userPreferencesService.PreferencesChanged -= OnPreferencesChanged;
        _disposed = true;
    }

    private void OnPreferencesChanged(object? sender, EventArgs e)
    {
        Reload(_userPreferencesService.Current.Language);
    }

    private static string ResolveLanguageCode(AppLanguagePreference preference)
    {
        if (preference is not AppLanguagePreference.System &&
            LanguageCodes.TryGetValue(preference, out string? code))
        {
            return code;
        }

        string candidate = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        return candidate is "fr" or "es" ? candidate : "en";
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
