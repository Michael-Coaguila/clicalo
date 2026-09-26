using System.Diagnostics.CodeAnalysis;
using Clicalo.Platform.Core.KeyLedger;

namespace Clicalo.Platform.Core.Injection;

/// <summary>
/// The generation fence (blueprint §3.2, rule 6, ADR-0004). Every effect with external consequences runs inside one
/// lock that first compares the caller's generation with the ledger's:
/// <code>
/// lock (gate) {
///   if (Volatile.Read(ledger.EngineGeneration) != g) return Fenced;   // a zombie thread sends nothing
///   ledger.BeginDown(...); SendInput(...); ledger.Commit(...);
/// }
/// </code>
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the engine package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public sealed class InjectionGate
{
    /// <summary>Creates the gate of a ledger.</summary>
    /// <param name="ledger">The engine's writable ledger.</param>
    /// <param name="sender">The real <see cref="LowLevelInjector"/>, or the tests' physical state injector.</param>
    public InjectionGate(KeyLedgerSection ledger, ILowLevelSender sender) =>
        throw new NotImplementedException();

    /// <summary>
    /// Sends a batch under the fence, recording key downs before sending and committing key ups after (INV-2).
    /// </summary>
    /// <param name="generation">The caller's generation.</param>
    /// <param name="batch">The events.</param>
    public GateOutcome TryInject(ulong generation, ReadOnlySpan<LowLevelInput> batch) =>
        throw new NotImplementedException();

    /// <summary>
    /// Runs another external effect under the fence (clipboard paste, handing a launch or system command to the Shell
    /// thread).
    /// </summary>
    /// <typeparam name="TState">Caller state, to avoid closures.</typeparam>
    /// <param name="generation">The caller's generation.</param>
    /// <param name="state">Passed to <paramref name="effect"/>.</param>
    /// <param name="effect">The effect.</param>
    public GateResult TryRun<TState>(ulong generation, TState state, Action<TState> effect) =>
        throw new NotImplementedException();

    /// <summary>
    /// The emergency of a hung engine (SysEvents, after <c>Timings.Engine.EngineStallThreshold</c> without heartbeat):
    /// tries to take the gate for <paramref name="wait"/> (<c>Timings.Engine.EmergencyGateWait</c>); if it can, raises
    /// the generation and releases everything recorded inside the lock.
    /// </summary>
    /// <param name="wait">How long to try to take the gate.</param>
    /// <param name="newGeneration">The generation of the new engine when released.</param>
    public EmergencyOutcome TryEmergencyRelease(TimeSpan wait, out ulong newGeneration) =>
        throw new NotImplementedException();
}
