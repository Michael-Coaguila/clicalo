using System.Collections.Immutable;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Settings;

/// <summary>
/// The handle position per monitor and side (PES-016, user decision D9, ADR-0028):
/// <see cref="UserSettings.HandlePositionsByMonitor"/> holds at most one entry per monitor and side. A monitor without
/// an entry, or one that is not recognized, uses the position per side of <see cref="DockSettings.HandlePositions"/>,
/// which a 1.0 document already has.
/// </summary>
/// <remarks>
/// The monitor identifier is whatever the platform gives as a stable identity of the physical monitor: the monitor
/// device path of DisplayConfig (<c>DISPLAYCONFIG_TARGET_DEVICE_NAME.monitorDevicePath</c>), which survives a restart
/// and a change of the display order, unlike the GDI device name (<c>\\.\DISPLAY1</c>). The Domain only compares it
/// ordinally.
/// </remarks>
public static class MonitorHandlePositions
{
    /// <summary>
    /// The handle position on <paramref name="side"/> of <paramref name="monitorId"/>: the remembered one, or the
    /// position per side when that monitor has none (or <paramref name="monitorId"/> is null or empty).
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="monitorId">The monitor of the panel, or <see langword="null"/> when it is not known.</param>
    /// <param name="side">The edge.</param>
    public static int PositionFor(UserSettings settings, string? monitorId, DockSide side)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!string.IsNullOrEmpty(monitorId))
        {
            foreach (var entry in settings.HandlePositionsByMonitor)
            {
                if (
                    entry.Side == side
                    && string.Equals(entry.MonitorId, monitorId, StringComparison.Ordinal)
                )
                {
                    return entry.Position;
                }
            }
        }

        var bySide = settings.Dock.HandlePositions;
        return side switch
        {
            DockSide.Left => bySide.Left,
            DockSide.Top => bySide.Top,
            DockSide.Bottom => bySide.Bottom,
            _ => bySide.Right,
        };
    }

    /// <summary>
    /// <paramref name="positions"/> with the entry of <paramref name="monitorId"/> and <paramref name="side"/> set to
    /// <paramref name="position"/> (replaced in place, or added at the end); the same list when nothing changes. The
    /// position is not clamped here: <see cref="SettingsSchema.Write"/> rejects one outside its range.
    /// </summary>
    /// <param name="positions">The current entries.</param>
    /// <param name="monitorId">A non-empty monitor identifier.</param>
    /// <param name="side">The edge.</param>
    /// <param name="position">The new position, in percent.</param>
    public static ValueList<MonitorHandlePosition> With(
        ValueList<MonitorHandlePosition> positions,
        string monitorId,
        DockSide side,
        int position
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(monitorId);
        var entry = new MonitorHandlePosition(monitorId, side, position);
        var items = positions.Items;
        for (var i = 0; i < items.Length; i++)
        {
            var current = items[i];
            if (
                current.Side == side
                && string.Equals(current.MonitorId, monitorId, StringComparison.Ordinal)
            )
            {
                return current == entry ? positions : new(items.SetItem(i, entry));
            }
        }

        return new(items.Add(entry));
    }

    /// <summary>
    /// The entries without empty monitors, undefined sides or repeats (the first one wins), with each position clamped
    /// inside <see cref="SettingsSchema.DockHandlePosition"/>; the same list when all are valid.
    /// </summary>
    /// <param name="positions">Entries read from disk or about to be written.</param>
    internal static ValueList<MonitorHandlePosition> Repair(
        ValueList<MonitorHandlePosition> positions
    )
    {
        var seen = new HashSet<(string, DockSide)>();
        var kept = ImmutableArray.CreateBuilder<MonitorHandlePosition>();
        var changed = false;
        foreach (var entry in positions)
        {
            if (
                entry is not { MonitorId.Length: > 0 }
                || !Enum.IsDefined(entry.Side)
                || !seen.Add((entry.MonitorId, entry.Side))
            )
            {
                changed = true;
                continue;
            }

            var clamped = (int)SettingsSchema.DockHandlePosition.Clamp(entry.Position);
            changed |= clamped != entry.Position;
            kept.Add(clamped == entry.Position ? entry : entry with { Position = clamped });
        }

        return changed ? new ValueList<MonitorHandlePosition>(kept.ToImmutable()) : positions;
    }
}
