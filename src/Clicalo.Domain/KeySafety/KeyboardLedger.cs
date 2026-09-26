using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Clicalo.Domain.Keys;

namespace Clicalo.Domain.KeySafety;

/// <summary>
/// The logical ledger of everything Clícalo holds (SEG-001, blueprint §7.4), with reference counts per physical key:
/// a key goes down when its first holder acquires it and up when its last holder releases it. Pure and immutable; the
/// engine keeps it in <c>EngineState</c> and the physical ledger (<c>Clicalo.Platform.Core.KeyLedger</c>) mirrors
/// what is actually down.
/// </summary>
/// <param name="Holders">Holders of each physical key.</param>
/// <param name="Items">Items by holder.</param>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the engine package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public sealed record KeyboardLedger(
    ImmutableDictionary<InjectedKey, ImmutableHashSet<HolderId>> Holders,
    ImmutableDictionary<HolderId, PressedItem> Items
)
{
    /// <summary>Nothing held.</summary>
    public static KeyboardLedger Empty { get; } =
        new(
            ImmutableDictionary<InjectedKey, ImmutableHashSet<HolderId>>.Empty,
            ImmutableDictionary<HolderId, PressedItem>.Empty
        );

    /// <summary>Whether nothing is held (the panic strip hides, SEG-002).</summary>
    public bool IsEmpty => Items.IsEmpty;

    /// <summary>Adds an item; emits a key down only for keys that go from 0 to 1 holders.</summary>
    /// <param name="item">The item; its holder must not hold anything yet.</param>
    public LedgerTransition Acquire(PressedItem item) => throw new NotImplementedException();

    /// <summary>
    /// Removes a holder's item; emits a key up (with the menu mask before Alt or Win) only for keys that go from 1 to 0
    /// holders, in reverse press order.
    /// </summary>
    /// <param name="holder">The holder.</param>
    public LedgerTransition Release(HolderId holder) => throw new NotImplementedException();

    /// <summary>Removes every item and releases everything in reverse order (SEG-003, INV-3).</summary>
    public LedgerTransition ReleaseAll() => throw new NotImplementedException();

    /// <summary>
    /// Recomputes every deadline from its press with a new global limit (SEG-004: changing the limit recalculates the
    /// deadlines in progress); items with their own limit keep it.
    /// </summary>
    /// <param name="globalLimit">The new global limit, or <see langword="null"/> for «Never».</param>
    /// <param name="ticksPerSecond">Tick frequency of the time source.</param>
    public KeyboardLedger WithGlobalLimit(TimeSpan? globalLimit, long ticksPerSecond) =>
        throw new NotImplementedException();

    /// <summary>The earliest deadline, so the engine keeps a single timer; <see langword="null"/> when none.</summary>
    public long? NextDeadline() => throw new NotImplementedException();
}
