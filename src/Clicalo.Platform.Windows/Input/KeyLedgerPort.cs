using Clicalo.Application.Ports;
using Clicalo.Platform.Core.KeyLedger;

namespace Clicalo.Platform.Windows.Input;

/// <summary>
/// The engine's <see cref="IKeyLedger"/> over the physical ledger (blueprint §7.4): heartbeat, marks and generation.
/// Key slots are never written here: only <c>InjectionGate</c> writes them.
/// </summary>
/// <param name="ledger">The engine's writable ledger.</param>
public sealed class KeyLedgerPort(KeyLedgerSection ledger) : IKeyLedger
{
    /// <inheritdoc />
    public EngineGeneration CurrentGeneration => new(ledger.Generation);

    /// <inheritdoc />
    public KeyLedgerMarks Marks => (KeyLedgerMarks)(ushort)ledger.Marks;

    /// <inheritdoc />
    public int PendingReleases =>
        ledger.Snapshot().Slots.Count(static s => s.State == LedgerSlotState.ReleasePending);

    /// <inheritdoc />
    public void WriteHeartbeat(long ticks) => ledger.WriteHeartbeat(ticks);

    /// <inheritdoc />
    public void SetMarks(KeyLedgerMarks marks) => ledger.SetMarks((LedgerMarks)(ushort)marks);

    /// <inheritdoc />
    public void ClearMarks(KeyLedgerMarks marks) => ledger.ClearMarks((LedgerMarks)(ushort)marks);
}
