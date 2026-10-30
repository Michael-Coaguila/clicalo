using Clicalo.Domain.Geometry;
using Clicalo.Domain.Settings;

namespace Clicalo.Presentation.ControlCenter;

/// <summary>
/// Where the Control Center opens (CCM-001, CCM-004, D9): on the monitor it was left on (or the panel's, or the
/// primary one), with the size it was left with (1120 × 680 by default, never under 760 × 520 or the work area when
/// that is smaller), and never over the panel: when they would overlap, it moves to the side of the panel, on either
/// axis, that keeps most of its size and moves it least, leaving a gap. Pure: everything is in physical pixels of the
/// virtual desktop, converted with the scale of the monitor.
/// </summary>
public static class ControlCenterPlacer
{
    /// <summary>The default width, in logical pixels (CCM-001).</summary>
    public const double DefaultWidth = 1120;

    /// <summary>The default height, in logical pixels (CCM-001).</summary>
    public const double DefaultHeight = 680;

    /// <summary>The least width, in logical pixels (CCM-001).</summary>
    public const double LeastWidth = 760;

    /// <summary>The least height, in logical pixels (CCM-001).</summary>
    public const double LeastHeight = 520;

    /// <summary>The gap left between the panel and the Control Center, in logical pixels (CCM-004).</summary>
    public const double Gap = 12;

    /// <summary>Plans where the window opens.</summary>
    /// <param name="monitors">The monitors connected now; never empty.</param>
    /// <param name="panel">The panel on screen, in physical pixels; null or empty when it is not showing.</param>
    /// <param name="remembered">Where it was left the last time (D9); null the first time.</param>
    public static ControlCenterSpot Plan(
        IReadOnlyList<DisplayMonitor> monitors,
        PhysicalRect? panel,
        ControlCenterPlacement? remembered
    )
    {
        ArgumentNullException.ThrowIfNull(monitors);
        if (monitors.Count == 0)
        {
            throw new ArgumentException("At least one monitor is needed.", nameof(monitors));
        }

        var avoid = panel is { IsEmpty: false } shown ? shown : (PhysicalRect?)null;
        var saved = remembered is { IsUsable: true } usable
            ? usable
            : (ControlCenterPlacement?)null;
        var home = saved is { } placement
            ? monitors.FirstOrDefault(m =>
                string.Equals(m.Id, placement.MonitorId, StringComparison.Ordinal)
            )
            : null;
        var monitor =
            home
            ?? (
                avoid is { } rect
                    ? monitors.FirstOrDefault(m => m.Bounds.Contains(rect.Center))
                    : null
            )
            ?? monitors.FirstOrDefault(static m => m.IsPrimary)
            ?? monitors[0];
        var work = monitor.WorkArea.IsEmpty ? monitor.Bounds : monitor.WorkArea;
        var least = (
            Width: Math.Min(monitor.ToPhysical(LeastWidth), work.Width),
            Height: Math.Min(monitor.ToPhysical(LeastHeight), work.Height)
        );
        var width = Math.Clamp(
            monitor.ToPhysical(home is null ? DefaultWidth : saved!.Value.Width),
            least.Width,
            work.Width
        );
        var height = Math.Clamp(
            monitor.ToPhysical(home is null ? DefaultHeight : saved!.Value.Height),
            least.Height,
            work.Height
        );
        var left = home is null
            ? work.Left + ((work.Width - width) / 2)
            : monitor.ToPhysical(saved!.Value.X);
        var top = home is null
            ? work.Top + ((work.Height - height) / 2)
            : monitor.ToPhysical(saved!.Value.Y);
        var bounds = Into(work, new PhysicalRect(left, top, width, height));
        if (avoid is { } taken)
        {
            bounds = Dodge(bounds, work, taken, monitor.ToPhysical(Gap), least);
        }

        return new ControlCenterSpot(monitor, bounds, home is not null && saved!.Value.Maximized);
    }

    /// <summary>What to remember of a window left at <paramref name="bounds"/> (D9).</summary>
    /// <param name="monitors">The monitors connected now; never empty.</param>
    /// <param name="bounds">The restored rectangle of the window, in physical pixels.</param>
    /// <param name="maximized">Whether it was maximized.</param>
    public static ControlCenterPlacement Remember(
        IReadOnlyList<DisplayMonitor> monitors,
        PhysicalRect bounds,
        bool maximized
    )
    {
        ArgumentNullException.ThrowIfNull(monitors);
        var monitor =
            monitors.FirstOrDefault(m => m.Bounds.Contains(bounds.Center))
            ?? monitors.FirstOrDefault(static m => m.IsPrimary)
            ?? monitors[0];
        var scale = monitor.Scale > 0 ? monitor.Scale : 1;
        return new ControlCenterPlacement(
            monitor.Id,
            Math.Round(bounds.Left / scale),
            Math.Round(bounds.Top / scale),
            Math.Round(bounds.Width / scale),
            Math.Round(bounds.Height / scale),
            maximized
        );
    }

