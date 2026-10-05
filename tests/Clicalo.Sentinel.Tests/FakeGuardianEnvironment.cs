using System.Collections.Immutable;
using Clicalo.Platform.Core.Guardian;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Sentinel.Tests;

/// <summary>The machine as the guardian loop sees it, without any process, pipe or file.</summary>
internal sealed class FakeGuardianEnvironment : IGuardianEnvironment
{
    public GuardianWake Wake { get; set; } = GuardianWake.ParentExited;

    public bool ParentExitsAfterBrokenPipe { get; set; } = true;

    public ImmutableArray<DateTimeOffset> Crashes { get; set; } = [];

    public bool CanRelaunch { get; set; } = true;

    public List<ImmutableArray<string>> Relaunches { get; } = [];

    /// <summary>The pauses between two attempts of a refused release.</summary>
    public List<TimeSpan> Waits { get; } = [];

    /// <summary>The clock a pause advances (the loop's own), if any.</summary>
    public FakeTimeProvider? Time { get; set; }

    /// <summary>Where the relaunch is written, to check its order against the sends.</summary>
    public List<string>? Log { get; set; }

    public GuardianWake WaitForParentOrPipe() => Wake;

    public bool WaitForParentExit(TimeSpan timeout) => ParentExitsAfterBrokenPipe;

    public void WaitBeforeRetry(TimeSpan interval)
    {
        Waits.Add(interval);
        Time?.Advance(interval);
    }

    public ImmutableArray<DateTimeOffset> RecentCrashes() => Crashes;

    public bool Relaunch(ImmutableArray<string> arguments)
    {
        Relaunches.Add(arguments);
        Log?.Add("relaunch");
        return CanRelaunch;
    }
}
