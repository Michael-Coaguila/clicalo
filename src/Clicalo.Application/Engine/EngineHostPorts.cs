using Clicalo.Application.Ports;

namespace Clicalo.Application.Engine;

/// <summary>The ports the engine host interprets effects against (blueprint §7.3).</summary>
/// <param name="Injector">Sends input through the gate.</param>
/// <param name="Ledger">Heartbeat, marks and generation of the physical ledger.</param>
/// <param name="Shell">Launches and system commands on the Shell thread.</param>
/// <param name="Clipboard">Pastes on the SysEvents thread.</param>
/// <param name="Observer">Receives snapshots, notices and usage.</param>
public sealed record EngineHostPorts(
    IInputInjector Injector,
    IKeyLedger Ledger,
    IShellExecutor Shell,
    IClipboardPaster Clipboard,
    IEngineObserver Observer
)
{
    /// <summary>
    /// Releases everything the <b>physical</b> ledger records, under the gate with the given generation
    /// (<c>InjectionGate.TryReleaseEverything</c>): what an engine that caught an exception releases (NFR-005), since
    /// its logical state may be the broken part. When absent, the host releases what its last good state held.
    /// </summary>
    public Func<EngineGeneration, bool>? ReleaseRecorded { get; init; }

    /// <summary>
    /// Where the host answers the internal chords it sent (<see cref="EngineKeyEffects"/>, blueprint §3.6); when
    /// absent, nobody waits for them.
    /// </summary>
    public InternalChordReplies? ChordReplies { get; init; }
}
