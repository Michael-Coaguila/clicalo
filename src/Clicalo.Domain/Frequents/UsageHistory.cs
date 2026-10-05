using System.Collections.Immutable;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Frequents;

/// <summary>
/// Execution time stamps per shortcut (FRE-002), persisted apart in <c>usage.json</c> (§6.5). Entries of deleted
/// shortcuts dangle on purpose until purged (FRE-005). The dictionary is compared by reference: the document store
/// detects touched slices by reference (§6.4).
/// </summary>
/// <remarks>
/// A mark is recent while it is less than the usage window old (<c>Timings.Frequents.UsageWindow</c>, 30 days): a mark
/// exactly one window old has expired. Marks in the future (the clock went back) count as recent.
/// </remarks>
/// <param name="Entries">Time stamps of each shortcut, oldest first.</param>
public sealed record UsageHistory(
    ImmutableDictionary<ShortcutId, ValueList<DateTimeOffset>> Entries
)
{
    /// <summary>No usage.</summary>
    public static UsageHistory Empty { get; } =
        new(ImmutableDictionary<ShortcutId, ValueList<DateTimeOffset>>.Empty);

    /// <summary>
    /// This history with one more execution of <paramref name="id"/> at <paramref name="at"/>, and every expired mark
    /// purged (FRE-002). Entries of deleted shortcuts are kept: undoing a deletion brings their usage back.
    /// </summary>
    /// <param name="id">The shortcut that ran.</param>
    /// <param name="at">When it ran.</param>
    /// <param name="window">The usage window.</param>
    public UsageHistory Record(ShortcutId id, DateTimeOffset at, TimeSpan window)
    {
        var builder = ImmutableDictionary.CreateBuilder<ShortcutId, ValueList<DateTimeOffset>>();
        foreach (var (key, marks) in Entries)
        {
            var kept = Recent(marks, at, window);
            if (!kept.IsEmpty)
            {
                builder[key] = kept;
            }
        }

        var own = builder.TryGetValue(id, out var existing) ? existing.Items : [];
        var position = own.Length;
        while (position > 0 && own[position - 1] > at)
        {
            position--;
        }

        builder[id] = new ValueList<DateTimeOffset>(own.Insert(position, at));
        return new UsageHistory(builder.ToImmutable());
    }

    /// <summary>
    /// This history without expired marks and without entries of shortcuts that no longer exist (FRE-002); the same
    /// instance when nothing is purged. Persistence calls it on load; commands never purge deleted shortcuts, so their
    /// undo keeps the usage (FRE-005).
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <param name="window">The usage window.</param>
    /// <param name="exists">Whether a shortcut still exists.</param>
    public UsageHistory Purge(DateTimeOffset now, TimeSpan window, Func<ShortcutId, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        var builder = ImmutableDictionary.CreateBuilder<ShortcutId, ValueList<DateTimeOffset>>();
        var changed = false;
        foreach (var (key, marks) in Entries)
        {
            var kept = exists(key) ? Recent(marks, now, window) : [];
            changed |= kept.Count != marks.Count;
            if (!kept.IsEmpty)
            {
                builder[key] = kept;
            }
        }

        return changed ? new UsageHistory(builder.ToImmutable()) : this;
    }

    /// <summary>How many recent executions <paramref name="id"/> has.</summary>
    /// <param name="id">The shortcut.</param>
    /// <param name="now">The current time.</param>
    /// <param name="window">The usage window.</param>
    public int CountRecent(ShortcutId id, DateTimeOffset now, TimeSpan window)
    {
        if (!Entries.TryGetValue(id, out var marks))
        {
            return 0;
        }

        var count = 0;
        foreach (var mark in marks)
        {
            if (IsRecent(mark, now, window))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>The newest execution of <paramref name="id"/>, or <see langword="null"/>.</summary>
    /// <param name="id">The shortcut.</param>
    public DateTimeOffset? LastUse(ShortcutId id) =>
        Entries.TryGetValue(id, out var marks) && !marks.IsEmpty ? marks[^1] : null;

    private static bool IsRecent(DateTimeOffset mark, DateTimeOffset now, TimeSpan window) =>
        now - mark < window;

    private static ValueList<DateTimeOffset> Recent(
        ValueList<DateTimeOffset> marks,
        DateTimeOffset now,
        TimeSpan window
    )
    {
        var first = 0;
        while (first < marks.Count && !IsRecent(marks[first], now, window))
        {
            first++;
        }

        return first == 0 ? marks : new ValueList<DateTimeOffset>(marks.Items[first..]);
    }
}
