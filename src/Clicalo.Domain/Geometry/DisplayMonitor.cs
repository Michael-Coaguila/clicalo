namespace Clicalo.Domain.Geometry;

/// <summary>
/// One monitor of the virtual desktop as the placement rules need it (blueprint §3.7): an identifier that survives a
/// restart, its bounds and its work area (without the taskbar) in physical pixels, whether it is the primary one and
/// its scale (physical pixels per logical pixel).
/// </summary>
/// <param name="Id">The persistent identifier of the monitor (its device name, <c>\\.\DISPLAY1</c>).</param>
/// <param name="Bounds">The whole monitor, in physical pixels.</param>
/// <param name="WorkArea">The monitor without the taskbar and the docked app bars (<c>rcWork</c>).</param>
/// <param name="IsPrimary">Whether it is the primary monitor.</param>
/// <param name="Scale">Physical pixels per logical pixel (1.0 at 96 DPI).</param>
public sealed record DisplayMonitor(
    string Id,
    PhysicalRect Bounds,
    PhysicalRect WorkArea,
    bool IsPrimary,
    double Scale
)
{
    /// <summary><paramref name="logicalPx"/> logical pixels in this monitor's physical pixels, rounded.</summary>
    /// <param name="logicalPx">A length in logical pixels.</param>
    public int ToPhysical(double logicalPx) =>
        (int)Math.Round(logicalPx * Scale, MidpointRounding.AwayFromZero);
}
