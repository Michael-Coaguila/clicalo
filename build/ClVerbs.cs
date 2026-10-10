namespace Clicalo.Build;

/// <summary>
/// The verbs of <c>cl</c> available so far (blueprint §13: M0 and M2), composed from <see cref="BuildSteps"/>. <c>devCliArguments</c>
/// are the words written after a developer CLI verb, passed to it unchanged.
/// </summary>
internal sealed class ClVerbs(
    BuildSteps steps,
    RepoLayout layout,
    IReadOnlyCollection<string> keepOnClean,
    IReadOnlyList<string> devCliArguments
)
{
    /// <summary><c>cl setup</c>: local tools, git settings, DCO hook and signing check.</summary>
    public async Task SetupAsync()
    {
        await steps.ToolRestoreAsync();
        await steps.GitAsync();
    }

    /// <summary><c>cl build</c>: the whole solution in Debug.</summary>
    public Task BuildAsync() => steps.BuildAsync(layout.Solution, BuildMode.Debug);

    /// <summary><c>cl fast</c>: the portable core and its tests (Core.slnf).</summary>
    public async Task FastAsync()
    {
        await steps.BuildAsync(layout.CoreFilter, BuildMode.Debug);
        await steps.TestAsync(layout.CoreFilter, BuildMode.Debug, TestSelection.Deterministic);
    }

    /// <summary><c>cl test</c>: every deterministic test (the pull request tier of <see cref="TestSelection"/>).</summary>
    public async Task TestAsync()
    {
        await steps.BuildAsync(layout.Solution, BuildMode.Debug);
        await steps.TestAsync(layout.Solution, BuildMode.Debug, TestSelection.Deterministic);
    }

    /// <summary><c>cl desk</c>: only the tests that need an interactive desktop.</summary>
    public async Task DeskAsync()
    {
        await steps.BuildAsync(layout.Solution, BuildMode.Debug);
        await steps.TestAsync(layout.Solution, BuildMode.Debug, TestSelection.DesktopOnly);
    }

    /// <summary><c>cl quarantine</c>: only the quarantined tests, headless or on the desktop (nightly.yml).</summary>
    public async Task QuarantineAsync()
    {
        await steps.BuildAsync(layout.Solution, BuildMode.Debug);
        await steps.TestAsync(layout.Solution, BuildMode.Debug, TestSelection.QuarantineOnly);
    }

    /// <summary><c>cl fix</c>: formats C# with CSharpier.</summary>
    public async Task FixAsync()
    {
        await steps.ToolRestoreAsync();
        await steps.FormatFixAsync();
    }

    /// <summary><c>cl check</c>: exactly what the <c>verify</c> job of pr.yml runs.</summary>
    public async Task CheckAsync()
    {
        await steps.PinsAsync();
        await steps.ToolRestoreAsync();
        await steps.FormatCheckAsync();
        await steps.RestoreLockedAsync();
        await steps.BuildAsync(layout.Solution, BuildMode.ReleaseGate);
        await steps.TestAsync(layout.Solution, BuildMode.ReleaseGate, TestSelection.Deterministic);
        await steps.I18nCheckAsync();
    }

    /// <summary><c>cl clean</c>: empties artifacts.</summary>
    public Task CleanAsync() => steps.CleanAsync(keepOnClean);

    /// <summary><c>cl i18n-check [--strict-unused]</c>: the i18n validation of the developer CLI.</summary>
    public Task I18nCheckAsync() => steps.DevCliAsync(VerbCatalog.I18nCheck, devCliArguments);

    /// <summary><c>cl i18n-import [--check]</c>: rebuilds (or compares) data/i18n from the reviewed recipe.</summary>
    public Task I18nImportAsync() => steps.DevCliAsync(VerbCatalog.I18nImport, devCliArguments);

    /// <summary><c>cl adr-check --base &lt;ref&gt;</c>: the <c>adr</c> job of pr.yml.</summary>
    public Task AdrCheckAsync() => steps.DevCliAsync(VerbCatalog.AdrCheck, devCliArguments);

    /// <summary>
    /// <c>cl trace</c>: writes <c>artifacts/cl/trace.md</c>, every requirement of the catalog with its tests, and says
    /// how many MUST requirements have neither a test nor a line in the manual acceptance script.
    /// </summary>
    public async Task TraceAsync()
    {
        await steps.DevCliAsync(VerbCatalog.Trace, devCliArguments);
        steps.NoteTraceReport();
    }

    /// <summary>
    /// <c>cl run</c>: builds the app and starts it with its data isolated in <c>%TEMP%\clicalo-dev</c> and without key
    /// sending (<c>--no-input</c>).
    /// </summary>
    public async Task RunAppAsync()
    {
        await steps.BuildAsync(Path.Combine(layout.Root, BuildSteps.AppProject), BuildMode.Debug);
        await steps.LaunchAsync(Path.Combine(Path.GetTempPath(), "clicalo-dev"));
    }

    /// <summary><c>cl note</c>: the user-facing news fragment of the branch, in Spanish and English.</summary>
    public Task NoteAsync() => steps.NoteAsync();

    /// <summary>
    /// <c>cl perf</c>: publishes the S5 variants, builds the measurements and runs them on the desktop; the numbers go to
    /// <c>artifacts/perf</c> (<c>s5.json</c>, <c>s5.md</c>).
    /// </summary>
    public async Task PerfAsync()
    {
        var output = Path.Combine(layout.Artifacts, "perf");
        var apps = Path.Combine(output, "apps");
        await steps.PublishAsync(PublishVariant.All, apps);
        var project = Path.Combine(layout.Root, BuildSteps.PerformanceProject);
        await steps.BuildAsync(project, BuildMode.Debug);
        await steps.TestAsync(
            project,
            BuildMode.Debug,
            TestSelection.PerfOnly,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["CLICALO_PERF_APPS"] = BuildSteps.PerfApps(PublishVariant.All, apps),
                ["CLICALO_PERF_RESULTS"] = output,
            }
        );
        steps.NotePerfResults(output);
    }

    /// <summary>
    /// <c>cl package [--channel stable|beta] [--version X.Y.Z[-beta.N]]</c>: publishes Clícalo and Sentinel
    /// self-contained for this machine's runtime and packs them with Velopack into <c>artifacts/package</c>
    /// (<c>Setup.exe</c>, the packages and the release feed). It never publishes anything (docs/guides/release.md).
    /// </summary>
    public async Task PackageAsync()
    {
        await steps.ToolRestoreAsync();
        await steps.PackageAsync(devCliArguments);
    }
}
