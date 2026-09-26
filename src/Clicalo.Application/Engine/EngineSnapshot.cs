using Clicalo.Domain.Execution;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.StickyModifiers;

namespace Clicalo.Application.Engine;

/// <summary>
/// What the UI sees of the engine (blueprint §6.4): immutable, published at most once per frame
/// (<c>Timings.Engine.SnapshotCoalescing</c>). The panic strip shows while <see cref="Held"/> is not empty (SEG-002).
/// </summary>
/// <param name="Held">Everything held, from the logical ledger.</param>
/// <param name="Macro">The running macro.</param>
/// <param name="Armed">The shortcut waiting for its confirmation tap.</param>
/// <param name="TestMode">Whether test mode is on.</param>
/// <param name="Paused">Whether sending is paused.</param>
/// <param name="Version">The engine state version it was taken from.</param>
public sealed record EngineSnapshot(
    ValueList<PressedItem> Held,
    MacroRun? Macro,
    ArmedConfirmation? Armed,
    bool TestMode,
    bool Paused,
    long Version
)
{
    /// <summary>A new engine: nothing held.</summary>
    public static EngineSnapshot Empty { get; } =
        new([], null, null, TestMode: false, Paused: false, 0);

    /// <summary>The levels of the sticky modifiers row (FIJ-005: released, once, locked).</summary>
    public StickyState Sticky { get; init; } = StickyState.Empty;
}
