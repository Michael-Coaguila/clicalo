using System.Collections.Immutable;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Frequents;

/// <summary>
/// Execution time stamps per shortcut (FRE-002), persisted apart in <c>usage.json</c> (§6.5). Entries of deleted
/// shortcuts dangle on purpose until purged (FRE-005). The dictionary is compared by reference: the document store
/// detects touched slices by reference (§6.4).
/// </summary>
/// <param name="Entries">Time stamps of each shortcut, oldest first.</param>
public sealed record UsageHistory(
    ImmutableDictionary<ShortcutId, ValueList<DateTimeOffset>> Entries
)
{
    /// <summary>No usage.</summary>
    public static UsageHistory Empty { get; } =
        new(ImmutableDictionary<ShortcutId, ValueList<DateTimeOffset>>.Empty);
}
