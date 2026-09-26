using System.Diagnostics.CodeAnalysis;
using Clicalo.Platform.Core.Guardian;
using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;

namespace Clicalo.Sentinel;

/// <summary>
/// The guardian (blueprint §3.1, ADR-0004): <c>WaitForMultipleObjects(parent, pipe)</c>; when the parent dies or the
/// pipe breaks, reads the ledger, sends the key ups in the recorded mode with the menu mask
/// (<see cref="LedgerRelease"/>) within 200 ms (S9), writes the crash journal and relaunches according to
/// <see cref="RelaunchPolicy"/>. Blocked in the wait, it uses no CPU.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the engine package implements it (docs/testing/spikes/M2-ownership.md)."
)]
internal sealed class GuardianLoop
{
    /// <summary>Creates the loop.</summary>
    /// <param name="startInfo">The parsed start-up contract.</param>
    /// <param name="ledger">The inherited read-only ledger.</param>
    /// <param name="sender">Sends the releases.</param>
    /// <param name="time">Clock of the crash journal.</param>
    public GuardianLoop(
        SentinelStartInfo startInfo,
        KeyLedgerSection ledger,
        ILowLevelSender sender,
        TimeProvider time
    ) => throw new NotImplementedException();

    /// <summary>Waits for the parent and acts; returns the exit code.</summary>
    public SentinelExitCode Run() => throw new NotImplementedException();
}
