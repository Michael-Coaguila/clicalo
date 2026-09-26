namespace Clicalo.Platform.Core.Injection;

/// <summary>The result of <see cref="InjectionGate.TryEmergencyRelease"/> (blueprint §3.2, rule 6).</summary>
public enum EmergencyOutcome
{
    /// <summary>The gate was taken: the generation went up and everything recorded was released inside the lock.</summary>
    Released,

    /// <summary>
    /// The hung thread holds the gate (for example inside a <c>SendInput</c> held by third-party hooks): order cannot be
    /// guaranteed, so the caller escalates to <c>EmergencyRestart</c> and terminates the process.
    /// </summary>
    GateBusy,
}
