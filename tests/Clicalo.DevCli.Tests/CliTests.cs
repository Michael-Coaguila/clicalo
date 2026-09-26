using Clicalo.TestKit;

namespace Clicalo.DevCli.Tests;

/// <summary>Verb dispatch, option parsing and exit codes (0 no problems, 1 problems, 2 usage error).</summary>
public sealed class CliTests
{
    [Fact]
    public void No_verb_is_a_usage_error_with_the_help()
    {
        var (code, output, error) = Run();

        code.ShouldBe(ExitCodes.Usage);
        output.ShouldBeEmpty();
        error.ShouldContain("Usage: Clicalo.DevCli <verb> [options]");
        LastLine(error).ShouldBe("No verb given.");
    }

    [Theory]
    [InlineData("help")]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Help_prints_every_verb_and_succeeds(string verb)
    {
        var (code, output, _) = Run(verb);

        code.ShouldBe(ExitCodes.Success);
        output.ShouldContain("i18n-check");
        output.ShouldContain("i18n-import");
        output.ShouldContain("adr-check");
    }

    [Fact]
    public void An_unknown_verb_is_a_usage_error()
    {
        var (code, _, error) = Run("i18n-chek");

        code.ShouldBe(ExitCodes.Usage);
        LastLine(error).ShouldBe("Unknown verb 'i18n-chek'.");
    }

    [Theory]
    [InlineData("i18n-check", "--check")]
    [InlineData("i18n-import", "--strict-unused")]
    [InlineData("i18n-import", "--base")]
    [InlineData("adr-check", "--base")]
    [InlineData("i18n-check", "--repo")]
    public void An_option_of_another_verb_or_without_its_value_is_a_usage_error(
        string verb,
        string option
    )
    {
        var (code, _, error) = Run(verb, option);

        code.ShouldBe(ExitCodes.Usage);
        LastLine(error).ShouldBe("Unknown or incomplete option '" + option + "' for " + verb + ".");
    }

    [Fact]
    public void Adr_check_needs_a_base()
    {
        var (code, _, error) = Run("adr-check", "--repo", RepoPaths.Root);

        code.ShouldBe(ExitCodes.Usage);
        LastLine(error).ShouldBe("adr-check needs --base <ref>.");
    }

    [Fact]
    public void A_folder_without_the_solution_is_a_usage_error()
    {
        using var folder = new TemporaryRepository();
        File.Delete(Path.Combine(folder.Root, "Clicalo.slnx"));

        var (code, _, error) = Run("i18n-check", "--repo", folder.Root);

        code.ShouldBe(ExitCodes.Usage);
        LastLine(error).ShouldStartWith("The repository root (the folder with Clicalo.slnx)");
    }

    [Fact]
    public void I18n_check_passes_on_the_repository_data()
    {
        var (code, output, _) = Run("i18n-check", "--repo", RepoPaths.Root);

        code.ShouldBe(ExitCodes.Success, output);
        LastLine(output).ShouldStartWith("i18n-check: no problems.");
    }

    [Fact]
    public void I18n_import_check_reproduces_the_repository_data()
    {
        var (code, output, _) = Run("i18n-import", "--check", "--repo", RepoPaths.Root);

        code.ShouldBe(ExitCodes.Success, output);
        LastLine(output).ShouldStartWith("i18n-import: data/i18n matches a fresh import");
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = Cli.Run(args, RepoPaths.Root, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static string LastLine(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[
            ^1
        ];
}
