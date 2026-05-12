using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Velune.Application.Abstractions;
using Velune.Application.Configuration;
using Velune.Application.DTOs;

namespace Velune.Infrastructure.State;

/// <summary>
/// Persists the recent files list as a JSON file on disk.
/// </summary>
public sealed partial class JsonRecentFilesService : IRecentFilesService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly ILogger<JsonRecentFilesService> _logger;
    private readonly string _filePath;
    private readonly int _limit;
    private readonly List<RecentFileItem> _items;
    private readonly object _syncRoot = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonRecentFilesService"/> class.
    /// </summary>
    /// <param name="logger">Logger for recording load/save failures.</param>
    /// <param name="options">Application options containing recent files path and limit.</param>
    public JsonRecentFilesService(
        ILogger<JsonRecentFilesService> logger,
        IOptions<AppOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _limit = Math.Max(1, options.Value.RecentFilesLimit);
        _filePath = ResolveFilePath(options.Value);
        _items = Load();
    }

    /// <inheritdoc />
    public IReadOnlyList<RecentFileItem> GetAll()
    {
        lock (_syncRoot)
        {
            return _items.ToArray();
        }
    }

    /// <inheritdoc />
    public void Add(RecentFileItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_syncRoot)
        {
            _items.RemoveAll(existing =>
                string.Equals(existing.FilePath, item.FilePath, StringComparison.OrdinalIgnoreCase));

            _items.Insert(0, item);

            if (_items.Count > _limit)
            {
                _items.RemoveRange(_limit, _items.Count - _limit);
            }

            Save();
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_syncRoot)
        {
            _items.Clear();
            Save();
        }
    }

    private List<RecentFileItem> Load()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            string json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            return (JsonSerializer.Deserialize<List<RecentFileItem>>(json, SerializerOptions) ?? [])
                .Where(item => !string.IsNullOrWhiteSpace(item.FilePath))
                .Take(_limit)
                .ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            LogLoadFailed(_logger, exception, _filePath);
            return [];
        }
    }

    private void Save()
    {
        try
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(_items, SerializerOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            LogSaveFailed(_logger, exception, _filePath);
        }
    }

    private static string ResolveFilePath(AppOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.RecentFilesPath))
        {
            return options.RecentFilesPath;
        }

        string applicationName = string.IsNullOrWhiteSpace(options.Name)
            ? "Velune"
            : options.Name;
        string applicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        return Path.Combine(applicationDataPath, applicationName, "recent-files.json");
    }

    [LoggerMessage(
        EventId = 90,
        Level = LogLevel.Warning,
        Message = "Unable to save recent files to {RecentFilesPath}.")]
    private static partial void LogSaveFailed(
        ILogger logger,
        Exception exception,
        string recentFilesPath);

    [LoggerMessage(
        EventId = 91,
        Level = LogLevel.Warning,
        Message = "Unable to load recent files from {RecentFilesPath}. Starting with an empty list.")]
    private static partial void LogLoadFailed(
        ILogger logger,
        Exception exception,
        string recentFilesPath);
}
