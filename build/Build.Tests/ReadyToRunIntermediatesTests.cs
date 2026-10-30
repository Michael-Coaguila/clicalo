namespace Clicalo.Build.Tests;

/// <summary>
/// Every publish of <c>cl perf</c> and <c>cl package</c> starts without the crossgen output of the last one: the SDK
/// reuses it while it is newer than the assemblies, even when it was compiled for the composite variant, and a plain
/// ReadyToRun package would then fail fast at start without its <c>Clicalo.r2r.dll</c>.
/// </summary>
public sealed class ReadyToRunIntermediatesTests : IDisposable
{
    private readonly string _artifacts = Path.Combine(
        Path.GetTempPath(),
        "clicalo-r2r-" + Guid.NewGuid().ToString("N")
    );

    public void Dispose()
    {
        if (Directory.Exists(_artifacts))
        {
            Directory.Delete(_artifacts, recursive: true);
        }
    }

    [Fact]
    public void Only_the_ReadyToRun_folders_of_the_published_project_are_found()
    {
        var release = Folder("obj", "Clicalo.App", "release_win-x64", "R2R");
        var arm64 = Folder("obj", "Clicalo.App", "release_win-arm64", "R2R");
        Folder("obj", "Clicalo.App", "release_win-x64", "ref");
        Folder("obj", "Clicalo.App", "debug");
        Folder("obj", "Clicalo.Sentinel", "release_win-x64", "R2R");

        BuildSteps
            .ReadyToRunIntermediates(_artifacts, BuildSteps.AppProject)
            .ShouldBe([release, arm64], ignoreOrder: true);
    }

    [Fact]
    public void A_project_that_was_never_built_has_none() =>
        BuildSteps.ReadyToRunIntermediates(_artifacts, BuildSteps.SentinelProject).ShouldBeEmpty();

    private string Folder(params string[] parts)
    {
        var folder = Path.Combine([_artifacts, .. parts]);
        Directory.CreateDirectory(folder);
        return folder;
    }
}
