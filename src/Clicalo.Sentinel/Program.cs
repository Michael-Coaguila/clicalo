namespace Clicalo.Sentinel;

/// <summary>
/// Guardian process: when Clicalo.exe ends, releases every key and button Windows reports down and, after an abnormal
/// exit, relaunches it (ADR-0022, blueprint §3.1). Started only by Clicalo.exe with the arguments and the inherited
/// parent handle of <c>SentinelStartInfo</c>.
/// </summary>
internal static class Program
{
    private static int Main(string[] args) => SentinelEntryPoint.Run(args);
}
