using Microsoft.Extensions.Logging;

namespace Velune.Tests.Unit.Support;

public sealed class ListLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries
    {
        get;
    } = [];

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        Entries.Add(new LogEntry(
            logLevel,
            formatter(state, exception)));
    }

    public sealed record LogEntry(
        LogLevel LogLevel,
        string Message);

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
