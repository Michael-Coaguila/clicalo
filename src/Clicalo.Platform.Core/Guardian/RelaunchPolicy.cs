namespace Clicalo.Platform.Core.Guardian;

/// <summary>
/// Whether Sentinel relaunches the main process (blueprint §3.1, ADR-0022): never after an exit code of 0 (a clean
/// exit, the end of the session, an update or the elevated handover), into safe mode when the crashes inside the window
/// reach the threshold (<c>Timings.App.CrashLoop</c>, spike S9), and no more once they pass it: the safe mode relaunch
/// crashed too, and relaunching again would only loop. Pure.
/// </summary>
public static class RelaunchPolicy
{
    /// <summary>Decides.</summary>
    /// <param name="exitCode">The main process's exit code.</param>
    /// <param name="recentCrashes">Earlier crash times from the crash journal.</param>
    /// <param name="now">Now.</param>
    /// <param name="crashLoopCount">Crashes that make a loop.</param>
    /// <param name="crashLoopWindow">The window they are counted in.</param>
    public static RelaunchDecision Decide(
        int exitCode,
        ReadOnlySpan<DateTimeOffset> recentCrashes,
        DateTimeOffset now,
        int crashLoopCount,
        TimeSpan crashLoopWindow
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(crashLoopCount);
        if (exitCode == 0)
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

        return inWindow < crashLoopCount ? RelaunchDecision.Relaunch
            : inWindow == crashLoopCount ? RelaunchDecision.RelaunchInSafeMode
            : RelaunchDecision.None;
    }
}