    /// <summary>
    /// A rectangle a WPF window reports (left, top, width and height in the units of the monitor it is on) in physical
    /// pixels: the scale is the one of the monitor that holds its center once converted. Null for an empty rectangle.
    /// </summary>
    /// <param name="monitors">The monitors connected now; never empty.</param>
    /// <param name="left">The left edge the window reports.</param>
    /// <param name="top">The top edge the window reports.</param>
    /// <param name="width">The width the window reports.</param>
    /// <param name="height">The height the window reports.</param>
    public static PhysicalRect? FromWindowUnits(
        IReadOnlyList<DisplayMonitor> monitors,
        double left,
        double top,
        double width,
        double height
    )
    {
        ArgumentNullException.ThrowIfNull(monitors);
        if (
            monitors.Count == 0
            || !double.IsFinite(left)
            || !double.IsFinite(top)
            || !double.IsFinite(width)
            || !double.IsFinite(height)
            || width <= 0
            || height <= 0
        )
        {
            return null;
        }

        foreach (var monitor in monitors)
        {
            var rect = Scaled(monitor);
            if (monitor.Bounds.Contains(rect.Center))
            {
                return rect;
            }
        }

        return Scaled(monitors.FirstOrDefault(static m => m.IsPrimary) ?? monitors[0]);

        PhysicalRect Scaled(DisplayMonitor monitor) =>
            new(
                monitor.ToPhysical(left),
                monitor.ToPhysical(top),
                monitor.ToPhysical(width),
                monitor.ToPhysical(height)
            );
    }

    private static PhysicalRect Into(PhysicalRect area, PhysicalRect rect)
    {
        var width = Math.Min(rect.Width, area.Width);
        var height = Math.Min(rect.Height, area.Height);
        return new PhysicalRect(
            Math.Clamp(rect.Left, area.Left, area.Right - width),
            Math.Clamp(rect.Top, area.Top, area.Bottom - height),
            width,
            height
        );
    }

    private static bool Overlap(PhysicalRect a, PhysicalRect b) =>
        a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;

    private static long OverlapArea(PhysicalRect a, PhysicalRect b)
    {
        var width = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
        var height = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top);
        return width <= 0 || height <= 0 ? 0 : (long)width * height;
    }

    private static PhysicalRect Dodge(
        PhysicalRect wanted,
        PhysicalRect work,
        PhysicalRect panel,
        int gap,
        (int Width, int Height) least
    )
    {
        var keepOut = panel.Inflate(gap);
        if (!Overlap(wanted, keepOut))
        {
            return wanted;
        }

        // The four strips of the work area beside the panel: to its right, to its left, under it and over it.
        PhysicalRect[] strips =
        [
            PhysicalRect.FromEdges(keepOut.Right, work.Top, work.Right, work.Bottom),
            PhysicalRect.FromEdges(work.Left, work.Top, keepOut.Left, work.Bottom),
            PhysicalRect.FromEdges(work.Left, keepOut.Bottom, work.Right, work.Bottom),
            PhysicalRect.FromEdges(work.Left, work.Top, work.Right, keepOut.Top),
        ];
        PhysicalRect? best = null;
        foreach (var strip in strips)
        {
            if (strip.Width < least.Width || strip.Height < least.Height)
            {
                continue;
            }

            var candidate = Into(strip, wanted);
            if (best is not { } chosen || Better(candidate, chosen, wanted))
            {
                best = candidate;
            }
        }

        if (best is { } free)
        {
            return free;
        }

        // No side has room for the least size: the place that covers least of the panel, moving as little as it can.
        PhysicalRect[] pushed =
        [
            Into(work, wanted with { Left = keepOut.Right }),
            Into(work, wanted with { Left = keepOut.Left - wanted.Width }),
            Into(work, wanted with { Top = keepOut.Bottom }),
            Into(work, wanted with { Top = keepOut.Top - wanted.Height }),
        ];
        var least0 = pushed[0];
        foreach (var candidate in pushed.AsSpan(1))
        {
            var (a, b) = (OverlapArea(candidate, panel), OverlapArea(least0, panel));
            if (a < b || (a == b && Moved(candidate, wanted) < Moved(least0, wanted)))
            {
                least0 = candidate;
            }
        }

        return least0;
    }

    private static bool Better(PhysicalRect candidate, PhysicalRect chosen, PhysicalRect wanted)
    {
        var (a, b) = ((long)candidate.Width * candidate.Height, (long)chosen.Width * chosen.Height);
        return a > b || (a == b && Moved(candidate, wanted) < Moved(chosen, wanted));
    }

    private static long Moved(PhysicalRect to, PhysicalRect from) =>
        Math.Abs((long)to.Left - from.Left) + Math.Abs((long)to.Top - from.Top);
}
