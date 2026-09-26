using Clicalo.Application.Ports;
using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;

namespace Clicalo.Platform.Windows.Input;

/// <summary>
/// The fixed chords Clícalo injects for itself (blueprint §3.6, D-14): the reserved rights chord
/// (Ctrl+Alt+Shift+F24, left modifiers) and Win+H for dictation. Behind the gate, like every injection (ADR-0004):
/// each chord goes as one balanced batch, and a key an engine holder keeps down is neither pressed again nor released
/// under it. Never AltGr or right Ctrl.
/// </summary>
/// <param name="gate">The gate.</param>
/// <param name="hotkey">The registration of the rights chord: an unregistered chord is never sent.</param>
public sealed class InternalKeyEffects(InjectionGate gate, IInternalRightsHotkey hotkey)
    : IInternalKeyEffects
{
    /// <summary>Ctrl+Alt+Shift+F24 with the left modifiers.</summary>
    public static IReadOnlyList<PhysicalKey> RightsChord { get; } =
    [
        new(0xA2, 0x1D, LedgerKeyAttributes.None),
        new(0xA4, 0x38, LedgerKeyAttributes.None),
        new(0xA0, 0x2A, LedgerKeyAttributes.None),
        new(0x87, 0x76, LedgerKeyAttributes.None),
    ];

    /// <summary>Win+H (Windows dictation, BUS-003).</summary>
    public static IReadOnlyList<PhysicalKey> DictationChord { get; } =
    [new(0x5B, 0x5B, LedgerKeyAttributes.Extended), new(0x48, 0x23, LedgerKeyAttributes.None)];

    /// <inheritdoc />
    public ValueTask<bool> SendRightsHotkeyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(hotkey.IsRegistered && SendChord(RightsChord));
    }

    /// <inheritdoc />
    public ValueTask<bool> SendDictationChordAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(SendChord(DictationChord));
    }

    private bool SendChord(IReadOnlyList<PhysicalKey> chord)
    {
        var outcome = gate.TryInjectChord(gate.Ledger.Generation, [.. chord]);
        return outcome.Result == GateResult.Ran
            && outcome.Send.LastError == 0
            && outcome.Send.Sent > 0;
    }
}
