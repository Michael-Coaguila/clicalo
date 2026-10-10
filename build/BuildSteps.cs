using System.Globalization;

namespace Clicalo.Build;

/// <summary>
/// The building blocks of every verb. Each step has a short dictable name (the one the final line says
/// when it fails) and turns a failure into a report with the exact errors, never just an exit code.
/// </summary>
internal sealed partial class BuildSteps(RepoLayout layout, RunContext context)
{
    /// <summary>The trait that marks tests needing an interactive desktop.</summary>
    public const string DesktopTrait = "Requires=Desktop";

    /// <summary>
    /// The trait of desktop tests that inject keys reserved for the maintainer's dictation and voice tools (right Ctrl,
    /// AltGr): <c>cl desk</c> runs them only in continuous integration, never on the maintainer's machine.
    /// </summary>
    public const string ReservedKeysTrait = "Injects=ReservedKeys";

    /// <summary>
    /// Makes every test module write its xUnit TRX as <c>&lt;AssemblyName&gt;.trx</c> (<c>Directory.Build.targets</c>):
    /// with xUnit's default name, taken from the clock when each module starts, two modules started together shared a
    /// file and <see cref="TestRunReport"/> counted fewer tests than <c>dotnet test</c>.
    /// </summary>
    public const string TrxReportProperty = "-p:ClicaloTrxReport=true";

    /// <summary>
    /// The trait of desktop tests that kill processes with keys held, freeze threads with real injection or lock the
    /// session (Sentinel's chaos, S9): only continuous integration runs them, never the maintainer's machine.
    /// </summary>
    public const string ChaosTrait = "Category=Chaos";

    /// <summary>The trait of the performance measurements: only <c>cl perf</c> runs them.</summary>
    public const string PerfTrait = "Category=Perf";

