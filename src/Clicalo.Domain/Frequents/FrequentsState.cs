using System.Collections.Immutable;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Frequents;

/// <summary>
/// The Frequents view state (FRE-*, blueprint §6.3): the curation (pins and hidden) and the usage, which are two
/// separate undo slices. <see cref="UsageEpoch"/> goes up on «Reset Frequents»; a <c>usage.json</c> with another epoch
/// is discarded on load (§6.5).
/// </summary>
/// <param name="Pins">Pinned shortcuts in pin order (FRE-001); ids of deleted shortcuts dangle on purpose (FRE-005).</param>
/// <param name="Hidden">Hidden shortcuts, sorted by id, without repetitions.</param>
/// <param name="UsageEpoch">Raised by «Reset Frequents».</param>
/// <param name="Usage">Execution time stamps.</param>
public sealed record FrequentsState(
    ValueList<ShortcutId> Pins,
    ValueList<ShortcutId> Hidden,
    long UsageEpoch,
    UsageHistory Usage
)
{
    private static readonly Comparer<ShortcutId> ById = Comparer<ShortcutId>.Create(
        static (left, right) => string.CompareOrdinal(left.Value, right.Value)
    );

    /// <summary>A new document's Frequents: nothing pinned, hidden or used, epoch 0.</summary>
    public static FrequentsState Empty { get; } = new([], [], 0, UsageHistory.Empty);

    /// <summary>
    /// Whether the state is well formed (a <c>UserDocument</c> invariant): no pin twice, hidden ids sorted without
    /// repetitions, a usage history and a usage epoch that is not negative.
    /// </summary>
    public bool IsWellFormed
    {
        get
        {
            if (Usage is null || UsageEpoch < 0 || Pins.Distinct().Count() != Pins.Count)
            {
                return false;
            }

            for (var i = 1; i < Hidden.Count; i++)
            {
                if (ById.Compare(Hidden[i - 1], Hidden[i]) >= 0)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Pinned at the end of the pins and no longer hidden (FRE-001); the same instance if nothing changes.</summary>
    /// <param name="id">The shortcut.</param>
    public FrequentsState WithPin(ShortcutId id)
    {
        var unhidden = WithoutHidden(id);
        return unhidden.Pins.Contains(id)
            ? unhidden
            : unhidden with
            {
                Pins = new(unhidden.Pins.Items.Add(id)),
            };
    }

    /// <summary>No longer pinned; the same instance if it was not.</summary>
    /// <param name="id">The shortcut.</param>
    public FrequentsState WithoutPin(ShortcutId id) =>
        Pins.Contains(id) ? this with { Pins = new(Pins.Items.Remove(id)) } : this;

    /// <summary>Hidden from the used list and no longer pinned (the «Remove from Frequents» menu, FRE-001).</summary>
    /// <param name="id">The shortcut.</param>
    public FrequentsState WithHidden(ShortcutId id)
    {
        var unpinned = WithoutPin(id);
        var index = ImmutableArray.BinarySearch(unpinned.Hidden.Items, id, ById);
        return index >= 0
            ? unpinned
            : unpinned with
            {
                Hidden = new(unpinned.Hidden.Items.Insert(~index, id)),
            };
    }

    /// <summary>No longer hidden; the same instance if it was not.</summary>
    /// <param name="id">The shortcut.</param>
    public FrequentsState WithoutHidden(ShortcutId id)
    {
        var index = ImmutableArray.BinarySearch(Hidden.Items, id, ById);
        return index < 0 ? this : this with { Hidden = new(Hidden.Items.RemoveAt(index)) };
    }

    /// <summary>«Reset Frequents» (FRE-004): no usage, pins or hidden, and the next usage epoch.</summary>
    public FrequentsState Reset() => new([], [], UsageEpoch + 1, UsageHistory.Empty);
}
