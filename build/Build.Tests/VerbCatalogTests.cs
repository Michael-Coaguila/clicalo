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
            verb.ShouldMatch("^[a-z]+(-[a-z]+)?$");
        }
    }
}
