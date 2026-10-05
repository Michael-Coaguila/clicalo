using System.Collections.Immutable;

namespace Clicalo.Sentinel.Tests;

/// <summary>The machine as the guardian loop sees it, without any process or file.</summary>
internal sealed class FakeGuardianEnvironment : IGuardianEnvironment
{
    public int? ParentExitCode { get; set; } = -1;

    public ImmutableArray<DateTimeOffset> Crashes { get; set; } = [];

    public bool CanRelaunch { get; set; } = true;

    public List<ImmutableArray<string>> Relaunches { get; } = [];

    /// <summary>The pauses between two attempts of a refused release.</summary>
    public List<TimeSpan> Waits { get; } = [];

    /// <summary>Runs at each pause (the session is unlocked, for example).</summary>
    public Action<int>? OnWait { get; set; }

    /// <summary>Where the relaunch is written, to check its order against the sends.</summary>
    public List<string>? Log { get; set; }

    public int? WaitForParentExit() => ParentExitCode;

    public void WaitBeforeRetry(TimeSpan interval)
    {
        Waits.Add(interval);
        OnWait?.Invoke(Waits.Count);
    }

    public ImmutableArray<DateTimeOffset> RecentCrashes() => Crashes;

    public bool Relaunch(ImmutableArray<string> arguments)
    {
        Relaunches.Add(arguments);
        Log?.Add("relaunch");
        return CanRelaunch;
    }
}
