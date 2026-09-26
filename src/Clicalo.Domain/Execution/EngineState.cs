using System.Collections.Immutable;
using Clicalo.Domain.KeySafety;

namespace Clicalo.Domain.Execution;

/// <summary>
/// The whole state of the engine (blueprint §7.3), owned by the engine thread only. What must survive a crash goes
/// to the physical ledger, not here. Sticky modifiers join it in M3.
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
    /// <summary>The state of a new engine, and after an emergency release.</summary>
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
}
