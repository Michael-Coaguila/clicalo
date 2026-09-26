namespace Clicalo.Application.Foreground;

/// <summary>
/// The step of the foreground rights ladder that brought a lease target to the front (blueprint §3.6). Recorded in
/// <see cref="ForegroundLease.GrantedAt"/> so spike S4 can report which step each origin needs.
/// </summary>
public enum LadderStep
{
    /// <summary>Step 1: the first <c>SetForegroundWindow</c> worked (Clícalo held the right).</summary>
    Direct,

    /// <summary>
    /// Step 1 with its single retry after <c>Timings.Foreground.RestoreRetryDelay</c> (<see cref="LeaseOrigin.Touch"/>,
    /// <see cref="LeaseOrigin.Tray"/> and <see cref="LeaseOrigin.GlobalHotkey"/>).
    /// </summary>
    DirectRetry,

    /// <summary>
    /// Step 2 (<see cref="LeaseOrigin.UiaInvoke"/>): the internal rights hotkey arrived as <c>WM_HOTKEY</c> and the
    /// retry that followed worked.
    /// </summary>
    RightsHotkey,
}
