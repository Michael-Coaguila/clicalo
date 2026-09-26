using Clicalo.Domain.Timing;
using Serilog.Core;
using Serilog.Events;

namespace Clicalo.Infrastructure.Logging;

/// <summary>
/// Debug logging on request (blueprint §9.4): it turns itself off after <c>Timings.Logging.DebugLoggingDuration</c>,
/// so a forgotten request never leaves the log verbose.
/// </summary>
public sealed class DebugLoggingWindow : IDisposable
{
    private readonly LoggingLevelSwitch _level;
    private readonly ITimer _timer;

    /// <summary>Creates the window over <paramref name="level"/>.</summary>
    /// <param name="level">The level switch of the product logger.</param>
    /// <param name="time">Clock of the automatic end.</param>
    public DebugLoggingWindow(LoggingLevelSwitch level, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(time);
        _level = level;
        _timer = time.CreateTimer(
            _ => _level.MinimumLevel = LogEventLevel.Information,
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan
        );
    }

    /// <summary>Whether Debug is on.</summary>
    public bool IsOpen => _level.MinimumLevel <= LogEventLevel.Debug;

    /// <summary>Turns Debug on for <c>Timings.Logging.DebugLoggingDuration</c> (restarting the count).</summary>
    public void Open()
    {
        _level.MinimumLevel = LogEventLevel.Debug;
        _timer.Change(Timings.Logging.DebugLoggingDuration, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Turns Debug off now.</summary>
    public void Close()
    {
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _level.MinimumLevel = LogEventLevel.Information;
    }

    /// <inheritdoc />
    public void Dispose() => _timer.Dispose();
}
