using System.Globalization;
using System.ComponentModel;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Presentation.Localization;

namespace Velune.Tests.Unit.Presentation.Localization;

[Collection(LocalizationCultureCollection.Name)]
public sealed class FileLocalizationServiceTests
{
    [Fact]
    public void GetString_ShouldParseEscapedSequencesFromCatalog()
    {
        using var workspace = new TemporaryDirectory();
        WriteCatalog(
            workspace.Path,
            "en",
            """
            message=Line 1\nLine 2\tTabbed\\Path
            """);

        using var service = CreateService(
            workspace.Path,
            new StubUserPreferencesService(new UserPreferences
            {
                Language = AppLanguagePreference.English
            }));

        Assert.Equal("Line 1\nLine 2\tTabbed\\Path", service.GetString("message"));
    }

    [Fact]
    public void GetString_ShouldFallbackToEnglish_WhenKeyIsMissingFromSelectedLanguage()
    {
        using var workspace = new TemporaryDirectory();
        WriteCatalog(
            workspace.Path,
            "en",
            """
            greeting=Hello
            only.english=Only in English
            """);
        WriteCatalog(
            workspace.Path,
            "fr",
            """
            greeting=Bonjour
            """);

        using var service = CreateService(
            workspace.Path,
            new StubUserPreferencesService(new UserPreferences
            {
                Language = AppLanguagePreference.French
            }));

        Assert.Equal("fr", service.CurrentLanguageCode);
        Assert.Equal("Bonjour", service.GetString("greeting"));
        Assert.Equal("Only in English", service.GetString("only.english"));
    }

    [Fact]
    public async Task LanguageChanged_ShouldReloadCatalogImmediately_WhenPreferenceChanges()
    {
        using var workspace = new TemporaryDirectory();
        WriteCatalog(
            workspace.Path,
            "en",
            """
            greeting=Hello
            """);
        WriteCatalog(
            workspace.Path,
            "fr",
            """
            greeting=Bonjour
            """);

        var userPreferencesService = new StubUserPreferencesService(new UserPreferences
        {
            Language = AppLanguagePreference.English
        });

        using var service = CreateService(workspace.Path, userPreferencesService);
        var languageChangedCount = 0;
        service.LanguageChanged += (_, _) => languageChangedCount++;

        await userPreferencesService.SaveAsync(userPreferencesService.Current with
        {
            Language = AppLanguagePreference.French
        });

        Assert.Equal("fr", service.CurrentLanguageCode);
        Assert.Equal("Bonjour", service.GetString("greeting"));
        Assert.True(languageChangedCount >= 1);
    }

    [Fact]
    public async Task LanguageChanged_ShouldRaisePropertyChangedForVersion()
    {
        using var workspace = new TemporaryDirectory();
        WriteCatalog(
            workspace.Path,
            "en",
            """
            greeting=Hello
            """);
        WriteCatalog(
            workspace.Path,
            "es",
            """
            greeting=Hola
            """);

        var userPreferencesService = new StubUserPreferencesService(new UserPreferences
        {
            Language = AppLanguagePreference.English
        });

        using var service = CreateService(workspace.Path, userPreferencesService);
        var propertyChangedNames = new List<string>();
        ((INotifyPropertyChanged)service).PropertyChanged += (_, eventArgs) =>
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.PropertyName))
            {
                propertyChangedNames.Add(eventArgs.PropertyName);
            }
        };

        await userPreferencesService.SaveAsync(userPreferencesService.Current with
        {
            Language = AppLanguagePreference.Spanish
        });

        Assert.Contains(nameof(FileLocalizationService.Version), propertyChangedNames);
        Assert.Contains(nameof(FileLocalizationService.CurrentLanguageCode), propertyChangedNames);
        Assert.Equal("es", service.CurrentLanguageCode);
        Assert.Equal("Hola", service.GetString("greeting"));
    }

    [Fact]
    public void SystemLanguage_ShouldUseSupportedCulture_WhenCatalogExists()
    {
        using var scope = new CultureScope("fr-FR");
        using var workspace = new TemporaryDirectory();
        WriteCatalog(
            workspace.Path,
            "en",
            """
            greeting=Hello
            """);
        WriteCatalog(
            workspace.Path,
            "fr",
            """
            greeting=Bonjour
            """);

        using var service = CreateService(
            workspace.Path,
            new StubUserPreferencesService(new UserPreferences
            {
                Language = AppLanguagePreference.System
            }));

        Assert.Equal("fr", service.CurrentLanguageCode);
        Assert.Equal("Bonjour", service.GetString("greeting"));
    }

    [Fact]
    public void SystemLanguage_ShouldFallbackToEnglish_WhenCultureIsUnsupported()
    {
        using var scope = new CultureScope("de-DE");
        using var workspace = new TemporaryDirectory();
        WriteCatalog(
            workspace.Path,
            "en",
            """
            greeting=Hello
            """);

        using var service = CreateService(
            workspace.Path,
            new StubUserPreferencesService(new UserPreferences
            {
                Language = AppLanguagePreference.System
            }));

        Assert.Equal("en", service.CurrentLanguageCode);
        Assert.Equal("Hello", service.GetString("greeting"));
    }

    private static FileLocalizationService CreateService(
        string localizationPath,
        IUserPreferencesService userPreferencesService)
    {
        return new FileLocalizationService(
            NullLogger<FileLocalizationService>.Instance,
            userPreferencesService,
            Options.Create(new AppOptions
            {
                LocalizationPath = localizationPath
            }));
    }

    private static void WriteCatalog(string directoryPath, string languageCode, string content)
    {
        File.WriteAllText(
            Path.Combine(directoryPath, $"{languageCode}.lang"),
            content.ReplaceLineEndings(Environment.NewLine));
    }

    private sealed class StubUserPreferencesService : IUserPreferencesService
    {
        public StubUserPreferencesService(UserPreferences current)
        {
            Current = current;
        }

        public UserPreferences Current { get; private set; }

        public event EventHandler? PreferencesChanged;

        public Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default)
        {
            Current = preferences;
            PreferencesChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"velune-localization-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previousCurrentCulture;
        private readonly CultureInfo _previousCurrentUICulture;
        private readonly CultureInfo? _previousDefaultThreadCurrentCulture;
        private readonly CultureInfo? _previousDefaultThreadCurrentUICulture;

        public CultureScope(string cultureName)
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);

            _previousCurrentCulture = CultureInfo.CurrentCulture;
            _previousCurrentUICulture = CultureInfo.CurrentUICulture;
            _previousDefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentCulture;
            _previousDefaultThreadCurrentUICulture = CultureInfo.DefaultThreadCurrentUICulture;

            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _previousCurrentCulture;
            CultureInfo.CurrentUICulture = _previousCurrentUICulture;
            CultureInfo.DefaultThreadCurrentCulture = _previousDefaultThreadCurrentCulture;
            CultureInfo.DefaultThreadCurrentUICulture = _previousDefaultThreadCurrentUICulture;
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LocalizationCultureCollection
{
    public const string Name = "Localization culture tests";
}
