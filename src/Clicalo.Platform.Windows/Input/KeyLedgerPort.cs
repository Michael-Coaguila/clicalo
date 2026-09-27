using Clicalo.Application.Ports;
using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;

namespace Clicalo.Platform.Windows.Input;

/// <summary>
/// The engine's <see cref="IKeyLedger"/> over the physical ledger (blueprint §7.4): heartbeat, marks and generation.
/// Key slots are never written here: only <c>InjectionGate</c> writes them. The engine's heartbeat and marks go
/// through the gate with the engine's generation (§3.2, rule 6), so a zombie engine never writes them.
/// </summary>
/// <param name="gate">The gate of the engine's writable ledger.</param>
public sealed class KeyLedgerPort(InjectionGate gate) : IKeyLedger
{
    /// <inheritdoc />
    public EngineGeneration CurrentGeneration => new(gate.Ledger.Generation);

    /// <inheritdoc />
    public KeyLedgerMarks Marks => (KeyLedgerMarks)(ushort)gate.Ledger.Marks;

    /// <inheritdoc />
    public int PendingReleases =>
        gate.Ledger.Snapshot().Slots.Count(static s => s.State == LedgerSlotState.ReleasePending);

    /// <inheritdoc />
    public bool TryWriteHeartbeat(EngineGeneration generation, long ticks) =>
        gate.TryWriteHeartbeat(generation.Value, ticks) == GateResult.Ran;

    /// <inheritdoc />
    public bool TryUpdateMarks(
        EngineGeneration generation,
        KeyLedgerMarks toSet,
        KeyLedgerMarks toClear
    ) =>
        gate.TryUpdateMarks(
            generation.Value,
            (LedgerMarks)(ushort)toSet,
            (LedgerMarks)(ushort)toClear
        ) == GateResult.Ran;

    /// <inheritdoc />
    public void SetMarks(KeyLedgerMarks marks) => gate.Ledger.SetMarks((LedgerMarks)(ushort)marks);

    /// <inheritdoc />
    public void ClearMarks(KeyLedgerMarks marks) =>
        gate.Ledger.ClearMarks((LedgerMarks)(ushort)marks);
}
