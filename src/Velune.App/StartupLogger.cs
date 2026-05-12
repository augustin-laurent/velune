using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Velune.Application.Configuration;

namespace Velune.App;

/// <summary>
/// Logs application startup information including environment and configuration.
/// </summary>
public sealed partial class StartupLogger
{
    private readonly ILogger<StartupLogger> _logger;
    private readonly AppOptions _options;

    /// <summary>
    /// Initializes the startup logger with the configured logger and options.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">Application configuration options.</param>
    public StartupLogger(
        ILogger<StartupLogger> logger,
        IOptions<AppOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Emits the startup log message with application name, environment, and settings.
    /// </summary>
    public void LogStartup()
    {
        LogStartingApplication(
            _logger,
            _options.Name,
            _options.Environment,
            _options.RecentFilesLimit);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Starting {ApplicationName} in {Environment} mode (RecentFilesLimit={RecentFilesLimit})")]
    private static partial void LogStartingApplication(
        ILogger logger,
        string applicationName,
        string environment,
        int recentFilesLimit);
}
