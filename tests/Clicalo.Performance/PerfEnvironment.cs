using Clicalo.TestKit.Windows;

namespace Clicalo.Performance;

/// <summary>
/// What <c>cl perf</c> tells the measurements (blueprint §10.3, spike S5). The measurements start <c>Clicalo.exe</c> as a
/// process, never by reference; they run only through <c>cl perf</c> (the variables below) on an interactive desktop.
/// On a developer's machine every start uses <c>--no-input</c>: nothing is ever injected there. Key sending (and with
/// it Sentinel and the touch → SendInput measurement) is used only in continuous integration.
/// </summary>
public static class PerfEnvironment
{
    /// <summary>The published variants: <c>name=path;name=path</c>.</summary>
    public const string AppsVariable = "CLICALO_PERF_APPS";

    /// <summary>Where the artifacts go (<c>artifacts/perf</c>).</summary>
    public const string ResultsVariable = "CLICALO_PERF_RESULTS";

    /// <summary>How many starts per variant; 6 by default.</summary>
    public const string StartsVariable = "CLICALO_PERF_STARTS";

    /// <summary><c>1</c> on the touch laboratory: the budgets fail the run instead of only being reported.</summary>
    public const string GateVariable = "CLICALO_PERF_GATE";

    /// <summary>The reason a measurement is skipped outside <c>cl perf</c>.</summary>
    public const string SkipReason =
        "Performance measurements run only through «cl perf» (CLICALO_PERF_APPS and an interactive desktop).";

    /// <summary>Whether this run was started by <c>cl perf</c> on an interactive desktop.</summary>
    public static bool IsEnabled => DesktopTestEnvironment.IsEnabled && Variants.Count > 0;

    /// <summary>The published variants.</summary>
    internal static IReadOnlyList<AppVariant> Variants =>
        AppVariant.Parse(Environment.GetEnvironmentVariable(AppsVariable));

    /// <summary>Whether Clicalo.exe may send keys: only in continuous integration.</summary>
    internal static bool SendInput => DesktopTestEnvironment.IsContinuousIntegration;

    /// <summary>Whether the budgets gate the run (the touch laboratory).</summary>
    internal static bool Gate =>
        string.Equals(
            Environment.GetEnvironmentVariable(GateVariable),
            "1",
            StringComparison.Ordinal
        );

    /// <summary>Starts per variant.</summary>
    internal static int Starts =>
        int.TryParse(
            Environment.GetEnvironmentVariable(StartsVariable),
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out var starts
        )
        && starts > 0
            ? starts
            : 6;

    /// <summary>The artifacts folder, created on demand.</summary>
    internal static string ResultsDirectory
    {
        get
        {
            var folder = Environment.GetEnvironmentVariable(ResultsVariable)
                is { Length: > 0 } configured
                ? configured
                : Path.Combine(Path.GetTempPath(), "clicalo-perf");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
