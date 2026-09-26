using System.Collections.Immutable;
using Clicalo.Platform.Core.Guardian;

namespace Clicalo.Sentinel.Tests;

/// <summary>The machine as the guardian loop sees it, without any process, pipe or file.</summary>
internal sealed class FakeGuardianEnvironment : IGuardianEnvironment
{
    public GuardianWake Wake { get; set; } = GuardianWake.ParentExited;

    public bool ParentExitsAfterBrokenPipe { get; set; } = true;

    public ImmutableArray<DateTimeOffset> Crashes { get; set; } = [];

    public bool CanRelaunch { get; set; } = true;

    public List<ImmutableArray<string>> Relaunches { get; } = [];

    public GuardianWake WaitForParentOrPipe() => Wake;

    public bool WaitForParentExit(TimeSpan timeout) => ParentExitsAfterBrokenPipe;

    public ImmutableArray<DateTimeOffset> RecentCrashes() => Crashes;

    public bool Relaunch(ImmutableArray<string> arguments)
    {
        Relaunches.Add(arguments);
        return CanRelaunch;
    }
}
