using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;

namespace Velune.Infrastructure.Preferences;

public sealed partial class JsonUserPreferencesService : IUserPreferencesService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly ILogger<JsonUserPreferencesService> _logger;
    private readonly string _filePath;
    private readonly int _defaultMemoryCacheEntryLimit;
    private bool _disposed;

    public JsonUserPreferencesService(
        ILogger<JsonUserPreferencesService> logger,
        IOptions<AppOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _defaultMemoryCacheEntryLimit = Math.Max(0, options.Value.RenderCacheEntryLimit);
        _filePath = ResolveFilePath(options.Value);
        Current = Load();
    }

    public UserPreferences Current
    {
        get;
        private set;
    }

    public event EventHandler? PreferencesChanged;

    public async Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var normalizedPreferences = preferences.Normalize(_defaultMemoryCacheEntryLimit);
        var directory = Path.GetDirectoryName(_filePath);

        await _saveLock.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(
                stream,
                normalizedPreferences,
                SerializerOptions,
                cancellationToken);

            Current = normalizedPreferences;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            LogSaveFailed(_logger, exception, _filePath);
            throw;
        }
        finally
        {
            _saveLock.Release();
        }

        PreferencesChanged?.Invoke(this, EventArgs.Empty);
    }

    private UserPreferences Load()
    {
        if (!File.Exists(_filePath))
        {
            return UserPreferences.CreateDefault(_defaultMemoryCacheEntryLimit);
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return UserPreferences.CreateDefault(_defaultMemoryCacheEntryLimit);
            }

            return JsonSerializer.Deserialize<UserPreferences>(json, SerializerOptions)?
                .Normalize(_defaultMemoryCacheEntryLimit)
                ?? UserPreferences.CreateDefault(_defaultMemoryCacheEntryLimit);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            LogLoadFailed(_logger, exception, _filePath);
            return UserPreferences.CreateDefault(_defaultMemoryCacheEntryLimit);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _saveLock.Dispose();
        _disposed = true;
    }

    private static string ResolveFilePath(AppOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.UserPreferencesPath))
        {
            return options.UserPreferencesPath;
        }

        var applicationName = string.IsNullOrWhiteSpace(options.Name)
            ? "Velune"
            : options.Name;
        var applicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        return Path.Combine(applicationDataPath, applicationName, "preferences.json");
    }

    [LoggerMessage(
        EventId = 70,
        Level = LogLevel.Warning,
        Message = "Unable to save user preferences to {PreferencesPath}.")]
    private static partial void LogSaveFailed(
        ILogger logger,
        Exception exception,
        string preferencesPath);

    [LoggerMessage(
        EventId = 71,
        Level = LogLevel.Warning,
        Message = "Unable to load user preferences from {PreferencesPath}. Falling back to defaults.")]
    private static partial void LogLoadFailed(
        ILogger logger,
        Exception exception,
        string preferencesPath);
}
