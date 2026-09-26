namespace Clicalo.Application.Ports;

/// <summary>
/// Step 2 of the foreground rights ladder for <c>LeaseOrigin.UiaInvoke</c> (blueprint §3.6, ADR-0005): Clícalo
/// registers a reserved chord with <c>RegisterHotKey</c> (Ctrl+Alt+Shift+F24, left modifiers), the engine injects it
/// (<see cref="IInternalKeyEffects.SendRightsHotkeyAsync"/>), the system consumes it (no app receives it) and the
/// resulting <c>WM_HOTKEY</c> gives Clícalo the right to call <c>SetForegroundWindow</c>. Implemented by
/// <c>Clicalo.Platform.Windows.Foreground.InternalRightsHotkey</c> on the SysEvents thread.
/// </summary>
public interface IInternalRightsHotkey
{
    /// <summary>
    /// True when the chord is registered. When false (another program owns it), the ladder skips step 2: injecting
    /// an unregistered chord would reach the foreground app.
    /// </summary>
    bool IsRegistered { get; }

    /// <summary>
    /// Arms a one-shot wait for the next <c>WM_HOTKEY</c> of the reserved chord. Call it BEFORE sending the chord.
    /// Completes with true when it arrives within <c>Timings.Foreground.RightsHotkeyTimeout</c>, false on timeout;
    /// throws <see cref="OperationCanceledException"/> when <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    ValueTask<bool> WaitForRightsAsync(CancellationToken cancellationToken);
}
