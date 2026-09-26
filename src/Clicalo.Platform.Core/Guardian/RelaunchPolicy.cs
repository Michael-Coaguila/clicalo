using Clicalo.Platform.Core.KeyLedger;

namespace Clicalo.Platform.Core.Guardian;

/// <summary>
/// Whether Sentinel relaunches the main process (blueprint §3.1): never with <c>CleanShutdown</c> or <c>NoRelaunch</c>
/// (updates and the elevated handover), and into safe mode when the crashes inside the window reach the threshold
/// (<c>Timings.App.CrashLoop</c>, spike S9). Pure, shared by Sentinel and the main process's start-up.
/// </summary>
public static class RelaunchPolicy
{
    /// <summary>Decides.</summary>
    /// <param name="marks">The ledger marks when the process died.</param>
    /// <param name="recentCrashes">Earlier crash times from the crash journal.</param>
    /// <param name="now">Now.</param>
    /// <param name="crashLoopCount">Crashes that make a loop.</param>
    /// <param name="crashLoopWindow">The window they are counted in.</param>
    public static RelaunchDecision Decide(
        LedgerMarks marks,
        ReadOnlySpan<DateTimeOffset> recentCrashes,
        DateTimeOffset now,
        int crashLoopCount,
        TimeSpan crashLoopWindow
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(crashLoopCount);
        if ((marks & (LedgerMarks.CleanShutdown | LedgerMarks.NoRelaunch)) != LedgerMarks.None)
        {
            return RelaunchDecision.None;
        }

        // This crash counts too: the loop is reached when it is the crashLoopCount-th inside the window.
        var inWindow = 1;
        foreach (var crash in recentCrashes)
        {
            if (crash <= now && now - crash <= crashLoopWindow)
            {
                inWindow++;
            }
        }

        return inWindow >= crashLoopCount
            ? RelaunchDecision.RelaunchInSafeMode
            : RelaunchDecision.Relaunch;
    }
}
