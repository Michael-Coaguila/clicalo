namespace Clicalo.Build.Tests;

public sealed class TestFilterTests
{
    [Fact]
    public void Cl_check_and_cl_test_leave_every_desktop_test_out() =>
        BuildSteps
            .SelectionArguments(TestSelection.WithoutDesktop, ci: false)
            .ShouldBe(["--filter-not-trait", "Requires=Desktop"]);

    [Fact]
    public void Cl_desk_on_the_maintainer_machine_never_injects_the_reserved_keys() =>
        BuildSteps
            .SelectionArguments(TestSelection.DesktopOnly, ci: false)
            .ShouldBe([
                "--filter-trait",
                "Requires=Desktop",
                "--filter-not-trait",
                "Injects=ReservedKeys",
                "--max-parallel-test-modules",
                "1",
            ]);

    [Fact]
    public void Cl_desk_in_continuous_integration_runs_every_desktop_test() =>
        BuildSteps
            .SelectionArguments(TestSelection.DesktopOnly, ci: true)
            .ShouldBe(["--filter-trait", "Requires=Desktop", "--max-parallel-test-modules", "1"]);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Desktop_test_modules_never_run_at_the_same_time(bool ci)
    {
        var arguments = BuildSteps.SelectionArguments(TestSelection.DesktopOnly, ci);

        arguments[Array.IndexOf(arguments, "--max-parallel-test-modules") + 1].ShouldBe("1");
    }
}
