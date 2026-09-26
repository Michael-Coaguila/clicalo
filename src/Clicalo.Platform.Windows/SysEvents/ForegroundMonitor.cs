using System.Diagnostics.CodeAnalysis;
using Clicalo.Application.Ports;

namespace Clicalo.Platform.Windows.SysEvents;

/// <summary>
/// Adapter of <see cref="IForegroundMonitor"/> (blueprint §7.9): <c>SetWinEventHook(EVENT_SYSTEM_FOREGROUND)</c>
/// out of context with <c>WINEVENT_SKIPOWNPROCESS</c> on the <see cref="SysEventsThread"/>; each event is confirmed
/// with <c>GetForegroundWindow</c> and <c>GetWindowThreadProcessId</c> before it is published, and repeated events
/// for the same window are coalesced.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality",
    Justification = "M1 contract stub: the foreground package implements it (docs/testing/spikes/M1-ownership.md)."
)]
public sealed class ForegroundMonitor : IForegroundMonitor, IDisposable
{
    /// <summary>Creates the monitor on <paramref name="thread"/>, stamping with <paramref name="timeProvider"/>.</summary>
    public ForegroundMonitor(SysEventsThread thread, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(thread);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Thread = thread;
        Clock = timeProvider;
    }

    /// <inheritdoc />
    public event EventHandler<ExternalForegroundChangedEventArgs>? ExternalForegroundChanged;

    /// <inheritdoc />
    public ExternalForeground? Current =>
        throw new NotImplementedException("M1 foreground package.");

    /// <summary>The thread that owns the hook.</summary>
    public SysEventsThread Thread { get; }

    /// <summary>Stamps each confirmed change.</summary>
    public TimeProvider Clock { get; }

    /// <summary>Installs the hook on the SysEvents thread and reads the current foreground once.</summary>
    public Task StartAsync() => throw new NotImplementedException("M1 foreground package.");

    /// <summary>Removes the hook.</summary>
    public void Dispose() { }

    private void Publish(ExternalForeground foreground) =>
        ExternalForegroundChanged?.Invoke(this, new ExternalForegroundChangedEventArgs(foreground));
}
