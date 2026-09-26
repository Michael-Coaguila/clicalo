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
/// <remarks>
/// <list type="bullet">
/// <item>A broken pipe with the parent still alive means the main process closed it on purpose: Sentinel waits a few
/// heartbeats for the parent to end and otherwise leaves quietly (the main process relaunches its guardian).</item>
/// <item>The release goes first, before anything else is read or decided: nothing Sentinel does after it can delay
/// it.</item>
/// <item>Sentinel cannot write files (only <c>AtomicFile</c> and the log sink may): the relaunched main process records
/// the crash from <see cref="CrashJournal.AfterCrashArgument"/>.</item>
/// </list>
/// </remarks>
internal sealed class GuardianLoop
{
    /// <summary>How many heartbeats Sentinel waits for the parent after the pipe broke.</summary>
    internal const int PipeGraceHeartbeats = 5;

    private readonly SentinelStartInfo _startInfo;
    private readonly KeyLedgerSection _ledger;
    private readonly ILowLevelSender _sender;
    private readonly TimeProvider _time;
    private readonly IGuardianEnvironment _environment;

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
    )
        : this(startInfo, ledger, sender, time, new SystemGuardianEnvironment(startInfo)) { }

    /// <summary>Creates the loop over another environment (tests).</summary>
    internal GuardianLoop(
        SentinelStartInfo startInfo,
        KeyLedgerSection ledger,
        ILowLevelSender sender,
        TimeProvider time,
        IGuardianEnvironment environment
    )
    {
        _startInfo = startInfo;
        _ledger = ledger;
        _sender = sender;
        _time = time;
        _environment = environment;
    }

    /// <summary>What the last run released, for the tests.</summary>
    internal int ReleasedEvents { get; private set; }

    /// <summary>Waits for the parent and acts; returns the exit code.</summary>
    public SentinelExitCode Run()
    {
        var wake = _environment.WaitForParentOrPipe();
        if (
            wake == GuardianWake.PipeBroken
            && !_environment.WaitForParentExit(_startInfo.HeartbeatInterval * PipeGraceHeartbeats)
        )
        {
            return SentinelExitCode.CleanExit;
        }

        var snapshot = _ledger.Snapshot();
        if (snapshot.LayoutVersion != KeyLedgerLayout.LayoutVersion)
        {
            return SentinelExitCode.LedgerUnreadable;
        }

        // Release first (S9: within 200 ms of the death).
        var batch = LedgerRelease.BuildReleaseBatch(snapshot);
        if (!batch.IsEmpty)
        {
            ReleasedEvents = _sender.Send(batch.AsSpan()).Sent;
        }

        var diedAt = _time.GetUtcNow();
        var decision = RelaunchPolicy.Decide(
            snapshot.Marks,
            _environment.RecentCrashes().AsSpan(),
            diedAt,
            _startInfo.CrashLoopCount,
            _startInfo.CrashLoopWindow
        );
        if (decision == RelaunchDecision.None)
        {
            return batch.IsEmpty && (snapshot.Marks & LedgerMarks.CleanShutdown) != LedgerMarks.None
                ? SentinelExitCode.CleanExit
                : SentinelExitCode.ReleasedWithoutRelaunch;
        }

        var relaunched = _environment.Relaunch(
            CrashJournal.RelaunchArguments(diedAt, decision == RelaunchDecision.RelaunchInSafeMode)
        );
        return relaunched
            ? SentinelExitCode.ReleasedAndRelaunched
            : SentinelExitCode.ReleasedWithoutRelaunch;
    }
}
