using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Velune.Application.Configuration;

namespace Velune.App;

public sealed partial class StartupLogger
{
    private readonly ILogger<StartupLogger> _logger;
    private readonly AppOptions _options;

    public StartupLogger(
        ILogger<StartupLogger> logger,
        IOptions<AppOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _options = options.Value;
    }

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
