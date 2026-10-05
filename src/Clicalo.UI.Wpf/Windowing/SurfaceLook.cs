using System.Windows;
using Clicalo.Domain.Settings;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Windowing;

/// <summary>
/// The shape and the shadow of a surface (PAN-003, docs/07 «Formas», spike S6): rounded corners drawn by WPF on a
/// per-pixel transparent window, so they look the same on Windows 10 and 11, and a precomputed shadow in a separate
/// window that never takes a touch.
/// </summary>
/// <param name="Corners">Corner radii in device-independent pixels; ignored when <paramref name="Round"/> is true.</param>
/// <param name="Shadow">The elevation shadow, or null for none.</param>
/// <param name="Round">A circle (or a pill): each radius is half the shorter side.</param>
public sealed record SurfaceLook(CornerRadius Corners, ShadowSpec? Shadow, bool Round = false)
{
    /// <summary>The panel and the edge bar: radius 18, shadow 0 18 50 at 45 % (PAN-003).</summary>
    public static SurfaceLook Panel { get; } = new(new CornerRadius(Radii.Panel), Shadows.Panel);

    /// <summary>The 64 px bubble: a circle with a shadow 0 10 30 at 40 % (BUR-001).</summary>
    public static SurfaceLook Bubble { get; } = new(default, Shadows.Bubble, Round: true);

    /// <summary>Side windows, menus and drop-downs: radius 14, shadow 0 14 40 at 45 %.</summary>
    public static SurfaceLook SideWindow { get; } =
        new(new CornerRadius(Radii.LargeCard), Shadows.Menu);

    /// <summary>
    /// The handle of the edge bar: radius 12 on the side away from the screen edge, shadow 0 6 20 at 35 %.
    /// </summary>
    /// <param name="side">The screen edge the bar is on.</param>
    public static SurfaceLook DockHandle(DockSide side) =>
        new(
            side switch
            {
                DockSide.Left => new CornerRadius(0, Radii.Tile, Radii.Tile, 0),
                DockSide.Top => new CornerRadius(0, 0, Radii.Tile, Radii.Tile),
                DockSide.Bottom => new CornerRadius(Radii.Tile, Radii.Tile, 0, 0),
                _ => new CornerRadius(Radii.Tile, 0, 0, Radii.Tile),
            },
            Shadows.Handle
        );
}
