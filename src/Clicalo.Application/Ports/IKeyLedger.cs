namespace Clicalo.Application.Ports;

/// <summary>
/// The engine's view of the physical ledger (<c>Clicalo.Platform.Core.KeyLedger</c>, 4 KiB of unnamed shared memory
/// inherited only by Sentinel). Key slots are written by the injector under the gate, never through this port.
/// </summary>
public interface IKeyLedger
{
    /// <summary>The current generation; an engine host is born with it.</summary>
    EngineGeneration CurrentGeneration { get; }

    /// <summary>The marks currently set.</summary>
    KeyLedgerMarks Marks { get; }

    /// <summary>Slots waiting to be released (secure desktop), for the <c>ledger.pending_release</c> metric.</summary>
    int PendingReleases { get; }

    /// <summary>
    /// Writes the engine heartbeat under the generation fence (blueprint §3.2, rule 6): on every mailbox turn and every
    /// <c>Timings.Engine.LedgerHeartbeatInterval</c>.
    /// </summary>
    /// <param name="generation">The caller's generation.</param>
    /// <param name="ticks">Now, in <see cref="TimeProvider"/> ticks.</param>
    /// <returns>
    /// <see langword="false"/> when <paramref name="generation"/> is no longer the ledger's: an emergency replaced the
    /// caller, which must stop (INV-11); it never renews the heartbeat of the engine that replaced it.
    /// </returns>
    bool TryWriteHeartbeat(EngineGeneration generation, long ticks);

    /// <summary>
    /// Sets and clears marks under the generation fence: the engine's <c>EngineAlive</c> and the marks of its terminal
    /// events, which a fenced engine must never touch.
    /// </summary>
    /// <param name="generation">The caller's generation.</param>
    /// <param name="toSet">Marks to set.</param>
    /// <param name="toClear">Marks to clear.</param>
    /// <returns><see langword="false"/> when <paramref name="generation"/> is no longer the ledger's.</returns>
    bool TryUpdateMarks(EngineGeneration generation, KeyLedgerMarks toSet, KeyLedgerMarks toClear);

    /// <summary>Sets marks (for example <c>CleanShutdown | NoRelaunch</c> before an elevated handover).</summary>
    /// <param name="marks">Marks to set.</param>
    void SetMarks(KeyLedgerMarks marks);

    /// <summary>Clears marks (for example <c>NoRelaunch</c> when the UAC prompt is cancelled).</summary>
    /// <param name="marks">Marks to clear.</param>
    void ClearMarks(KeyLedgerMarks marks);
}
