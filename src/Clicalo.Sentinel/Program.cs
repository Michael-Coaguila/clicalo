namespace Clicalo.Sentinel;

/// <summary>
/// Guardian process: releases every key recorded in the KeyLedger when Clicalo.exe dies (ADR-0004, blueprint §3.1).
/// Started only by Clicalo.exe with the arguments and the three inherited handles of <c>SentinelStartInfo</c>.
/// </summary>
internal static class Program
{
    private static int Main(string[] args) => SentinelEntryPoint.Run(args);
}
