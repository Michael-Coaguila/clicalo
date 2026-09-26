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

    /// <summary>Writes the engine heartbeat: on every mailbox turn and every <c>Timings.Engine.LedgerHeartbeatInterval</c>.</summary>
    /// <param name="ticks">Now, in <see cref="TimeProvider"/> ticks.</param>
    void WriteHeartbeat(long ticks);

    /// <summary>Sets marks (for example <c>CleanShutdown | NoRelaunch</c> before an elevated handover).</summary>
    /// <param name="marks">Marks to set.</param>
    void SetMarks(KeyLedgerMarks marks);

    /// <summary>Clears marks (for example <c>NoRelaunch</c> when the UAC prompt is cancelled).</summary>
    /// <param name="marks">Marks to clear.</param>
    void ClearMarks(KeyLedgerMarks marks);
}
