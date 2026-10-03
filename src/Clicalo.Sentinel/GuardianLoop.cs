using Clicalo.Platform.Core.Guardian;
using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;

namespace Clicalo.Sentinel;

/// <summary>
/// The guardian (blueprint §3.1, ADR-0004, ADR-0018): <c>WaitForMultipleObjects(parent, pipe)</c>; when the parent
/// dies or the pipe breaks, reads the ledger, sends the key ups in the recorded mode with the menu mask
/// (<see cref="LedgerRelease"/>) within 200 ms (S9), retries whatever the desktop refused until it goes, and only then
/// relaunches according to <see cref="RelaunchPolicy"/>. Blocked in its waits, it uses no CPU.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A broken pipe with the parent still alive means the main process closed it on purpose: Sentinel waits a few
/// heartbeats for the parent to end and otherwise leaves quietly (the main process relaunches its guardian).</item>
/// <item>The release goes first, before anything else is read or decided: nothing Sentinel does after it can delay
/// it.</item>
/// <item>A release the desktop refuses (user decision D3 of 2026-10-03, ADR-0018) is sent again every
/// <see cref="SentinelStartInfo.HeartbeatInterval"/>, only what did not go. A refusal by the secure desktop
/// (<c>SendInput</c> accepts nothing, <c>ERROR_ACCESS_DENIED</c>: a locked session, UAC, Ctrl+Alt+Supr or another
/// input desktop) is retried without limit: Sentinel is the only one that knows what is down. Any other refusal is
/// retried for at most <see cref="SentinelStartInfo.RefusedReleaseWait"/>, so a refusal that never clears cannot keep
/// Clícalo from coming back. If the session ends meanwhile, Windows ends Sentinel with it and nothing is relaunched.</item>
/// <item>The relaunch comes strictly after the last release: the new process never presses a key that Sentinel would
/// release afterwards.</item>
/// <item>Sentinel cannot write files (only <c>AtomicFile</c> and the log sink may): the relaunched main process records
/// the crash from <see cref="CrashJournal.AfterCrashArgument"/>, with the time of the death, not of the release.</item>
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
    /// <param name="time">Clock of the crash journal and of the retries.</param>
    public GuardianLoop(
        SentinelStartInfo startInfo,
        KeyLedgerSection ledger,
        ILowLevelSender sender,
        TimeProvider time
    )
        : this(startInfo, ledger, sender, time, new SystemGuardianEnvironment(startInfo, time)) { }

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

    /// <summary>How many sends the desktop refused in the last run, for the tests.</summary>
    internal int RefusedSends { get; private set; }

    /// <summary>Whether the last run gave up on a refusal that was not the secure desktop's, for the tests.</summary>
    internal bool GaveUpOnRefusal { get; private set; }

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

        // Release first (S9: within 200 ms of the death); a refused release keeps Sentinel here until it went.
        var diedAt = _time.GetUtcNow();
        var batch = LedgerRelease.BuildReleaseBatch(snapshot);
        if (!batch.IsEmpty)
        {
            ReleaseUntilAccepted(batch.AsSpan());
        }

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

    /// <summary>
    /// Sends <paramref name="batch"/> and, while the desktop refuses part of it, sends what did not go again every
    /// heartbeat: without limit while the refusal is the secure desktop's, for at most
    /// <see cref="SentinelStartInfo.RefusedReleaseWait"/> of uninterrupted other refusals.
    /// </summary>
    private void ReleaseUntilAccepted(ReadOnlySpan<LowLevelInput> batch)
    {
        var remaining = batch;
        long? otherRefusalsSince = null;
        while (true)
        {
            var result = _sender.Send(remaining);
            var sent = Math.Clamp(result.Sent, 0, remaining.Length);
            ReleasedEvents += sent;
            remaining = remaining[sent..];
            if (remaining.IsEmpty)
            {
                return;
            }

            RefusedSends++;
            if (IsSecureDesktopRefusal(result))
            {
                otherRefusalsSince = null;
            }
            else
            {
                var now = _time.GetTimestamp();
                otherRefusalsSince ??= now;
                if (
                    _time.GetElapsedTime(otherRefusalsSince.Value, now)
                    >= _startInfo.RefusedReleaseWait
                )
                {
                    GaveUpOnRefusal = true;
                    return;
                }
            }

            _environment.WaitBeforeRetry(_startInfo.HeartbeatInterval);
        }
    }

    /// <summary>
    /// The secure desktop's refusal, as <c>InjectionGate</c> reads it: nothing accepted and
    /// <c>ERROR_ACCESS_DENIED</c>.
    /// </summary>
    private static bool IsSecureDesktopRefusal(SendResult result) =>
        result.Sent == 0 && result.LastError == InjectionGate.AccessDenied;
}
