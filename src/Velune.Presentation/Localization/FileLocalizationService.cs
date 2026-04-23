using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;

namespace Velune.Presentation.Localization;

public sealed partial class FileLocalizationService : ILocalizationService, INotifyPropertyChanged, IDisposable
{
    private static readonly Dictionary<AppLanguagePreference, string> LanguageCodes =
        new Dictionary<AppLanguagePreference, string>
        {
            [AppLanguagePreference.English] = "en",
            [AppLanguagePreference.French] = "fr",
            [AppLanguagePreference.Spanish] = "es"
        };

    private readonly ILogger<FileLocalizationService> _logger;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly string _catalogRootPath;
    private readonly ConcurrentDictionary<string, byte> _missingKeys = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    private Dictionary<string, string> _fallbackCatalog = new(StringComparer.Ordinal);
    private Dictionary<string, string> _activeCatalog = new(StringComparer.Ordinal);
    private AppLanguagePreference _currentLanguagePreference;
    private string _currentLanguageCode = "en";
    private int _version;
    private bool _disposed;

    public FileLocalizationService(
        ILogger<FileLocalizationService> logger,
        IUserPreferencesService userPreferencesService,
        IOptions<AppOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(userPreferencesService);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _userPreferencesService = userPreferencesService;
        _catalogRootPath = ResolveCatalogRootPath(options.Value);

        LocalizationServiceLocator.Current = this;
        ReloadCatalogs(_userPreferencesService.Current.Language);
        _userPreferencesService.PreferencesChanged += OnPreferencesChanged;
    }

    public string CurrentLanguageCode
    {
        get
        {
            lock (_gate)
            {
                return _currentLanguageCode;
            }
        }
    }

    public AppLanguagePreference CurrentLanguagePreference
    {
        get
        {
            lock (_gate)
            {
                return _currentLanguagePreference;
            }
        }
    }

    public int Version
    {
        get
        {
            lock (_gate)
            {
                return _version;
            }
        }
    }

    public event EventHandler? LanguageChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string GetString(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (TryGetValue(key, out var value))
        {
            return value;
        }

        if (_missingKeys.TryAdd(key, 0))
        {
            LogMissingTranslation(_logger, key, CurrentLanguageCode, _catalogRootPath);
        }

        return key;
    }

    public string GetString(string key, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var format = GetString(key);
        if (arguments.Length == 0)
        {
            return format;
        }

        return string.Format(CultureInfo.CurrentCulture, format, arguments);
    }

    public bool HasKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return TryGetValue(key, out _);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _userPreferencesService.PreferencesChanged -= OnPreferencesChanged;
        if (ReferenceEquals(LocalizationServiceLocator.Current, this))
        {
            LocalizationServiceLocator.Current = null;
        }

        _disposed = true;
    }

    private bool TryGetValue(string key, out string value)
    {
        lock (_gate)
        {
            if (_activeCatalog.TryGetValue(key, out var activeValue))
            {
                value = activeValue;
                return true;
            }

            if (_fallbackCatalog.TryGetValue(key, out var fallbackValue))
            {
                value = fallbackValue;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private void OnPreferencesChanged(object? sender, EventArgs e)
    {
        ReloadCatalogs(_userPreferencesService.Current.Language);
    }

    private void ReloadCatalogs(AppLanguagePreference preference)
    {
        var fallbackCatalog = LoadCatalog("en");
        var languageCode = ResolveLanguageCode(preference, fallbackCatalog);
        var activeCatalog = string.Equals(languageCode, "en", StringComparison.OrdinalIgnoreCase)
            ? fallbackCatalog
            : LoadCatalog(languageCode);

        lock (_gate)
        {
            _fallbackCatalog = fallbackCatalog;
            _activeCatalog = activeCatalog;
            _currentLanguagePreference = preference;
            _currentLanguageCode = languageCode;
            _version++;
        }

        ApplyCurrentCulture(languageCode);
        RaisePropertyChanged(nameof(CurrentLanguageCode));
        RaisePropertyChanged(nameof(CurrentLanguagePreference));
        RaisePropertyChanged(nameof(Version));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private Dictionary<string, string> LoadCatalog(string languageCode)
    {
        var path = Path.Combine(_catalogRootPath, $"{languageCode}.lang");
        if (!File.Exists(path))
        {
            LogMissingCatalog(_logger, languageCode, path);
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return LocalizationFileParser.Parse(path);
    }

    private static void ApplyCurrentCulture(string languageCode)
    {
        var culture = CreateCulture(languageCode);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    private static CultureInfo CreateCulture(string languageCode)
    {
        return languageCode switch
        {
            "fr" => CultureInfo.GetCultureInfo("fr-FR"),
            "es" => CultureInfo.GetCultureInfo("es-ES"),
            _ => CultureInfo.GetCultureInfo("en-US")
        };
    }

    private static string ResolveLanguageCode(
        AppLanguagePreference preference,
        Dictionary<string, string> fallbackCatalog)
    {
        if (preference is not AppLanguagePreference.System &&
            LanguageCodes.TryGetValue(preference, out var configuredLanguageCode))
        {
            return configuredLanguageCode;
        }

        var candidate = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (string.Equals(candidate, "fr", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "es", StringComparison.OrdinalIgnoreCase))
        {
            return candidate.ToLowerInvariant();
        }

        return fallbackCatalog.Count > 0 ? "en" : candidate.ToLowerInvariant();
    }

    private static string ResolveCatalogRootPath(AppOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.LocalizationPath))
        {
            return Path.GetFullPath(options.LocalizationPath);
        }

        return Path.Combine(AppContext.BaseDirectory, "Localization");
    }

    [LoggerMessage(
        EventId = 410,
        Level = LogLevel.Warning,
        Message = "The localization catalog for language '{LanguageCode}' was not found at {CatalogPath}.")]
    private static partial void LogMissingCatalog(
        ILogger logger,
        string languageCode,
        string catalogPath);

    [LoggerMessage(
        EventId = 411,
        Level = LogLevel.Warning,
        Message = "Missing translation key '{Key}' for language '{LanguageCode}' in {CatalogRootPath}.")]
    private static partial void LogMissingTranslation(
        ILogger logger,
        string key,
        string languageCode,
        string catalogRootPath);
}
