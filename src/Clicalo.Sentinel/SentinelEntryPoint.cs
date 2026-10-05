using Clicalo.Platform.Core.Guardian;
using Clicalo.Platform.Core.Injection;

namespace Clicalo.Sentinel;

/// <summary>
/// Sentinel's start-up: parses <see cref="SentinelStartInfo"/> and runs <see cref="GuardianLoop"/> over the real
/// machine. <c>Program.Main</c> calls it.
/// </summary>
internal static class SentinelEntryPoint
{
    /// <summary>Runs Sentinel and returns its <see cref="SentinelExitCode"/> as an integer.</summary>
    /// <param name="arguments">The command line arguments.</param>
    public static int Run(ReadOnlySpan<string> arguments)
    {
        if (!SentinelStartInfo.TryParse(arguments, out var startInfo))
        {
            return (int)SentinelExitCode.InvalidArguments;
        }

        var loop = new GuardianLoop(
            startInfo,
            SystemKeyState.Instance,
            new LowLevelInjector(),
            TimeProvider.System,
            new SystemGuardianEnvironment(startInfo, TimeProvider.System)
        );
        return (int)loop.Run();
    }
}
