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

    /// <summary>
    /// Waits until every key of the reserved chord is up again (<c>GetAsyncKeyState</c>), sampled every
    /// <c>Timings.Foreground.ChordReleasePoll</c> for at most <c>Timings.Foreground.RightsHotkeyTimeout</c>. Call it
    /// after <c>WM_HOTKEY</c> and before taking the foreground: the system hands each key message to the thread in front
    /// when it processes the key, so a release processed after the foreground moved would reach the new window and
    /// leave the modifier down in the app that received the press (spike S4).
    /// </summary>
    /// <returns>True when every key is up; false when the wait ended first (the ladder goes on anyway).</returns>
    /// <remarks>
    /// The default says the chord is already up: it is for an implementation whose chord never reaches the keyboard (a
    /// fake, or one written before this member existed). <c>InternalRightsHotkey</c>, the only implementation that
    /// registers the chord with the system, implements it.
    /// </remarks>
    ValueTask<bool> WaitForChordReleaseAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(true);
}
