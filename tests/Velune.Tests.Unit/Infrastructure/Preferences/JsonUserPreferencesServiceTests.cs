using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Velune.Application.Configuration;
using Velune.Infrastructure.Preferences;

namespace Velune.Tests.Unit.Infrastructure.Preferences;

public sealed class JsonUserPreferencesServiceTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "velune-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Constructor_ShouldFallbackToDefaults_WhenPreferencesFileDoesNotExist()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        var filePath = Path.Combine(_temporaryDirectory, "preferences.json");

        using var service = CreateService(filePath, defaultCacheLimit: 64);

        Assert.Equal(AppThemePreference.System, service.Current.Theme);
        Assert.Equal(DefaultZoomPreference.FitToPage, service.Current.DefaultZoom);
        Assert.True(service.Current.ShowThumbnailsPanel);
        Assert.Equal(64, service.Current.MemoryCacheEntryLimit);
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistPreferencesBetweenInstances()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        var filePath = Path.Combine(_temporaryDirectory, "preferences.json");
        var preferences = UserPreferences.CreateDefault(64) with
        {
            Theme = AppThemePreference.Dark,
            DefaultZoom = DefaultZoomPreference.ActualSize,
            ShowThumbnailsPanel = false,
            MemoryCacheEntryLimit = 128
        };

        using var service = CreateService(filePath, defaultCacheLimit: 64);

        await service.SaveAsync(preferences);

        using var reloadedService = CreateService(filePath, defaultCacheLimit: 64);

        Assert.Equal(AppThemePreference.Dark, reloadedService.Current.Theme);
        Assert.Equal(DefaultZoomPreference.ActualSize, reloadedService.Current.DefaultZoom);
        Assert.False(reloadedService.Current.ShowThumbnailsPanel);
        Assert.Equal(128, reloadedService.Current.MemoryCacheEntryLimit);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup for temporary preference files.
        }
    }

    private static JsonUserPreferencesService CreateService(string filePath, int defaultCacheLimit)
    {
        return new JsonUserPreferencesService(
            NullLogger<JsonUserPreferencesService>.Instance,
            Options.Create(new AppOptions
            {
                Name = "Velune.Tests",
                RenderCacheEntryLimit = defaultCacheLimit,
                UserPreferencesPath = filePath
            }));
    }
}
