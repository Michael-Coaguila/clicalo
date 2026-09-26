namespace Clicalo.Build.Tests;

public sealed class VerbCatalogTests
{
    /// <summary>The flow of blueprint §13: every dictable verb, one word each.</summary>
    private static readonly string[] BlueprintVerbs =
    [
        "setup",
        "build",
        "fast",
        "test",
        "desk",
        "fix",
        "check",
        "run",
        "states",
        "accept",
        "trace",
        "note",
        "pr",
        "beta",
        "perf",
        "sign-manifest",
    ];

    [Fact]
    public void Every_blueprint_verb_is_either_available_or_planned()
    {
        foreach (var verb in BlueprintVerbs)
        {
            (
                VerbCatalog.IsAvailable(verb) || VerbCatalog.FindFuture(verb) is not null
            ).ShouldBeTrue(verb);
        }
    }

    [Fact]
    public void Available_and_planned_verbs_do_not_overlap() =>
        VerbCatalog
            .Available.Intersect(
                VerbCatalog.Future.Select(verb => verb.Name),
                StringComparer.Ordinal
            )
            .ShouldBeEmpty();

    [Fact]
    public void M0_delivers_the_inner_loop_and_the_gate() =>
        VerbCatalog.Available.ShouldBe([
            "setup",
            "build",
            "fast",
            "test",
            "desk",
            "fix",
            "check",
            "clean",
            "i18n-check",
            "i18n-import",
            "adr-check",
        ]);

    [Fact]
    public void Planned_verbs_name_a_later_milestone_of_the_roadmap()
    {
        foreach (var verb in VerbCatalog.Future)
        {
            verb.Milestone.ShouldMatch("^M[1-7]$", verb.Name);
        }
    }

    [Fact]
    public void Verbs_are_single_dictable_words()
    {
        foreach (var verb in VerbCatalog.Available.Concat(VerbCatalog.Future.Select(f => f.Name)))
        {
            // "i18n" is dictated as it is written in the documentation.
            verb.ShouldMatch("^[a-z][a-z0-9]*(-[a-z]+)?$");
        }
    }

    [Fact]
    public void Everything_after_the_first_developer_cli_verb_goes_to_the_developer_cli()
    {
        var (cl, devCli) = VerbCatalog.SplitArguments([
            "--verbose",
            "i18n-check",
            "--strict-unused",
        ]);

        cl.ShouldBe(["--verbose", "i18n-check"]);
        devCli.ShouldBe(["--strict-unused"]);
    }

    [Fact]
    public void Without_a_developer_cli_verb_every_argument_stays_with_cl()
    {
        var (cl, devCli) = VerbCatalog.SplitArguments(["check", "--dry-run"]);

        cl.ShouldBe(["check", "--dry-run"]);
        devCli.ShouldBeEmpty();
    }

    [Fact]
    public void The_developer_cli_verbs_are_the_verbs_of_tools_Clicalo_DevCli() =>
        VerbCatalog.DevCli.ShouldBe(["i18n-check", "i18n-import", "adr-check"]);
}
