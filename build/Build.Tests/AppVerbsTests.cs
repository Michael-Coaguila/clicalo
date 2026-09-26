namespace Clicalo.Build.Tests;

/// <summary>The pure parts of the M2 verbs: <c>cl note</c> names and fills its fragment, <c>cl perf</c> its variants.</summary>
public sealed class AppVerbsTests
{
    [Theory]
    [InlineData("m2/app", "m2-app")]
    [InlineData("feat/Panel_Tray", "feat-panel-tray")]
    [InlineData("  fix//double--slash  ", "fix-double-slash")]
    public void A_note_is_named_after_its_branch(string branch, string expected) =>
        BuildSteps.NoteName(branch).ShouldBe(expected);

    [Theory]
    [InlineData("")]
    [InlineData("HEAD")]
    [InlineData("main")]
    public void Without_a_feature_branch_a_note_is_named_after_the_moment(string branch) =>
        BuildSteps.NoteName(branch).ShouldStartWith("note-");

    [Fact]
    public void The_note_template_asks_for_one_sentence_in_Spanish_and_one_in_English()
    {
        var lines = BuildSteps.NoteTemplate.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        lines.ShouldContain("es: \"\"", StringComparer.Ordinal);
        lines.ShouldContain("en: \"\"", StringComparer.Ordinal);
        lines.ShouldContain(line => line.StartsWith("type: feat", StringComparison.Ordinal));
        BuildSteps.NoteTemplate.ShouldNotContain("\r");
    }

    [Fact]
    public void Cl_perf_publishes_the_three_variants_of_S5()
    {
        PublishVariant
            .All.Select(variant => variant.Name)
            .ShouldBe(["sc-r2r", "sc-r2r-composite", "fdd"]);
        PublishVariant
            .All[0]
            .Properties.ShouldBe([
                "--self-contained",
                "true",
                "-p:PublishReadyToRun=true",
                "-p:PublishReadyToRunComposite=false",
            ]);
        PublishVariant
            .All[2]
            .Properties.ShouldContain("-p:PublishReadyToRun=false", StringComparer.Ordinal);
        PublishVariant
            .All.Where(variant => variant.Composite)
            .ShouldAllBe(variant => variant.SelfContained);
    }

    [Fact]
    public void The_measurements_receive_every_published_executable()
    {
        var apps = BuildSteps.PerfApps(PublishVariant.All, Path.Combine("C:", "perf"));

        apps.Split(';')
            .Select(entry => entry.Split('=')[0])
            .ShouldBe(["sc-r2r", "sc-r2r-composite", "fdd"]);
        apps.ShouldContain(Path.Combine("C:", "perf", "fdd", "Clicalo.exe"));
    }

    [Fact]
    public void The_measurements_run_on_the_architecture_of_the_machine() =>
        BuildSteps.RuntimeIdentifier.ShouldBeOneOf("win-x64", "win-arm64");
}
