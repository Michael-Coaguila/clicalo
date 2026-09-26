namespace Clicalo.Build;

/// <summary>
/// The M0 verbs of <c>cl</c> (blueprint §13), composed from <see cref="BuildSteps"/>. <c>devCliArguments</c>
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
        await steps.TestAsync(layout.CoreFilter, BuildMode.Debug, TestSelection.WithoutDesktop);
    }

    /// <summary><c>cl test</c>: every test that runs headless.</summary>
    public async Task TestAsync()
    {
        await steps.BuildAsync(layout.Solution, BuildMode.Debug);
        await steps.TestAsync(layout.Solution, BuildMode.Debug, TestSelection.WithoutDesktop);
    }

    /// <summary><c>cl desk</c>: only the tests that need an interactive desktop.</summary>
    public async Task DeskAsync()
    {
        await steps.BuildAsync(layout.Solution, BuildMode.Debug);
        await steps.TestAsync(layout.Solution, BuildMode.Debug, TestSelection.DesktopOnly);
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
        await steps.TestAsync(layout.Solution, BuildMode.ReleaseGate, TestSelection.WithoutDesktop);
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
}
