using Clicalo.Domain.Geometry;

namespace Clicalo.Presentation.ControlCenter;

/// <summary>Where the Control Center opens (CCM-001, CCM-004).</summary>
/// <param name="Monitor">The monitor it opens on.</param>
/// <param name="Bounds">Its restored rectangle, in physical pixels of the virtual desktop.</param>
/// <param name="Maximized">Whether it opens maximized, as it was left.</param>
public sealed record ControlCenterSpot(DisplayMonitor Monitor, PhysicalRect Bounds, bool Maximized);
