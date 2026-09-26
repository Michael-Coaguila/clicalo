using System.Collections.Immutable;
using System.Windows;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;

namespace Clicalo.Tools.SpikeLab.Surfaces;

/// <summary>
/// Where every laboratory surface starts, from its measured size and the work area (both in logical pixels), and how
/// a surface is kept inside the work area (in physical pixels). The top-left quarter of the work area is the
/// <see cref="TargetZone"/>: the scripts ask the maintainer to snap the app under test there, so no surface starts on
/// it. The guide strip goes bottom left, the panel bottom right with the bubble and the profile side window above it,
/// and the edge bar top right with its side window and the search (S4) to its left.
/// </summary>
internal static class LabLayout
{
    /// <summary>Space between surfaces and from the edges of the work area, in logical pixels.</summary>
    public const double Gap = 16;

    /// <summary>Space between the guide strip and the bottom of the work area, in logical pixels.</summary>
    public const double StripBottomGap = 8;

    /// <summary>Widest guide strip, in logical pixels: its buttons fit in one row.</summary>
    public const double StripPreferredWidth = 760;

    /// <summary>Narrowest guide strip, in logical pixels.</summary>
    public const double StripMinimumWidth = 480;

    /// <summary>The quarter of the work area where the app under test goes (top left).</summary>
    public static Rect TargetZone(Rect area) =>
        new(area.Left, area.Top, area.Width / 2, area.Height / 2);

    /// <summary>
    /// The width of the guide strip: what is left at the bottom next to the panel, between
    /// <see cref="StripMinimumWidth"/> and <see cref="StripPreferredWidth"/>, never wider than the work area.
    /// </summary>
    public static double StripWidth(Rect area, Size panel) =>
        Math.Min(
            Math.Clamp(
                area.Width - panel.Width - (3 * Gap),
                StripMinimumWidth,
                StripPreferredWidth
            ),
            Math.Max(0, area.Width - (2 * Gap))
        );

    /// <summary>
    /// The starting rectangle of every surface in <paramref name="sizes"/> (logical pixels), each one inside
    /// <paramref name="area"/>.
    /// </summary>
    public static ImmutableDictionary<SurfaceId, Rect> Arrange(
        Rect area,
        IReadOnlyDictionary<SurfaceId, Size> sizes
    )
    {
        ArgumentNullException.ThrowIfNull(sizes);
        Size Of(SurfaceId id) => sizes.TryGetValue(id, out var size) ? size : default;

        var panelSize = Of(LabSurfaceIds.Panel);
        var panel = At(
            area.Right - Gap - panelSize.Width,
            area.Bottom - Gap - panelSize.Height,
            panelSize
        );
        var bubbleSize = Of(LabSurfaceIds.Bubble);
        var bubble = At(panel.Left, panel.Top - Gap - bubbleSize.Height, bubbleSize);
        var profilesSize = Of(LabSurfaceIds.Profiles);
        var profiles = At(
            area.Right - Gap - profilesSize.Width,
            panel.Top - Gap - profilesSize.Height,
            profilesSize
        );

        var dockSize = Of(LabSurfaceIds.Dock);
        var dock = At(area.Right - dockSize.Width, area.Top + Gap, dockSize);
        var sideSize = Of(LabSurfaceIds.DockSide);
        var side = At(dock.Left - (Gap / 2) - sideSize.Width, dock.Top, sideSize);

        // The search (S4) and the side window of the edge bar (S1) are never open in the same script.
        var searchSize = Of(LabSurfaceIds.Search);
        var search = At(dock.Left - Gap - searchSize.Width, area.Top + Gap, searchSize);

        var guideSize = Of(LabSurfaceIds.Guide);
        var guide = At(area.Left + Gap, area.Bottom - StripBottomGap - guideSize.Height, guideSize);

        return new Dictionary<SurfaceId, Rect>
        {
            [LabSurfaceIds.Panel] = Inside(panel, area),
            [LabSurfaceIds.Bubble] = Inside(bubble, area),
            [LabSurfaceIds.Profiles] = Inside(profiles, area),
            [LabSurfaceIds.Dock] = Inside(dock, area),
            [LabSurfaceIds.DockSide] = Inside(side, area),
            [LabSurfaceIds.Search] = Inside(search, area),
            [LabSurfaceIds.Guide] = Inside(guide, area),
        }.ToImmutableDictionary();
    }

    /// <summary>
    /// <paramref name="rect"/> moved (never resized) so it lies inside <paramref name="area"/>; when it is larger
    /// than the area, its top-left corner goes to the area's.
    /// </summary>
    public static Rect Inside(Rect rect, Rect area) =>
        new(
            Fit(rect.Left, rect.Width, area.Left, area.Right),
            Fit(rect.Top, rect.Height, area.Top, area.Bottom),
            rect.Width,
            rect.Height
        );

    /// <summary>The same as <see cref="Inside(Rect, Rect)"/>, in physical pixels.</summary>
    public static PhysicalRect Inside(PhysicalRect rect, PhysicalRect area) =>
        rect with
        {
            Left = (int)Fit(rect.Left, rect.Width, area.Left, area.Right),
            Top = (int)Fit(rect.Top, rect.Height, area.Top, area.Bottom),
        };

    /// <summary>
    /// Where a surface of <paramref name="bounds"/> (physical pixels, with its current size) goes to stay inside
    /// <paramref name="area"/>: with <paramref name="bottom"/>, its bottom edge goes there first (the guide strip keeps
    /// its bottom edge while it grows or folds). Never resized.
    /// </summary>
    public static PhysicalRect KeepInside(PhysicalRect bounds, PhysicalRect area, int? bottom) =>
        Inside(bottom is { } edge ? bounds with { Top = edge - bounds.Height } : bounds, area);

    /// <summary>
    /// The tallest guide strip, in logical pixels: the work area (<paramref name="areaHeight"/>, logical pixels) less
    /// <see cref="StripBottomGap"/> above and below, so its buttons never leave it.
    /// </summary>
    public static double StripMaximumHeight(double areaHeight) =>
        Math.Max(0, areaHeight - (2 * StripBottomGap));

    /// <summary>True when the center of <paramref name="rect"/> is in the lower half of <paramref name="area"/>.</summary>
    public static bool IsInLowerHalf(PhysicalRect rect, PhysicalRect area) =>
        rect.Center.Y >= area.Center.Y;

    private static Rect At(double left, double top, Size size) =>
        new(left, top, size.Width, size.Height);

    private static double Fit(double start, double length, double min, double max) =>
        length >= max - min ? min : Math.Clamp(start, min, max - length);
}
