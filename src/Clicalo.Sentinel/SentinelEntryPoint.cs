using Clicalo.Platform.Core.Guardian;
using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;

namespace Clicalo.Sentinel;

/// <summary>
/// Sentinel's start-up: parses <see cref="SentinelStartInfo"/>, maps the inherited ledger and runs
/// <see cref="GuardianLoop"/>. <c>Program.Main</c> calls it.
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

        if (!KeyLedgerSection.TryOpenInherited(startInfo.Ledger, out var ledger))
        {
            return (int)SentinelExitCode.LedgerUnreadable;
        }

        using (ledger)
        {
            var loop = new GuardianLoop(
                startInfo,
                ledger!,
                new LowLevelInjector(),
                TimeProvider.System
            );
            return (int)loop.Run();
        }
    }
}
