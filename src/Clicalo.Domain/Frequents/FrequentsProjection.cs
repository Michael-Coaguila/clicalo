using System.Collections.Immutable;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Timing;

namespace Clicalo.Domain.Frequents;

/// <summary>
/// The composition of the Frequents view (FRE-001), a pure function of the library, the curation and the usage: it
/// never changes by itself, only when one of them does.
/// </summary>
public static class FrequentsProjection
{
    /// <summary>
    /// The tiles of Frequents, at most <c>Timings.Frequents.MaxShown</c> (9): first the pins that still exist, in pin
    /// order; then the shortcuts neither pinned nor hidden with a use in the usage window, by uses (most first), then
    /// the most recent use, then document order (Always visible, then each profile in order and each list in order).
    /// Shortcuts of Always visible are left out while its row is shown, so none appears twice (FRE-001, PQ-23).
    /// </summary>
    /// <param name="library">The shortcuts.</param>
    /// <param name="state">Pins, hidden and usage.</param>
    /// <param name="now">The current time.</param>
    /// <param name="alwaysVisibleRowShown">Whether the Always visible row is shown (GEN-007).</param>
    public static ImmutableArray<FrequentEntry> Compose(
        ShortcutLibrary library,
        FrequentsState state,
        DateTimeOffset now,
        bool alwaysVisibleRowShown
    )
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(state);
        var window = Timings.Frequents.UsageWindow;
        var max = Timings.Frequents.MaxShown;
        var entries = ImmutableArray.CreateBuilder<FrequentEntry>(max);
        var shown = new HashSet<ShortcutId>();

        foreach (var id in state.Pins)
        {
            if (entries.Count == max)
            {
                return entries.ToImmutable();
            }

            if (
                library.TryLocate(id, out var location)
                && !Excluded(location, alwaysVisibleRowShown)
                && library.TryGetShortcut(id, out var shortcut)
                && shown.Add(id)
            )
            {
                entries.Add(
                    new FrequentEntry(
                        shortcut,
                        location,
                        Pinned: true,
                        state.Usage.CountRecent(id, now, window)
                    )
                );
            }
        }

        var hidden = new HashSet<ShortcutId>(state.Hidden);
        var used = new List<(LocatedShortcut Located, int Uses, DateTimeOffset Last, int Order)>();
        var order = 0;
        foreach (var located in library.EnumerateShortcuts())
        {
            order++;
            var id = located.Shortcut.Id;
            if (
                shown.Contains(id)
                || hidden.Contains(id)
                || Excluded(located.Location, alwaysVisibleRowShown)
            )
            {
                continue;
            }

            var uses = state.Usage.CountRecent(id, now, window);
            if (uses > 0)
            {
                used.Add((located, uses, state.Usage.LastUse(id) ?? default, order));
            }
        }

        used.Sort(
            static (left, right) =>
            {
                var byUses = right.Uses.CompareTo(left.Uses);
                if (byUses != 0)
                {
                    return byUses;
                }

                var byLast = right.Last.CompareTo(left.Last);
                return byLast != 0 ? byLast : left.Order.CompareTo(right.Order);
            }
        );

        foreach (var (located, uses, _, _) in used)
        {
            if (entries.Count == max)
            {
                break;
            }

            entries.Add(new FrequentEntry(located.Shortcut, located.Location, Pinned: false, uses));
        }

        return entries.ToImmutable();
    }

    /// <summary>
    /// Whether pinning one more would pass the limit of tiles (FRE-001: the pin menu warns; the pins beyond the limit
    /// are kept but not shown).
    /// </summary>
    /// <param name="library">The shortcuts.</param>
    /// <param name="state">Pins, hidden and usage.</param>
    public static bool IsPinLimitReached(ShortcutLibrary library, FrequentsState state)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(state);
        return state.Pins.Count(id => library.TryLocate(id, out _)) >= Timings.Frequents.MaxShown;
    }

    private static bool Excluded(ShortcutLocation location, bool alwaysVisibleRowShown) =>
        alwaysVisibleRowShown && location.List is ListRef.AlwaysVisible;
}
