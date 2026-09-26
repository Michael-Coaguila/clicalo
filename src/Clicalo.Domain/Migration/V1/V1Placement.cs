using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;

namespace Clicalo.Domain.Migration.V1;

/// <summary>
/// Places the v1 window on this machine (catalog §7.4): <c>window_pos</c> is in Qt logical pixels, where each screen
/// keeps its physical top-left corner and its size is divided by its scale. The point goes to the monitor that
/// contains it, converted to physical pixels; outside every monitor (x = 1963 with 1200 logical pixels) it moves to
/// the primary monitor at the v1 default offset.
/// </summary>
/// <remarks>
/// v1 ran on PyQt5 with <c>AA_EnableHighDpiScaling</c> and the Qt 5 default rounding policy (<c>Round</c>), so the
/// scale Qt used is the Windows scale rounded to a whole number, never below 1: 175 % and 150 % were 2, 125 % was 1.
/// The real files agree: at 175 % on a 2400 × 1600 screen v1 saved an edit height of 742, which is Qt's 800 logical
/// pixels minus the taskbar (42) and its 8-pixel margins, not the 914 of an unrounded 1.75.
/// </remarks>
internal static class V1Placement
{
    /// <summary>The v1 default <c>window_pos</c> (catalog §7.2).</summary>
    public static V1Pair DefaultPosition { get; } = new(80, 80);

    /// <summary>The panel position for <paramref name="position"/>.</summary>
    /// <param name="position">The v1 window position.</param>
    /// <param name="monitors">The monitors of this machine.</param>
    /// <returns>
    /// The position, or <see langword="null"/> without monitors (the panel keeps its default placement), and whether
    /// it had to move to the primary monitor.
    /// </returns>
    public static (MonitorPosition? Position, bool Moved) ToPanelPosition(
        V1Pair position,
        ValueList<V1Monitor> monitors
    )
    {
        if (monitors.IsEmpty)
        {
            return (null, false);
        }

        foreach (var monitor in monitors)
        {
            if (Contains(monitor, position))
            {
                return (Map(monitor, position), false);
            }
        }

        var primary = monitors.FirstOrDefault(static m => m.IsPrimary) ?? monitors[0];
        var fallback = new V1Pair(
            primary.Left + DefaultPosition.First,
            primary.Top + DefaultPosition.Second
        );
        return (Map(primary, fallback), true);
    }

    private static bool Contains(V1Monitor monitor, V1Pair point)
    {
        var scale = Scale(monitor);
        return point.First >= monitor.Left
            && point.Second >= monitor.Top
            && point.First < monitor.Left + (monitor.Width / scale)
            && point.Second < monitor.Top + (monitor.Height / scale);
    }

    private static MonitorPosition Map(V1Monitor monitor, V1Pair point)
    {
        var scale = Scale(monitor);
        var x =
            monitor.Left
            + (int)Math.Round((point.First - monitor.Left) * scale, MidpointRounding.AwayFromZero);
        var y =
            monitor.Top
            + (int)Math.Round((point.Second - monitor.Top) * scale, MidpointRounding.AwayFromZero);
        return new MonitorPosition(monitor.Id, x, y);
    }

    /// <summary>The scale Qt 5 used on <paramref name="monitor"/>: its Windows scale rounded (qRound), at least 1.</summary>
    private static double Scale(V1Monitor monitor) =>
        double.IsFinite(monitor.Scale) && monitor.Scale > 0
            ? Math.Max(1, Math.Round(monitor.Scale, MidpointRounding.AwayFromZero))
            : 1;
}
