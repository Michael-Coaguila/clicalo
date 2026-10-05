namespace Clicalo.Domain.Settings;

/// <summary>Where the panel was on a monitor (docs/02 <c>panelPosByMonitor</c>), in physical pixels.</summary>
/// <param name="MonitorId">The monitor device name (<c>\\.\DISPLAY1</c>).</param>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
public sealed record MonitorPosition(string MonitorId, int X, int Y);
