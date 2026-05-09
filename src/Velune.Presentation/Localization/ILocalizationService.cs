using Velune.Application.Configuration;

namespace Velune.Presentation.Localization;

/// <summary>
/// Provides localized string lookups and language change notifications.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Gets the active two-letter language code (e.g. "en", "fr").
    /// </summary>
    string CurrentLanguageCode
    {
        get;
    }

    /// <summary>
    /// Gets the active language preference setting.
    /// </summary>
    AppLanguagePreference CurrentLanguagePreference
    {
        get;
    }

    /// <summary>
    /// Gets a version counter that increments on each language change.
    /// </summary>
    int Version
    {
        get;
    }

    /// <summary>
    /// Raised when the active language changes.
    /// </summary>
    event EventHandler? LanguageChanged;

    /// <summary>
    /// Returns the localized string for the given key, or the key itself if not found.
    /// </summary>
    /// <param name="key">The translation key.</param>
    /// <returns>The localized string.</returns>
    string GetString(string key);

    /// <summary>
    /// Returns a formatted localized string for the given key with arguments.
    /// </summary>
    /// <param name="key">The translation key.</param>
    /// <param name="arguments">Format arguments.</param>
    /// <returns>The formatted localized string.</returns>
    string GetString(string key, params object?[] arguments);

    /// <summary>
    /// Returns whether the given translation key exists in the active catalog.
    /// </summary>
    /// <param name="key">The translation key.</param>
    /// <returns>True if the key is present.</returns>
    bool HasKey(string key);
}
