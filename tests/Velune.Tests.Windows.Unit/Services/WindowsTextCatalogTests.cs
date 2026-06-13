using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Windows.Services;

namespace Velune.Tests.Windows.Unit.Services;

public sealed class WindowsTextCatalogTests
{
    [Fact]
    public async Task SavePreferences_ReloadsCatalogAndRaisesLanguageChanged()
    {
        var preferencesService = new StubUserPreferencesService(new UserPreferences
        {
            Language = AppLanguagePreference.English
        });
        using var catalog = new WindowsTextCatalog(preferencesService);
        int languageChangedCount = 0;
        catalog.LanguageChanged += (_, _) => languageChangedCount++;

        await preferencesService.SaveAsync(preferencesService.Current with
        {
            Language = AppLanguagePreference.Spanish
        });

        Assert.Equal(1, languageChangedCount);
        Assert.Equal(
            "Barra de herramientas de anotacion de texto",
            catalog.GetString("panel.annotations.text_toolbar"));
    }

    [Fact]
    public void GetString_ReturnsLocalizedTextAnnotationToolbarName()
    {
        using var catalog = new WindowsTextCatalog(new StubUserPreferencesService(new UserPreferences
        {
            Language = AppLanguagePreference.English
        }));

        Assert.Equal(
            "Text annotation toolbar",
            catalog.GetString("panel.annotations.text_toolbar"));
    }

    private sealed class StubUserPreferencesService : IUserPreferencesService
    {
        public StubUserPreferencesService(UserPreferences current)
        {
            Current = current;
        }

        public UserPreferences Current
        {
            get;
            private set;
        }

        public event EventHandler? PreferencesChanged;

        public Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default)
        {
            Current = preferences;
            PreferencesChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }
}