    /// <summary>
    /// The trait of a flaky test registered with a GitHub issue (<c>[Trait("Issue", "&lt;number&gt;")]</c>): it never
    /// gates a pull request; only <c>cl quarantine</c> runs it, every night.
    /// </summary>
    public const string QuarantineTrait = "Category=Quarantine";

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
    /// <param name="target">A solution or solution filter, or a test project when <paramref name="environment"/> is given.</param>
    /// <param name="mode">The configuration that was built.</param>
    /// <param name="selection">Which tests.</param>
    /// <param name="environment">More variables for the test processes (<c>cl perf</c>); <paramref name="target"/> is then a project.</param>
    public Task TestAsync(
        string target,
        BuildMode mode,
        TestSelection selection,
        IReadOnlyDictionary<string, string?>? environment = null
    ) =>
        context.Steps.RunAsync(
            selection == TestSelection.PerfOnly ? "perf" : "test",
            selection switch
            {
                TestSelection.DesktopOnly => Messages.DeskPurpose,
                TestSelection.PerfOnly => Messages.PerfPurpose,
                TestSelection.QuarantineOnly => Messages.QuarantinePurpose,
                _ => Messages.TestPurpose,
            },
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
                    environment is null ? "--solution" : "--project",
                    layout.Relative(target),
                    "-c",
                    Configuration(mode),
                    "--no-build",
                    "--no-progress",
                    "--results-directory",
                    layout.Relative(results),
                    // One <AssemblyName>.trx per module (Directory.Build.targets), so no module overwrites another's.
                    TrxReportProperty,
                    .. SelectionArguments(selection, context.Mode.Ci),
                    // A project may hold only desktop tests (or none of them): zero tests there is fine.
                    "--ignore-exit-code",
                    ZeroTestsExitCode.ToString(CultureInfo.InvariantCulture),
                ];
                var variables = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [DesktopVariable] = selection == TestSelection.Deterministic ? null : "1",
                };
                foreach (
                    var (name, value) in environment
                        ?? new Dictionary<string, string?>(StringComparer.Ordinal)
                )
                {
                    variables[name] = value;
                }

                var exitCode = await RunAsync(args, variables);
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

    /// <summary>
    /// How a test run selects its tests (<see cref="TestSelection"/>). The pull request tier leaves out everything that
    /// is not deterministic: the desktop, chaos, the measurements and the quarantine. The other selections may touch the
    /// desktop, so their modules run one at a time, because each one takes the foreground with its own InputProbe and
    /// two at once would take it from each other; outside continuous integration they also leave out the tests that
    /// inject reserved keys (<see cref="ReservedKeysTrait"/>) and the chaos tests (<see cref="ChaosTrait"/>).
    /// </summary>
    internal static string[] SelectionArguments(TestSelection selection, bool ci)
    {
        if (selection == TestSelection.Deterministic)
        {
            return
            [
                "--filter-not-trait",
                DesktopTrait,
                "--filter-not-trait",
                ChaosTrait,
                "--filter-not-trait",
                PerfTrait,
                "--filter-not-trait",
                QuarantineTrait,
            ];
        }

        string[] filters = selection switch
        {
            TestSelection.DesktopOnly =>
            [
                "--filter-trait",
                DesktopTrait,
                "--filter-not-trait",
                PerfTrait,
                "--filter-not-trait",
                QuarantineTrait,
            ],
            TestSelection.PerfOnly => ["--filter-trait", PerfTrait],
            _ => ["--filter-trait", QuarantineTrait, "--filter-not-trait", PerfTrait],
        };
        string[] local = ci
            ? []
            : ["--filter-not-trait", ReservedKeysTrait, "--filter-not-trait", ChaosTrait];
        return [.. filters, .. local, "--max-parallel-test-modules", "1"];
    }

    /// <summary>
    /// Runs the i18n verbs of the developer CLI: <c>i18n-check</c> (the generator's validation, CLDR rules and unused
    /// keys) and <c>i18n-import --check</c> (data/i18n is exactly what the reviewed recipe produces).
    /// </summary>
    public Task I18nCheckAsync() =>
        context.Steps.RunAsync(
            "i18n",
            Messages.I18nPurpose,
            async () =>
            {
                await RunDevCliAsync(
                    ["i18n-check"],
                    Messages.I18nFailed,
                    Messages.I18nSection,
                    Messages.I18nHint
                );
                await RunDevCliAsync(
                    ["i18n-import", "--check"],
                    Messages.I18nImportFailed,
                    Messages.I18nImportSection,
                    Messages.I18nImportHint
                );
            }
        );

    /// <summary>
    /// Runs <paramref name="verb"/> of the developer CLI with <paramref name="arguments"/> as one step named after the
    /// verb (<c>cl i18n-check</c>, <c>cl i18n-import</c>, <c>cl adr-check</c>, <c>cl trace</c>), with the same report as
    /// the i18n step.
    /// </summary>
    public Task DevCliAsync(string verb, IReadOnlyList<string> arguments) =>
        context.Steps.RunAsync(
            verb,
            Messages.DevCliPurpose(verb),
            () =>
                RunDevCliAsync(
                    [verb, .. arguments],
                    verb switch
                    {
                        VerbCatalog.I18nCheck => Messages.I18nFailed,
                        VerbCatalog.AdrCheck => Messages.AdrCheckFailed,
                        VerbCatalog.Trace => Messages.TraceFailed,
                        _ => Messages.DevCliFailed(verb),
                    },
                    Messages.DevCliSection(verb),
                    verb switch
                    {
                        VerbCatalog.I18nCheck => Messages.I18nHint,
                        VerbCatalog.I18nImport => Messages.I18nImportHint,
                        VerbCatalog.Trace => Messages.TraceHint,
                        _ => Messages.AdrCheckHint,
                    }
                )
        );

    private async Task RunDevCliAsync(string[] verb, string summary, string section, string hint)
    {
        string[] args =
        [
            "run",
            "--project",
            Path.Combine("tools", "Clicalo.DevCli"),
            "--",
            .. verb,
        ];
        var result = await ReadAsync(args);
        if (result.ExitCode != 0)
        {
            throw new StepFailedException(
                new FailureDetails
                {
                    Summary = summary,
                    Command = CommandRunner.Display(Dotnet, args),
                    ExitCode = result.ExitCode,
                    Sections = [new ReportSection(section, Markdown.CodeBlock(result.Combined))],
                    Hint = hint,
                }
            );
        }
    }

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
