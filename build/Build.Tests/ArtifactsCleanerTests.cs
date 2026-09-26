namespace Clicalo.Build.Tests;

public sealed class ArtifactsCleanerTests : IDisposable
{
    private readonly string _artifacts = Path.Combine(
        Path.GetTempPath(),
        "clicalo-clean-" + Guid.NewGuid().ToString("N")
    );

    public void Dispose()
    {
        if (Directory.Exists(_artifacts))
        {
            Directory.Delete(_artifacts, recursive: true);
        }
    }

    [Fact]
    public async Task Deletes_everything_except_the_running_orchestrator()
    {
        var ownBin = Create("bin", "Build", "debug", "Build.dll");
        var ownObj = Create("obj", "Build", "project.assets.json");
        var otherBin = Create("bin", "Clicalo.Domain", "debug", "Clicalo.Domain.dll");
        var otherObj = Create("obj", "Clicalo.Domain", "project.assets.json");
        var report = Create("cl", "last-error.md");

        var failures = await ArtifactsCleaner.CleanAsync(
            _artifacts,
            [Path.Combine(_artifacts, "bin", "Build"), Path.Combine(_artifacts, "obj", "Build")]
        );

        failures.ShouldBeEmpty();
        File.Exists(ownBin).ShouldBeTrue();
        File.Exists(ownObj).ShouldBeTrue();
        File.Exists(otherBin).ShouldBeFalse();
        File.Exists(otherObj).ShouldBeFalse();
        File.Exists(report).ShouldBeFalse();
        Directory.Exists(Path.Combine(_artifacts, "cl")).ShouldBeFalse();
    }

    [Fact]
    public async Task A_missing_artifacts_folder_is_already_clean() =>
        (await ArtifactsCleaner.CleanAsync(_artifacts, [])).ShouldBeEmpty();

    [Fact]
    public async Task Files_in_use_are_reported_instead_of_failing_the_whole_clean()
    {
        var locked = Create("bin", "Clicalo.App", "debug", "Clicalo.dll");
        var free = Create("bin", "Clicalo.Domain", "debug", "Clicalo.Domain.dll");

        IReadOnlyList<string> failures;
        await using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            failures = await ArtifactsCleaner.CleanAsync(_artifacts, []);
        }

        if (OperatingSystem.IsWindows())
        {
            // Only Windows refuses to delete an open file; the report names exactly that file.
            failures.ShouldBe([Path.Combine("bin", "Clicalo.App", "debug", "Clicalo.dll")]);
        }

        File.Exists(free).ShouldBeFalse();
    }

    private string Create(params string[] parts)
    {
        var path = Path.Combine([_artifacts, .. parts]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }
}
