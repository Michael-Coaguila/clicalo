namespace Clicalo.Application.Ports;

/// <summary>Marks of the physical ledger that Sentinel reads (blueprint §7.4, ADR-0004).</summary>
[Flags]
public enum KeyLedgerMarks
{
    /// <summary>No mark.</summary>
    None = 0,

    /// <summary>The app exits on purpose: Sentinel does not relaunch it.</summary>
    CleanShutdown = 1 << 0,

    /// <summary>Never relaunch (elevated handover, update).</summary>
    NoRelaunch = 1 << 1,

    /// <summary>The engine is running.</summary>
    EngineAlive = 1 << 2,

    /// <summary>The emergency could not take the gate and the process is terminating itself (§3.2, rule 6).</summary>
    EmergencyRestart = 1 << 3,
}
