using Microsoft.Extensions.Logging;

namespace Clicalo.Infrastructure.Tests.Migration;

/// <summary>Keeps every formatted log line, to prove what the import never logs (LOG-001).</summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<string> Lines { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    ) => Lines.Add(formatter(state, exception));
}
