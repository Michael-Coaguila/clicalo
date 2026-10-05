using System.Collections.Immutable;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.StickyModifiers;
using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Execution;

/// <summary>
/// The whole state of the engine (blueprint §7.3), owned by the engine thread only. What is down when the process dies is
/// released by Sentinel from what Windows reports (ADR-0022).
/// </summary>
/// <param name="Keys">The logical ledger.</param>
/// <param name="Armed">The shortcut waiting for its confirmation tap.</param>
/// <param name="Macro">The running macro.</param>
/// <param name="TestMode">Whether test mode is on (INV-7).</param>
/// <param name="Paused">Whether sending is paused (INV-7).</param>
/// <param name="Foreground">The verified external foreground, or <see langword="null"/> before the first one.</param>
/// <param name="ShiftGuard">Recent Shift presses (SEG-008).</param>
/// <param name="PendingExternal">External effects waiting for their result.</param>
/// <param name="Version">Raised on every transition.</param>
public sealed record EngineState(
    KeyboardLedger Keys,
    ArmedConfirmation? Armed,
    MacroRun? Macro,
    bool TestMode,
    bool Paused,
    ForegroundInfo? Foreground,
    ShiftBurstWindow ShiftGuard,
    ImmutableDictionary<EffectId, PendingExternal> PendingExternal,
    long Version
)
{
    /// <summary>The state of a new engine, and after an exception (NFR-005).</summary>
    public static EngineState Empty { get; } =
        new(
            KeyboardLedger.Empty,
            null,
            null,
            TestMode: false,
            Paused: false,
            null,
            ShiftBurstWindow.Empty,
            ImmutableDictionary<EffectId, PendingExternal>.Empty,
            0
        );

    /// <summary>Key operations waiting for their turn (<see cref="QueuedStep"/>), in order.</summary>
    public ValueList<QueuedStep> Outbox { get; init; }

    /// <summary>When the next queued step may go, or <see langword="null"/> when it may go now.</summary>
    public long? OutboxDueTicks { get; init; }

    /// <summary>The touch filter memory of each shortcut (TAC-002: one target never blocks another).</summary>
    public ImmutableDictionary<ShortcutId, ButtonFilterState> Filters { get; init; } =
        ImmutableDictionary<ShortcutId, ButtonFilterState>.Empty;

    /// <summary>The scroll action repeating under a contact, if any.</summary>
    public ScrollRepeat? Scroll { get; init; }

    /// <summary>The last number given to a transaction, effect or macro run; each new one is the next.</summary>
    public long Sequence { get; init; }

    /// <summary>
    /// The last press batches sent (at most <see cref="PressMemory"/>), so a batch that <c>SendInput</c> took only in
    /// part releases whatever it may have left down (INV-5). The failure comes back a few events later at most.
    /// </summary>
    public ImmutableDictionary<EffectId, PressedBatch> PressEffects { get; init; } =
        ImmutableDictionary<EffectId, PressedBatch>.Empty;

    /// <summary>How many press batches <see cref="PressEffects"/> remembers.</summary>
    public const int PressMemory = 64;

    /// <summary>The sticky modifiers of the panel row (FIJ-005, FIJ-006).</summary>
    public StickyState Sticky { get; init; } = StickyState.Empty;

    /// <summary>
    /// Releases the secure desktop refused (a locked session): sent again when the session comes back (INV-3,
    /// <see cref="EngineEvent.SessionResumed"/>).
    /// </summary>
    public ValueList<InjectedEvent> BlockedReleases { get; init; }

    /// <summary>
    /// Whether nothing is held or about to be pressed: no holder, no queued step, no macro and no repeating scroll
    /// (INV-3 after a terminal event).
    /// </summary>
    public bool IsQuiet => Keys.IsEmpty && Outbox.IsEmpty && Macro is null && Scroll is null;
}
