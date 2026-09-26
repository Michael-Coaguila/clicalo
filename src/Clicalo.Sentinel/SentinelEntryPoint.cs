using System.Diagnostics.CodeAnalysis;

namespace Clicalo.Sentinel;

/// <summary>
/// Sentinel's start-up: parses <see cref="Platform.Core.Guardian.SentinelStartInfo"/>, maps the inherited ledger and
/// runs <see cref="GuardianLoop"/>. <c>Program.Main</c> calls it once implemented.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the engine package implements it (docs/testing/spikes/M2-ownership.md)."
)]
internal static class SentinelEntryPoint
{
    /// <summary>Runs Sentinel and returns its <see cref="Platform.Core.Guardian.SentinelExitCode"/> as an integer.</summary>
    /// <param name="arguments">The command line arguments.</param>
    public static int Run(ReadOnlySpan<string> arguments) => throw new NotImplementedException();
}
