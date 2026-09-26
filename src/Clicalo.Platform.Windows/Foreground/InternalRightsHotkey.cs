using System.Diagnostics.CodeAnalysis;
using Clicalo.Application.Ports;
using Clicalo.Platform.Windows.SysEvents;

namespace Clicalo.Platform.Windows.Foreground;

/// <summary>
/// Adapter of <see cref="IInternalRightsHotkey"/> (blueprint §3.6): registers Ctrl+Alt+Shift+F24 (left modifiers,
/// <c>MOD_NOREPEAT</c>) with <c>RegisterHotKey</c> on the message window of the <see cref="SysEventsThread"/> and
/// completes the armed wait when its <c>WM_HOTKEY</c> arrives.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality",
    Justification = "M1 contract stub: the foreground package implements it (docs/testing/spikes/M1-ownership.md)."
)]
public sealed class InternalRightsHotkey : IInternalRightsHotkey, IDisposable
{
    /// <summary>The <c>RegisterHotKey</c> id of the reserved chord.</summary>
    public const int HotkeyId = 0x4C43;

    /// <summary>Creates the adapter on <paramref name="thread"/>; waits are bounded with <paramref name="timeProvider"/>.</summary>
    public InternalRightsHotkey(SysEventsThread thread, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(thread);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Thread = thread;
        Clock = timeProvider;
    }

    /// <inheritdoc />
    public bool IsRegistered => throw new NotImplementedException("M1 foreground package.");

    /// <summary>The thread whose message window owns the registration.</summary>
    public SysEventsThread Thread { get; }

    /// <summary>Bounds the waits (<c>Timings.Foreground.RightsHotkeyTimeout</c>).</summary>
    public TimeProvider Clock { get; }

    /// <summary>Registers the chord on the SysEvents thread; false if another program already owns it.</summary>
    public Task<bool> RegisterAsync() =>
        throw new NotImplementedException("M1 foreground package.");

    /// <inheritdoc />
    public ValueTask<bool> WaitForRightsAsync(CancellationToken cancellationToken) =>
        throw new NotImplementedException("M1 foreground package.");

    /// <summary>Unregisters the chord.</summary>
    public void Dispose() { }
}
