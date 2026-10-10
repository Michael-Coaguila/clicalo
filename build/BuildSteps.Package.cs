using System.Text;

namespace Clicalo.Build;

/// <summary>
/// <c>cl package</c> (ADR-0027, docs/guides/release.md): a local, reproducible package of this machine's runtime, or of
/// <c>--runtime win-arm64</c>, that is never published by the verb.
/// </summary>
internal sealed partial class BuildSteps
{
    /// <summary>The package id of ADR-0012: the install folder is <c>%LocalAppData%\Clicalo.App</c>.</summary>
    public const string PackId = "Clicalo.App";

    /// <summary>
    /// Publishes Clícalo self-contained with ReadyToRun and Sentinel self-contained next to it (without Native AOT: the
    /// package does not need the C++ linker), writes the release notes and runs <c>vpk pack</c> into
    /// <c>artifacts/package/&lt;channel&gt;</c> (<c>&lt;channel&gt;-arm64</c> for ARM64), emptied first: the same commit and options always give the same
    /// files, full packages only (no deltas; Velopack downloads the full package).
    /// </summary>
    /// <param name="args">The words after <c>cl package</c>.</param>
    public async Task PackageAsync(IReadOnlyList<string> args)
    {
        var root = Path.Combine(layout.Artifacts, "package");
        var output = root;
        PackageOptions? options = null;
        string? publish = null;
        await context.Steps.RunAsync(
            "publish",
            Messages.PackagePublishPurpose,
            async () =>
            {
                var prefix = PackageOptions.VersionPrefixOf(
                    await File.ReadAllTextAsync(Path.Combine(layout.Root, "Directory.Build.props"))
                );
                if (
                    !PackageOptions.TryParse(
                        args,
                        prefix,
                        RuntimeIdentifier,
                        out options,
                        out var error
                    )
                )
                {
                    throw new StepFailedException(
                        new FailureDetails { Summary = error, Hint = Messages.PackageUsage }
                    );
                }

                output = Path.Combine(root, options.PackChannel);
                publish = Path.Combine(root, "publish", options.PackChannel);
                foreach (var folder in new[] { output, publish })
                {
                    if (Directory.Exists(folder))
                    {
                        Directory.Delete(folder, recursive: true);
                    }
                }

                Directory.CreateDirectory(output);

                // Sentinel first: Clícalo's own copies of the shared files are the ones that stay.
                await PublishProjectAsync(
                    SentinelProject,
                    publish,
                    PackageProperties(options, aot: false),
                    options.Runtime
                );
                await PublishProjectAsync(
                    AppProject,
                    publish,
                    PackageProperties(options, aot: null),
                    options.Runtime
                );
            }
        );
        await context.Steps.RunAsync(
            "package",
            Messages.PackagePurpose,
            async () =>
            {
                var notes = Path.Combine(root, "notes-" + options!.PackChannel + ".md");
                await File.WriteAllTextAsync(
                    notes,
                    ReleaseNotesWriter.Write(options.Version, await CommitDateAsync(), Fragments()),
                    new UTF8Encoding(false)
                );
                List<string> pack = PackArguments(
                    options,
                    layout.Relative(publish!),
                    layout.Relative(output),
                    layout.Relative(notes)
                );
                var result = await ReadAsync(pack);
                if (result.ExitCode != 0)
                {
                    throw new StepFailedException(
                        new FailureDetails
                        {
                            Summary = Messages.PackageFailed,
                            Command = CommandRunner.Display(Dotnet, pack),
                            ExitCode = result.ExitCode,
                            Sections =
                            [
                                new ReportSection(
                                    Messages.PackageSection,
                                    Markdown.CodeBlock(result.Combined)
                                ),
                            ],
                            Hint = Messages.PackageHint,
                        }
                    );
                }

                var setup = Directory
                    .EnumerateFiles(output, "*Setup.exe")
                    .Where(file =>
                        file.Contains(
                            "-" + options.PackChannel + "-",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    .DefaultIfEmpty(
                        Path.Combine(output, PackId + "-" + options.PackChannel + "-Setup.exe")
                    )
                    .First();
                context.AddNote(Messages.PackageDone(layout.Relative(setup)));
            }
        );
    }

    /// <summary>The <c>dotnet publish</c> properties of the package: self-contained, ReadyToRun, the version.</summary>
    /// <param name="options">The channel and the version.</param>
    /// <param name="aot">False turns Native AOT off (Sentinel); null leaves the project as it is.</param>
    internal static List<string> PackageProperties(PackageOptions options, bool? aot)
    {
        List<string> properties =
        [
            "--self-contained",
            "true",
            "-p:PublishReadyToRun=true",
            "-p:Version=" + options.Version,
        ];
        if (aot is false)
        {
            properties.Add("-p:PublishAot=false");
        }

        return properties;
    }

    /// <summary>The icon of Clícalo, relative to the repository root (<c>app-icon</c> of tools/Clicalo.DevCli).</summary>
    internal const string AppIcon = "assets/icons/clicalo.ico";

    /// <summary>The <c>dotnet vpk pack</c> arguments (Velopack, ADR-0012 and ADR-0027).</summary>
    /// <param name="options">The channel and the version.</param>
    /// <param name="publish">The published folder.</param>
    /// <param name="output">Where the installer and the packages go.</param>
    /// <param name="notes">The release notes.</param>
    internal static List<string> PackArguments(
        PackageOptions options,
        string publish,
        string output,
        string notes
    ) =>
        [
            "vpk",
            "pack",
            "--packId",
            PackId,
            "--packVersion",
            options.Version,
            "--packDir",
            publish,
            "--mainExe",
            "Clicalo.exe",
            // BUR-003: the installer shows the icon of Clícalo too.
            "--icon",
            AppIcon,
            "--packTitle",
            "Clícalo",
            "--packAuthors",
            "Michael Coaguila",
            "--channel",
            options.PackChannel,
            "--runtime",
            options.Runtime,
            "--releaseNotes",
            notes,
            "--delta",
            "None",
            "--outputDir",
            output,
        ];

    private List<string> Fragments()
    {
        var folder = Path.Combine(layout.Root, "changes", "unreleased");
        return Directory.Exists(folder)
            ? Directory
                .EnumerateFiles(folder, "*.yml")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText)
                .ToList()
            : [];
    }

    private static async Task<string> CommitDateAsync()
    {
        var date = await CommandRunner.ReadAsync("git", ["log", "-1", "--format=%cs"]);
        var text = date.StandardOutput.Trim();
        return date.ExitCode == 0 && text.Length == 10 ? text : "1970-01-01";
    }
}
