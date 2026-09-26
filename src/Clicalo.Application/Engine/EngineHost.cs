using System.Diagnostics.CodeAnalysis;
using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Microsoft.Extensions.Logging;

namespace Clicalo.Application.Engine;

/// <summary>
/// The engine actor (blueprint §7.3, ADR-0004): owns <see cref="EngineState"/> on the engine thread, runs
/// <see cref="EngineReducer"/> for each event of its <see cref="EngineMailbox"/> and interprets the effects against the
/// ports, every external one through the injection gate with its <see cref="Generation"/>. Each message runs in a
/// try/catch: an exception releases from the ledger under the gate, resets to <see cref="EngineState.Empty"/> and warns
/// «Something failed; the keys were released» (NFR-005). Writes the ledger heartbeat on every turn and every
/// <c>Timings.Engine.LedgerHeartbeatInterval</c>; timers run on <see cref="TimeProvider"/>.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the engine package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public sealed class EngineHost : IEngineInbox
{
    /// <summary>Creates a host; <see cref="Run"/> starts it on the engine thread.</summary>
    /// <param name="ports">The ports.</param>
    /// <param name="generation">The ledger's current generation; a host never changes it.</param>
    /// <param name="config">The initial settings.</param>
    /// <param name="time">Clock and timers.</param>
    /// <param name="logger">Logs codes, never keys or text (LOG-001).</param>
    public EngineHost(
        EngineHostPorts ports,
        EngineGeneration generation,
        EngineConfig config,
        TimeProvider time,
        ILogger<EngineHost> logger
    ) => throw new NotImplementedException();

    /// <summary>The generation every effect of this host carries.</summary>
    public EngineGeneration Generation => throw new NotImplementedException();

    /// <summary>The latest published snapshot; safe to read from any thread.</summary>
    public EngineSnapshot Snapshot => throw new NotImplementedException();

    /// <inheritdoc />
    public bool Post(EngineEvent engineEvent) => throw new NotImplementedException();

    /// <summary>
    /// The engine loop: runs on the dedicated engine thread (AboveNormal) until <paramref name="cancellationToken"/>
    /// is cancelled or a <see cref="EngineEvent.Terminal"/> with <see cref="TerminalReason.Exit"/> is processed.
    /// Never does disk or network I/O, never calls the UI and never waits on anything but its mailbox.
    /// </summary>
    /// <param name="cancellationToken">Stops the loop after releasing everything.</param>
    public void Run(CancellationToken cancellationToken) => throw new NotImplementedException();
}
