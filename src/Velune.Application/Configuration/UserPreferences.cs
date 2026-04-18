namespace Velune.Application.Configuration;

public enum AppThemePreference
{
    System = 0,
    Light = 1,
    Dark = 2
}

public enum DefaultZoomPreference
{
    FitToPage = 0,
    FitToWidth = 1,
    ActualSize = 2
}

public sealed record UserPreferences
{
    public AppThemePreference Theme
    {
        get; init;
    } = AppThemePreference.System;

    public DefaultZoomPreference DefaultZoom
    {
        get; init;
    } = DefaultZoomPreference.FitToPage;

    public bool ShowThumbnailsPanel
    {
        get; init;
    } = true;

    public int MemoryCacheEntryLimit
    {
        get; init;
    }

    public static UserPreferences CreateDefault(int defaultMemoryCacheEntryLimit)
    {
        return new UserPreferences
        {
            MemoryCacheEntryLimit = Math.Max(0, defaultMemoryCacheEntryLimit)
        };
    }

    public UserPreferences Normalize(int defaultMemoryCacheEntryLimit)
    {
        var normalizedTheme = Enum.IsDefined(Theme)
            ? Theme
            : AppThemePreference.System;
        var normalizedZoom = Enum.IsDefined(DefaultZoom)
            ? DefaultZoom
            : DefaultZoomPreference.FitToPage;
        var normalizedCacheEntryLimit = MemoryCacheEntryLimit < 0
            ? Math.Max(0, defaultMemoryCacheEntryLimit)
            : MemoryCacheEntryLimit;

        return this with
        {
            Theme = normalizedTheme,
            DefaultZoom = normalizedZoom,
            MemoryCacheEntryLimit = normalizedCacheEntryLimit
        };
    }
}
