using Clicalo.Domain.Geometry;

namespace Clicalo.Domain.PanelLayout;

/// <summary>Where <see cref="PanelGeometry.Place"/> puts a surface: its monitor and its bounds.</summary>
/// <param name="Monitor">The monitor the surface is on.</param>
/// <param name="Bounds">The surface, whole inside the work area of <paramref name="Monitor"/>.</param>
public sealed record PanelPlacement(DisplayMonitor Monitor, PhysicalRect Bounds);
