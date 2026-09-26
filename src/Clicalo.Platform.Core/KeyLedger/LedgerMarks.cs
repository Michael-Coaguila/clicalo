namespace Clicalo.Platform.Core.KeyLedger;

/// <summary>Marks of the ledger header (blueprint §7.4), read by Sentinel to decide whether to relaunch.</summary>
[Flags]
public enum LedgerMarks : ushort
{
    /// <summary>No mark.</summary>
    None = 0,

    /// <summary>The app exits on purpose.</summary>
    CleanShutdown = 1 << 0,

    /// <summary>Never relaunch (elevated handover, update).</summary>
    NoRelaunch = 1 << 1,

    /// <summary>The engine is running.</summary>
    EngineAlive = 1 << 2,

    /// <summary>The emergency could not take the gate; the process terminates itself and Sentinel releases.</summary>
    EmergencyRestart = 1 << 3,
}
