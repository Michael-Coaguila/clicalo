namespace Clicalo.Domain.Settings;

/// <summary>
/// Where the Control Center was when it last moved or closed (CCM-001, user decision D9, ADR-0028): its restored
/// rectangle in device-independent pixels (DIP, 1/96 inch) in virtual-screen coordinates, the monitor it was on and
/// whether it was maximized. Placement: never undoable. A value type, so <see cref="UserSettings.ControlCenter"/> is a
/// single setting.
/// </summary>
/// <remarks>
/// The Domain only checks that the values can be used (<see cref="IsUsable"/>); the window applies its own minimum
/// (760 × 520, or the work area when it is smaller) and brings the rectangle back inside the work area of that monitor,
/// or of the panel's monitor when that one is not connected.
/// </remarks>
/// <param name="MonitorId">The stable identifier of the monitor, as in <see cref="MonitorHandlePositions"/>.</param>
/// <param name="X">Left edge, in DIP.</param>
/// <param name="Y">Top edge, in DIP.</param>
/// <param name="Width">Width of the restored window, in DIP.</param>
/// <param name="Height">Height of the restored window, in DIP.</param>
/// <param name="Maximized">Whether it was maximized (the rectangle is then the one it restores to).</param>
public readonly record struct ControlCenterPlacement(
    string MonitorId,
    double X,
    double Y,
    double Width,
    double Height,
    bool Maximized
)
{
    /// <summary>Whether the values can be restored: a monitor, finite coordinates and a positive size.</summary>
    public bool IsUsable =>
        !string.IsNullOrEmpty(MonitorId)
        && double.IsFinite(X)
        && double.IsFinite(Y)
        && double.IsFinite(Width)
        && double.IsFinite(Height)
        && Width > 0
        && Height > 0;
}
