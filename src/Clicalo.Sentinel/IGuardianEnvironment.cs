using System.Collections.Immutable;
using Clicalo.Platform.Core.Guardian;

namespace Clicalo.Sentinel;

/// <summary>
/// What the guardian loop needs from the machine: waiting on the parent and the pipe, the pause between two attempts
/// of a refused release, the crash journal and the relaunch. The real one is <see cref="SystemGuardianEnvironment"/>; the tests give their own, so the loop is tested
/// without killing a process.
/// </summary>
internal interface IGuardianEnvironment
{
    /// <summary>Blocks until the parent ends or the heartbeat pipe breaks.</summary>
    GuardianWake WaitForParentOrPipe();

    /// <summary>Whether the parent ends within <paramref name="timeout"/>.</summary>
    /// <param name="timeout">How long to wait.</param>
    bool WaitForParentExit(TimeSpan timeout);

    /// <summary>
    /// Blocks for <paramref name="interval"/> before a refused release is sent again, using no CPU (the session is
    /// usually locked meanwhile).
    /// </summary>
    /// <param name="interval">The heartbeat interval.</param>
    void WaitBeforeRetry(TimeSpan interval);

    /// <summary>The crash times of the journal (empty when it is missing or unreadable).</summary>
    ImmutableArray<DateTimeOffset> RecentCrashes();

    /// <summary>Starts the main process again with <paramref name="arguments"/>; <see langword="false"/> if it could not.</summary>
    /// <param name="arguments">The relaunch arguments (<see cref="CrashJournal.RelaunchArguments"/>).</param>
    bool Relaunch(ImmutableArray<string> arguments);
}
