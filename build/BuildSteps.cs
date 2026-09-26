using System.Globalization;

namespace Clicalo.Build;

/// <summary>
/// The building blocks of every verb. Each step has a short dictable name (the one the final line says
/// when it fails) and turns a failure into a report with the exact errors, never just an exit code.
/// </summary>
internal sealed class BuildSteps(RepoLayout layout, RunContext context)
{
    /// <summary>The trait that marks tests needing an interactive desktop.</summary>
    public const string DesktopTrait = "Requires=Desktop";

    /// <summary>Tells desktop tests that the run is deliberate (they self-skip otherwise).</summary>
    public const string DesktopVariable = "CLICALO_DESKTOP_TESTS";

    /// <summary>Optional cap on MSBuild parallelism (for example on a shared machine).</summary>
    public const string MaxCpuVariable = "CLICALO_MAXCPU";

    /// <summary>Exit code of Microsoft.Testing.Platform when a module ran no test.</summary>
    public const int ZeroTestsExitCode = 8;

    private const string Dotnet = "dotnet";
    private const string Debug = "Debug";
    private const string Release = "Release";

    /// <summary>Checks exact versions and pinned actions (NFR-014).</summary>
    public Task PinsAsync() =>
        context.Steps.RunAsync(
            "pins",
            Messages.PinsPurpose,
            () =>
            {
                var violations = VersionPins.Check(layout);
                if (violations.Count > 0)
                {
                    throw new StepFailedException(
                        new FailureDetails
                        {
                            Summary = Messages.PinsFailed,
                            Sections =
                            [
                                new ReportSection(
                                    Messages.CountedHeading(Messages.PinsSection, violations.Count),
                                    VersionPins.Render(violations)
                                ),
                            ],
                            Hint = Messages.PinsHint,
                        }
                    );
                }

                return Task.CompletedTask;
            }
        );

    /// <summary>Restores the local tools of <c>.config/dotnet-tools.json</c>.</summary>
    public Task ToolRestoreAsync() =>
        context.Steps.RunAsync(
            "tools",
            Messages.ToolsPurpose,
            async () =>
            {
                string[] args = ["tool", "restore"];
                var exitCode = await RunAsync(args);
                if (exitCode != 0)
                {
                    throw new StepFailedException(
                        new FailureDetails
                        {
                            Summary = Messages.ToolsFailed,
                            Command = CommandRunner.Display(Dotnet, args),
                            ExitCode = exitCode,
                            Hint = Messages.ToolsHint,
                        }
                    );
                }
            }
        );

    /// <summary>Fails if any C# file differs from CSharpier's output.</summary>
    public Task FormatCheckAsync() =>
        context.Steps.RunAsync(
            "format",
            Messages.FormatCheckPurpose,
            async () =>
            {
                string[] args = ["csharpier", "check", "."];
                var result = await ReadAsync(args);
                if (result.ExitCode == 0)
                {
                    return;
                }

                var files = CSharpierReport.UnformattedFiles(result.Combined);
                throw new StepFailedException(
                    new FailureDetails
                    {
                        Summary = Messages.FormatFailed,
                        Command = CommandRunner.Display(Dotnet, args),
                        ExitCode = result.ExitCode,
                        Sections =
                            files.Count > 0
                                ?
                                [
                                    new ReportSection(
                                        Messages.CountedHeading(
                                            Messages.FormatSection,
                                            files.Count
                                        ),
                                        CSharpierReport.Render(files)
                                    ),
                                ]
                                :
                                [
                                    new ReportSection(
                                        Messages.OutputSection,
                                        Markdown.CodeBlock(result.Combined)
                                    ),
                                ],
                        Hint = Messages.FormatHint,
                    }
                );
            }
        );

    /// <summary>Formats every C# file with CSharpier.</summary>
    public Task FormatFixAsync() =>
        context.Steps.RunAsync(
            "format",
            Messages.FormatFixPurpose,
            async () =>
            {
                string[] args = ["csharpier", "format", "."];
                var exitCode = await RunAsync(args);
                if (exitCode != 0)
                {
                    throw new StepFailedException(
                        new FailureDetails
                        {
                            Summary = Messages.FormatFixFailed,
                            Command = CommandRunner.Display(Dotnet, args),
                            ExitCode = exitCode,
                        }
                    );
                }
            }
        );

    /// <summary>Restores the solution exactly as the lock files say (what CI does with CI=true).</summary>
    public Task RestoreLockedAsync() =>
        context.Steps.RunAsync(
            "restore",
            Messages.RestorePurpose,
            async () =>
            {
                var log = PrepareErrorLog("restore");
                List<string> args = ["restore", layout.Relative(layout.Solution), "--locked-mode"];
                AddMsBuildSwitches(args, log);
                var exitCode = await RunAsync(args);
                if (exitCode != 0)
                {
                    throw MsBuildFailure(
                        Messages.RestoreFailed,
                        Messages.RestoreSection,
                        Messages.RestoreHint,
                        args,
                        exitCode,
                        log
                    );
                }
            }
        );

