namespace Clicalo.Application.Foreground;

/// <summary>The typed foreground leases (blueprint §3.6, ADR-0005). Only one lease is active at a time.</summary>
public enum LeaseKind
{
    /// <summary>
    /// The panel search and text fields on surfaces (BUS-002): the surface loses <c>WS_EX_NOACTIVATE</c> while the
    /// lease lasts. On release it goes back to the previous window, verified with one retry; if that fails nothing
    /// is executed and the user is told.
    /// </summary>
    TextInput,

    /// <summary>Keyboard and voice mode on the panel; restored like <see cref="TextInput"/>.</summary>
    KeyboardNavigation,

    /// <summary>
    /// Opening and closing the Control Center (CCM-004): on close, back to the app that was in front before it
    /// opened, verified with one retry.
    /// </summary>
    ControlCenter,

    /// <summary>
    /// «Try now» (PRB-004): the target app comes to the front; afterwards back to the Control Center, or
    /// <c>FlashTaskbar</c> on it if Windows refuses (PRB-007).
    /// </summary>
    TryNowTarget,

    /// <summary>
    /// The tray context menu in <c>TrayMenuHost</c>; back to the previous window after <c>TrackPopupMenuEx</c> and
    /// <c>PostMessage(WM_NULL)</c>. Has priority over every other lease.
    /// </summary>
    TrayMenu,
}
