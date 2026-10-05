namespace Clicalo.Build.Tests;

public sealed class TestFilterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cl_check_and_cl_test_run_only_the_deterministic_tier(bool ci) =>
        BuildSteps
            .SelectionArguments(TestSelection.Deterministic, ci)
            .ShouldBe([
                "--filter-not-trait",
                "Requires=Desktop",
                "--filter-not-trait",
                "Category=Chaos",
                "--filter-not-trait",
                "Category=Perf",
                "--filter-not-trait",
                "Category=Quarantine",
            ]);

    [Fact]
    public void Cl_desk_on_the_maintainer_machine_never_injects_reserved_keys_nor_runs_chaos() =>
        BuildSteps
            .SelectionArguments(TestSelection.DesktopOnly, ci: false)
            .ShouldBe([
                "--filter-trait",
                "Requires=Desktop",
                "--filter-not-trait",
                "Category=Perf",
                "--filter-not-trait",
                "Category=Quarantine",
                "--filter-not-trait",
                "Injects=ReservedKeys",
                "--filter-not-trait",
                "Category=Chaos",
                "--max-parallel-test-modules",
                "1",
            ]);

    [Fact]
    public void Cl_desk_in_continuous_integration_runs_every_desktop_test_but_the_measurements_and_the_quarantine() =>
        BuildSteps
            .SelectionArguments(TestSelection.DesktopOnly, ci: true)
            .ShouldBe([
                "--filter-trait",
                "Requires=Desktop",
                "--filter-not-trait",
                "Category=Perf",
                "--filter-not-trait",
                "Category=Quarantine",
                "--max-parallel-test-modules",
                "1",
            ]);

    [Fact]
    public void Cl_perf_runs_only_the_measurements_and_never_chaos_or_reserved_keys_locally()
    {
        BuildSteps
            .SelectionArguments(TestSelection.PerfOnly, ci: false)
            .ShouldBe([
                "--filter-trait",
                "Category=Perf",
                "--filter-not-trait",
                "Injects=ReservedKeys",
                "--filter-not-trait",
                "Category=Chaos",
                "--max-parallel-test-modules",
                "1",
            ]);
        BuildSteps
            .SelectionArguments(TestSelection.PerfOnly, ci: true)
            .ShouldBe(["--filter-trait", "Category=Perf", "--max-parallel-test-modules", "1"]);
    }

    [Fact]
    public void Cl_quarantine_runs_only_the_quarantined_tests_and_never_chaos_or_reserved_keys_locally()
    {
        BuildSteps
            .SelectionArguments(TestSelection.QuarantineOnly, ci: false)
            .ShouldBe([
                "--filter-trait",
                "Category=Quarantine",
                "--filter-not-trait",
                "Category=Perf",
                "--filter-not-trait",
                "Injects=ReservedKeys",
                "--filter-not-trait",
                "Category=Chaos",
                "--max-parallel-test-modules",
                "1",
            ]);
        BuildSteps
            .SelectionArguments(TestSelection.QuarantineOnly, ci: true)
            .ShouldBe([
                "--filter-trait",
                "Category=Quarantine",
                "--filter-not-trait",
                "Category=Perf",
                "--max-parallel-test-modules",
                "1",
            ]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Desktop_test_modules_never_run_at_the_same_time(bool ci)
    {
        foreach (
            var selection in new[]
            {
                TestSelection.DesktopOnly,
                TestSelection.PerfOnly,
                TestSelection.QuarantineOnly,
            }
        )
        {
            var arguments = BuildSteps.SelectionArguments(selection, ci);

            arguments[Array.IndexOf(arguments, "--max-parallel-test-modules") + 1].ShouldBe("1");
        }
    }
}
