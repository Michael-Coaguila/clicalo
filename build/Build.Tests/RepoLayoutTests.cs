namespace Clicalo.Build.Tests;

public sealed class RepoLayoutTests
{
    [Fact]
    public void Finds_the_root_from_the_build_output()
    {
        var layout = RepoLayout.Locate(AppContext.BaseDirectory);

        File.Exists(layout.Solution).ShouldBeTrue();
        File.Exists(layout.CoreFilter).ShouldBeTrue();
        layout.LastErrorFile.ShouldBe(
            Path.Combine(layout.Root, "artifacts", "cl", "last-error.md")
        );
    }

    [Fact]
    public void Paths_are_relative_to_the_root_as_they_are_read_aloud()
    {
        var root = Path.Combine(Path.GetTempPath(), "repo");
        var layout = RepoLayout.FromRoot(root + Path.DirectorySeparatorChar);

        layout
            .Relative(layout.LastErrorFile)
            .ShouldBe(Path.Combine("artifacts", "cl", "last-error.md"));
        layout.RelativeForward(layout.LastErrorFile).ShouldBe("artifacts/cl/last-error.md");
    }

    [Fact]
    public void Outside_a_repository_the_error_says_what_is_missing()
    {
        var outside = Path.GetPathRoot(Path.GetTempPath())!;

        Should
            .Throw<InvalidOperationException>(() => RepoLayout.Locate(outside))
            .Message.ShouldContain(RepoLayout.SolutionFileName);
    }
}
