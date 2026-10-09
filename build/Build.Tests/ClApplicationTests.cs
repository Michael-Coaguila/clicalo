namespace Clicalo.Build.Tests;

/// <summary>Paths of <c>cl</c> that decide what to say without starting any process.</summary>
public sealed class ClApplicationTests
{
    private const string Verbs =
        "setup, build, fast, test, desk, fix, check, clean, i18n-check, i18n-import, adr-check, run, note, perf, quarantine y package";

    private const string VerbList = "cl: las órdenes son " + Verbs;

    private static async Task<(int ExitCode, string Output)> RunAsync(params string[] args)
    {
        using var output = new StringWriter();
        var application = new ClApplication(
            RepoLayout.FromRoot(Path.Combine(Path.GetTempPath(), "clicalo-no-repo")),
            output,
            TimeProvider.System,
            []
        );

        var exitCode = await application.RunAsync(args);
        return (exitCode, output.ToString());
    }

    private static string LastLine(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[
            ^1
        ];

    [Fact]
    public async Task Without_a_verb_it_lists_the_verbs_and_ends_with_them_in_one_line()
    {
        var (exitCode, output) = await RunAsync();

        exitCode.ShouldBe(0);
        output.ShouldContain("  check ");
        output.ShouldContain("La misma puerta que la CI");
        output.ShouldContain("  i18n-import ");
        LastLine(output).ShouldBe(VerbList);
    }

    [Theory]
    [InlineData("trace", "M3")]
    [InlineData("states", "M3")]
    [InlineData("sign-manifest", "M5")]
    public async Task A_planned_verb_answers_its_milestone_and_fails(string verb, string milestone)
    {
        var (exitCode, output) = await RunAsync(verb);

        exitCode.ShouldBe(ClApplication.FailureExitCode);
        output.Trim().ShouldBe($"cl {verb}: disponible en {milestone}");
    }

    [Fact]
    public async Task An_unknown_verb_names_the_valid_ones()
    {
        var (exitCode, output) = await RunAsync("chek");

        exitCode.ShouldBe(ClApplication.UsageExitCode);
        output.Trim().ShouldBe("cl chek: orden desconocida; las órdenes son " + Verbs);
    }

    [Fact]
    public async Task Listing_targets_shows_planned_verbs_too()
    {
        var (exitCode, output) = await RunAsync("--list-targets");

        exitCode.ShouldBe(0);
        output.ShouldContain("sign-manifest");
        output.ShouldContain("Disponible en M5.");
        LastLine(output).ShouldBe(VerbList);
    }

    [Fact]
    public async Task A_dry_run_says_that_nothing_ran()
    {
        var (exitCode, output) = await RunAsync("check", "--dry-run");

        exitCode.ShouldBe(0);
        LastLine(output).ShouldBe("cl check: simulación sin ejecutar nada (--dry-run)");
    }

    [Fact]
    public async Task An_unknown_option_is_a_usage_error_in_one_line()
    {
        var (exitCode, output) = await RunAsync("check", "--no-such-option");

        exitCode.ShouldBe(ClApplication.UsageExitCode);
        LastLine(output).ShouldStartWith("cl check: uso no válido; ");
    }

    [Fact]
    public async Task Words_after_a_developer_cli_verb_are_its_options_not_options_of_cl()
    {
        var (exitCode, output) = await RunAsync("--dry-run", "i18n-import", "--check");

        exitCode.ShouldBe(0);
        LastLine(output)
            .ShouldBe("cl i18n-import --check: simulación sin ejecutar nada (--dry-run)");
    }

    [Fact]
    public async Task Listing_targets_shows_the_developer_cli_verbs()
    {
        var (exitCode, output) = await RunAsync("--list-targets");

        exitCode.ShouldBe(0);
        output.ShouldContain("i18n-check");
        output.ShouldContain("adr-check");
    }
}
