using Clicalo.Domain.Catalog;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;

namespace Clicalo.Domain.PanelLayout;

/// <summary>
/// Where the panel and the bubble go (PAN-002, PAN-006, BUR-001, blueprint §3.7 <c>PanelGeometry.Place</c>), in
/// physical pixels: the position is remembered per monitor, a surface is always whole inside the work area of its
/// monitor with a margin of 8, and a monitor that no longer exists gives way to the primary one. Pure.
/// </summary>
public static class PanelGeometry
{
    /// <summary>The primary monitor, or the first one when none says it is primary.</summary>
    /// <param name="monitors">The monitors of the desktop; at least one.</param>
    /// <exception cref="ArgumentException">There is no monitor.</exception>
    public static DisplayMonitor Primary(IReadOnlyList<DisplayMonitor> monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        if (monitors.Count == 0)
        {
            throw new ArgumentException("The desktop has at least one monitor.", nameof(monitors));
        }

        foreach (var monitor in monitors)
        {
            if (monitor.IsPrimary)
            {
                return monitor;
            }
        }

        return monitors[0];
    }

    /// <summary>The monitor called <paramref name="id"/>, or <see langword="null"/> when it is not connected.</summary>
    /// <param name="monitors">The monitors of the desktop.</param>
    /// <param name="id">The persistent identifier.</param>
    public static DisplayMonitor? Find(IReadOnlyList<DisplayMonitor> monitors, string? id)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        foreach (var monitor in monitors)
        {
            if (string.Equals(monitor.Id, id, StringComparison.Ordinal))
            {
                return monitor;
            }
        }

