namespace Velune.Application.Configuration;

/// <summary>Specifies the application theme preference.</summary>
public enum AppThemePreference
{
    System = 0,
    Light = 1,
    Dark = 2
}

/// <summary>Specifies the default zoom behavior when opening a document.</summary>
public enum DefaultZoomPreference
{
    FitToPage = 0,
    FitToWidth = 1,
    ActualSize = 2
}

/// <summary>Specifies the application UI language preference.</summary>
public enum AppLanguagePreference
{
    System = 0,
    English = 1,
    French = 2,
    Spanish = 3
}

/// <summary>Stores user-configurable application preferences.</summary>
public sealed record UserPreferences
{
    /// <summary>Gets the theme preference.</summary>
    public AppThemePreference Theme
    {
        get; init;
    } = AppThemePreference.System;

    /// <summary>Gets the default zoom preference.</summary>
    public DefaultZoomPreference DefaultZoom
    {
        get; init;
    } = DefaultZoomPreference.FitToPage;

    /// <summary>Gets the UI language preference.</summary>
    public AppLanguagePreference Language
    {
        get; init;
    } = AppLanguagePreference.System;

    /// <summary>Gets whether the thumbnails panel should be visible.</summary>
    public bool ShowThumbnailsPanel
    {
        get; init;
    } = true;

    /// <summary>Gets the maximum number of entries allowed in the render memory cache.</summary>
    public int MemoryCacheEntryLimit
    {
        get; init;
    }

    /// <summary>Creates a default preferences instance with the specified cache limit.</summary>
    /// <param name="defaultMemoryCacheEntryLimit">The default memory cache entry limit.</param>
    /// <returns>A new <see cref="UserPreferences"/> with default values.</returns>
    public static UserPreferences CreateDefault(int defaultMemoryCacheEntryLimit)
    {
        return new UserPreferences
        {
            MemoryCacheEntryLimit = Math.Max(0, defaultMemoryCacheEntryLimit)
        };
    }

    /// <summary>Returns a normalized copy with invalid enum values and negative limits corrected.</summary>
    /// <param name="defaultMemoryCacheEntryLimit">The fallback cache limit for invalid values.</param>
    /// <returns>A normalized <see cref="UserPreferences"/> instance.</returns>
    public UserPreferences Normalize(int defaultMemoryCacheEntryLimit)
    {
        var normalizedTheme = Enum.IsDefined(Theme)
            ? Theme
            : AppThemePreference.System;
        var normalizedZoom = Enum.IsDefined(DefaultZoom)
            ? DefaultZoom
            : DefaultZoomPreference.FitToPage;
        var normalizedLanguage = Enum.IsDefined(Language)
            ? Language
            : AppLanguagePreference.System;
        var normalizedCacheEntryLimit = MemoryCacheEntryLimit < 0
            ? Math.Max(0, defaultMemoryCacheEntryLimit)
            : MemoryCacheEntryLimit;

        return this with
        {
            Theme = normalizedTheme,
            DefaultZoom = normalizedZoom,
            Language = normalizedLanguage,
            MemoryCacheEntryLimit = normalizedCacheEntryLimit
        };
    }
}
