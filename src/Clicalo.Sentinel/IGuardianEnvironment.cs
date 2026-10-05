using System.Collections.Immutable;
using Clicalo.Platform.Core.Guardian;

namespace Clicalo.Sentinel;

/// <summary>
/// What the guardian loop needs from the machine besides the keys: waiting on the parent, the pause between two
/// attempts of a refused release, the crash journal and the relaunch. The real one is
/// <see cref="SystemGuardianEnvironment"/>; the tests give their own, so the loop is tested without a process.
/// </summary>
internal interface IGuardianEnvironment
{
    /// <summary>
    /// Blocks until the parent ends, using no CPU; returns its exit code, or <see langword="null"/> when the parent
    /// handle cannot be waited on (then nothing is released: the parent may still be alive).
    /// </summary>
    int? WaitForParentExit();

    /// <summary>Blocks for <paramref name="interval"/> before a refused release is tried again, using no CPU.</summary>
    /// <param name="interval">The retry interval.</param>
    void WaitBeforeRetry(TimeSpan interval);

    /// <summary>The crash times of the journal (empty when it is missing or unreadable).</summary>
    ImmutableArray<DateTimeOffset> RecentCrashes();

    /// <summary>Starts the main process again with <paramref name="arguments"/>; <see langword="false"/> if it could not.</summary>
    /// <param name="arguments">The relaunch arguments (<see cref="CrashJournal.RelaunchArguments"/>).</param>
    bool Relaunch(ImmutableArray<string> arguments);
}
