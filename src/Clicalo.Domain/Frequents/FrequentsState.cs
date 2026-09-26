using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Frequents;

/// <summary>
/// The Frequents view state (FRE-*, blueprint §6.3): the curation (pins and hidden) and the usage, which are two
/// separate undo slices. <see cref="UsageEpoch"/> goes up on «Reset Frequents»; a <c>usage.json</c> with another epoch
/// is discarded on load (§6.5).
/// </summary>
/// <param name="Pins">Pinned shortcuts in pin order (FRE-001).</param>
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
    /// <summary>A new document's Frequents: nothing pinned, hidden or used, epoch 0.</summary>
    public static FrequentsState Empty { get; } = new([], [], 0, UsageHistory.Empty);
}
