using System.Text;

namespace Clicalo.Build;

/// <summary>
/// The git part of <c>cl setup</c> (blueprint §13): repository settings, the DCO trailer hook and commit
/// signing with an SSH key the maintainer already has. It never creates, copies or reads private keys.
/// </summary>
internal sealed class GitSetup(RepoLayout layout, TextWriter output)
{
    /// <summary>Settings written to the repository configuration on every run (idempotent).</summary>
    public static IReadOnlyList<GitSetting> RepositorySettings { get; } =
    [
        new(
            "core.autocrlf",
            "false",
            ".gitattributes owns line endings; no conversion on top of it."
        ),
        new("core.longpaths", "true", "Deep test and artifact paths exceed MAX_PATH on Windows."),
        new(
            "core.hooksPath",
            "build/githooks",
            "Adds the DCO Signed-off-by trailer to every commit."
        ),
        new(
            "format.signOff",
            "true",
            "Patches created with format-patch carry the DCO trailer too."
        ),
        new(
            "pull.rebase",
            "true",
            "Trunk-based with linear history: pulls never create merge commits."
        ),
        new(
            "fetch.prune",
            "true",
            "Branches are deleted after the squash merge; drop them locally too."
        ),
        new(
            "push.autoSetupRemote",
            "true",
            "The first push of a short-lived branch needs no extra words."
        ),
    ];

    /// <summary>Applies the settings and returns what is still pending for the maintainer.</summary>
    public async Task<IReadOnlyList<string>> ConfigureAsync()
    {
        foreach (var setting in RepositorySettings)
        {
            await SetLocalAsync(setting.Key, setting.Value);
        }

        var pending = new List<string>();
        var notes = new StringBuilder();

        var name = await GetAsync("user.name");
        var email = await GetAsync("user.email");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
        {
            pending.Add(Messages.PendingIdentity);
            AppendIdentityNotes(notes);
        }

        var signing = await DescribeSigningAsync();
        if (signing.Ready)
        {
            await SetLocalAsync("commit.gpgsign", "true");
            await SetLocalAsync("tag.gpgsign", "true");
            await output.WriteLineAsync(Messages.SigningEnabled);
        }
        else
        {
            pending.Add(Messages.PendingSigning);
            AppendSigningNotes(notes, signing.MissingKeyFile);
        }

        if (pending.Count > 0)
        {
            Directory.CreateDirectory(layout.ClDirectory);
            var document =
                "# " + Messages.SetupNotesTitle + "\n\n" + Messages.SetupNotesIntro + "\n" + notes;
            await File.WriteAllTextAsync(layout.SetupNotesFile, document, new UTF8Encoding(false));
        }
        else if (File.Exists(layout.SetupNotesFile))
        {
            File.Delete(layout.SetupNotesFile);
        }

        return pending;
    }

    private static async Task<(bool Ready, string? MissingKeyFile)> DescribeSigningAsync()
    {
        var format = await GetAsync("gpg.format");
        var key = await GetAsync("user.signingkey");
        if (
            !string.Equals(format, "ssh", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(key)
        )
        {
            return (false, null);
        }

        // A literal key ("key::ssh-..." or "ssh-...") needs no file; anything else is a path to a public key.
        if (
            key.StartsWith("key::", StringComparison.Ordinal)
            || key.StartsWith("ssh-", StringComparison.Ordinal)
        )
        {
            return (true, null);
        }

        var path = ExpandHome(key);
        return File.Exists(path) ? (true, null) : (false, key);
    }

    private async Task SetLocalAsync(string key, string value)
    {
        var result = await CommandRunner.ReadAsync("git", ["config", "--local", key, value]);
        if (result.ExitCode != 0)
        {
            throw new StepFailedException(
                new FailureDetails
                {
                    Summary = Messages.GitFailed,
                    Command = CommandRunner.Display("git", ["config", "--local", key, value]),
                    ExitCode = result.ExitCode,
                    Sections =
                    [
                        new ReportSection(
                            Messages.OutputSection,
                            Markdown.CodeBlock(result.Combined)
                        ),
                    ],
                    Hint = Messages.GitHint,
                }
            );
        }

        await output.WriteLineAsync(Messages.GitSettingApplied(key, value));
    }

    private static async Task<string?> GetAsync(string key)
    {
        // Exit code 1 means "not set"; the effective value may come from any scope.
        var result = await CommandRunner.ReadAsync("git", ["config", "--get", key]);
        return result.ExitCode == 0 ? result.StandardOutput.Trim() : null;
    }

    private static void AppendIdentityNotes(StringBuilder notes)
    {
        notes.Append("\n## ").Append(Messages.IdentityHeading).Append("\n\n");
        notes.Append(Messages.IdentityBody).Append("\n\n");
        notes.Append(
            Markdown.CodeBlock(
                "git config --global user.name \""
                    + Messages.IdentityNamePlaceholder
                    + "\"\ngit config --global user.email \""
                    + Messages.IdentityEmailPlaceholder
                    + "\""
            )
        );
        notes.Append('\n');
    }

    private static void AppendSigningNotes(StringBuilder notes, string? missingKeyFile)
    {
        notes.Append("\n## ").Append(Messages.SigningHeading).Append("\n\n");
        notes.Append(Messages.SigningIntro).Append("\n\n");

        if (missingKeyFile is not null)
        {
            notes
                .Append(Messages.SigningMissingKeyFile)
                .Append(' ')
                .Append(Markdown.InlineCode(missingKeyFile));
            notes.Append("\n\n");
        }

        var keys = FindPublicKeys();
        if (keys.Count == 0)
        {
            notes.Append(Messages.SigningNoKeysFound).Append("\n\n");
        }
        else
        {
            notes.Append(Messages.SigningKeysFound).Append("\n\n");
            foreach (var key in keys)
            {
                notes.Append("- ").Append(Markdown.InlineCode(key)).Append('\n');
            }

            notes.Append('\n');
        }

        var example = keys.Count > 0 ? keys[0] : "~/.ssh/id_ed25519.pub";
        notes.Append("1. ").Append(Messages.SigningStepConfigure).Append("\n\n");
        notes.Append(
            Indent(
                Markdown.CodeBlock(
                    "git config --global gpg.format ssh\ngit config --global user.signingkey \""
                        + example
                        + "\""
                )
            )
        );
        notes.Append("\n2. ").Append(Messages.SigningStepGitHub).Append('\n');
        notes.Append("3. ").Append(Messages.SigningStepAgent).Append("\n\n");
        notes.Append(
            Indent(
                Markdown.CodeBlock(
                    "Get-Service ssh-agent | Set-Service -StartupType Automatic\n"
                        + "Start-Service ssh-agent\n"
                        + "ssh-add\n"
                        + "git config --global gpg.ssh.program \"C:/Windows/System32/OpenSSH/ssh-keygen.exe\""
                )
            )
        );
        notes.Append("\n4. ").Append(Messages.SigningStepRerun).Append('\n');
    }

    private static List<string> FindPublicKeys()
    {
        var sshDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ssh"
        );
        return Directory.Exists(sshDirectory)
            ? Directory
                .EnumerateFiles(sshDirectory, "*.pub")
                .Select(path => "~/.ssh/" + Path.GetFileName(path))
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];
    }

    private static string ExpandHome(string path) =>
        path.StartsWith("~/", StringComparison.Ordinal)
        || path.StartsWith("~\\", StringComparison.Ordinal)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                path[2..]
            )
            : path;

    private static string Indent(string block) =>
        string.Join('\n', block.Split('\n').Select(line => line.Length == 0 ? line : "   " + line))
        + "\n";
}
