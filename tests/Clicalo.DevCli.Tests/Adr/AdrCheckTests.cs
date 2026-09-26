using System.Collections.Immutable;
using Clicalo.DevCli.Adr;
using Clicalo.TestKit;

namespace Clicalo.DevCli.Tests.Adr;

/// <summary>Blueprint §13: a change to a sensitive path needs an ADR in the same pull request.</summary>
public sealed class AdrCheckTests
{
    private static readonly ImmutableArray<SensitivePath> Sensitive =
    [
        new(new PathGlob("src/Clicalo.Launcher/**"), "trust-boundary", "Elevated launcher."),
        new(new PathGlob("nuget.config"), "signing", "Trusted signers."),
        new(new PathGlob(".github/workflows/release*.yml"), "signing", "Code signing."),
    ];

    [Theory]
    [InlineData("src/Clicalo.Launcher/**", "src/Clicalo.Launcher/Program.cs", true)]
    [InlineData("src/Clicalo.Launcher/**", "src/Clicalo.Launcher/Deep/Er/File.cs", true)]
    [InlineData("src/Clicalo.Launcher/**", "src/Clicalo.LauncherX/Program.cs", false)]
    [InlineData("data/schemas/**", "DATA/Schemas/seed.schema.json", true)]
    [InlineData(".github/workflows/release*.yml", ".github/workflows/release-beta.yml", true)]
    [InlineData(".github/workflows/release*.yml", ".github/workflows/nested/release.yml", false)]
    [InlineData("nuget.config", "src\\nuget.config", false)]
    [InlineData("src/**/app.manifest", "src/app.manifest", true)]
    public void Globs_match_like_the_architecture_registries(
        string pattern,
        string path,
        bool matches
    ) => new PathGlob(pattern).IsMatch(path).ShouldBe(matches);

    [Theory]
    [InlineData("docs/adr/0018-nuevo-firmante.md", true)]
    [InlineData("docs\\adr\\0005-superficies.md", true)]
    [InlineData("docs/adr/0000-template.md", false)]
    [InlineData("docs/adr/README.md", false)]
    [InlineData("docs/adr/drafts/0019-x.md", false)]
    [InlineData("docs/architecture/0001-x.md", false)]
    public void Only_numbered_ADRs_count(string path, bool isAdr) =>
        AdrCheck.IsAdr(path).ShouldBe(isAdr);

    [Fact]
    public void A_change_outside_the_sensitive_paths_passes()
    {
        var result = AdrCheck.Evaluate(
            Sensitive,
            ["src/Clicalo.Domain/Keys/KeyId.cs", "README.md"]
        );

        result.Touched.ShouldBeEmpty();
        result.Passed.ShouldBeTrue();
        Report(result, 2)
            .ShouldBe((ExitCodes.Success, "adr-check: no sensitive path changed (2 files)."));
    }

    [Fact]
    public void A_sensitive_change_without_an_ADR_fails_at_each_file()
    {
        var result = AdrCheck.Evaluate(
            Sensitive,
            ["nuget.config", "src/Clicalo.Launcher/Program.cs", "docs/adr/README.md"]
        );

        result.Passed.ShouldBeFalse();
        var (code, lines) = ReportLines(result, 3);
        code.ShouldBe(ExitCodes.Failure);
        lines.ShouldBe([
            "nuget.config: error CLCA010: 'nuget.config' (signing) needs a new or changed ADR under docs/adr/. Trusted signers.",
            "src/Clicalo.Launcher/Program.cs: error CLCA010: 'src/Clicalo.Launcher/**' (trust-boundary) needs a new or changed ADR under docs/adr/. Elevated launcher.",
            "adr-check: 2 files in sensitive paths changed without an ADR. Add or update docs/adr/NNNN-*.md.",
        ]);
    }

    [Fact]
    public void A_sensitive_change_with_an_ADR_passes()
    {
        var result = AdrCheck.Evaluate(
            Sensitive,
            ["nuget.config", "docs/adr/0018-nuevo-firmante.md"]
        );

        result.Passed.ShouldBeTrue();
        Report(result, 2)
            .ShouldBe(
                (ExitCodes.Success, "adr-check: 1 file in a sensitive path changed with an ADR.")
            );
    }

    [Fact]
    public void The_repository_registry_parses_and_protects_itself()
    {
        var registry = SensitivePaths.Parse(
            File.ReadAllText(RepoPaths.Combine("architecture", "sensitive-paths.json"))
        );

        registry.ShouldNotBeEmpty();
        registry.ShouldContain(p => p.Glob.IsMatch("architecture/sensitive-paths.json"));
        registry.ShouldContain(p => p.Glob.IsMatch("nuget.config"));
    }

    [Theory]
    [InlineData("[]", "the root needs a 'paths' array.")]
    [InlineData(
        "{ \"paths\": [ { \"pattern\": \"LICENSE\", \"category\": \"license\" } ] }",
        "paths[0] needs a non-empty 'reason'."
    )]
    [InlineData("{ \"paths\": ", "invalid JSON")]
    public void An_invalid_registry_is_reported(string json, string message) =>
        Should
            .Throw<InvalidDataException>(() => SensitivePaths.Parse(json))
            .Message.ShouldContain(message);

    private static (int Code, string Line) Report(AdrCheckResult result, int changed)
    {
        var (code, lines) = ReportLines(result, changed);
        return (code, lines.ShouldHaveSingleItem());
    }

    private static (int Code, string[] Lines) ReportLines(AdrCheckResult result, int changed)
    {
        using var output = new StringWriter();
        var code = AdrCheckCommand.Report(result, changed, output);
        return (
            code,
            output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
        );
    }
}
