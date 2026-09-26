namespace Clicalo.Build.Tests;

public sealed class TestFilterTests
{
    [Fact]
    public void Cl_check_and_cl_test_leave_every_desktop_test_out() =>
        BuildSteps
            .FilterArguments(TestSelection.WithoutDesktop, ci: false)
            .ShouldBe(["--filter-not-trait", "Requires=Desktop"]);

    [Fact]
    public void Cl_desk_on_the_maintainer_machine_never_injects_the_reserved_keys() =>
        BuildSteps
            .FilterArguments(TestSelection.DesktopOnly, ci: false)
            .ShouldBe([
                "--filter-trait",
                "Requires=Desktop",
                "--filter-not-trait",
                "Injects=ReservedKeys",
            ]);

    [Fact]
    public void Cl_desk_in_continuous_integration_runs_every_desktop_test() =>
        BuildSteps
            .FilterArguments(TestSelection.DesktopOnly, ci: true)
            .ShouldBe(["--filter-trait", "Requires=Desktop"]);
}
