using Clicalo.Platform.Core.Guardian;
using Clicalo.Platform.Core.Injection;

namespace Clicalo.Sentinel;

/// <summary>
/// The guardian (blueprint §3.1, ADR-0022): waits for the parent to end; then asks Windows what is down and releases
/// it all (<see cref="PressedInputRelease"/>), and only after that relaunches the main process, if its exit was
/// abnormal (<see cref="RelaunchPolicy"/>). Blocked in its waits, it uses no CPU.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>The release goes first, before anything else is read or decided.</item>
/// <item>While the input desktop is not Sentinel's (a locked session, UAC, Ctrl+Alt+Supr), Windows reports every key
/// up and refuses <c>SendInput</c>: Sentinel tries again every <see cref="SentinelStartInfo.ReleaseRetryInterval"/>
/// until the release goes, without limit (user decision D3 of 2026-10-03: release before relaunching). If the
/// session ends meanwhile, Windows ends Sentinel with it and nothing is relaunched.</item>
/// <item>Each attempt reads the state again, so a batch Windows took only in part is completed by the next one.</item>
/// <item>The relaunch comes strictly after the release: the new process never presses a key that Sentinel would
/// release afterwards.</item>
/// <item>Sentinel cannot write files (only <c>AtomicFile</c> and the log sink may): the relaunched main process records
/// the crash from <see cref="CrashJournal.AfterCrashArgument"/>, with the time of the death, not of the release.</item>
/// </list>
/// </remarks>
/// <param name="startInfo">The parsed start-up contract.</param>
/// <param name="keys">What Windows reports down.</param>
/// <param name="sender">Sends the releases.</param>
/// <param name="time">Clock of the crash journal.</param>
/// <param name="environment">The parent, the pauses, the journal and the relaunch.</param>
internal sealed class GuardianLoop(
    SentinelStartInfo startInfo,
    IKeyStateReader keys,
    ILowLevelSender sender,
    TimeProvider time,
    IGuardianEnvironment environment
)
{
    /// <summary>How many attempts the last release needed, for the tests.</summary>
    internal int Attempts { get; private set; }

    /// <summary>Waits for the parent and acts; returns the exit code.</summary>
    public SentinelExitCode Run()
    {
        if (environment.WaitForParentExit() is not { } exitCode)
        {
            // A handle that cannot be waited on says nothing about the parent: releasing now could let go of keys a
            // live engine holds.
            return SentinelExitCode.InvalidArguments;
        }

        var diedAt = time.GetUtcNow();
        ReleaseUntilAccepted();

        var decision = RelaunchPolicy.Decide(
            exitCode,
            environment.RecentCrashes().AsSpan(),
            diedAt,
            startInfo.CrashLoopCount,
            startInfo.CrashLoopWindow
        );
        if (decision == RelaunchDecision.None)
        {
            return exitCode == 0
                ? SentinelExitCode.CleanExit
                : SentinelExitCode.ReleasedWithoutRelaunch;
        }

        return environment.Relaunch(
            CrashJournal.RelaunchArguments(diedAt, decision == RelaunchDecision.RelaunchInSafeMode)
        )
            ? SentinelExitCode.ReleasedAndRelaunched
            : SentinelExitCode.ReleasedWithoutRelaunch;
    }

    private void ReleaseUntilAccepted()
    {
        Attempts = 0;
        while (true)
        {
            Attempts++;
            if (PressedInputRelease.ReleaseOnce(keys, sender).Completed)
            {
                return;
            }

            environment.WaitBeforeRetry(startInfo.ReleaseRetryInterval);
        }
    }
}
