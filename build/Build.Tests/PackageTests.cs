namespace Clicalo.Build.Tests;

/// <summary>
/// <c>cl package</c> (ADR-0027): its options, the publish properties (self-contained, ReadyToRun, Sentinel without
/// Native AOT, never <c>-r</c>), the arguments of <c>vpk pack</c> and the release notes it writes.
/// </summary>
public sealed class PackageTests
{
    [Fact]
    public void The_defaults_are_the_stable_channel_and_the_version_of_the_repository()
    {
        Parse([]).ShouldBe(new PackageOptions("stable", "2.0.0", "win-x64"));
        Parse(["--channel", "beta"])
            .ShouldBe(new PackageOptions("beta", "2.0.0-beta.1", "win-x64"));
        Parse(["--channel", "BETA", "--version", "2.0.0-beta.3"])
            .ShouldBe(new PackageOptions("beta", "2.0.0-beta.3", "win-x64"));
    }

    [Fact]
    [Trait("Req", "NFR-011")]
    public void Arm64_is_packaged_on_request_in_a_channel_of_its_own()
    {
        var options = Parse(["--runtime", "WIN-ARM64"]);

        options.ShouldBe(new PackageOptions("stable", "2.0.0", "win-arm64"));
        options.PackChannel.ShouldBe("stable-arm64");
        Parse(["--channel", "beta", "--runtime", "win-arm64"]).PackChannel.ShouldBe("beta-arm64");
        Parse(["--runtime", "win-x64"]).PackChannel.ShouldBe("stable");
        BuildSteps
            .PublishArguments(BuildSteps.AppProject, "out", [], options.Runtime)
            .ShouldContain("-p:ClicaloRuntimeIdentifier=win-arm64", StringComparer.Ordinal);
        var pack = BuildSteps.PackArguments(options, "pub", "out", "notes.md");
        pack[pack.IndexOf("--channel") + 1].ShouldBe("stable-arm64");
        pack[pack.IndexOf("--runtime") + 1].ShouldBe("win-arm64");
    }

    [Theory]
    [InlineData("--channel", "nightly")]
    [InlineData("--version", "2.0")]
    [InlineData("--version", "v2.0.0")]
    [InlineData("--runtime", "linux-x64")]
    [InlineData("--sign", "yes")]
    public void Wrong_options_are_refused_with_the_usage(string option, string value)
    {
        PackageOptions
            .TryParse([option, value], "2.0.0", "win-x64", out var options, out var error)
            .ShouldBeFalse();
        options.ShouldBeNull();
        error.ShouldContain("cl package");
    }

    private static PackageOptions Parse(string[] args)
    {
        PackageOptions
            .TryParse(args, "2.0.0", "win-x64", out var options, out var error)
            .ShouldBeTrue(error);
        return options!;
    }

    [Fact]
    public void The_version_prefix_comes_from_Directory_Build_props() =>
        PackageOptions
            .VersionPrefixOf(
                "<Project><PropertyGroup><VersionPrefix>2.1.0</VersionPrefix></PropertyGroup></Project>"
            )
            .ShouldBe("2.1.0");

    [Fact]
    public void Clicalo_and_Sentinel_are_published_self_contained_and_Sentinel_without_Native_AOT()
    {
        var options = new PackageOptions("beta", "2.0.0-beta.1", "win-x64");

        BuildSteps
            .PackageProperties(options, aot: null)
            .ShouldBe([
                "--self-contained",
                "true",
                "-p:PublishReadyToRun=true",
                "-p:Version=2.0.0-beta.1",
            ]);
        BuildSteps
            .PackageProperties(options, aot: false)
            .ShouldContain("-p:PublishAot=false", StringComparer.Ordinal);
        var publish = BuildSteps.PublishArguments(
            BuildSteps.SentinelProject,
            "out",
            BuildSteps.PackageProperties(options, aot: false)
        );
        publish.ShouldNotContain("-r", StringComparer.Ordinal);
        publish.ShouldContain(
            "-p:ClicaloRuntimeIdentifier=" + BuildSteps.RuntimeIdentifier,
            StringComparer.Ordinal
        );
    }

    [Fact]
    public void Velopack_packs_the_channel_with_the_package_id_of_the_installation() =>
        BuildSteps
            .PackArguments(
                new PackageOptions("beta", "2.0.0-beta.1", "win-x64"),
                "pub",
                "out",
                "notes.md"
            )
            .ShouldBe([
                "vpk",
                "pack",
                "--packId",
                "Clicalo.App",
                "--packVersion",
                "2.0.0-beta.1",
                "--packDir",
                "pub",
                "--mainExe",
                "Clicalo.exe",
                "--packTitle",
                "Clícalo",
                "--packAuthors",
                "Michael Coaguila",
                "--channel",
                "beta",
                "--runtime",
                "win-x64",
                "--releaseNotes",
                "notes.md",
                "--delta",
                "None",
                "--outputDir",
                "out",
            ]);

    [Fact]
    public void The_release_notes_gather_the_user_sentences_of_the_fragments()
    {
        var notes = ReleaseNotesWriter.Write(
            "2.0.0",
            "2026-10-09",
            [
                "# comentario\ntype: feat # feat, fix\nes: \"Pestaña lateral\"\nen: \"Edge tab\"\n",
                "type: fix\nes: \"\"\nen: \"\"\n",
                "es: Sin comillas # nota\nen: Without quotes\n",
            ]
        );

        notes.ShouldBe(
            "# Clícalo 2.0.0\n\n<!-- date: 2026-10-09 -->\n\n## es\n- Pestaña lateral\n- Sin comillas\n\n## en\n- Edge tab\n- Without quotes\n"
        );
    }

    [Fact]
    public void The_package_verb_takes_its_own_options()
    {
        var (cl, rest) = VerbCatalog.SplitArguments(["package", "--channel", "beta"]);

        cl.ShouldBe(["package"]);
        rest.ShouldBe(["--channel", "beta"]);
    }
}