    /// <summary>Builds <paramref name="target"/> (a solution or solution filter).</summary>
    public Task BuildAsync(string target, BuildMode mode) =>
        context.Steps.RunAsync(
            "build",
            mode == BuildMode.ReleaseGate
                ? Messages.BuildReleasePurpose
                : Messages.BuildDebugPurpose,
            async () =>
            {
                var log = PrepareErrorLog("build");
                List<string> args = ["build", layout.Relative(target), "-c", Configuration(mode)];
                if (mode == BuildMode.ReleaseGate)
                {
                    // The restore step already ran in locked mode; any MSBuild or NuGet warning fails the gate.
                    args.Add("--no-restore");
                    args.Add("-warnaserror");
                }

                AddMsBuildSwitches(args, log);
                var exitCode = await RunAsync(args);
                if (exitCode != 0)
                {
                    throw MsBuildFailure(
                        Messages.BuildFailed,
                        Messages.BuildSection,
                        Messages.BuildHint,
                        args,
                        exitCode,
                        log
                    );
                }
            }
        );

    /// <summary>Runs the already built tests of <paramref name="target"/> and reports failures from TRX.</summary>
    public Task TestAsync(string target, BuildMode mode, TestSelection selection) =>
        context.Steps.RunAsync(
            "test",
            selection == TestSelection.DesktopOnly ? Messages.DeskPurpose : Messages.TestPurpose,
            async () =>
            {
                var results = layout.TestResultsDirectory;
                if (Directory.Exists(results))
                {
                    Directory.Delete(results, recursive: true);
                }

                Directory.CreateDirectory(results);
                string[] args =
                [
                    "test",
                    "--solution",
                    layout.Relative(target),
                    "-c",
                    Configuration(mode),
                    "--no-build",
                    "--no-progress",
                    "--results-directory",
                    layout.Relative(results),
                    "--report-xunit-trx",
                    selection == TestSelection.DesktopOnly
                        ? "--filter-trait"
                        : "--filter-not-trait",
                    DesktopTrait,
                    // A project may hold only desktop tests (or none of them): zero tests there is fine.
                    "--ignore-exit-code",
                    ZeroTestsExitCode.ToString(CultureInfo.InvariantCulture),
                ];
                var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [DesktopVariable] = selection == TestSelection.DesktopOnly ? "1" : null,
                };

                var exitCode = await RunAsync(args, environment);
                var report = TestRunReport.Load(results);
                context.AddNote(report.DescribeCount());
                if (exitCode == 0 && report.Failures.Count == 0)
                {
                    return;
                }

                List<ReportSection> sections = [];
                if (report.Failures.Count > 0)
                {
                    sections.Add(
                        new ReportSection(
                            Messages.CountedHeading(Messages.TestsSection, report.Failures.Count),
                            report.RenderFailures()
                        )
                    );
                }

                if (report.Files.Count > 0)
                {
                    sections.Add(
                        new ReportSection(Messages.TestResultsLabel, report.RenderFiles(layout))
                    );
                }

                throw new StepFailedException(
                    new FailureDetails
                    {
                        Summary =
                            report.Failures.Count > 0
                                ? Messages.TestsFailed
                                : Messages.TestRunFailedWithoutFailures,
                        Command = CommandRunner.Display(Dotnet, args),
                        ExitCode = exitCode,
                        ExitCodeMeaning = Messages.TestExitCodeMeaning(exitCode),
                        Sections = sections,
                        Hint = Messages.TestsHint,
                    }
                );
            }
        );

    /// <summary>Runs the i18n verb of the developer CLI (implemented by the localization work package).</summary>
    public Task I18nCheckAsync() =>
        context.Steps.RunAsync(
            "i18n",
            Messages.I18nPurpose,
            async () =>
            {
                string[] args =
                [
                    "run",
                    "--project",
                    Path.Combine("tools", "Clicalo.DevCli"),
                    "--",
                    "i18n-check",
                ];
                var result = await ReadAsync(args);
                if (result.ExitCode != 0)
                {
                    throw new StepFailedException(
                        new FailureDetails
                        {
                            Summary = Messages.I18nFailed,
                            Command = CommandRunner.Display(Dotnet, args),
                            ExitCode = result.ExitCode,
                            Sections =
                            [
                                new ReportSection(
                                    Messages.I18nSection,
                                    Markdown.CodeBlock(result.Combined)
                                ),
                            ],
                            Hint = Messages.I18nHint,
                        }
                    );
                }
            }
        );

    /// <summary>Empties <c>artifacts/</c>, keeping the running orchestrator's own folders.</summary>
    public Task CleanAsync(IReadOnlyCollection<string> keep) =>
        context.Steps.RunAsync(
            "clean",
            Messages.CleanPurpose,
            async () =>
            {
                var failures = await ArtifactsCleaner.CleanAsync(layout.Artifacts, keep);
                if (failures.Count > 0)
                {
                    throw new StepFailedException(
                        new FailureDetails
                        {
                            Summary = Messages.CleanFailed,
                            Sections =
                            [
                                new ReportSection(
                                    Messages.CountedHeading(Messages.CleanSection, failures.Count),
                                    string.Concat(
                                        failures.Select(path =>
                                            "- "
                                            + Markdown.Text("artifacts/" + path.Replace('\\', '/'))
                                            + "\n"
                                        )
                                    )
                                ),
                            ],
                            Hint = Messages.CleanHint,
                        }
                    );
                }
            }
        );

    /// <summary>Configures git for this repository and records what the maintainer still has to do.</summary>
    public Task GitAsync() =>
        context.Steps.RunAsync(
            "git",
            Messages.GitPurpose,
            async () =>
            {
                var pending = await new GitSetup(layout, context.Output).ConfigureAsync();
                if (pending.Count > 0)
                {
                    context.AddNote(
                        Messages.SetupPending(
                            Messages.JoinList(pending),
                            layout.Relative(layout.SetupNotesFile)
                        )
                    );
                }
            }
        );

    private static string Configuration(BuildMode mode) =>
        mode == BuildMode.ReleaseGate ? Release : Debug;

    private async Task<int> RunAsync(
        IReadOnlyList<string> args,
        IReadOnlyDictionary<string, string?>? environment = null
    )
    {
        await EchoAsync(args);
        return await CommandRunner.RunAsync(Dotnet, args, environment);
    }

    /// <summary>Runs a short command with captured output, then shows that output.</summary>
    private async Task<CommandOutput> ReadAsync(IReadOnlyList<string> args)
    {
        await EchoAsync(args);
        var result = await CommandRunner.ReadAsync(Dotnet, args);
        var text = result.Combined.TrimEnd();
        if (text.Length > 0)
        {
            await context.Output.WriteLineAsync(text);
        }

        return result;
    }

    private async Task EchoAsync(IReadOnlyList<string> args)
    {
        if (context.Mode.EchoCommands)
        {
            await context.Output.WriteLineAsync("cl: " + CommandRunner.Display(Dotnet, args));
        }
    }

    /// <summary>Switches shared by every MSBuild-based step, including the errors-only log.</summary>
    private void AddMsBuildSwitches(List<string> args, string errorLog)
    {
        args.Add("-nologo");
        args.Add("-nodeReuse:false");
        // Append-only console output: predictable for screen readers and identical in CI logs.
        args.Add("-tl:off");
        args.Add("-v:" + context.Mode.MsBuildVerbosity);
        args.Add(
            "-flp1:LogFile=" + layout.RelativeForward(errorLog) + ";ErrorsOnly;Encoding=UTF-8"
        );

        var value = Environment.GetEnvironmentVariable(MaxCpuVariable);
        if (
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var maxCpu)
            && maxCpu > 0
        )
        {
            args.Add("-m:" + maxCpu.ToString(CultureInfo.InvariantCulture));
        }
    }

    private string PrepareErrorLog(string name)
    {
        Directory.CreateDirectory(layout.LogsDirectory);
        var path = Path.Combine(layout.LogsDirectory, name + ".errors.log");
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return path;
    }

    private StepFailedException MsBuildFailure(
        string summary,
        string sectionHeading,
        string hint,
        IReadOnlyList<string> args,
        int exitCode,
        string log
    )
    {
        var diagnostics = MsBuildErrorLog.Load(log);
        var section =
            diagnostics.Count > 0
                ? new ReportSection(
                    Messages.CountedHeading(sectionHeading, diagnostics.Count),
                    MsBuildErrorLog.Render(diagnostics, layout, layout.ClDirectory)
                        + "\n"
                        + Markdown.Text(Messages.LogFileLine(layout.RelativeForward(log)))
                        + "\n"
                )
                : new ReportSection(sectionHeading, Markdown.Text(Messages.NoParsedErrors) + "\n");
        return new StepFailedException(
            new FailureDetails
            {
                Summary = summary,
                Command = CommandRunner.Display(Dotnet, args),
                ExitCode = exitCode,
                Sections = [section],
                Hint = hint,
            }
        );
    }
}