        return null;
    }

    /// <summary>
    /// The monitor a surface is on: the one that holds its center, else the one it overlaps most, else the primary
    /// one (PAN-006).
    /// </summary>
    /// <param name="bounds">The surface.</param>
    /// <param name="monitors">The monitors of the desktop; at least one.</param>
    public static DisplayMonitor MonitorOf(
        PhysicalRect bounds,
        IReadOnlyList<DisplayMonitor> monitors
    )
    {
        ArgumentNullException.ThrowIfNull(monitors);
        var primary = Primary(monitors);
        foreach (var monitor in monitors)
        {
            if (monitor.Bounds.Contains(bounds.Center))
            {
                return monitor;
            }
        }

        DisplayMonitor? best = null;
        long bestArea = 0;
        foreach (var monitor in monitors)
        {
            var area = OverlapArea(bounds, monitor.Bounds);
            if (area > bestArea)
            {
                best = monitor;
                bestArea = area;
            }
        }

        return best ?? primary;
    }

    /// <summary>
    /// <paramref name="bounds"/> moved, never resized, so that it lies whole inside the work area of
    /// <paramref name="monitor"/> with the margin of <c>sizes.json</c> (8 logical pixels) on every side (PAN-002). A
    /// surface larger than the work area keeps its top-left corner at the margin.
    /// </summary>
    /// <param name="bounds">The surface, in physical pixels.</param>
    /// <param name="monitor">Its monitor.</param>
    public static PhysicalRect Clamp(PhysicalRect bounds, DisplayMonitor monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        var margin = monitor.ToPhysical(PanelSizes.Layout.PanelWorkAreaMarginPx);
        return ClampInto(bounds, monitor.WorkArea, margin);
    }

    /// <summary>
    /// <paramref name="bounds"/> moved, never resized, inside <paramref name="area"/> less <paramref name="margin"/>
    /// on every side; a surface larger than that keeps its top-left corner at the margin.
    /// </summary>
    /// <param name="bounds">The surface.</param>
    /// <param name="area">The area it must stay in.</param>
    /// <param name="margin">The free space kept on every side.</param>
    public static PhysicalRect ClampInto(PhysicalRect bounds, PhysicalRect area, int margin)
    {
        var left = Math.Max(
            area.Left + margin,
            Math.Min(bounds.Left, area.Right - margin - bounds.Width)
        );
        var top = Math.Max(
            area.Top + margin,
            Math.Min(bounds.Top, area.Bottom - margin - bounds.Height)
        );
        return bounds with { Left = left, Top = top };
    }

    /// <summary>
    /// BUS-002, EC-BUS-01: the touch keyboard must not cover the panel. A panel that <paramref name="occluded"/> covers
    /// goes right above it; when it does not fit there, right below it; and when it fits nowhere, to the top of the work
    /// area, where its header and its search field still show. A panel the keyboard does not touch stays where it is.
    /// </summary>
    /// <param name="bounds">The panel, in physical pixels, already inside the work area.</param>
    /// <param name="occluded">What the touch keyboard covers, in physical pixels; empty when it is hidden.</param>
    /// <param name="monitor">The panel's monitor.</param>
    public static PhysicalRect Avoid(
        PhysicalRect bounds,
        PhysicalRect occluded,
        DisplayMonitor monitor
    )
    {
        ArgumentNullException.ThrowIfNull(monitor);
        if (bounds.IsEmpty || OverlapArea(bounds, occluded) == 0)
        {
            return bounds;
        }

        var margin = monitor.ToPhysical(PanelSizes.Layout.PanelWorkAreaMarginPx);
        var work = monitor.WorkArea;
        var top = work.Top + margin;
        var above = occluded.Top - margin - bounds.Height;
        if (above >= top)
        {
            return bounds with { Top = above };
        }

        var below = occluded.Bottom + margin;
        return below + bounds.Height <= work.Bottom - margin
            ? bounds with
            {
                Top = below,
            }
            : bounds with
            {
                Top = top,
            };
    }

    /// <summary>
    /// The first position of the panel on <paramref name="monitor"/> (PAN-002): top right, with x = right edge of the
    /// work area − 72 − width and y = top + 40, then kept inside the work area.
    /// </summary>
    /// <param name="width">Width of the surface, in physical pixels.</param>
    /// <param name="height">Height of the surface, in physical pixels.</param>
    /// <param name="monitor">The monitor.</param>
    public static PhysicalRect Initial(int width, int height, DisplayMonitor monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        var layout = PanelSizes.Layout;
        var work = monitor.WorkArea;
        var left = work.Right - monitor.ToPhysical(layout.PanelInitialRightOffsetPx) - width;
        var top = work.Top + monitor.ToPhysical(layout.PanelInitialTopPx);
        return Clamp(new PhysicalRect(left, top, width, height), monitor);
    }

    /// <summary>
    /// Where a surface of <paramref name="width"/> × <paramref name="height"/> goes (PAN-006): on the monitor
    /// <paramref name="monitorId"/> if it is still connected, otherwise on the primary one; at the position saved for
    /// that monitor, or at the initial position when it has none; always whole inside its work area.
    /// </summary>
    /// <param name="width">Width of the surface, in physical pixels.</param>
    /// <param name="height">Height of the surface, in physical pixels.</param>
    /// <param name="saved">The positions saved per monitor (docs/02 <c>panelPosByMonitor</c>).</param>
    /// <param name="monitorId">The monitor the surface was on, or <see langword="null"/> at start.</param>
    /// <param name="monitors">The monitors of the desktop; at least one.</param>
    public static PanelPlacement Place(
        int width,
        int height,
        ValueList<MonitorPosition> saved,
        string? monitorId,
        IReadOnlyList<DisplayMonitor> monitors
    )
    {
        ArgumentNullException.ThrowIfNull(monitors);
        var monitor = Find(monitors, monitorId) ?? StartMonitor(saved, monitors);
        var position = PositionOn(saved, monitor.Id);
        var bounds = position is null
            ? Initial(width, height, monitor)
            : Clamp(new PhysicalRect(position.X, position.Y, width, height), monitor);
        return new PanelPlacement(monitor, bounds);
    }

    /// <summary>The position saved for <paramref name="monitorId"/>, or <see langword="null"/>.</summary>
    /// <param name="saved">The positions saved per monitor.</param>
    /// <param name="monitorId">The monitor.</param>
    public static MonitorPosition? PositionOn(ValueList<MonitorPosition> saved, string monitorId)
    {
        foreach (var position in saved.Items)
        {
            if (string.Equals(position.MonitorId, monitorId, StringComparison.Ordinal))
            {
                return position;
            }
        }

        return null;
    }

    /// <summary>
    /// The monitor the panel opens on at start: the primary one when it has a saved position, else the first connected
    /// monitor with one, else the primary one.
    /// </summary>
    private static DisplayMonitor StartMonitor(
        ValueList<MonitorPosition> saved,
        IReadOnlyList<DisplayMonitor> monitors
    )
    {
        var primary = Primary(monitors);
        if (PositionOn(saved, primary.Id) is not null)
        {
            return primary;
        }

        foreach (var position in saved.Items)
        {
            if (Find(monitors, position.MonitorId) is { } monitor)
            {
                return monitor;
            }
        }

        return primary;
    }

    private static long OverlapArea(PhysicalRect a, PhysicalRect b)
    {
        long width = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
        long height = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top);
        return width > 0 && height > 0 ? width * height : 0;
    }
}
