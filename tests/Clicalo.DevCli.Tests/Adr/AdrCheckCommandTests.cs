using System.Diagnostics;

namespace Clicalo.DevCli.Tests.Adr;

/// <summary><c>adr-check --base</c> end to end on a throwaway git repository.</summary>
public sealed class AdrCheckCommandTests
{
    private const string Registry = """
        { "paths": [ { "pattern": "nuget.config", "category": "signing", "reason": "Trusted signers." } ] }
        """;

    [Fact]
    public void Compares_the_branch_with_its_merge_base()
    {
        using var repo = new TemporaryRepository();
        repo.Write("architecture/sensitive-paths.json", Registry);
        repo.Write("nuget.config", "<configuration />\n");
        Git(repo, "init", "--quiet", "--initial-branch=main");
        Git(repo, "add", "-A");
        Git(repo, "commit", "--quiet", "-m", "base");
        Git(repo, "switch", "--quiet", "-c", "feature");
        repo.Write("nuget.config", "<configuration><!-- changed --></configuration>\n");
        Git(repo, "commit", "--quiet", "-am", "change signers");

        Run(repo, "main")
            .ShouldBe(
                (
                    ExitCodes.Failure,
                    "adr-check: 1 file in a sensitive path changed without an ADR. Add or update docs/adr/NNNN-*.md."
                )
            );

        repo.Write("docs/adr/0018-nuevo-firmante.md", "# ADR-0018\n");
        Git(repo, "add", "-A");
        Git(repo, "commit", "--quiet", "-m", "adr");

        Run(repo, "main")
            .ShouldBe(
                (ExitCodes.Success, "adr-check: 1 file in a sensitive path changed with an ADR.")
            );
    }

    [Fact]
    public void An_unknown_base_is_a_failure_with_the_git_error()
    {
        using var repo = new TemporaryRepository();
        repo.Write("architecture/sensitive-paths.json", Registry);
        Git(repo, "init", "--quiet", "--initial-branch=main");
        Git(repo, "add", "-A");
        Git(repo, "commit", "--quiet", "-m", "base");

        Run(repo, "does-not-exist")
            .ShouldBe((ExitCodes.Failure, "adr-check: the changed files could not be listed."));
    }

    [Fact]
    public void A_missing_registry_is_a_failure()
    {
        using var repo = new TemporaryRepository();

        Run(repo, "main")
            .ShouldBe(
                (ExitCodes.Failure, "adr-check: the sensitive-path registry could not be read.")
            );
    }

    private static (int Code, string LastLine) Run(TemporaryRepository repo, string baseRef)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = Cli.Run(
            ["adr-check", "--base", baseRef, "--repo", repo.Root],
            repo.Root,
            output,
            error
        );
        return (
            code,
            output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)[^1]
        );
    }

    private static void Git(TemporaryRepository repo, params string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = repo.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        // A throwaway identity and no signing or hooks from the developer's global configuration.
        foreach (
            var argument in new[]
            {
                "-c",
                "user.name=Clicalo Tests",
                "-c",
                "user.email=tests@clicalo.invalid",
                "-c",
                "commit.gpgsign=false",
                "-c",
                "core.hooksPath=",
                "-c",
                "core.autocrlf=false",
            }.Concat(args)
        )
        {
            start.ArgumentList.Add(argument);
        }

        using var git = Process.Start(start)!;
        var errors = git.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        git.ExitCode.ShouldBe(0, "git " + string.Join(' ', args) + ": " + errors.Result);
    }
}
